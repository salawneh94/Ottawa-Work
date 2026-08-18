using Window = System.Windows.Window;
using Border = System.Windows.Controls.Border;
using CornerRadius = System.Windows.CornerRadius;
using Thickness = System.Windows.Thickness;
using UIElement = System.Windows.UIElement;
using WindowStyle = System.Windows.WindowStyle;
using ResizeMode = System.Windows.ResizeMode;
using SizeToContent = System.Windows.SizeToContent;
using WindowStartupLocation = System.Windows.WindowStartupLocation;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using DropShadowEffect = System.Windows.Media.Effects.DropShadowEffect;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;

namespace BIMFlow.Shared;

/// <summary>
/// Base class for every converted plugin dialog: a borderless, draggable,
/// dark rounded-card window (same chrome as the tool-picker dashboard),
/// closable with Escape. Subclasses build their own content and pass it to
/// <see cref="SetContent"/> — this only owns the window frame, not what's
/// inside it.
/// </summary>
public abstract class BimFlowWindow : Window
{
    protected BimFlowWindow(string title, double minWidth = 420)
    {
        Title = title;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        MinWidth = minWidth;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnKeyDown;
    }

    /// <summary>Wraps the given content in the standard dark rounded card + drop shadow and sets it as the window's content.</summary>
    protected void SetContent(UIElement content, double padding = 24)
    {
        Content = new Border
        {
            Background = BimFlowUi.BrushOf(BimFlowUi.Background),
            BorderBrush = BimFlowUi.BrushOf(BimFlowUi.BorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(padding),
            MinWidth = MinWidth,
            Child = content,
            Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.45, BlurRadius = 36, ShadowDepth = 10 },
        };
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BimFlowUi.SafeDragMove(this, e);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
