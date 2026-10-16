using Window = System.Windows.Window;
using Border = System.Windows.Controls.Border;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using ScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using CornerRadius = System.Windows.CornerRadius;
using Thickness = System.Windows.Thickness;
using Rect = System.Windows.Rect;
using Point = System.Windows.Point;
using UIElement = System.Windows.UIElement;
using WindowStyle = System.Windows.WindowStyle;
using ResizeMode = System.Windows.ResizeMode;
using SizeToContent = System.Windows.SizeToContent;
using WindowStartupLocation = System.Windows.WindowStartupLocation;
using SystemParameters = System.Windows.SystemParameters;
using PresentationSource = System.Windows.PresentationSource;
using WindowChrome = System.Windows.Shell.WindowChrome;
using WindowInteropHelper = System.Windows.Interop.WindowInteropHelper;
using Screen = System.Windows.Forms.Screen;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using DropShadowEffect = System.Windows.Media.Effects.DropShadowEffect;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;

namespace OttawaWork.Shared;

/// <summary>
/// Base class for every plugin dialog: a borderless, draggable, dark rounded-card window, closable with
/// Escape or its title-bar ✕. Subclasses build their own content and pass it to <see cref="SetContent"/> —
/// this only owns the window frame, not what's inside it.
///
/// Sizing: the window still opens sized to its content, but never larger than the screen it opens on,
/// and can be resized from any edge or the bottom-right grip; content that doesn't fit scrolls instead of
/// running off-screen. Confirmed live (user-reported, on a laptop): Family Param Renamer opened taller and
/// wider than the screen with no way to shrink it, because this window used ResizeMode.NoResize and an
/// unbounded SizeToContent. A WindowStyle.None + AllowsTransparency window gets no native resize border
/// from Windows at all, which is why the resize edges come from WindowChrome rather than ResizeMode alone.
/// </summary>
public abstract class OttawaWorkWindow : Window
{
    private const double ScreenMargin = 16;
    private readonly double _layoutMinWidth;

    protected OttawaWorkWindow(string title, double minWidth = 420)
    {
        Title = title;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // The tool's preferred layout width now lives on the card inside the scroll area (SetContent),
        // not on the window — so the window itself can be made narrower than the layout and scroll.
        _layoutMinWidth = minWidth;
        MinWidth = 360;
        MinHeight = 240;

        // First guess from the primary screen; refined to the actual screen in OnSourceInitialized.
        var workArea = SystemParameters.WorkArea;
        MaxWidth = Math.Max(MinWidth, workArea.Width - 2 * ScreenMargin);
        MaxHeight = Math.Max(MinHeight, workArea.Height - 2 * ScreenMargin);

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            ResizeBorderThickness = new Thickness(8),
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnKeyDown;
    }

    /// <summary>Caps the window to the work area of the screen it's actually opening on (Revit may be on a
    /// second monitor), in device-independent units, then keeps it fully on that screen once sized.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var workArea = CurrentWorkArea();
        if (workArea is not { } area) return;

        MaxWidth = Math.Max(MinWidth, area.Width - 2 * ScreenMargin);
        MaxHeight = Math.Max(MinHeight, area.Height - 2 * ScreenMargin);
        Loaded += (_, _) => KeepOnScreen(area);
    }

    private Rect? CurrentWorkArea()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var pixels = Screen.FromHandle(handle).WorkingArea;
            var toDips = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
            if (toDips is not { } transform) return SystemParameters.WorkArea;
            var topLeft = transform.Transform(new Point(pixels.Left, pixels.Top));
            var bottomRight = transform.Transform(new Point(pixels.Right, pixels.Bottom));
            return new Rect(topLeft, bottomRight);
        }
        catch (Exception)
        {
            return SystemParameters.WorkArea;
        }
    }

    private void KeepOnScreen(Rect area)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || double.IsNaN(Left) || double.IsNaN(Top)) return;
        Left = Math.Max(area.Left + ScreenMargin / 2, Math.Min(Left, area.Right - ActualWidth - ScreenMargin / 2));
        Top = Math.Max(area.Top + ScreenMargin / 2, Math.Min(Top, area.Bottom - ActualHeight - ScreenMargin / 2));
    }

    /// <summary>Wraps the given content in the standard dark rounded card + drop shadow and sets it as the
    /// window's content. The content sits in a scroll area, so a window smaller than its layout scrolls.</summary>
    protected void SetContent(UIElement content, double padding = 24)
    {
        Content = new Border
        {
            Background = OttawaWorkUi.BrushOf(OttawaWorkUi.Background),
            BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(padding, padding, padding / 2, padding / 2),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, padding / 2, padding / 2),
                Content = new Border { MinWidth = _layoutMinWidth, Child = content },
            },
            Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.45, BlurRadius = 36, ShadowDepth = 10 },
        };
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OttawaWorkUi.SafeDragMove(this, e);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
