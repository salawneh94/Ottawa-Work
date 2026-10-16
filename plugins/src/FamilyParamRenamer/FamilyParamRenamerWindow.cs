using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using TextTrimming = System.Windows.TextTrimming;
using TextWrapping = System.Windows.TextWrapping;
using Visibility = System.Windows.Visibility;
using MessageBox = System.Windows.MessageBox;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using WpfColor = System.Windows.Media.Color;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.FamilyParamRenamer;

/// <summary>
/// Swap shared parameters inside loaded families for new ones (e.g. OMC_* → OI_*): scan the families,
/// map each old parameter to one in the new shared parameter file (auto-matched by prefix, picked by
/// hand where the name changes more, or loaded from a saved mapping), preview, then run. Parameters
/// bound in Manage → Project Parameters are shown as such and replaced at project level (new one bound
/// to the same categories, values copied, old one removed). The run itself happens in Command.cs after
/// this window closes — EditFamily/LoadFamily need the project to have no
/// open transaction and the modal dialog out of the way.
/// </summary>
public class FamilyParamRenamerWindow : OttawaWorkWindow
{
    private const string SkipLabel = "(not mapped)";

    private sealed class MappingRow
    {
        public required FoundSharedParam Source { get; init; }
        public required List<SharedParamDefinition> Options { get; init; }
        public required ComboBox Box { get; init; }
        public required StackPanel BadgeHost { get; init; }
        public string? MismatchName { get; set; }

        public SharedParamDefinition? Selected => Box.SelectedIndex > 0 ? Options[Box.SelectedIndex - 1] : null;

        public MappingStatus Status =>
            Selected is null
                ? MismatchName is not null ? MappingStatus.TypeMismatch : MappingStatus.Skip
                : FamilyParamRenamerEngine.Evaluate(Source, Selected);
    }

    private readonly Document _doc;
    private List<SharedParamDefinition> _definitions = new();
    private string _sharedFilePath;

