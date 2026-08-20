using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

public enum SyncScope { Instances, Types }
public enum DiffStatus { Changed, TypeChange, Conflict, Same }

/// <summary>A parameter offered in the picker — Name plus whether it lives on the element's
/// type (rather than the instance itself) and whether Revit allows writing to it at all.</summary>
public record ParamColumn(string Name, bool IsTypeParam, bool IsReadOnly);

/// <summary>One parameter comparison from a re-imported export: the element it belongs to,
/// what Revit currently has, what the spreadsheet says, and which of the four buckets that lands in.</summary>
public record DiffRow(ElementId ElementId, string ElementLabel, string ParamName, bool IsTypeParam, string CurrentValue, string NewValue, DiffStatus Status);

/// <summary>
/// The real engine behind Batch Excel Sync: gather a scope of elements (a
/// category or the current selection, as instances or as their types),
/// classify which parameters are available to export (instance-owned,
/// type-owned, or read-only), build the export table, and — on re-import —
/// diff each cell against Revit's live value and commit only the rows the
/// user approved. Values round-trip through Parameter.AsValueString() /
/// SetValueString() rather than raw type-switched Set(...) calls, so units
/// (mm, feet-and-inches, etc.) are read and written the same way Revit's
/// own UI displays them instead of needing separate parsing per StorageType.
/// </summary>
public static class BatchExcelSyncEngine
{
    public const string CurrentSelectionLabel = "Current Selection";
    public const string ElementIdColumn = "Element Id";
    public const string ElementLabelColumn = "Element";

    public static List<Element> CollectScope(
        Document doc,
        ICollection<ElementId> currentSelection,
        SyncScope scope,
        string categoryLabel,
        Category? category,
        ElementId? levelFilterId)
    {
        if (categoryLabel == CurrentSelectionLabel)
        {
            var selected = currentSelection
                .Select(doc.GetElement)
                .Where(e => e is not null)
                .Cast<Element>();

            return scope == SyncScope.Types
                ? selected.Select(e => e is ElementType ? e : doc.GetElement(e.GetTypeId())).Where(e => e is not null).Cast<Element>().DistinctBy(e => e.Id).ToList()
                : selected.Where(e => e is not ElementType).ToList();
        }

        if (category is null) return new List<Element>();

        var collector = new FilteredElementCollector(doc).OfCategoryId(category.Id);
        var elements = (scope == SyncScope.Types ? collector.WhereElementIsElementType() : collector.WhereElementIsNotElementType()).ToList();

        if (scope == SyncScope.Instances && levelFilterId is { } levelId && levelId != ElementId.InvalidElementId)
            elements = elements.Where(e => e.LevelId == levelId).ToList();

        return elements;
    }

    /// <summary>Samples the first element's own parameters, then — for an Instances scope — also its type's
    /// parameters, tagging each with where it actually lives. A Types scope has nothing but type-level
    /// parameters, since the "elements" being browsed are already the types themselves.</summary>
    public static List<ParamColumn> ClassifyParameters(Document doc, List<Element> elements, SyncScope scope)
    {
        if (elements.Count == 0) return new List<ParamColumn>();
        var sample = elements[0];

        var columns = new List<ParamColumn>();
        var seen = new HashSet<string>();

        foreach (var p in sample.GetOrderedParameters())
        {
            var name = p.Definition.Name;
            if (!seen.Add(name)) continue;
            columns.Add(new ParamColumn(name, IsTypeParam: scope == SyncScope.Types, p.IsReadOnly));
        }

        if (scope == SyncScope.Instances)
        {
            var type = doc.GetElement(sample.GetTypeId());
            if (type is not null)
            {
                foreach (var p in type.GetOrderedParameters())
                {
                    var name = p.Definition.Name;
                    if (!seen.Add(name)) continue;
                    columns.Add(new ParamColumn(name, IsTypeParam: true, p.IsReadOnly));
                }
            }
        }

        return columns;
    }

    public static string ElementLabel(Element element) => $"{element.Category?.Name ?? element.GetType().Name} [{element.Id.Value}]";

