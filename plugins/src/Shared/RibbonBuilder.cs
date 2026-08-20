// See LicenseStore.cs for why these are explicit aliases rather than plain
// "using System.IO;" (Shared has both UseWindowsForms and UseWPF).
using Path = System.IO.Path;
using File = System.IO.File;

using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace BIMFlow.Shared;

/// <summary>
/// Every add-in in this internal Ottawa-Work build contributes into the same
/// "Ottawa Tools" ribbon tab — the unlicensed, ribbon-restricted fork of
/// BIMFlow.Shared's RibbonBuilder, which uses a "BIMFlow" tab and the full
/// 75+ plugin roster instead. This build's ribbon is driven from
/// OttawaRoster (not PluginRoster) in BIMFlow.QuickAccessRibbon's
/// Application.cs, restricted to the internal firm's own curated tool set.
///
/// An earlier version routed each category through one shared button that
/// opened a custom WPF tool-picker window (BIMFlow.Dashboard), with every
/// individual plugin tucked into a hidden "More" dropdown behind it. A
/// customer's Revit journal showed that flyout crashing Revit outright on
/// open, every time, while every individual plugin invoked directly (via
/// that same "More" dropdown) worked fine — so the flyout is gone entirely,
/// not just deprioritized, and BIMFlow.Dashboard's code went with it.
///
/// A later version had every plugin's own Application.cs add its own button
/// here directly (RibbonBuilder.AddButton). That's gone too, in favor of
/// BIMFlow.QuickAccessRibbon building the whole ribbon in one centralized
/// pass — see that project's Application.cs for why (matching a native
/// Revit ribbon's mix of large/stacked buttons needs the full roster of a
/// panel up front, not 73 independent add-ins each adding one button with
/// no defined order between them). This class now only holds what that
/// central build actually needs: panel lookup, icon loading, and cross-
/// assembly DLL path resolution.
/// </summary>
public static class RibbonBuilder
{
    private const string TabName = "Ottawa Tools";

    public static RibbonPanel EnsurePanel(UIControlledApplication app, string panelName)
    {
        try
        {
            app.CreateRibbonTab(TabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Tab already created by another add-in loaded earlier this session.
        }

        var existing = app.GetRibbonPanels(TabName).FirstOrDefault(p => p.Name == panelName);
        return existing ?? app.CreateRibbonPanel(TabName, panelName);
    }

    /// <summary>
    /// Looks up "&lt;iconKey&gt;_16.png" / "_32.png" next to the add-in's own
    /// DLL, in an "Icons" subfolder (pre-baked PNGs, not rendered at
    /// runtime — see plugins/src/Shared/Icons and the CI packaging step
    /// that copies them alongside every DLL). Missing files are skipped
    /// silently; a button without an icon still works, just plainer. Takes
    /// the common ButtonData base (not PushButtonData specifically) so the
    /// same call works for a PulldownButtonData too.
    /// </summary>
    public static void ApplyIcon(ButtonData data, string assemblyLocation, string iconKey)
    {
        var iconsDir = Path.Combine(Path.GetDirectoryName(assemblyLocation)!, "Icons");
        var small = Path.Combine(iconsDir, $"{iconKey}_16.png");
        var large = Path.Combine(iconsDir, $"{iconKey}_32.png");

        if (File.Exists(small)) data.Image = LoadIcon(small);
        if (File.Exists(large)) data.LargeImage = LoadIcon(large);
    }

    private static BitmapImage LoadIcon(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Resolves another plugin's DLL from this add-in's own assembly
    /// location. Every BIMFlow DLL and .addin lands flat in the same
    /// install folder (see plugins/README.md), so a sibling plugin's
    /// assembly is just its own filename next to this one.
    /// </summary>
    public static string SiblingAssembly(string ownAssemblyLocation, string pluginAssemblyName) =>
        Path.Combine(Path.GetDirectoryName(ownAssemblyLocation)!, pluginAssemblyName);
}
