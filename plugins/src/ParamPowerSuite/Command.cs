using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ParamPowerSuite;

/// <summary>
/// Opens the Param Power Suite. Unlike every other BIMFlow command, this
/// one doesn't open a transaction itself — ParamPowerSuiteWindow does that
/// per Apply click, once for each of its 7 tabs, since the whole point of
/// this tool is running several independent actions in one open session
/// rather than one pending action deferred to here. See that class's own
/// doc comment for why.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "parampowersuite";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        new ParamPowerSuiteWindow(doc).ShowDialog();
        return Result.Succeeded;
    }
}
