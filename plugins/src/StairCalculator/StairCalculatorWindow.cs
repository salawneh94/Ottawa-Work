using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using FontWeights = System.Windows.FontWeights;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Visibility = System.Windows.Visibility;
using Grid = System.Windows.Controls.Grid;
using Canvas = System.Windows.Controls.Canvas;
using Polyline = System.Windows.Shapes.Polyline;
using PointCollection = System.Windows.Media.PointCollection;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.StairCalculator;

/// <summary>
/// Two modes in one window: a Design Calculator that searches riser/tread
/// combinations for a given floor-to-floor rise (ranked by a DIN 18065
/// compliance score, with a small profile diagram for whichever combination
/// was last clicked) and an Audit mode that checks every real Stairs element
/// already in the model against the same rules. Deliberately doesn't attempt
/// to place a stair from a chosen combination — generating real stair run/
/// landing geometry via the API is a different order of risk (untestable
/// here beyond compiling) than searching numbers or reading existing
/// elements' own calculated parameters, so that's left as a manual step:
/// pick a combination here, then set its riser/tread on a stair type in
/// Revit yourself.
/// </summary>
public class StairCalculatorWindow : OttawaWorkWindow
{
    private readonly Document _doc;

    private readonly Button _calcModeButton;
    private readonly Button _auditModeButton;
    private readonly StackPanel _calcPanel;
    private readonly StackPanel _auditPanel;

    private readonly TextBox _floorToFloorBox = OttawaWorkUi.TextBox();
    private readonly TextBox _availableRunBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _calcResultsStack = new();
    private readonly Canvas _profileCanvas = new() { Width = 260, Height = 150, ClipToBounds = true };
    private readonly TextBlock _calcSummary = new()
    {
        FontSize = 11,
        Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
        Margin = new Thickness(0, 0, 0, 10),
        TextWrapping = System.Windows.TextWrapping.Wrap,
    };

    private readonly StackPanel _auditResultsStack = new();
    private readonly TextBlock _auditSummary = new()
    {
        FontSize = 11,
        Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
        Margin = new Thickness(0, 0, 0, 10),
        TextWrapping = System.Windows.TextWrapping.Wrap,
    };
    private readonly List<(Border RowBorder, ElementId StairId)> _auditRows = new();
    private readonly HashSet<int> _selectedAuditRows = new();

    public List<ElementId> ElementsToSelect { get; private set; } = new();

    public StairCalculatorWindow(Document doc) : base("Ottawa Tools — Stair Calculator", minWidth: 620)
    {
        _doc = doc;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar(
            "🪜",
            "Stair Calculator",
            "Design against DIN 18065's reference riser/tread thresholds, or audit stairs already in this model — a design-proportion check, not a certified code compliance review.",
            Close));

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        _calcModeButton = OttawaWorkUi.PrimaryButton("Design Calculator");
        _auditModeButton = OttawaWorkUi.SecondaryButton("Audit Existing Stairs");
        _calcModeButton.Margin = new Thickness(0, 0, 8, 0);
        _calcModeButton.Click += (_, _) => SetMode(calculate: true);
        _auditModeButton.Click += (_, _) => SetMode(calculate: false);
        modeRow.Children.Add(_calcModeButton);
        modeRow.Children.Add(_auditModeButton);
        root.Children.Add(modeRow);