    public static (List<string> Headers, List<List<string>> Rows) BuildExportTable(Document doc, List<Element> elements, List<ParamColumn> selectedColumns, SyncScope scope)
    {
        var headers = new List<string> { ElementIdColumn, ElementLabelColumn };
        headers.AddRange(selectedColumns.Select(c => c.Name));

        var rows = new List<List<string>>();
        foreach (var element in elements)
        {
            var row = new List<string> { element.Id.Value.ToString(), ElementLabel(element) };
            foreach (var col in selectedColumns)
            {
                var param = ResolveParameter(doc, element, col, scope);
                row.Add(ReadValue(param));
            }
            rows.Add(row);
        }

        return (headers, rows);
    }

    public static List<DiffRow> ComputeDiff(Document doc, List<string> headers, List<List<string>> importRows, SyncScope scope, List<ParamColumn> knownColumns)
    {
        var results = new List<DiffRow>();
        var idIndex = headers.IndexOf(ElementIdColumn);
        var labelIndex = headers.IndexOf(ElementLabelColumn);
        if (idIndex < 0) return results;

        var columnsByName = knownColumns.ToDictionary(c => c.Name);

        foreach (var row in importRows)
        {
            var idText = row.ElementAtOrDefault(idIndex);
            if (!long.TryParse(idText, out var idValue)) continue;

            var element = doc.GetElement(new ElementId(idValue));
            if (element is null) continue;

            var label = labelIndex >= 0 ? row.ElementAtOrDefault(labelIndex) : null;
            if (string.IsNullOrWhiteSpace(label)) label = ElementLabel(element);

            for (var i = 0; i < headers.Count; i++)
            {
                if (i == idIndex || i == labelIndex) continue;
                var paramName = headers[i];
                if (!columnsByName.TryGetValue(paramName, out var col)) continue;

                var param = ResolveParameter(doc, element, col, scope);
                if (param is null) continue;

                var currentValue = ReadValue(param);
                var newValue = row.ElementAtOrDefault(i) ?? "";

                var status =
                    currentValue == newValue ? DiffStatus.Same :
                    param.IsReadOnly ? DiffStatus.Conflict :
                    col.IsTypeParam ? DiffStatus.TypeChange :
                    DiffStatus.Changed;

                results.Add(new DiffRow(element.Id, label!, paramName, col.IsTypeParam, currentValue, newValue, status));
            }
        }

        return results;
    }

    /// <summary>Writes every approved, writable row's new value. Conflict rows (read-only) are skipped even if
    /// somehow marked approved — there's no Revit call that could honor them.</summary>
    public static int Commit(Document doc, List<DiffRow> approvedRows, SyncScope scope, List<ParamColumn> knownColumns)
    {
        var columnsByName = knownColumns.ToDictionary(c => c.Name);
        var updated = 0;

        foreach (var row in approvedRows)
        {
            if (row.Status == DiffStatus.Conflict) continue;

            var element = doc.GetElement(row.ElementId);
            if (element is null) continue;
            if (!columnsByName.TryGetValue(row.ParamName, out var col)) continue;

            var param = ResolveParameter(doc, element, col, scope);
            if (param is not { IsReadOnly: false }) continue;

            try
            {
                if (param.SetValueString(row.NewValue)) updated++;
            }
            catch (Exception)
            {
                // Value doesn't fit the parameter's expected format (units, enum, etc.) — skip that one field.
            }
        }

        return updated;
    }

    private static Parameter? ResolveParameter(Document doc, Element element, ParamColumn col, SyncScope scope)
    {
        if (scope == SyncScope.Instances && col.IsTypeParam)
        {
            var type = doc.GetElement(element.GetTypeId());
            return type?.LookupParameter(col.Name);
        }
        return element.LookupParameter(col.Name);
    }

    private static string ReadValue(Parameter? param)
    {
        if (param is null || !param.HasValue) return "";
        return param.AsValueString() ?? param.AsString() ?? "";
    }
}
