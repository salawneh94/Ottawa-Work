using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.QuickAccessRibbon;

/// <summary>
/// One-click category selectors for the "Select" and "Highlight" quick-access
/// panels — thin wrappers around QuickCategorySelect, one per hardcoded
/// category/filter so each ribbon button needs no picker dialog. These are
/// bundled ribbon utilities, not separately-sold catalog plugins, so
/// RequiresLicense is explicitly false regardless of what BimFlowCommand's
/// own default is set to.
/// </summary>
public abstract class QuickSelectCommand : BimFlowCommand
{
    protected override bool RequiresLicense => false;
}

[Transaction(TransactionMode.Manual)]
public class SelectAllDoorsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-alldoors";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Doors, "doors");
}

[Transaction(TransactionMode.Manual)]
public class SelectExteriorDoorsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-extdoors";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectByWallFunction(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Doors, WallFunction.Exterior, "exterior doors");
}

[Transaction(TransactionMode.Manual)]
public class SelectInteriorDoorsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-intdoors";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectByWallFunction(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Doors, WallFunction.Interior, "interior doors");
}

[Transaction(TransactionMode.Manual)]
public class SelectFloorsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-floors";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Floors, "floors");
}

[Transaction(TransactionMode.Manual)]
public class SelectWindowsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-windows";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Windows, "windows");
}

[Transaction(TransactionMode.Manual)]
public class SelectRoofsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-roofs";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Roofs, "roofs");
}

[Transaction(TransactionMode.Manual)]
public class SelectCeilingsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-ceilings";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Ceilings, "ceilings");
}

[Transaction(TransactionMode.Manual)]
public class SelectBeamsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-beams";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_StructuralFraming, "structural framing (beams)");
}

[Transaction(TransactionMode.Manual)]
public class SelectRoomsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-rooms";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectAll(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Rooms, "rooms");
}

[Transaction(TransactionMode.Manual)]
public class HighlightExteriorWallsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-hlexterior";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectByWallFunction(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Walls, WallFunction.Exterior, "exterior walls");
}

[Transaction(TransactionMode.Manual)]
public class HighlightInteriorWallsCommand : QuickSelectCommand
{
    protected override string PluginSlug => "quickaccess-hlinterior";
    protected override Result Run(ExternalCommandData commandData, ref string message) =>
        QuickCategorySelect.SelectByWallFunction(commandData.Application.ActiveUIDocument, BuiltInCategory.OST_Walls, WallFunction.Interior, "interior walls");
}
