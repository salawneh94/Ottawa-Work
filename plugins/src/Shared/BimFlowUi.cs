using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using RadioButton = System.Windows.Controls.RadioButton;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using Border = System.Windows.Controls.Border;
using CornerRadius = System.Windows.CornerRadius;
using FontWeights = System.Windows.FontWeights;
// Directory.Build.props declares a solution-wide global alias
// ("Using Include=Autodesk.Revit.DB.Color Alias=Color") so every other
// plugin's bare "Color" means the Revit one. A file-level "using Color =
// System.Windows.Media.Color;" here would collide with that global alias
// (CS1537: an alias can't be redefined, global or not) — so this uses a
// distinctly-named alias instead of trying to reclaim the bare name.
using WpfColor = System.Windows.Media.Color;

using System.Windows;
using System.Windows.Media;

namespace BIMFlow.Shared;

/// <summary>
/// Shared dark UI palette + a small set of programmatically-built styled
/// elements (cards, section headers, stat tiles, badges, buttons, checkbox
/// items), reused across the tool-picker dashboard and every converted
/// plugin dialog so they all read as one product instead of 70+ bespoke
/// WinForms dialogs. Built as plain C# helpers rather than XAML
/// styles/templates — there's no way to visually iterate on this blind,
/// and a typo in a declarative ControlTemplate fails silently at runtime
/// far more easily than a C# compile error does.
/// </summary>
public static class BimFlowUi
{
    public static readonly WpfColor Background = WpfColor.FromRgb(0x0B, 0x12, 0x20);
    public static readonly WpfColor CardBackground = WpfColor.FromRgb(0x13, 0x1B, 0x2E);
    public static readonly WpfColor CardBackgroundAlt = WpfColor.FromRgb(0x1A, 0x24, 0x3A);
    public static readonly WpfColor BorderColor = WpfColor.FromRgb(0x22, 0x2E, 0x47);
    public static readonly WpfColor Accent = WpfColor.FromRgb(0x25, 0x63, 0xEB);
    public static readonly WpfColor AccentSoft = WpfColor.FromArgb(0x30, 0x25, 0x63, 0xEB);
    public static readonly WpfColor TextPrimary = WpfColor.FromRgb(0xF1, 0xF5, 0xF9);
    public static readonly WpfColor TextSecondary = WpfColor.FromRgb(0x94, 0xA3, 0xB8);
    public static readonly WpfColor Success = WpfColor.FromRgb(0x22, 0xC5, 0x5E);
    public static readonly WpfColor Warning = WpfColor.FromRgb(0xF5, 0x9E, 0x0B);
    public static readonly WpfColor Danger = WpfColor.FromRgb(0xEF, 0x44, 0x44);

    public static SolidColorBrush BrushOf(WpfColor c) => new(c);

    /// <summary>A rounded, bordered dark panel — the basic building block every section sits in.</summary>
    public static Border Card(UIElement content, double padding = 16)
    {
        return new Border
        {
            Background = BrushOf(CardBackground),
            BorderBrush = BrushOf(BorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(padding),
            Child = content,
        };
    }

    /// <summary>A small-caps label with a colored accent bar — "SWAP RULES", "SCOPE", etc.</summary>
    public static UIElement SectionHeader(string text, WpfColor? color = null)
    {
        var bar = new Border
        {
            Width = 3,
            Background = BrushOf(color ?? Accent),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 8, 2),
        };
        var label = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushOf(color ?? Accent),
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(bar);
        stack.Children.Add(label);
        return stack;
    }

