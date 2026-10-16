using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OttawaWork.Shared;

/// <summary>
/// Base class every command in this internal Ottawa-Work build derives from.
/// This is the unlicensed fork of OttawaWork.Shared's OttawaWorkCommand — Ottawa-
/// Work ships as an internal firm suite, never gated behind a license check
/// or a call out to bimflow.app, so Execute() only turns unexpected
/// exceptions into a readable dialog instead of a raw Revit crash report.
/// PluginSlug stays as an abstract property (rather than being removed
/// outright) purely so every plugin's own Command.cs — identical source to
/// BIMFlow's licensed build — keeps compiling here unchanged; nothing in
/// this build actually reads it.
/// </summary>
[Transaction(TransactionMode.Manual)]
public abstract class OttawaWorkCommand : IExternalCommand
{
    /// <summary>Catalog slug (e.g. "view-renamer") — unused in this unlicensed build, kept so plugin Command.cs files stay identical to BIMFlow's.</summary>
    protected abstract string PluginSlug { get; }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            return Run(commandData, ref message);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            // Named by tool (every plugin's class is just "Command", which told nobody which tool failed)
            // and with the first Ottawa Tools stack frame, so a screenshot of this dialog is enough to
            // find the line that threw.
            var tool = GetType().Namespace?.Split('.').LastOrDefault() ?? GetType().Name;
            var where = ex.StackTrace?
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Contains("OttawaWork."));
            new TaskDialog("Ottawa Tools — Error")
            {
                MainInstruction = $"{tool} hit an unexpected error",
                MainContent = ex.Message,
                ExpandedContent = $"{ex.GetType().Name}\n{where ?? ex.StackTrace ?? ""}",
            }.Show();
            return Result.Failed;
        }
    }

    protected abstract Result Run(ExternalCommandData commandData, ref string message);
}
