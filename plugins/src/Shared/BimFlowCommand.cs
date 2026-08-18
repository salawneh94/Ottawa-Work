using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMFlow.Shared;

/// <summary>
/// Base class every BIMFlow command derives from. Handles the license check
/// and turns unexpected exceptions into a readable dialog instead of a raw
/// Revit crash report, so individual commands only need to implement
/// <see cref="Run"/>.
/// </summary>
[Transaction(TransactionMode.Manual)]
public abstract class BimFlowCommand : IExternalCommand
{
    /// <summary>Catalog slug (e.g. "view-renamer") checked against the caller's license.</summary>
    protected abstract string PluginSlug { get; }

    /// <summary>Override to false for any tool BIMFlow decides to ship for free.</summary>
    // TEMP-NO-LICENSE-QA-BUILD: forced to false so testers can exercise plugin
    // behavior before the web app is deployed and can issue real license keys.
    // MUST be reverted to `true` before this ships for real — nothing should
    // be purchasable-but-free.
    protected virtual bool RequiresLicense => false;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (RequiresLicense)
            {
                var license = LicenseClient.EnsureLicensed(PluginSlug);
                if (!license.Valid)
                {
                    TaskDialog.Show(
                        "BIMFlow",
                        $"This tool isn't licensed on this machine.\n\n{license.Reason}\n\n" +
                        "Buy or activate it from your BIMFlow account, then try again.");
                    return Result.Cancelled;
                }
            }

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
