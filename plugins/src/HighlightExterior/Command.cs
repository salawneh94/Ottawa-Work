using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.HighlightExterior;

/// <summary>One click colors every exterior wall in the active view red, a second click clears it — see WallHighlighter.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "highlightexterior";

    private static readonly Color ExteriorColor = new(230, 25, 75);

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var analysis = WallHighlighter.Analyze(doc, view, WallFunction.Exterior, ExteriorColor);
        if (analysis.Walls.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — HL Exterior", "No exterior walls are visible in the active view.");
            return Result.Succeeded;
        }

        using var transaction = new Transaction(doc, analysis.CurrentlyOn ? "Ottawa Tools: Clear HL Exterior" : "Ottawa Tools: HL Exterior");
        transaction.Start();
        try
        {
            WallHighlighter.Apply(view, analysis, ExteriorColor);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "Ottawa Tools — HL Exterior",
            analysis.CurrentlyOn
                ? $"Cleared the highlight on {analysis.Walls.Count} exterior wall(s)."
                : $"Highlighted {analysis.Walls.Count} exterior wall(s) in red.");

        return Result.Succeeded;
    }
}