        _calcPanel = BuildCalculatePanel();
        _auditPanel = BuildAuditPanel();
        _auditPanel.Visibility = Visibility.Collapsed;
        root.Children.Add(_calcPanel);
        root.Children.Add(_auditPanel);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var closeButton = OttawaWorkUi.SecondaryButton("Close");
        closeButton.Click += (_, _) => { DialogResult = false; Close(); };
        buttonRow.Children.Add(closeButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void SetMode(bool calculate)
    {
        _calcPanel.Visibility = calculate ? Visibility.Visible : Visibility.Collapsed;
        _auditPanel.Visibility = calculate ? Visibility.Collapsed : Visibility.Visible;
        OttawaWorkUi.SetToggleActive(_calcModeButton, calculate);
        OttawaWorkUi.SetToggleActive(_auditModeButton, !calculate);
    }

    private StackPanel BuildCalculatePanel()
    {
        var panel = new StackPanel();

        panel.Children.Add(OttawaWorkUi.SectionHeader("Inputs (mm)"));
        var inputStack = new StackPanel();
        inputStack.Children.Add(OttawaWorkUi.FieldLabel("Floor-to-floor height"));
        _floorToFloorBox.Text = "2800";
        inputStack.Children.Add(_floorToFloorBox);
        inputStack.Children.Add(OttawaWorkUi.FieldLabel("Available run (0 = unlimited)"));
        _availableRunBox.Text = "4000";
        inputStack.Children.Add(_availableRunBox);
        var calcButton = OttawaWorkUi.PrimaryButton("Calculate");
        calcButton.HorizontalAlignment = HorizontalAlignment.Left;
        calcButton.Click += (_, _) => RunCalculate();
        inputStack.Children.Add(calcButton);
        panel.Children.Add(OttawaWorkUi.Card(inputStack));

        panel.Children.Add(new Grid { Height = 14 });
        panel.Children.Add(OttawaWorkUi.SectionHeader("Ranked combinations"));
        panel.Children.Add(_calcSummary);
        var resultsScroll = new ScrollViewer { MaxHeight = 260, Content = _calcResultsStack };
        panel.Children.Add(OttawaWorkUi.Card(resultsScroll, padding: 8));

        panel.Children.Add(new Grid { Height = 14 });
        panel.Children.Add(OttawaWorkUi.SectionHeader("Profile (click a result)"));
        panel.Children.Add(OttawaWorkUi.Card(_profileCanvas, padding: 8));

        return panel;
    }

    private StackPanel BuildAuditPanel()
    {
        var panel = new StackPanel();

        var auditButton = OttawaWorkUi.PrimaryButton("Run audit");
        auditButton.HorizontalAlignment = HorizontalAlignment.Left;
        auditButton.Margin = new Thickness(0, 0, 0, 14);
        auditButton.Click += (_, _) => RunAudit();
        panel.Children.Add(auditButton);

        panel.Children.Add(_auditSummary);
        var resultsScroll = new ScrollViewer { MaxHeight = 340, Content = _auditResultsStack };
        panel.Children.Add(OttawaWorkUi.Card(resultsScroll, padding: 8));

        var hint = new TextBlock
        {
            Text = "Click row(s) to pick a subset for selection — leave none picked to select every flagged stair.",
            FontSize = 10,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            Margin = new Thickness(0, 8, 0, 0),
        };
        panel.Children.Add(hint);

        var selectButton = OttawaWorkUi.PrimaryButton("Select flagged in model");
        selectButton.HorizontalAlignment = HorizontalAlignment.Left;
        selectButton.Margin = new Thickness(0, 10, 0, 0);
        selectButton.Click += (_, _) => SelectFlagged();
        panel.Children.Add(selectButton);

        return panel;
    }

    private void RunCalculate()
    {
        _calcResultsStack.Children.Clear();
        _profileCanvas.Children.Clear();

        if (!double.TryParse(_floorToFloorBox.Text, out var floorToFloorMm) || floorToFloorMm <= 0)
        {
            _calcSummary.Text = "Enter a floor-to-floor height greater than 0.";
            return;
        }
        double.TryParse(_availableRunBox.Text, out var availableRunMm);

        var combos = StairCalculatorEngine.SearchCombinations(floorToFloorMm, availableRunMm);
        if (combos.Count == 0)
        {
            _calcSummary.Text = $"No riser count between {StairCalculatorEngine.RiserMinMm:0}-{StairCalculatorEngine.RiserMaxMm:0}mm fits a {floorToFloorMm:0}mm rise.";
            return;
        }
        _calcSummary.Text = $"{combos.Count} combination(s) found, best first.";

        var first = true;
        foreach (var combo in combos)
        {
            var row = BuildComboRow(combo);
            row.MouseLeftButtonDown += (_, _) => DrawProfile(combo);
            _calcResultsStack.Children.Add(row);
            if (first) { DrawProfile(combo); first = false; }
        }
    }

    private Border BuildComboRow(StairCombination combo)
    {
        var content = new StackPanel { Cursor = Cursors.Hand };
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
        headerRow.Children.Add(new TextBlock
        {
            Text = $"{combo.RiserCount} risers",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
            Margin = new Thickness(0, 0, 10, 0),
        });
        headerRow.Children.Add(OttawaWorkUi.Badge(
            $"Score {combo.Score}",
            combo.Score >= 80 ? OttawaWorkUi.Success : combo.Score >= 50 ? OttawaWorkUi.Warning : OttawaWorkUi.Danger));
        content.Children.Add(headerRow);

        content.Children.Add(new TextBlock
        {
            Text = $"Riser {combo.RiserMm:0}mm · Tread {combo.TreadMm:0}mm · 2s+a {combo.StepMeasureMm:0}mm · {combo.PitchDeg:0.0}° · Run {combo.TotalRunMm:0}mm",
            FontSize = 11,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            Margin = new Thickness(0, 3, 0, 6),
        });

        content.Children.Add(RuleBadgeRow(combo.Rules));

        return WrapRow(content);
    }

    private void DrawProfile(StairCombination combo)
    {
        _profileCanvas.Children.Clear();

        var drawnSteps = Math.Min(combo.RiserCount, 8);
        var margin = 12.0;
        var usableWidth = _profileCanvas.Width - margin * 2;
        var usableHeight = _profileCanvas.Height - margin * 2;
        var stepWidth = usableWidth / drawnSteps;
        var stepHeight = usableHeight / drawnSteps;

        var points = new PointCollection { new Point(margin, _profileCanvas.Height - margin) };
        for (var i = 1; i <= drawnSteps; i++)
        {
            var x = margin + i * stepWidth;
            var yTop = _profileCanvas.Height - margin - i * stepHeight;
            var yBottom = _profileCanvas.Height - margin - (i - 1) * stepHeight;
            points.Add(new Point(x - stepWidth, yTop));
            points.Add(new Point(x, yTop));
            points.Add(new Point(x, yBottom));
        }

        var outline = new Polyline
        {
            Points = points,
            Stroke = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent),
            StrokeThickness = 2,
        };
        _profileCanvas.Children.Add(outline);

        if (combo.RiserCount > drawnSteps)
        {
            var moreLabel = new TextBlock
            {
                Text = $"+{combo.RiserCount - drawnSteps} more",
                FontSize = 9,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            };
            Canvas.SetRight(moreLabel, margin);
            Canvas.SetTop(moreLabel, margin);
            _profileCanvas.Children.Add(moreLabel);
        }
    }

