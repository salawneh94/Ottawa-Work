using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using FontWeights = System.Windows.FontWeights;
using TextTrimming = System.Windows.TextTrimming;
using Cursors = System.Windows.Input.Cursors;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using WpfColor = System.Windows.Media.Color;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.OverriddenDimensionDetector;

public enum DetectorAction { None, Select, Fix }

/// <summary>
/// Scans the model for overridden/annotated dimensions (OverriddenDimensionEngine),
/// shows them grouped by severity with clickable filter chips, and hands off
/// to either a plain model selection or the shared DimensionEditorWindow to
/// actually fix what's flagged — this window never writes to the model
/// itself (no transaction happens here), matching how every other dialog in
/// this codebase only collects intent for Command.cs to act on.
/// </summary>
public class OverriddenDimensionDetectorWindow : OttawaWorkWindow
{
    private readonly Document _doc;
    private List<OverriddenDimensionRow> _allRows = new();
    private DimensionOverrideSeverity? _activeFilter;

    private readonly StackPanel _chipRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
    private readonly StackPanel _resultsStack = new();
    private readonly TextBlock _summary = new()
    {
        FontSize = 11,
        Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
        Margin = new Thickness(0, 0, 0, 8),
    };

    private readonly List<(Border RowBorder, OverriddenDimensionRow Row)> _rowBorders = new();
    private readonly HashSet<int> _selectedIndices = new();

    public DetectorAction Action { get; private set; } = DetectorAction.None;
    public List<OverriddenDimensionRow> TargetRows { get; private set; } = new();

    public OverriddenDimensionDetectorWindow(Document doc) : base("Ottawa Tools — Overridden Dimension Detector", minWidth: 720)
    {
        _doc = doc;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar(
            "🔍",
            "Overridden Dimension Detector",
            "Scans every dimension for a manual override or annotation and flags what it finds — a reference check against DIN/local code isn't performed, this only compares what's shown to what the model actually measures."));

        root.Children.Add(_chipRow);

        var toolRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var rescanButton = OttawaWorkUi.SecondaryButton("Rescan");
        var exportButton = OttawaWorkUi.SecondaryButton("Export CSV");
        rescanButton.Margin = new Thickness(0, 0, 6, 0);
        rescanButton.Click += (_, _) => Rescan();
        exportButton.Click += (_, _) => ExportCsv();
        toolRow.Children.Add(rescanButton);
        toolRow.Children.Add(exportButton);
        root.Children.Add(toolRow);

        root.Children.Add(_summary);
        var resultsScroll = new ScrollViewer { MaxHeight = 380, Content = _resultsStack };
        root.Children.Add(OttawaWorkUi.Card(resultsScroll, padding: 8));

        var hint = new TextBlock
        {
            Text = "Click row(s) to pick a subset — leave none picked to act on every flagged dimension.",
            FontSize = 10,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            Margin = new Thickness(0, 8, 0, 0),
        };
        root.Children.Add(hint);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var closeButton = OttawaWorkUi.SecondaryButton("Close");
        var selectButton = OttawaWorkUi.SecondaryButton("Select in Model");
        var fixButton = OttawaWorkUi.PrimaryButton("Fix Selected");
        closeButton.Margin = new Thickness(0, 0, 8, 0);
        selectButton.Margin = new Thickness(0, 0, 8, 0);
        closeButton.Click += (_, _) => { DialogResult = false; Close(); };
        selectButton.Click += (_, _) => Finish(DetectorAction.Select);
        fixButton.Click += (_, _) => Finish(DetectorAction.Fix);
        buttonRow.Children.Add(closeButton);
        buttonRow.Children.Add(selectButton);
        buttonRow.Children.Add(fixButton);
        root.Children.Add(buttonRow);

