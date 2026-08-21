using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;
using FontWeights = System.Windows.FontWeights;
using Visibility = System.Windows.Visibility;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.Excel2Revit;

/// <summary>
/// The Excel Sync workspace: an Export side (browse every schedule in the
/// project, filter/search them, pick a batch, and export them all at once
/// with a chosen format/delimiter/encoding/filename pattern — matching the
/// reference tool's own "Batch Exporter" browser) and an Import side
/// (load a previously exported-and-edited file, then commit its parameter
/// values back to the model). Export is pure file I/O — it runs directly
/// from this window, same as Batch Excel Sync's own Export step — so only
/// Import, which actually writes to the document, needs a transaction;
/// that happens in Command.cs after this window closes with
/// <see cref="DoImport"/> set.
///
/// The reference screenshot this was modeled on also had a "Date format"
/// dropdown; that's deliberately not here; there's no real ViewSchedule
/// export-side per-cell date reformatting available (would require
/// reparsing every value by its own parameter type, well past what a
/// "format the export file" option should silently promise) — the same
/// mistake that put a non-functional control in the old Highlight
/// Dashboard. "Encoding" IS real — it's a plain re-write of the exported
/// text file in a chosen <see cref="System.Text.Encoding"/> — so it stayed.
/// </summary>
public class ExcelSyncWindow : OttawaWorkWindow
{
    private readonly Document _doc;
    private readonly List<ScheduleInfo> _allSchedules;

    private readonly Button _exportModeButton = OttawaWorkUi.SecondaryButton("Export");
    private readonly Button _importModeButton = OttawaWorkUi.SecondaryButton("Import");
    private readonly StackPanel _exportPanel = new();
    private readonly StackPanel _importPanel = new();

    private readonly StackPanel _statsPanel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
    private readonly StackPanel _categoryChipsPanel = new() { Orientation = Orientation.Horizontal };
    private readonly List<(string Category, Button Chip)> _categoryChips = new();
    private string? _activeCategory;

    private readonly TextBox _searchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _scheduleListPanel = new();
    private readonly List<(ScheduleInfo Info, CheckBox Box, Border Container)> _scheduleRows = new();

    private readonly TextBlock _outputFolderText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
    private string _outputFolder = "";

    private readonly ComboBox _formatBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _delimiterBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _encodingBox = OttawaWorkUi.ComboBox();