    /// <summary>A big-number metric tile — "49 elements affected", "16 Valid", etc.</summary>
    public static Border StatTile(string value, string label, WpfColor? valueColor = null)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = BrushOf(valueColor ?? TextPrimary),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = BrushOf(TextSecondary),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });

        return new Border
        {
            Background = BrushOf(CardBackgroundAlt),
            BorderBrush = BrushOf(BorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = stack,
        };
    }

    /// <summary>A small rounded pill label — "27 instances", "Layers 24", etc.</summary>
    public static Border Badge(string text, WpfColor? color = null)
    {
        var c = color ?? Accent;
        return new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(0x28, c.R, c.G, c.B)),
            BorderBrush = BrushOf(c),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Child = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.Medium, Foreground = BrushOf(c) },
        };
    }

    /// <summary>The solid blue call-to-action button — "APPLY SWAP", "GENERATE", etc.</summary>
    public static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Content = text.ToUpperInvariant(),
            Background = BrushOf(Accent),
            Foreground = BrushOf(TextPrimary),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 12, 0, 12),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Template = RoundedButtonTemplate();
        return button;
    }

    /// <summary>A quieter, outlined button for secondary actions.</summary>
    public static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Background = BrushOf(CardBackgroundAlt),
            Foreground = BrushOf(TextPrimary),
            FontSize = 12,
            BorderBrush = BrushOf(BorderColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 8, 14, 8),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Template = RoundedButtonTemplate();
        return button;
    }

    private static System.Windows.Controls.ControlTemplate RoundedButtonTemplate()
    {
        var template = new System.Windows.Controls.ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

        var presenter = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;
        return template;
    }

    /// <summary>Standard page title row: icon glyph, name, subtitle — the header every dialog starts with.</summary>
    public static UIElement TitleBar(string icon, string title, string subtitle)
    {
        var iconBorder = new Border
        {
            Width = 36,
            Height = 36,
            Background = BrushOf(AccentSoft),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = icon,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Margin = new Thickness(0, 0, 12, 0),
        };

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = BrushOf(TextPrimary) });
        textStack.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = BrushOf(TextSecondary), Margin = new Thickness(0, 2, 0, 0), TextWrapping = System.Windows.TextWrapping.Wrap, MaxWidth = 420 });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        row.Children.Add(iconBorder);
        row.Children.Add(textStack);
        return row;
    }

    /// <summary>A dark-styled checkbox row — for category/option pickers.</summary>
    public static CheckBox CheckBoxItem(string text, bool isChecked = false)
    {
        return new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            Foreground = BrushOf(TextPrimary),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 5),
        };
    }

    /// <summary>
    /// A dark-styled dropdown, closed box and open popup both. WPF's
    /// Foreground property is inherited, so the near-white text set below
    /// for the closed box was bleeding into the popup too — but the
    /// popup's own background stayed the default white (never themed),
    /// making every dropdown item read as near-white-on-white: legible only
    /// for the one row highlighted blue on selection. ItemContainerStyle
    /// gives every row its own explicit dark background/text instead of
    /// inheriting anything, and the two SystemColors resource keys below
    /// cover the popup's own default-template chrome around those rows —
    /// deliberately not a full custom ControlTemplate (the popup's open/
    /// close animation, sizing, and placement logic all keep working
    /// exactly as before; only colors change).
    /// </summary>
    public static ComboBox ComboBox()
    {
        var combo = new ComboBox
        {
            Background = BrushOf(CardBackgroundAlt),
            Foreground = BrushOf(TextPrimary),
            BorderBrush = BrushOf(BorderColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 10),
            ItemContainerStyle = ComboBoxItemStyle(),
        };

        combo.Resources[System.Windows.SystemColors.WindowBrushKey] = BrushOf(CardBackgroundAlt);
        combo.Resources[System.Windows.SystemColors.ControlTextBrushKey] = BrushOf(TextPrimary);

        return combo;
    }

    private static System.Windows.Style ComboBoxItemStyle()
    {
        var style = new System.Windows.Style(typeof(System.Windows.Controls.ComboBoxItem));
        style.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.BackgroundProperty, BrushOf(CardBackgroundAlt)));
        style.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.ForegroundProperty, BrushOf(TextPrimary)));
        style.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
        return style;
    }

    /// <summary>A dark-styled single-line text input.</summary>
    public static TextBox TextBox()
    {
        return new TextBox
        {
            Background = BrushOf(CardBackgroundAlt),
            Foreground = BrushOf(TextPrimary),
            CaretBrush = BrushOf(TextPrimary),
            BorderBrush = BrushOf(BorderColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 10),
        };
    }

    /// <summary>A muted field label above an input — "Swap from:", etc.</summary>
    public static TextBlock FieldLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = BrushOf(TextSecondary),
            Margin = new Thickness(0, 0, 0, 4),
        };
    }

    /// <summary>A dark-styled radio button — pass the same groupName to every option in one choice set.</summary>
    public static RadioButton RadioButtonItem(string text, string groupName, bool isChecked = false)
    {
        return new RadioButton
        {
            Content = text,
            GroupName = groupName,
            IsChecked = isChecked,
            Foreground = BrushOf(TextPrimary),
            FontSize = 13,
            Margin = new Thickness(0, 4, 16, 4),
        };
    }

    /// <summary>
    /// WPF's ItemCollection has no AddRange (unlike WinForms' ComboBox.Items) —
    /// this fills the gap for the many dialogs bulk-populating a ComboBox
    /// from a collector/enum, ported over from the old WinForms code.
    /// </summary>
    public static void AddRange(this System.Windows.Controls.ItemCollection items, IEnumerable<object> values)
    {
        foreach (var value in values) items.Add(value);
    }

    /// <summary>
    /// Drags <paramref name="window"/> from a MouseLeftButtonDown handler, but
    /// only when the click didn't originate on (or inside) an interactive
    /// control. Calling Window.DragMove() while a TextBox is handling its own
    /// caret/focus click, or while a custom clickable row (ResultsListForm,
    /// WarningResultsWindow — anything with Cursor = Hand) is handling its own
    /// click, starts DragMove's native reentrant move-loop on top of that
    /// control's own input handling. In a hosted process like Revit that
    /// reentrancy is a known cause of hard, unrecoverable crashes — not a
    /// catchable .NET exception — so this must be prevented before DragMove
    /// is ever called, not caught afterward.
    /// </summary>
    public static void SafeDragMove(System.Windows.Window window, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState != System.Windows.Input.MouseButtonState.Pressed) return;
        if (IsInteractive(e.OriginalSource as DependencyObject)) return;

        try { window.DragMove(); }
        catch (InvalidOperationException) { /* button already released by the time the move-loop started */ }
    }

    private static bool IsInteractive(DependencyObject? element)
    {
        while (element is not null and not System.Windows.Window)
        {
            if (element is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox or CheckBox or RadioButton or System.Windows.Controls.Primitives.ButtonBase)
                return true;
            if (element is FrameworkElement fe && ReferenceEquals(fe.Cursor, System.Windows.Input.Cursors.Hand))
                return true;

            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}
