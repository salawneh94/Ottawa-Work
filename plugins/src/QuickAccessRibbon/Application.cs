using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.QuickAccessRibbon;

/// <summary>
/// Builds the entire BIMFlow ribbon in one pass, for every plugin — not just
/// the quick-select extras this project started out with. Individual
/// plugins' own BimFlowApplication.OnStartup no longer adds anything to the
/// ribbon (see that file); it's all driven from PluginRoster.Entries here
/// instead, referencing each plugin's own Command class in its own DLL via
/// RibbonBuilder.SiblingAssembly, the same cross-assembly trick the old
/// tool-picker dashboard used.
///
/// Why centralize this: matching a native Revit ribbon's mix of large
/// "hero" buttons and small stacked-3 rows (see e.g. the View tab) needs
/// RibbonPanel.AddStackedItems, which takes 2 or 3 items in one call — it
/// can't be built by 73 separate add-ins each independently adding one
/// button during their own OnStartup with no coordination or defined
/// ordering between them.
///
/// Hero tier: the 10 plugins featured in data/plugin-impact.json (the same
/// "most valuable" list the website's homepage slider uses), capped at 2
/// per panel — extras beyond 2 are demoted back to the stacked tier so no
/// one panel is hero-heavy. Any panel with none of the 10 gets its single
/// highest-priced plugin promoted instead, so every panel has at least one
/// hero button. See the generator this was built from in git history if
/// this needs regenerating after adding a plugin.
/// </summary>
public class Application : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        var ownAssembly = GetType().Assembly.Location;

        BuildCatalogPanels(application, ownAssembly);
        BuildSelectPanel(application, ownAssembly);
        AddHighlightExtras(application, ownAssembly);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private static void BuildCatalogPanels(UIControlledApplication application, string ownAssembly)
    {
        foreach (var group in PluginRoster.Entries.GroupBy(e => e.Panel))
        {
            var panel = RibbonBuilder.EnsurePanel(application, group.Key);
            if (panel.GetItems().Count > 0) continue;

            foreach (var hero in group.Where(e => e.Hero))
                panel.AddItem(SiblingButtonData(hero, ownAssembly));

            var stacked = group.Where(e => !e.Hero).Select(e => SiblingButtonData(e, ownAssembly)).ToList();
            AddStacked(panel, stacked);
        }
    }

    private static void BuildSelectPanel(UIControlledApplication application, string ownAssembly)
    {
        var panel = RibbonBuilder.EnsurePanel(application, "Select");
        if (panel.GetItems().Count > 0) return;

        var items = new List<PushButtonData>
        {
            LocalButtonData("QaAllDoors", "All Doors", "Select every door in the active view.", typeof(SelectAllDoorsCommand), ownAssembly, "SelectAllDoors"),
            LocalButtonData("QaExtDoors", "Ext Doors", "Select every exterior door in the active view.", typeof(SelectExteriorDoorsCommand), ownAssembly, "SelectExtDoors"),
            LocalButtonData("QaIntDoors", "Int Doors", "Select every interior door in the active view.", typeof(SelectInteriorDoorsCommand), ownAssembly, "SelectIntDoors"),
            LocalButtonData("QaFloors", "Floors", "Select every floor in the active view.", typeof(SelectFloorsCommand), ownAssembly, "SelectFloors"),
            LocalButtonData("QaWindows", "Windows", "Select every window in the active view.", typeof(SelectWindowsCommand), ownAssembly, "SelectWindows"),
            LocalButtonData("QaRoofs", "Roofs", "Select every roof in the active view.", typeof(SelectRoofsCommand), ownAssembly, "SelectRoofs"),
            LocalButtonData("QaCeilings", "Ceilings", "Select every ceiling in the active view.", typeof(SelectCeilingsCommand), ownAssembly, "SelectCeilings"),
            LocalButtonData("QaBeams", "Beams", "Select every structural framing element in the active view.", typeof(SelectBeamsCommand), ownAssembly, "SelectBeams"),
            LocalButtonData("QaRooms", "Rooms", "Select every room in the active view.", typeof(SelectRoomsCommand), ownAssembly, "SelectRooms"),
        };

        AddStacked(panel, items);
    }

    private static void AddHighlightExtras(UIControlledApplication application, string ownAssembly)
    {
        var panel = RibbonBuilder.EnsurePanel(application, "Highlight");
        if (panel.GetItems().Any(i => i.Name is "QaHlExterior" or "QaHlInterior")) return;

        var items = new List<PushButtonData>
        {
            LocalButtonData("QaHlExterior", "HL Exterior", "Select every exterior wall in the active view.", typeof(HighlightExteriorWallsCommand), ownAssembly, "HLExterior"),
            LocalButtonData("QaHlInterior", "HL Interior", "Select every interior wall in the active view.", typeof(HighlightInteriorWallsCommand), ownAssembly, "HLInterior"),
        };

        AddStacked(panel, items);
    }

    private static PushButtonData SiblingButtonData(RosterEntry entry, string ownAssembly)
    {
        var assembly = RibbonBuilder.SiblingAssembly(ownAssembly, $"BIMFlow.{entry.Folder}.dll");
        var commandClass = $"BIMFlow.{entry.Folder}.Command";
        var data = new PushButtonData($"Ribbon{entry.Folder}", entry.Text, assembly, commandClass) { ToolTip = entry.Tooltip };
        RibbonBuilder.ApplyIcon(data, ownAssembly, entry.Folder);
        return data;
    }

    private static PushButtonData LocalButtonData(string internalName, string text, string tooltip, Type commandType, string ownAssembly, string iconKey)
    {
        var data = new PushButtonData(internalName, text, ownAssembly, commandType.FullName!) { ToolTip = tooltip };
        RibbonBuilder.ApplyIcon(data, ownAssembly, iconKey);
        return data;
    }

    /// <summary>
    /// Groups items into RibbonPanel.AddStackedItems calls of 3 (Revit only
    /// supports 2 or 3 per stack) — a remainder of 1 borrows from the last
    /// group-of-3 to make two groups-of-2 instead, since Revit has no
    /// 1-item stack; a genuine single-item list falls back to a plain
    /// AddItem (rendered a bit larger, but still a normal working button).
    /// </summary>
    private static void AddStacked(RibbonPanel panel, List<PushButtonData> items)
    {
        var threeCount = items.Count / 3;
        var remainder = items.Count % 3;

        if (remainder == 1 && threeCount > 0)
        {
            threeCount -= 1;
            remainder = 4;
        }

        var index = 0;
        for (var i = 0; i < threeCount; i++)
        {
            panel.AddStackedItems(items[index], items[index + 1], items[index + 2]);
            index += 3;
        }

        if (remainder == 4)
        {
            panel.AddStackedItems(items[index], items[index + 1]);
            panel.AddStackedItems(items[index + 2], items[index + 3]);
        }
        else if (remainder == 2)
        {
            panel.AddStackedItems(items[index], items[index + 1]);
        }
        else if (remainder == 1)
        {
            panel.AddItem(items[index]);
        }
    }
}
