using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.HighlightInterior;

/// <summary>One click colors every interior wall in the active view blue, a second click clears it — see WallHighlighter.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "highlightinterior";

    private static readonly Color InteriorColor = new(0, 130, 200);

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var analysis = WallHighlighter.Analyze(doc, view, WallFunction.Interior, InteriorColor);
        if (analysis.Walls.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — HL Interior", "No interior walls are visible in the active view.");
            return Result.Succeeded;
        }

        using var transaction = new Transaction(doc, analysis.CurrentlyOn ? "Ottawa Tools: Clear HL Interior" : "Ottawa Tools: HL Interior");
        transaction.Start();
        try
        {
            WallHighlighter.Apply(view, analysis, InteriorColor);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "Ottawa Tools — HL Interior",
            analysis.CurrentlyOn
                ? $"Cleared the highlight on {analysis.Walls.Count} interior wall(s)."
                : $"Highlighted {analysis.Walls.Count} interior wall(s) in blue.");

        return Result.Succeeded;
    }
}
