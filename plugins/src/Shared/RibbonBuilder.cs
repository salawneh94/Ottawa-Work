// See LicenseStore.cs for why these are explicit aliases rather than plain
// "using System.IO;" (Shared has both UseWindowsForms and UseWPF).
using Path = System.IO.Path;

using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace OttawaWork.Shared;

/// <summary>
/// Every add-in in this internal Ottawa-Work build contributes into the same
/// "Ottawa Tools" ribbon tab — the unlicensed, ribbon-restricted fork of
/// OttawaWork.Shared's RibbonBuilder, which uses a "Ottawa Tools" tab and the full
/// 75+ plugin roster instead. This build's ribbon is driven from
/// OttawaRoster (not PluginRoster) in OttawaWork.QuickAccessRibbon's
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
/// OttawaWork.QuickAccessRibbon building the whole ribbon in one centralized
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
    private const string IconComponent = "OttawaWork.QuickAccessRibbon";

    /// <summary>
    /// pack:// URIs (used by LoadIcon below) only resolve once WPF's pack
    /// UriParser has been registered — which normally happens automatically
    /// the first time a System.Windows.Application is constructed, but this
    /// add-in never constructs one (Revit hosts its own message loop; every
    /// window in this codebase is a bare System.Windows.Window shown via
    /// ShowDialog, not an Application-owned one). Merely reading
    /// Application.Current — even though it's null here — still runs
    /// Application's static constructor, which is what actually registers
    /// the scheme, so this is enough without ever creating a real instance.
    /// A static constructor guarantees it runs exactly once, before the
    /// first time anything on this type is touched (including the first
    /// ApplyIcon call), with no explicit call needed at any call site.
    /// </summary>
    static RibbonBuilder()
    {
        _ = System.Windows.Application.Current;
    }

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
    /// Loads "&lt;iconKey&gt;_16.png" / "_32.png" from this project's own
    /// embedded Resources (plugins/src/QuickAccessRibbon/Resources/Icons/3d —
    /// see that folder and this project's csproj Resource item), via a
    /// pack://application:,,,/OttawaWork.QuickAccessRibbon;component/... URI
    /// rather than a loose file next to some add-in's DLL: every icon this
    /// ribbon ever shows is built here, in this one assembly, so embedding
    /// them here means they can never go missing at install time the way a
    /// loose-file copy step could silently drop one. Takes the common
    /// ButtonData base (not PushButtonData specifically) so the same call
    /// works for a PulldownButtonData too. Any failure to load (a typo'd
    /// iconKey, a genuinely missing resource) is caught and skipped — a
    /// button without an icon still works, just plainer. This is a tolerant
    /// *loading* fallback only: there is no runtime-drawn placeholder (no
    /// generated monogram/badge) behind it — every iconKey below resolves to
    /// a real, hand-drawn pictogram PNG actually shipped in Resources/Icons/3d.
    /// </summary>
    public static void ApplyIcon(ButtonData data, string iconKey)
    {
        if (TryLoadIcon(iconKey, 16, out var small)) data.Image = small;
        if (TryLoadIcon(iconKey, 32, out var large)) data.LargeImage = large;
    }

    private static bool TryLoadIcon(string iconKey, int size, out BitmapImage? image)
    {
        try
        {
            image = LoadIcon(new Uri($"pack://application:,,,/{IconComponent};component/Resources/Icons/3d/{iconKey}_{size}.png"));
            return true;
        }
        catch (Exception)
        {
            image = null;
            return false;
        }
    }

    private static BitmapImage LoadIcon(Uri uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Resolves another plugin's DLL from this add-in's own assembly
    /// location. Every OttawaWork DLL and .addin lands flat in the same
    /// install folder (see plugins/README.md), so a sibling plugin's
    /// assembly is just its own filename next to this one.
    /// </summary>
    public static string SiblingAssembly(string ownAssemblyLocation, string pluginAssemblyName) =>
        Path.Combine(Path.GetDirectoryName(ownAssemblyLocation)!, pluginAssemblyName);
}
