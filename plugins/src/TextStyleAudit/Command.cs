using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.TextStyleAudit;

/// <summary>
/// Scans every placed text note and dimension for its type name, lets the
/// user mark which type names are "approved" for this project, then reports
/// every instance using a type that isn't on that list.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "textstyleaudit";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var textNotes = new FilteredElementCollector(doc).OfClass(typeof(TextNote)).Cast<TextNote>().ToList();
        var dimensions = new FilteredElementCollector(doc).OfClass(typeof(Dimension)).Cast<Dimension>().ToList();

        var usedTypeNames = textNotes.Select(t => doc.GetElement(t.GetTypeId())?.Name ?? "Unknown")
            .Concat(dimensions.Select(d => doc.GetElement(d.GetTypeId())?.Name ?? "Unknown"))
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (usedTypeNames.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Text Style Audit", "No text notes or dimensions were found in this model.");
            return Result.Succeeded;
        }

        var configWindow = new StandardsPickerWindow(usedTypeNames);
        if (configWindow.ShowDialog() != true)
            return Result.Cancelled;

        var approved = configWindow.ApprovedTypeNames;

        var flagged = new List<(Element Instance, string TypeName)>();
        flagged.AddRange(textNotes
            .Where(t => !approved.Contains(doc.GetElement(t.GetTypeId())?.Name ?? string.Empty))
            .Select(t => ((Element)t, doc.GetElement(t.GetTypeId())?.Name ?? "Unknown")));
        flagged.AddRange(dimensions
            .Where(d => !approved.Contains(doc.GetElement(d.GetTypeId())?.Name ?? string.Empty))
            .Select(d => ((Element)d, doc.GetElement(d.GetTypeId())?.Name ?? "Unknown")));

        if (flagged.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Text Style Audit", "Every text note and dimension already uses an approved type.");
            return Result.Succeeded;
        }

        var rows = flagged
            .Select(f => new ResultRow(
                new[] { f.Instance.Category?.Name ?? "Unknown", f.TypeName, f.Instance.Id.ToString() },
                new List<ElementId> { f.Instance.Id }))
            .ToList();

        var results = new ResultsListForm(
            "BIMFlow — Text Style Audit Results",
            $"{flagged.Count} off-standard element(s) found.",
            new[] { "Category", "Type", "Element Id" },
            rows,
            actionButtonText: "Select all in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
        {
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);
            uiDoc.ShowElements(results.ElementsToSelect);
        }

        return Result.Succeeded;
    }
}
