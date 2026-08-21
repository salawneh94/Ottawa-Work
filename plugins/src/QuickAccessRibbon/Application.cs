using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.QuickAccessRibbon;

/// <summary>
/// Builds the "Ottawa Tools" ribbon tab in one pass, restricted to the
/// internal firm's own curated roster (OttawaRoster) — not the full 75+
/// plugin BIMFlow catalog (PluginRoster), which this build never references.
/// Individual plugins' own OttawaWorkApplication.OnStartup no longer adds
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
/// them. Most OttawaRoster entries are Hero=false (plain stacked, 16x16
/// icons) — only the suite's 3 primary launchers (Batch Excel Sync, Param
/// Power Suite, DIN 276 Costs) are Hero=true, rendered as large standalone
/// 32x32 buttons via AddItem below. The Select and Highlight panels
/// intentionally have no Hero entries at all — every button there is a
/// small stacked quick-action, matching the reference Direct Selection
/// layout's uniform 16x16 columns.
///
/// A roster entry can also carry a PulldownGroup name: entries sharing one
/// get nested under a single named PulldownButton (a flyout menu) instead
/// of sitting flat in the panel — see AddPulldown. Not currently used by
/// any panel (Select and Highlight both use plain stacked columns instead),
/// kept as general-purpose ribbon infrastructure.
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

            foreach (var hero in group.Where(e => e.Hero && e.PulldownGroup is null))
                panel.AddItem(SiblingButtonData(hero, ownAssembly));

            foreach (var pulldownGroup in group.Where(e => e.PulldownGroup is not null).GroupBy(e => e.PulldownGroup))
                AddPulldown(panel, pulldownGroup.Key!, pulldownGroup.ToList(), ownAssembly);

            var stacked = group.Where(e => !e.Hero && e.PulldownGroup is null).Select(e => SiblingButtonData(e, ownAssembly)).ToList();
            AddStacked(panel, stacked);
        }
    }

    /// <summary>Nests a group of roster entries under one named pulldown/flyout button instead of showing
    /// them flat in the panel — matches the reference tool's "Highlight" button opening a menu of
    /// HL Exterior/HL Interior rather than listing them as their own top-level buttons.</summary>
    private static void AddPulldown(RibbonPanel panel, string groupName, List<RosterEntry> entries, string ownAssembly)
    {
        var pulldownData = new PulldownButtonData($"Ribbon{groupName}Pulldown", groupName);
        RibbonBuilder.ApplyIcon(pulldownData, groupName);

        if (panel.AddItem(pulldownData) is not PulldownButton pulldown) return;
        foreach (var entry in entries)
            pulldown.AddPushButton(SiblingButtonData(entry, ownAssembly));
    }

    private static PushButtonData SiblingButtonData(RosterEntry entry, string ownAssembly)
    {
        var assembly = RibbonBuilder.SiblingAssembly(ownAssembly, $"OttawaWork.{entry.Folder}.dll");
        var commandClass = $"OttawaWork.{entry.Folder}.Command";
        var data = new PushButtonData($"Ribbon{entry.Folder}", entry.Text, assembly, commandClass) { ToolTip = entry.Tooltip };
        RibbonBuilder.ApplyIcon(data, entry.Folder);
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