    private void RunAudit()
    {
        _auditResultsStack.Children.Clear();
        _auditRows.Clear();
        _selectedAuditRows.Clear();

        var rows = StairCalculatorEngine.AuditExistingStairs(_doc);
        if (rows.Count == 0)
        {
            _auditSummary.Text = "No stairs with a computed riser/tread were found in this project.";
            return;
        }

        var flagged = rows.Count(r => r.Rules.Any(rule => !rule.Pass));
        _auditSummary.Text = $"{rows.Count} stair(s) checked, {flagged} with at least one rule outside the DIN 18065 reference range.";

        foreach (var auditRow in rows)
        {
            var content = new StackPanel { Cursor = Cursors.Hand };
            content.Children.Add(new TextBlock
            {
                Text = $"{auditRow.StairName}" + (string.IsNullOrEmpty(auditRow.LevelName) ? "" : $" ({auditRow.LevelName})") + $"  ·  ID {auditRow.StairId.Value}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
            });
            content.Children.Add(new TextBlock
            {
                Text = $"Riser {auditRow.RiserMm:0}mm · Tread {auditRow.TreadMm:0}mm" + (auditRow.WidthMm is { } w ? $" · Width {w:0}mm" : ""),
                FontSize = 11,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
                Margin = new Thickness(0, 3, 0, 6),
            });
            content.Children.Add(RuleBadgeRow(auditRow.Rules));

            var rowBorder = WrapRow(content);
            var index = _auditRows.Count;
            rowBorder.MouseLeftButtonDown += (_, _) => ToggleAuditRow(index);
            _auditRows.Add((rowBorder, auditRow.StairId));
            _auditResultsStack.Children.Add(rowBorder);
        }
    }

    private void ToggleAuditRow(int index)
    {
        var (rowBorder, _) = _auditRows[index];
        if (!_selectedAuditRows.Add(index))
        {
            _selectedAuditRows.Remove(index);
            rowBorder.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor);
        }
        else
        {
            rowBorder.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.AccentSoft);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent);
        }
    }

    private void SelectFlagged()
    {
        var rows = StairCalculatorEngine.AuditExistingStairs(_doc);
        var flaggedIds = rows.Where(r => r.Rules.Any(rule => !rule.Pass)).Select(r => r.StairId).ToHashSet();

        ElementsToSelect = _selectedAuditRows.Count > 0
            ? _selectedAuditRows.Select(i => _auditRows[i].StairId).Where(id => flaggedIds.Contains(id)).ToList()
            : _auditRows.Select(r => r.StairId).Where(id => flaggedIds.Contains(id)).ToList();

        DialogResult = true;
        Close();
    }

    private static StackPanel RuleBadgeRow(List<StairRuleResult> rules)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var rule in rules)
        {
            var badge = OttawaWorkUi.Badge(
                $"{rule.ShortName} {(rule.Pass ? "✓" : "✗")} {rule.Detail}",
                rule.Pass ? OttawaWorkUi.Success : OttawaWorkUi.Danger);
            badge.Margin = new Thickness(0, 0, 6, 0);
            row.Children.Add(badge);
        }
        return row;
    }

    private static Border WrapRow(StackPanel content)
    {
        return new Border
        {
            Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt),
            BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 2, 0, 2),
            Child = content,
        };
    }
}