    private readonly TextBox _oldPrefixBox = OttawaWorkUi.TextBox();
    private readonly TextBox _newPrefixBox = OttawaWorkUi.TextBox();
    private readonly TextBlock _fileText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 560 };
    private readonly TextBlock _statusText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent), Margin = new Thickness(0, 4, 0, 10), TextWrapping = TextWrapping.Wrap };

    private readonly TextBox _familySearchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _familyPanel = new();
    private readonly List<(RenameFamily Family, CheckBox Box)> _familyRows = new();

    private readonly StackPanel _mappingPanel = new();
    private readonly List<MappingRow> _mappingRows = new();
    private readonly TextBlock _affectedText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Warning), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly Button _runButton = OttawaWorkUi.PrimaryButton("Swap parameters");
    private readonly CheckBox _projectBox = OttawaWorkUi.CheckBoxItem("Also replace project parameters", isChecked: true);

    public bool RunRequested { get; private set; }
    public List<ElementId> SelectedFamilyIds { get; private set; } = new();
    public Dictionary<Guid, Guid> Map { get; private set; } = new();
    /// <summary>The mapped parameters that are project parameters — replaced at project level.</summary>
    public Dictionary<Guid, Guid> ProjectMap { get; private set; } = new();
    public string SharedFilePath => _sharedFilePath;

    public FamilyParamRenamerWindow(Document doc) : base("Ottawa Tools — Family Parameter Renamer", minWidth: 1120)
    {
        _doc = doc;
        _sharedFilePath = doc.Application.SharedParametersFilename ?? "";

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🔁", "Family Parameter Renamer",
            "Swap shared parameters for new ones (e.g. OMC → OI) — inside families (values and formulas kept, each family reloaded) and in Project Parameters (new one bound to the same categories, values copied, old one removed).", Close));

        // ---- Settings ----
        var settings = new StackPanel();
        var prefixRow = new StackPanel { Orientation = Orientation.Horizontal };
        var oldCol = new StackPanel { Width = 160, Margin = new Thickness(0, 0, 12, 0) };
        oldCol.Children.Add(OttawaWorkUi.FieldLabel("Old parameters start with"));
        _oldPrefixBox.Text = "OMC";
        oldCol.Children.Add(_oldPrefixBox);
        var newCol = new StackPanel { Width = 160, Margin = new Thickness(0, 0, 12, 0) };
        newCol.Children.Add(OttawaWorkUi.FieldLabel("New parameters start with"));
        _newPrefixBox.Text = "OI";
        newCol.Children.Add(_newPrefixBox);
        prefixRow.Children.Add(oldCol);
        prefixRow.Children.Add(newCol);

        var scanCol = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 10) };
        var quickScan = OttawaWorkUi.PrimaryButton("Scan");
        quickScan.Margin = new Thickness(0, 0, 8, 0);
        quickScan.ToolTip = "Fast: reads parameters from family types and placed instances.";
        quickScan.Click += (_, _) => Scan(deep: false);
        var deepScan = OttawaWorkUi.SecondaryButton("Deep scan (slower)");
        deepScan.ToolTip = "Opens every family to also find instance parameters in families that aren't placed in the model.";
        deepScan.Click += (_, _) => Scan(deep: true);
        scanCol.Children.Add(quickScan);
        scanCol.Children.Add(deepScan);
        prefixRow.Children.Add(scanCol);
        settings.Children.Add(prefixRow);

        settings.Children.Add(OttawaWorkUi.FieldLabel("New shared parameter file (the one containing the new parameters)"));
        var fileRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var browse = OttawaWorkUi.SecondaryButton("Browse...");
        browse.Click += (_, _) => BrowseSharedFile();
        fileRow.Children.Add(browse);
        fileRow.Children.Add(_fileText);
        settings.Children.Add(fileRow);
        root.Children.Add(OttawaWorkUi.Card(settings, padding: 14));
        root.Children.Add(_statusText);

        // ---- Families + mapping ----
        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        var left = new StackPanel { Width = 330, Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(OttawaWorkUi.SectionHeader("Families"));
        _familySearchBox.ToolTip = "Search families...";
        _familySearchBox.TextChanged += (_, _) => ApplyFamilySearch();
        left.Children.Add(_familySearchBox);
        var familyButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var all = OttawaWorkUi.SecondaryButton("All");
        all.Margin = new Thickness(0, 0, 6, 0);
        all.Click += (_, _) => { foreach (var r in _familyRows.Where(r => r.Box.Visibility == Visibility.Visible)) r.Box.IsChecked = true; RefreshRunButton(); };
        var none = OttawaWorkUi.SecondaryButton("None");
        none.Click += (_, _) => { foreach (var r in _familyRows) r.Box.IsChecked = false; RefreshRunButton(); };
        familyButtons.Children.Add(all);
        familyButtons.Children.Add(none);
        left.Children.Add(familyButtons);
        left.Children.Add(OttawaWorkUi.Card(new ScrollViewer { MaxHeight = 380, Content = _familyPanel }, padding: 8));
        columns.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(OttawaWorkUi.SectionHeader("Parameter mapping"));
        var mappingButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var auto = OttawaWorkUi.SecondaryButton("Auto-match by prefix");
        auto.Margin = new Thickness(0, 0, 6, 0);
        auto.Click += (_, _) => AutoMatchAll();
        var load = OttawaWorkUi.SecondaryButton("Load mapping...");
        load.Margin = new Thickness(0, 0, 6, 0);
        load.Click += (_, _) => LoadMapping();
        var save = OttawaWorkUi.SecondaryButton("Save mapping...");
        save.Click += (_, _) => SaveMapping();
        save.Margin = new Thickness(0, 0, 6, 0);
        var fixIds = OttawaWorkUi.SecondaryButton("Give new IDs...");
        fixIds.ToolTip = "For a new shared parameter file made by renaming a copy of the old one: gives every renamed parameter its own ID so it can replace the old one. Makes a backup first.";
        fixIds.Click += (_, _) => GiveNewIds();
        mappingButtons.Children.Add(auto);
        mappingButtons.Children.Add(load);
        mappingButtons.Children.Add(save);
        mappingButtons.Children.Add(fixIds);
        right.Children.Add(mappingButtons);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 4) };
        foreach (var (text, width) in new[] { ("OLD PARAMETER", 210.0), ("TYPE", 90.0), ("WHERE", 120.0), ("NEW PARAMETER", 250.0), ("STATUS", 120.0) })
            header.Children.Add(new TextBlock { Text = text, Width = width, FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        right.Children.Add(header);
        right.Children.Add(OttawaWorkUi.Card(new ScrollViewer { MaxHeight = 380, Content = _mappingPanel }, padding: 8));
        right.Children.Add(_affectedText);
        columns.Children.Add(right);
        root.Children.Add(columns);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var close = OttawaWorkUi.SecondaryButton("Close");
        close.Margin = new Thickness(0, 0, 8, 0);
        close.Click += (_, _) => { DialogResult = false; Close(); };
        _runButton.Click += (_, _) => ConfirmAndRun();
        _projectBox.VerticalAlignment = VerticalAlignment.Center;
        _projectBox.Margin = new Thickness(0, 0, 16, 0);
        _projectBox.ToolTip = "For parameters in Manage → Project Parameters: bind the new parameter to the same categories (instance/type kept), copy every element's value, then remove the old one.";
        _projectBox.Click += (_, _) => RefreshRunButton();
        actions.Children.Add(_projectBox);
        actions.Children.Add(close);
        actions.Children.Add(_runButton);
        root.Children.Add(actions);

        SetContent(root, padding: 24);

        LoadDefinitions();
        Scan(deep: false);
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
        // Let the label paint before a blocking Revit API call (which has to stay on this thread).
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }

    private void LoadDefinitions()
    {
        _definitions = string.IsNullOrWhiteSpace(_sharedFilePath)
            ? new List<SharedParamDefinition>()
            : FamilyParamRenamerEngine.ReadDefinitions(_doc.Application, _sharedFilePath);
        _fileText.Text = string.IsNullOrWhiteSpace(_sharedFilePath)
            ? "No shared parameter file chosen."
            : $"{_sharedFilePath}  —  {_definitions.Count} parameter(s)";
    }

    private void BrowseSharedFile()
    {
        using var dialog = new OpenFileDialog { Title = "Choose the new shared parameter file", Filter = "Shared parameter file (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;
        _sharedFilePath = dialog.FileName;
        LoadDefinitions();
        RebuildMapping(keepSelections: true);
    }

    private void Scan(bool deep)
    {
        var prefix = _oldPrefixBox.Text.Trim();
        SetStatus(deep ? "Deep scan: opening families..." : "Scanning families...");
        var (families, parameters) = deep
            ? FamilyParamRenamerEngine.DeepScan(_doc, prefix, (i, n) => { if (i % 5 == 0 || i == n) SetStatus($"Deep scan: family {i} of {n}..."); })
            : FamilyParamRenamerEngine.QuickScan(_doc, prefix);

        _familyPanel.Children.Clear();
        _familyRows.Clear();
        foreach (var family in families)
        {
            var count = parameters.Count(p => p.FamilyNames.Contains(family.Name));
            var label = string.IsNullOrEmpty(family.Category) ? $"{family.Name}  ({count})" : $"{family.Name}  ·  {family.Category}  ({count})";
            var box = OttawaWorkUi.CheckBoxItem(label, isChecked: true);
            box.ToolTip = label;
            box.Click += (_, _) => RefreshRunButton();
            _familyRows.Add((family, box));
            _familyPanel.Children.Add(box);
        }
        var projectCount = parameters.Count(p => p.Project is not null);
        if (families.Count == 0)
            _familyPanel.Children.Add(Note($"No families contain shared parameters starting with \"{prefix}\" themselves." +
                (projectCount > 0 ? $" The {projectCount} found are project parameters — they're replaced at project level." : "") +
                (deep ? "" : " Try Deep scan if some families aren't placed in the model.")));

        _found = parameters;
        RebuildMapping(keepSelections: false);
        AutoMatchAll();
        ApplyFamilySearch();

        var inFamilies = parameters.Count(p => p.FamilyNames.Count > 0);
        SetStatus(
            $"{parameters.Count} \"{prefix}\" shared parameter(s): {inFamilies} inside {families.Count} famil{(families.Count == 1 ? "y" : "ies")}, " +
            $"{projectCount} in Project Parameters (Manage → Project Parameters)." +
            (deep ? " (deep scan)" : " Deep scan also opens families that aren't placed, or that contain a project parameter themselves."));
    }

    private List<FoundSharedParam> _found = new();

    // Set during bulk changes (auto-match, load mapping) so each combo's SelectionChanged doesn't
    // re-run RefreshRunButton — which scans every schedule — once per row; refreshed once at the end.
    private bool _bulkUpdate;

    private void RebuildMapping(bool keepSelections)
    {
        var previous = keepSelections
            ? _mappingRows.Where(r => r.Selected is not null).ToDictionary(r => r.Source.Guid, r => r.Selected!.Name)
            : new Dictionary<Guid, string>();

        _mappingPanel.Children.Clear();
        _mappingRows.Clear();
        foreach (var source in _found)
        {
            var options = _definitions.Where(d => FamilyParamRenamerEngine.SameDataType(d.DataType, source.DataType)).ToList();
            var box = OttawaWorkUi.ComboBox();
            box.Width = 250;
            box.Margin = new Thickness(0);
            box.Items.Add(SkipLabel);
            foreach (var option in options) box.Items.Add(option.Name);
            box.SelectedIndex = 0;

            var row = new MappingRow { Source = source, Options = options, Box = box, BadgeHost = new StackPanel { Width = 120, VerticalAlignment = VerticalAlignment.Center } };
            if (previous.TryGetValue(source.Guid, out var name)) Select(row, name);
            box.SelectionChanged += (_, _) =>
            {
                if (_bulkUpdate) return;
                row.MismatchName = null;
                RefreshRow(row);
                RefreshRunButton();
            };

            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(new TextBlock
            {
                Text = source.Name, Width = 210, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = source.Name,
            });
            line.Children.Add(new TextBlock
            {
                Text = FamilyParamRenamerEngine.DataTypeLabel(source.DataType), Width = 90, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var (where, whereTip) = WhereLabel(source);
            line.Children.Add(new TextBlock
            {
                Text = where, Width = 120, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                Foreground = OttawaWorkUi.BrushOf(source.Project is not null ? OttawaWorkUi.Accent : OttawaWorkUi.TextSecondary),
                TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = whereTip,
            });
            line.Children.Add(box);
            line.Children.Add(new Border { Width = 10 });
            line.Children.Add(row.BadgeHost);

            var card = OttawaWorkUi.Card(line, padding: 6);
            card.Margin = new Thickness(0, 2, 0, 2);
            _mappingPanel.Children.Add(card);
            _mappingRows.Add(row);
            RefreshRow(row);
        }
        if (_found.Count == 0)
            _mappingPanel.Children.Add(Note("Nothing to map yet — scan first."));
        RefreshRunButton();
    }

    private static (string Text, string Tip) WhereLabel(FoundSharedParam source)
    {
        var families = source.FamilyNames.Count;
        var familyText = $"{families} famil{(families == 1 ? "y" : "ies")}";
        var familyTip = families == 0 ? "" : $"Inside families: {string.Join(", ", source.FamilyNames)}";
        if (source.Project is not { } project) return (familyText, familyTip);

        var projectTip =
            $"Project parameter (Manage → Project Parameters), {(project.IsInstance ? "instance" : "type")}, " +
            $"bound to: {string.Join(", ", project.CategoryNames)}.\n" +
            "Replaced at project level: the new parameter is bound to the same categories, values are copied, the old one is removed.";
        return families == 0
            ? ("Project", projectTip)
            : ($"Project + {familyText}", projectTip + "\n\n" + familyTip);
    }

    /// <summary>Selects <paramref name="name"/> if it's a same-type option; if the name only exists with a
    /// different data type, leaves the row unmapped and remembers it so the status says why.</summary>
    private void Select(MappingRow row, string name)
    {
        var index = row.Options.FindIndex(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        row.Box.SelectedIndex = index >= 0 ? index + 1 : 0;
        row.MismatchName = index < 0 && _definitions.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ? name : null;
    }

    private void RefreshRow(MappingRow row)
    {
        var (text, color, tip) = row.Status switch
        {
            MappingStatus.Ready => ("Ready", OttawaWorkUi.Success, "Will be swapped."),
            MappingStatus.SameId => ("Same ID", OttawaWorkUi.Danger,
                "This new parameter has the same ID (GUID) as the old one — it's a renamed copy, so Revit treats both as the same parameter and projects keep showing the old name. Create it as a new parameter in the shared parameter file instead."),
            MappingStatus.TypeMismatch => ("Type differs", OttawaWorkUi.Danger,
                $"{row.MismatchName} exists in the file but with a different data type — Revit can't swap between types."),
            _ => ("Not mapped", OttawaWorkUi.Warning, "Pick the new parameter, or leave it unmapped to skip."),
        };
        row.BadgeHost.Children.Clear();
        var badge = OttawaWorkUi.Badge(text, color);
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        badge.ToolTip = tip;
        row.BadgeHost.Children.Add(badge);
    }

    private void AutoMatchAll()
    {
        var oldPrefix = _oldPrefixBox.Text.Trim();
        var newPrefix = _newPrefixBox.Text.Trim();
        _bulkUpdate = true;
        try
        {
            foreach (var row in _mappingRows)
            {
                if (row.Selected is not null) continue;
                var candidate = FamilyParamRenamerEngine.AutoMatch(row.Source.Name, oldPrefix, newPrefix, _definitions);
                if (candidate is not null) Select(row, candidate.Name);
                RefreshRow(row);
            }
        }
        finally { _bulkUpdate = false; }
        RefreshRunButton();
    }

    private void LoadMapping()
    {
        using var dialog = new OpenFileDialog { Title = "Load parameter mapping", Filter = "Mapping (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;
        var mapping = FamilyParamRenamerEngine.LoadMapping(dialog.FileName);
        var applied = 0;
        _bulkUpdate = true;
        try
        {
            foreach (var row in _mappingRows)
            {
                if (!mapping.TryGetValue(row.Source.Name, out var newName)) continue;
                Select(row, newName);
                RefreshRow(row);
                if (row.Selected is not null) applied++;
            }
        }
        finally { _bulkUpdate = false; }
        RefreshRunButton();
        SetStatus($"Mapping loaded: {applied} of {_mappingRows.Count} parameter(s) mapped from {mapping.Count} line(s) in the file.");
    }

    private void SaveMapping()
    {
        var pairs = _mappingRows.Where(r => r.Selected is not null).Select(r => (r.Source.Name, r.Selected!.Name)).ToList();
        if (pairs.Count == 0) { MessageBox.Show("Nothing is mapped yet.", "Ottawa Tools — Family Parameter Renamer"); return; }
        using var dialog = new SaveFileDialog { Title = "Save parameter mapping", Filter = "Mapping (*.csv)|*.csv", FileName = "Parameter mapping.csv" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;
        FamilyParamRenamerEngine.SaveMapping(dialog.FileName, pairs);
        SetStatus($"Saved {pairs.Count} mapping(s) to {dialog.FileName} — reuse it with Load mapping in other projects.");
    }

    /// <summary>Fixes a new shared parameter file that was made by renaming entries in a copy of the old
    /// one: every renamed entry still carries its old GUID, so Revit sees it as the same parameter. Finds
    /// those by comparing against the old file (same GUID, different name) plus any "Same ID" rows, gives
    /// them fresh GUIDs in the file (backup first), then reloads the mapping.</summary>
    private void GiveNewIds()
    {
        const string title = "Ottawa Tools — Family Parameter Renamer";
        if (string.IsNullOrWhiteSpace(_sharedFilePath) || !System.IO.File.Exists(_sharedFilePath))
        {
            MessageBox.Show("Choose the new shared parameter file first (Browse...).", title);
            return;
        }

        var projectFile = _doc.Application.SharedParametersFilename ?? "";
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the OLD shared parameter file (the company file the new one was copied from)",
            Filter = "Shared parameter file (*.txt)|*.txt|All files (*.*)|*.*",
        };
        if (System.IO.File.Exists(projectFile) && !string.Equals(projectFile, _sharedFilePath, StringComparison.OrdinalIgnoreCase))
        {
            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(projectFile);
            dialog.FileName = System.IO.Path.GetFileName(projectFile);
        }
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;
        if (string.Equals(dialog.FileName, _sharedFilePath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("That's the new file — pick the old company file it was copied from.", title);
            return;
        }

        var copies = FamilyParamRenamerEngine.RenamedCopies(dialog.FileName, _sharedFilePath);
        foreach (var row in _mappingRows.Where(r => r.Status == MappingStatus.SameId)) copies.Add(row.Source.Guid);
        if (copies.Count == 0)
        {
            MessageBox.Show("No renamed copies found — every parameter in the new file already has its own ID. Nothing to fix.", title);
            return;
        }

        var confirm = MessageBox.Show(
            $"Give {copies.Count} renamed parameter(s) in the new file their own IDs?\n\n" +
            "Names, data types and groups stay exactly the same. Parameters you didn't rename are left alone. " +
            "A backup copy of the file is saved next to it first.\n\n" +
            "From now on, use this new file (and share it with colleagues) — not any earlier copy of it.",
            title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var (changed, backup) = FamilyParamRenamerEngine.RegenerateGuids(_sharedFilePath, copies);
            LoadDefinitions();
            RebuildMapping(keepSelections: true);
            AutoMatchAll();
            SetStatus($"Gave {changed.Count} parameter(s) new IDs. Backup of the original: {backup}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't update the file — it was not changed.\n\n{ex.Message}", title);
        }
    }

    private void ApplyFamilySearch()
    {
        var search = _familySearchBox.Text?.Trim() ?? "";
        foreach (var (family, box) in _familyRows)
            box.Visibility = search.Length == 0 || family.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || family.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private Dictionary<Guid, Guid> ReadyMap() =>
        _mappingRows.Where(r => r.Status == MappingStatus.Ready).ToDictionary(r => r.Source.Guid, r => r.Selected!.Guid);

    private List<RenameFamily> CheckedFamilies() => _familyRows.Where(r => r.Box.IsChecked == true).Select(r => r.Family).ToList();

    private List<RenameFamily> FamiliesToRun(Dictionary<Guid, Guid> map) =>
        CheckedFamilies().Where(f => _found.Any(p => map.ContainsKey(p.Guid) && p.FamilyNames.Contains(f.Name))).ToList();

    private Dictionary<Guid, Guid> ProjectMapToRun(Dictionary<Guid, Guid> map) =>
        _projectBox.IsChecked == true
            ? _found.Where(p => p.Project is not null && map.ContainsKey(p.Guid)).ToDictionary(p => p.Guid, p => map[p.Guid])
            : new Dictionary<Guid, Guid>();

    private void RefreshRunButton()
    {
        var map = ReadyMap();
        var families = FamiliesToRun(map).Count;
        var project = ProjectMapToRun(map).Count;
        var parts = new List<string>();
        if (families > 0 || project == 0) parts.Add($"IN {families} FAMIL{(families == 1 ? "Y" : "IES")}");
        if (project > 0) parts.Add($"{project} PROJECT PARAMETER(S)");
        _runButton.Content = $"SWAP {string.Join(" + ", parts)}";
        _runButton.IsEnabled = map.Count > 0 && (families > 0 || project > 0);

        var affected = map.Count == 0 ? new List<string>() : FamilyParamRenamerEngine.AffectedViews(_doc, map.Keys);
        _affectedText.Text = affected.Count == 0
            ? ""
            : $"⚠ These still use the old parameters and will show blank after the swap until you switch them to the new ones:\n• {string.Join("\n• ", affected.Take(15))}{(affected.Count > 15 ? $"\n…and {affected.Count - 15} more" : "")}";
    }

    private void ConfirmAndRun()
    {
        var map = ReadyMap();
        var families = FamiliesToRun(map);
        var projectMap = ProjectMapToRun(map);
        if (map.Count == 0 || (families.Count == 0 && projectMap.Count == 0)) return;

        var text = new System.Text.StringBuilder();
        if (families.Count > 0)
            text.Append($"• Inside {families.Count} famil{(families.Count == 1 ? "y" : "ies")}: each family is opened, its parameters swapped (values and formulas kept) and reloaded. Undo is per family (Ctrl+Z once per family).\n\n");
        if (projectMap.Count > 0)
            text.Append($"• {projectMap.Count} project parameter(s): the new parameter is bound to the same categories (instance/type kept), every element's value is copied over, then the old one is removed from Project Parameters — or kept, if any value couldn't be copied. One Ctrl+Z undoes this step.\n\n");
        var confirm = MessageBox.Show(
            "Swap shared parameters?\n\n" + text,
            "Ottawa Tools — Family Parameter Renamer",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        Map = map;
        ProjectMap = projectMap;
        SelectedFamilyIds = families.Select(f => f.FamilyId).ToList();
        RunRequested = true;
        DialogResult = true;
        Close();
    }

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(4),
    };
}
