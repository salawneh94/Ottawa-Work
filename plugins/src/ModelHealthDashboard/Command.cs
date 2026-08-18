using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ModelHealthDashboard;

/// <summary>
/// Logs element/warning/view/sheet counts to a local per-model history file
/// each time it's run, and shows the trend. Logging happens on-demand (when
/// you click the ribbon button), not silently on every save — a background
/// auto-logger would need its own IExternalApplication event subscription,
/// which is a reasonable follow-up but not required for the tool to be useful.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "modelhealthdashboard";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var elementCount = new FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount();
        var warningCount = doc.GetWarnings().Count;
        var viewCount = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Count(v => !v.IsTemplate);
        var sheetCount = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();

        var docTitle = string.IsNullOrEmpty(doc.Title) ? "Untitled" : doc.Title;
        var entry = new HealthEntry(DateTime.UtcNow, elementCount, warningCount, viewCount, sheetCount);
        HealthLogStore.Append(docTitle, entry);

        var history = HealthLogStore.Load(docTitle);

        var window = new HealthDashboardWindow(docTitle, history);
        window.ShowDialog();

        return Result.Succeeded;
    }
}
