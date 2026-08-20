using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.QuickAccessRibbon;

/// <summary>
/// Builds the "Ottawa Tools" ribbon tab in one pass, restricted to the
/// internal firm's own curated roster (OttawaRoster) — not the full 75+
/// plugin BIMFlow catalog (PluginRoster), which this build never references.
/// No Select panel and no ad-hoc Highlight extras here either: those are
/// BIMFlow-catalog conveniences, not part of the firm's 7 named panels.
/// Individual plugins' own BimFlowApplication.OnStartup no longer adds
/// anything to the ribbon (see that file); it's all driven from
/// OttawaRoster.Entries here instead, referencing each plugin's own Command
/// class in its own DLL via RibbonBuilder.SiblingAssembly, the same cross-
/// assembly trick the old tool-picker dashboard used.
///
/// Why centralize this: matching a native Revit ribbon's mix of large
/// "hero" buttons and small stacked-3 rows (see e.g. the View tab) needs
/// RibbonPanel.AddStackedItems, which takes 2 or 3 items in one call — it
/// can't be built by independent add-ins each adding one button during
/// their own OnStartup with no coordination or defined ordering between
/// them. Every OttawaRoster entry is Hero=false (plain stacked buttons) —
/// a 23-tool internal suite doesn't need the catalog's hero-tier styling.
/// </summary>
public class Application : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        var ownAssembly = GetType().Assembly.Location;

        BuildCatalogPanels(application, ownAssembly);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private static void BuildCatalogPanels(UIControlledApplication application, string ownAssembly)
    {
        foreach (var group in OttawaRoster.Entries.GroupBy(e => e.Panel))
        {
            var panel = RibbonBuilder.EnsurePanel(application, group.Key);
            if (panel.GetItems().Count > 0) continue;

            foreach (var hero in group.Where(e => e.Hero))
                panel.AddItem(SiblingButtonData(hero, ownAssembly));

            var stacked = group.Where(e => !e.Hero).Select(e => SiblingButtonData(e, ownAssembly)).ToList();
            AddStacked(panel, stacked);
        }
    }

    private static PushButtonData SiblingButtonData(RosterEntry entry, string ownAssembly)
    {
        var assembly = RibbonBuilder.SiblingAssembly(ownAssembly, $"BIMFlow.{entry.Folder}.dll");
        var commandClass = $"BIMFlow.{entry.Folder}.Command";
        var data = new PushButtonData($"Ribbon{entry.Folder}", entry.Text, assembly, commandClass) { ToolTip = entry.Tooltip };
        RibbonBuilder.ApplyIcon(data, ownAssembly, entry.Folder);
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
