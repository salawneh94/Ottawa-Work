using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMFlow.Shared;

/// <summary>
/// Base class every command in this internal Ottawa-Work build derives from.
/// This is the unlicensed fork of BIMFlow.Shared's BimFlowCommand — Ottawa-
/// Work ships as an internal firm suite, never gated behind a license check
/// or a call out to bimflow.app, so Execute() only turns unexpected
/// exceptions into a readable dialog instead of a raw Revit crash report.
/// PluginSlug stays as an abstract property (rather than being removed
/// outright) purely so every plugin's own Command.cs — identical source to
/// BIMFlow's licensed build — keeps compiling here unchanged; nothing in
/// this build actually reads it.
/// </summary>
[Transaction(TransactionMode.Manual)]
public abstract class BimFlowCommand : IExternalCommand
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
            TaskDialog.Show("BIMFlow — Error", $"{GetType().Name} hit an unexpected error:\n\n{ex.Message}");
            return Result.Failed;
        }
    }

    protected abstract Result Run(ExternalCommandData commandData, ref string message);
}
