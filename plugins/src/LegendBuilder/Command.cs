using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.LegendBuilder;

/// <summary>
/// Duplicates a legend view you pick as a starting template, then places
/// one instance of every detail component and annotation symbol type
/// actually used in the model into it, in a labeled grid. Revit has no API
/// to create a legend view from nothing, so this always needs at least one
/// existing (even blank) legend view to duplicate — the same constraint
/// the native "New Legend" ribbon command has under the hood.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "legendbuilder";

    private const double ColumnSpacingFeet = 4.0;
    private const double RowSpacingFeet = 3.0;
    private const int Columns = 6;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var legends = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
            .OrderBy(v => v.Name)
            .ToList();

        if (legends.Count == 0)
        {
            TaskDialog.Show(
                "Ottawa Tools — LegendBuilder",
                "No legend views were found to use as a starting point. Create one blank legend view first (View tab → Legends → Legend), then run this again.");
            return Result.Cancelled;
        }

        var picker = new SimplePickerDialog(
            "Ottawa Tools — LegendBuilder",
            "Duplicate which legend as the starting point?",
            legends.Select(v => v.Name).ToList());

        if (picker.ShowDialog() != true || picker.SelectedText is null)
            return Result.Cancelled;

        var sourceLegend = legends.First(v => v.Name == picker.SelectedText);

        var usedTypeIds = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(fi => fi.Category is not null &&
                         (fi.Category.Id.Value == (long)BuiltInCategory.OST_DetailComponents ||
                          fi.Category.Id.Value == (long)BuiltInCategory.OST_GenericAnnotation))
            .Select(fi => fi.GetTypeId())
            .Distinct()
            .ToList();

        var symbols = usedTypeIds
            .Select(id => doc.GetElement(id) as FamilySymbol)
            .Where(s => s is not null)
            .Cast<FamilySymbol>()
            .OrderBy(s => s.Family.Name).ThenBy(s => s.Name)
            .ToList();

        if (symbols.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — LegendBuilder", "No used detail component or annotation symbol types were found in the model.");
            return Result.Succeeded;
        }

        var textNoteTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        var placed = 0;

        using var transaction = new Transaction(doc, "Ottawa Tools: Build Legend");
        transaction.Start();
        try
        {
            var newLegendId = sourceLegend.Duplicate(ViewDuplicateOption.Duplicate);
            var newLegend = (View)doc.GetElement(newLegendId);
            newLegend.Name = MakeUniqueName(doc, $"{sourceLegend.Name} — Auto-Built");

            for (var i = 0; i < symbols.Count; i++)
            {
                var symbol = symbols[i];
                if (!symbol.IsActive) symbol.Activate();

                var col = i % Columns;
                var row = i / Columns;
                var origin = new XYZ(col * ColumnSpacingFeet, -row * RowSpacingFeet, 0);

                doc.Create.NewFamilyInstance(origin, symbol, newLegend);

                if (textNoteTypeId != ElementId.InvalidElementId)
                {
                    var labelPosition = new XYZ(col * ColumnSpacingFeet, -row * RowSpacingFeet - 1.0, 0);
                    TextNote.Create(doc, newLegend.Id, labelPosition, symbol.Family.Name + " — " + symbol.Name, textNoteTypeId);
                }

                placed++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — LegendBuilder", $"Built a new legend with {placed} type(s) placed.");
        return Result.Succeeded;
    }

    private static string MakeUniqueName(Document doc, string baseName)
    {
        var existingNames = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Select(v => v.Name)
            .ToHashSet();

        if (!existingNames.Contains(baseName)) return baseName;

        var i = 2;
        while (existingNames.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }
}