    private readonly TextBox _patternBox = OttawaWorkUi.TextBox();
    private readonly TextBlock _previewText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent), Margin = new Thickness(0, 0, 0, 14), FontWeight = FontWeights.SemiBold };

    private readonly Button _exportButton = OttawaWorkUi.PrimaryButton("Export selected schedules (0)");

    private readonly TextBlock _importFileText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Border _importPreviewCard;
    private readonly TextBlock _importPreviewText = new() { FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), TextWrapping = TextWrapping.Wrap };
    private readonly Button _importCommitButton = OttawaWorkUi.SuccessButton("Import to model");

    private List<string>? _importHeaders;
    private List<List<string>>? _importRows;

    public bool DoImport { get; private set; }
    public List<string> ImportHeaders => _importHeaders ?? new List<string>();
    public List<List<string>> ImportRows => _importRows ?? new List<List<string>>();

    public ExcelSyncWindow(Document doc) : base("Ottawa Tools — Excel Sync", minWidth: 900)
    {
        _doc = doc;
        _allSchedules = ExcelSyncEngine.ListSchedules(doc);

        _importPreviewCard = OttawaWorkUi.Card(_importPreviewText, padding: 12);
        _importPreviewCard.Visibility = Visibility.Collapsed;
        _importPreviewCard.Margin = new Thickness(0, 12, 0, 12);

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("📊", "Excel Sync", "Export any of this project's schedules to Excel/CSV, or import parameter values back from a previously exported file."));

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        _exportModeButton.Margin = new Thickness(0, 0, 8, 0);
        _exportModeButton.Click += (_, _) => SetMode(export: true);
        _importModeButton.Click += (_, _) => SetMode(export: false);
        modeRow.Children.Add(_exportModeButton);
        modeRow.Children.Add(_importModeButton);
        root.Children.Add(modeRow);

        BuildExportPanel();
        BuildImportPanel();
        root.Children.Add(_exportPanel);
        root.Children.Add(_importPanel);

        SetContent(root, padding: 24);

        RefreshCategoryChips();
        RefreshScheduleList();
        RefreshStats();
        RefreshPreview();
        SetMode(export: true);
    }

    private void SetMode(bool export)
    {
        OttawaWorkUi.SetToggleActive(_exportModeButton, export);
        OttawaWorkUi.SetToggleActive(_importModeButton, !export);
        _exportPanel.Visibility = export ? Visibility.Visible : Visibility.Collapsed;
        _importPanel.Visibility = export ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BuildExportPanel()
    {
        _exportPanel.Width = 852;
        _exportPanel.Children.Add(_statsPanel);

        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        var leftCol = new StackPanel { Width = 460, Margin = new Thickness(0, 0, 16, 0) };
        leftCol.Children.Add(OttawaWorkUi.SectionHeader("Schedules"));

        // A project's schedules can span far more categories than fit on one
        // row (walls, doors, windows, furniture, structural framing, ...) —
        // scrolling horizontally here keeps that from stretching the whole
        // window (SizeToContent=WidthAndHeight) out to match the widest chip
        // row instead of the 460px this column is supposed to be.
        var chipScroll = new ScrollViewer
        {
            Width = 452,
            Height = 32,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
            Content = _categoryChipsPanel,
            Margin = new Thickness(0, 0, 0, 10),
        };
        leftCol.Children.Add(chipScroll);

        _searchBox.ToolTip = "Search schedules...";
        _searchBox.TextChanged += (_, _) => RefreshScheduleVisibility();
        leftCol.Children.Add(_searchBox);

        var listHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 4) };
        listHeaderRow.Children.Add(new TextBlock { Text = "", Width = 24 });
        listHeaderRow.Children.Add(new TextBlock { Text = "NAME", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 190 });
        listHeaderRow.Children.Add(new TextBlock { Text = "CATEGORY", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 120 });
        listHeaderRow.Children.Add(new TextBlock { Text = "ROWS", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 50 });
        listHeaderRow.Children.Add(new TextBlock { Text = "COLS", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 50 });
        leftCol.Children.Add(listHeaderRow);

        var scroll = new ScrollViewer { MaxHeight = 340, Content = _scheduleListPanel };
        leftCol.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var allButton = OttawaWorkUi.SecondaryButton("Select all");
        var noneButton = OttawaWorkUi.SecondaryButton("Select none");
        allButton.Margin = new Thickness(0, 0, 6, 0);
        allButton.Click += (_, _) => { foreach (var r in _scheduleRows) r.Box.IsChecked = true; RefreshStats(); RefreshPreview(); };
        noneButton.Click += (_, _) => { foreach (var r in _scheduleRows) r.Box.IsChecked = false; RefreshStats(); RefreshPreview(); };
        quickRow.Children.Add(allButton);
        quickRow.Children.Add(noneButton);
        leftCol.Children.Add(quickRow);

        columns.Children.Add(leftCol);
        columns.Children.Add(BuildOptionsColumn());
        _exportPanel.Children.Add(columns);
    }

    private StackPanel BuildOptionsColumn()
    {
        var col = new StackPanel { Width = 340 };
        col.Children.Add(OttawaWorkUi.SectionHeader("Output folder"));
        var folderRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var browseFolderButton = OttawaWorkUi.SecondaryButton("Browse...");
        browseFolderButton.Click += (_, _) => BrowseOutputFolder();
        _outputFolderText.Text = "No folder chosen yet.";
        folderRow.Children.Add(browseFolderButton);
        folderRow.Children.Add(_outputFolderText);
        col.Children.Add(folderRow);

        col.Children.Add(OttawaWorkUi.SectionHeader("Format"));
        _formatBox.Items.AddRange(new object[] { "Excel (.xlsx)", "CSV (.csv)", "Tab-delimited (.txt)" });
        _formatBox.SelectedIndex = 0;
        _formatBox.SelectionChanged += (_, _) => { RefreshDelimiterEnabled(); RefreshPreview(); };
        col.Children.Add(_formatBox);

        col.Children.Add(OttawaWorkUi.SectionHeader("Delimiter"));
        _delimiterBox.Items.AddRange(new object[] { "Comma", "Semicolon", "Tab" });
        _delimiterBox.SelectedIndex = 0;
        col.Children.Add(_delimiterBox);

        col.Children.Add(OttawaWorkUi.SectionHeader("Encoding"));
        _encodingBox.Items.AddRange(new object[] { "UTF-8", "UTF-8 with BOM", "System default (Latin1)" });
        _encodingBox.SelectedIndex = 0;
        col.Children.Add(_encodingBox);

        col.Children.Add(OttawaWorkUi.SectionHeader("Filename pattern"));
        _patternBox.Text = "{name}_{date}";
        _patternBox.TextChanged += (_, _) => RefreshPreview();
        col.Children.Add(_patternBox);

        var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -4, 0, 8) };
        foreach (var token in new[] { "{name}", "{category}", "{project}", "{date}", "{id}" })
        {
            var chip = OttawaWorkUi.SecondaryButton(token);
            chip.Margin = new Thickness(0, 0, 4, 0);
            chip.Padding = new Thickness(8, 4, 8, 4);
            chip.FontSize = 10;
            chip.Click += (_, _) => { _patternBox.Text += token; _patternBox.CaretIndex = _patternBox.Text.Length; };
            chipRow.Children.Add(chip);
        }
        col.Children.Add(chipRow);
        col.Children.Add(_previewText);

        RefreshDelimiterEnabled();

        _exportButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _exportButton.Click += (_, _) => RunExport();
        col.Children.Add(_exportButton);

        return col;
    }

    private void BuildImportPanel()
    {
        _importPanel.Width = 560;
        _importPanel.Visibility = Visibility.Collapsed;
        _importPanel.Children.Add(OttawaWorkUi.SectionHeader("Load an exported file"));

        var browseRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var browseButton = OttawaWorkUi.PrimaryButton("Browse & load");
        browseButton.Click += (_, _) => BrowseAndLoadImport();
        browseRow.Children.Add(browseButton);
        browseRow.Children.Add(_importFileText);
        _importPanel.Children.Add(browseRow);

        _importPanel.Children.Add(new TextBlock
        {
            Text = "Rows are matched back to elements by the file's first column (e.g. Mark) via LookupParameter; every other column is written as a same-named parameter.",
            FontSize = 10,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });

        _importPanel.Children.Add(_importPreviewCard);

        _importCommitButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _importCommitButton.IsEnabled = false;
        _importCommitButton.Margin = new Thickness(0, 4, 0, 0);
        _importCommitButton.Click += (_, _) => CommitImport();
        _importPanel.Children.Add(_importCommitButton);
    }

    private void RefreshCategoryChips()
    {
        _categoryChipsPanel.Children.Clear();
        _categoryChips.Clear();

        var allChip = OttawaWorkUi.SecondaryButton("All");
        allChip.Margin = new Thickness(0, 0, 6, 0);
        allChip.Click += (_, _) => SetActiveCategory(null);
        _categoryChipsPanel.Children.Add(allChip);
        _categoryChips.Add(("", allChip));

        foreach (var category in _allSchedules.Select(s => s.Category).Distinct().OrderBy(c => c))
        {
            var chip = OttawaWorkUi.SecondaryButton(category);
            chip.Margin = new Thickness(0, 0, 6, 0);
            chip.Click += (_, _) => SetActiveCategory(category);
            _categoryChipsPanel.Children.Add(chip);
            _categoryChips.Add((category, chip));
        }

        SetActiveCategory(null);
    }

    private void SetActiveCategory(string? category)
    {
        _activeCategory = category;
        foreach (var (chipCategory, chip) in _categoryChips)
            OttawaWorkUi.SetToggleActive(chip, chipCategory == (category ?? ""));
        RefreshScheduleVisibility();
    }

    private void RefreshScheduleList()
    {
        _scheduleListPanel.Children.Clear();
        _scheduleRows.Clear();

        foreach (var info in _allSchedules)
        {
            var box = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Width = 24 };
            box.Click += (_, _) => { RefreshStats(); RefreshPreview(); };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(box);
            row.Children.Add(new TextBlock { Text = info.Name, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 190, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            row.Children.Add(new TextBlock { Text = info.Category, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 120, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            row.Children.Add(new TextBlock { Text = info.Rows.ToString(), FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 50 });
            row.Children.Add(new TextBlock { Text = info.Columns.ToString(), FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 50 });

            var container = new Border { Padding = new Thickness(2), Child = row };
            _scheduleListPanel.Children.Add(container);
            _scheduleRows.Add((info, box, container));
        }
    }

    private void RefreshScheduleVisibility()
    {
        var search = _searchBox.Text?.Trim() ?? "";
        foreach (var (info, _, container) in _scheduleRows)
        {
            var matchesCategory = _activeCategory is null || info.Category == _activeCategory;
            var matchesSearch = string.IsNullOrEmpty(search) || info.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            container.Visibility = matchesCategory && matchesSearch ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshStats()
    {
        _statsPanel.Children.Clear();
        var selectedCount = _scheduleRows.Count(r => r.Box.IsChecked == true);
        var categoryCount = _allSchedules.Select(s => s.Category).Distinct().Count();

        var t1 = OttawaWorkUi.StatTile(_allSchedules.Count.ToString(), "SCHEDULES");
        var t2 = OttawaWorkUi.StatTile(categoryCount.ToString(), "CATEGORIES");
        var t3 = OttawaWorkUi.StatTile(selectedCount.ToString(), "SELECTED", OttawaWorkUi.Accent);
        t1.Margin = new Thickness(0, 0, 10, 0);
        t2.Margin = new Thickness(0, 0, 10, 0);
        _statsPanel.Children.Add(t1);
        _statsPanel.Children.Add(t2);
        _statsPanel.Children.Add(t3);

        _exportButton.Content = $"Export selected schedules ({selectedCount})".ToUpperInvariant();
    }

    private void RefreshDelimiterEnabled()
    {
        _delimiterBox.IsEnabled = SelectedFormat() == ExportFormat.Csv;
    }

    private void RefreshPreview()
    {
        var sample = _scheduleRows.FirstOrDefault(r => r.Box.IsChecked == true).Info ?? _allSchedules.FirstOrDefault();
        if (sample is null)
        {
            _previewText.Text = "";
            return;
        }

        var format = SelectedFormat();
        var ext = format switch { ExportFormat.Excel => ".xlsx", ExportFormat.TabDelimited => ".txt", _ => ".csv" };
        var resolved = ExcelSyncEngine.ResolveFileName(_patternBox.Text, sample, _doc.Title, DateTime.Now);
        _previewText.Text = $"Preview: {resolved}{ext}";
    }

    private ExportFormat SelectedFormat() => (_formatBox.SelectedIndex) switch
    {
        1 => ExportFormat.Csv,
        2 => ExportFormat.TabDelimited,
        _ => ExportFormat.Excel,
    };

    private SyncEncoding SelectedEncoding() => (_encodingBox.SelectedIndex) switch
    {
        1 => SyncEncoding.Utf8Bom,
        2 => SyncEncoding.SystemDefault,
        _ => SyncEncoding.Utf8,
    };

    private string SelectedDelimiter() => ExcelSyncEngine.DelimiterChar(_delimiterBox.SelectedItem as string ?? "Comma");

    private void BrowseOutputFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose a folder for the exported schedule(s)" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;

        _outputFolder = dialog.SelectedPath;
        _outputFolderText.Text = _outputFolder;
    }

    private void RunExport()
    {
        var selected = _scheduleRows.Where(r => r.Box.IsChecked == true).Select(r => r.Info).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Check at least one schedule to export.", "Ottawa Tools — Excel Sync");
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputFolder))
        {
            MessageBox.Show("Choose an output folder first.", "Ottawa Tools — Excel Sync");
            return;
        }

        var format = SelectedFormat();
        var delimiter = SelectedDelimiter();
        var encoding = SelectedEncoding();
        var written = new List<string>();
        var failed = new List<string>();

        foreach (var info in selected)
        {
            try
            {
                var fileName = ExcelSyncEngine.ResolveFileName(_patternBox.Text, info, _doc.Title, DateTime.Now);
                var path = ExcelSyncEngine.ExportOne(info.Schedule, _outputFolder, fileName, format, delimiter, encoding, _doc.Title);
                written.Add(path);
            }
            catch (Exception ex)
            {
                failed.Add($"{info.Name}: {ex.Message}");
            }
        }

        var summary = $"Exported {written.Count} of {selected.Count} schedule(s) to:\n{_outputFolder}";
        if (failed.Count > 0)
            summary += $"\n\n{failed.Count} failed:\n{string.Join("\n", failed)}";

        MessageBox.Show(summary, "Ottawa Tools — Excel Sync");
    }

    private void BrowseAndLoadImport()
    {
        using var dialog = new OpenFileDialog { Title = "Load edited export", Filter = "Excel Workbook (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;

        var table = BrandedXlsx.ReadTable(dialog.FileName);
        if (table is null || table.Value.Headers.Count == 0)
        {
            MessageBox.Show("This file doesn't look like an Excel Sync export — no header row was found.", "Ottawa Tools — Excel Sync");
            return;
        }

        var (headers, rows) = table.Value;
        _importHeaders = headers;
        _importRows = rows;

        _importFileText.Text = $"{Path.GetFileName(dialog.FileName)} — {rows.Count} row(s) loaded";
        _importPreviewText.Text = $"Key column: \"{headers[0]}\"\nParameter columns: {string.Join(", ", headers.Skip(1))}\n{rows.Count} row(s) ready to match against the model.";
        _importPreviewCard.Visibility = Visibility.Visible;
        _importCommitButton.IsEnabled = true;
    }

    private void CommitImport()
    {
        if (_importHeaders is null || _importRows is null) return;

        DoImport = true;
        DialogResult = true;
        Close();
    }
}