        SetContent(root);
        Rescan();
    }

    private void Rescan()
    {
        _allRows = OverriddenDimensionEngine.Scan(_doc);
        _activeFilter = null;
        _selectedIndices.Clear();
        RefreshChips();
        RefreshList();
    }

    private void RefreshChips()
    {
        _chipRow.Children.Clear();

        var falsified = _allRows.Count(r => r.Severity == DimensionOverrideSeverity.Falsified);
        var frozen = _allRows.Count(r => r.Severity == DimensionOverrideSeverity.Frozen);
        var annotated = _allRows.Count(r => r.Severity == DimensionOverrideSeverity.Annotated);
        var views = _allRows.Select(r => r.ViewName).Distinct().Count();

        _chipRow.Children.Add(MakeChip($"Total {_allRows.Count}", OttawaWorkUi.TextSecondary, _activeFilter is null, () => SetFilter(null)));
        _chipRow.Children.Add(MakeChip($"Falsified {falsified}", OttawaWorkUi.Danger, _activeFilter == DimensionOverrideSeverity.Falsified, () => SetFilter(DimensionOverrideSeverity.Falsified)));
        _chipRow.Children.Add(MakeChip($"Frozen {frozen}", OttawaWorkUi.Warning, _activeFilter == DimensionOverrideSeverity.Frozen, () => SetFilter(DimensionOverrideSeverity.Frozen)));
        _chipRow.Children.Add(MakeChip($"Annotated {annotated}", OttawaWorkUi.Accent, _activeFilter == DimensionOverrideSeverity.Annotated, () => SetFilter(DimensionOverrideSeverity.Annotated)));
        _chipRow.Children.Add(MakeChip($"Views {views}", OttawaWorkUi.TextSecondary, false, null));
    }

    // Explicitly System.Action, not the bare "Action" — this class has its
    // own Action property (DetectorAction), and within a class a bare type
    // reference that collides with a member name resolves to the MEMBER,
    // not the type (CS0118: "Action is a 'property' but is used like a
    // 'type'") — confirmed against C#'s member-hides-type-in-scope rule.
    private Border MakeChip(string text, WpfColor color, bool active, System.Action? onClick)
    {
        var chip = OttawaWorkUi.Badge(text, color);
        chip.Margin = new Thickness(0, 0, 6, 0);
        if (active)
        {
            chip.Background = OttawaWorkUi.BrushOf(color);
            (chip.Child as TextBlock)!.Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary);
        }
        if (onClick is not null)
        {
            chip.Cursor = Cursors.Hand;
            chip.MouseLeftButtonDown += (_, _) => onClick();
        }
        return chip;
    }

    private void SetFilter(DimensionOverrideSeverity? severity)
    {
        _activeFilter = severity;
        _selectedIndices.Clear();
        RefreshChips();
        RefreshList();
    }

    private void RefreshList()
    {
        _resultsStack.Children.Clear();
        _rowBorders.Clear();

        var visible = _activeFilter is null ? _allRows : _allRows.Where(r => r.Severity == _activeFilter).ToList();
        _summary.Text = visible.Count == 0
            ? "No overridden or annotated dimensions found."
            : $"Showing {visible.Count} of {_allRows.Count} flagged dimension segment(s).";

        foreach (var row in visible)
        {
            var (severityText, severityColor) = row.Severity switch
            {
                DimensionOverrideSeverity.Falsified => ("Falsified", OttawaWorkUi.Danger),
                DimensionOverrideSeverity.Frozen => ("Frozen", OttawaWorkUi.Warning),
                _ => ("Annotated", OttawaWorkUi.Accent),
            };

            var content = new StackPanel { Cursor = Cursors.Hand };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
            headerRow.Children.Add(OttawaWorkUi.Badge(severityText, severityColor));
            headerRow.Children.Add(new TextBlock
            {
                Text = $"  ID {row.DimensionId.Value}" + (row.SegmentIndex is int seg ? $" · Segment {seg}" : ""),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
                Margin = new Thickness(6, 0, 0, 0),
            });
            content.Children.Add(headerRow);

            content.Children.Add(new TextBlock
            {
                Text = row.OverrideSummary,
                FontSize = 12,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
                Margin = new Thickness(0, 4, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            content.Children.Add(new TextBlock
            {
                Text = $"{row.DimTypeName} · {row.ViewName}" + (row.ActualValueFeet is { } actual
                    ? $" · Actual: {UnitFormatUtils.Format(_doc.GetUnits(), SpecTypeId.Length, actual, false)}"
                    : ""),
                FontSize = 11,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var rowBorder = OttawaWorkUi.Card(content, padding: 8);
            rowBorder.Margin = new Thickness(0, 2, 0, 2);
            var index = _rowBorders.Count;
            rowBorder.MouseLeftButtonDown += (_, _) => ToggleRow(index);
            _rowBorders.Add((rowBorder, row));
            _resultsStack.Children.Add(rowBorder);
        }
    }

    private void ToggleRow(int index)
    {
        var (rowBorder, _) = _rowBorders[index];
        if (!_selectedIndices.Add(index))
        {
            _selectedIndices.Remove(index);
            rowBorder.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackground);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor);
        }
        else
        {
            rowBorder.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.AccentSoft);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent);
        }
    }

    private void Finish(DetectorAction action)
    {
        var visible = _rowBorders.Select(r => r.Row).ToList();
        TargetRows = _selectedIndices.Count > 0
            ? _selectedIndices.Select(i => _rowBorders[i].Row).ToList()
            : visible;

        if (TargetRows.Count == 0)
        {
            System.Windows.MessageBox.Show("No flagged dimensions to act on.", "Ottawa Tools — Overridden Dimension Detector");
            return;
        }

        Action = action;
        DialogResult = true;
        Close();
    }

    private void ExportCsv()
    {
        var visible = _activeFilter is null ? _allRows : _allRows.Where(r => r.Severity == _activeFilter).ToList();
        if (visible.Count == 0)
        {
            System.Windows.MessageBox.Show("Nothing to export.", "Ottawa Tools — Overridden Dimension Detector");
            return;
        }

        using var dialog = new SaveFileDialog { Title = "Export overridden dimensions", Filter = "CSV (*.csv)|*.csv", FileName = "overridden-dimensions.csv" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;

        var lines = new List<string> { "ID,Segment,Severity,Override,DimensionType,View,ActualValue" };
        foreach (var row in visible)
        {
            var actual = row.ActualValueFeet is { } a ? UnitFormatUtils.Format(_doc.GetUnits(), SpecTypeId.Length, a, false) : "";
            lines.Add(string.Join(",", new[]
            {
                Csv.Escape(row.DimensionId.Value.ToString()),
                Csv.Escape(row.SegmentIndex?.ToString() ?? ""),
                Csv.Escape(row.Severity.ToString()),
                Csv.Escape(row.OverrideSummary),
                Csv.Escape(row.DimTypeName),
                Csv.Escape(row.ViewName),
                Csv.Escape(actual),
            }));
        }
        System.IO.File.WriteAllLines(dialog.FileName, lines);
    }
}
