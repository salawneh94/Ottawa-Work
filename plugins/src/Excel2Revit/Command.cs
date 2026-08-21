using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.Excel2Revit;

/// <summary>
/// Opens the Excel Sync workspace (<see cref="ExcelSyncWindow"/>): browse
/// every schedule in the project, filter/search, batch-export a chosen
/// selection (format/delimiter/encoding/filename pattern, all backed by
/// real <see cref="ExcelSyncEngine"/> calls), or load a previously exported
/// file back in and commit its parameter values to the model. Export never
/// touches the document, so it runs directly from inside the window; only
/// Import does, so that's the one thing done here, inside a transaction,
/// once the window reports <see cref="ExcelSyncWindow.DoImport"/>.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "excel2revit";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var window = new ExcelSyncWindow(doc);
        if (window.ShowDialog() != true || !window.DoImport)
            return Result.Cancelled;

        ExcelSyncEngine.ImportResult result;
        using (var transaction = new Transaction(doc, "Ottawa Tools: Import Parameter Values"))
        {
            transaction.Start();
            try
            {
                result = ExcelSyncEngine.ImportParameters(doc, window.ImportHeaders, window.ImportRows);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                throw;
            }
        }

        TaskDialog.Show("Ottawa Tools — Excel Sync", $"Updated {result.UpdatedFields} field(s) across {result.UpdatedRows} element(s).");
        return Result.Succeeded;
    }
}
