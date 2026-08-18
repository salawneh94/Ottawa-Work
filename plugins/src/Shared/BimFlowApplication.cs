using Autodesk.Revit.UI;

namespace BIMFlow.Shared;

/// <summary>
/// Base <see cref="IExternalApplication"/> every plugin's own Application.cs
/// derives from. Ribbon registration used to happen here, one plugin at a
/// time as each add-in's own OnStartup ran — but matching a native Revit
/// ribbon's mix of large "hero" buttons and small stacked-3 rows needs
/// RibbonPanel.AddStackedItems, which needs the full roster of a panel's
/// buttons up front, not 73 independent add-ins adding one button each with
/// no defined order between them. All of that now happens in one place —
/// BIMFlow.QuickAccessRibbon's Application.cs, driven by PluginRoster — so
/// this class's OnStartup is intentionally a no-op. The abstract properties
/// stay (every plugin's Application.cs still declares them) as a compile-
/// time record of what each plugin's own panel/text/tooltip/command is,
/// even though nothing reads them at runtime anymore.
/// </summary>
public abstract class BimFlowApplication : IExternalApplication
{
    protected abstract string PanelName { get; }
    protected abstract string ButtonInternalName { get; }
    protected abstract string ButtonText { get; }
    protected abstract string ToolTip { get; }
    protected abstract string CommandFullClassName { get; }

    public Result OnStartup(UIControlledApplication application) => Result.Succeeded;

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
