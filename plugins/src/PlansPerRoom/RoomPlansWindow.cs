using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using TextWrapping = System.Windows.TextWrapping;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.PlansPerRoom;

/// <summary>
/// The "specify exactly what you want, per room" dialog: naming/numbering
/// templates with tokens, which view types to generate (floor plan / key
/// plan / elevations / wall sections / RCP), a filterable checkable room
/// list, and a detail panel for the currently-focused room including its
/// editable finish parameters. Generate builds one sheet per checked room
/// via RoomPlanGenerator; Save Parameters writes the finish fields back to
/// the focused room immediately, independent of Generate.
/// </summary>
public class RoomPlansWindow : OttawaWorkWindow
{
    private readonly Document _doc;
    private readonly List<RoomEntry> _allRooms;
    private readonly List<RoomRow> _rows = new();
    private readonly Dictionary<string, ElementId> _titleBlocksByName;
    private readonly Dictionary<string, ElementId> _viewTemplatesByName;
    private readonly Dictionary<string, ElementId> _ceilingPlanTemplatesByName;
    private readonly Dictionary<string, ElementId> _elevationTemplatesByName;
    private readonly Dictionary<string, ElementId> _sectionTemplatesByName;

    private readonly ComboBox _titleBlockBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _sheetNumberBox = OttawaWorkUi.TextBox();
    private readonly TextBox _sheetNameBox = OttawaWorkUi.TextBox();
    private readonly TextBox _viewNameBox = OttawaWorkUi.TextBox();
    private readonly TextBox _firstSheetNumberBox = OttawaWorkUi.TextBox();
    private readonly ComboBox _browserSortBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _sortValueBox = OttawaWorkUi.TextBox();
    private readonly ComboBox _scaleBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _cropMarginBox = OttawaWorkUi.TextBox();
    private readonly CheckBox _cropAnnotationsBox = OttawaWorkUi.CheckBoxItem("Crop annotations tight to the room", isChecked: true);
    private readonly CheckBox _autoFitBox = OttawaWorkUi.CheckBoxItem("Auto-fit scale to room size");
    private readonly CheckBox _showCropBox = OttawaWorkUi.CheckBoxItem("Show crop region");
    private readonly CheckBox _overwriteBox = OttawaWorkUi.CheckBoxItem("Overwrite existing sheets");
    private readonly CheckBox _autoFillBox = OttawaWorkUi.CheckBoxItem("Auto-fill sheet params", isChecked: true);

    private readonly CheckBox _floorPlanBox = OttawaWorkUi.CheckBoxItem("Create floor plans", isChecked: true);
    private readonly ComboBox _floorPlanTemplateBox = OttawaWorkUi.ComboBox();
    private readonly CheckBox _keyPlanBox = OttawaWorkUi.CheckBoxItem("Add key plan", isChecked: true);
    private readonly ComboBox _keyPlanCornerBox = OttawaWorkUi.ComboBox();
    private readonly CheckBox _elevationsBox = OttawaWorkUi.CheckBoxItem("Add 4 room elevations");
    private readonly ComboBox _elevationTemplateBox = OttawaWorkUi.ComboBox();
    private readonly CheckBox _wallSectionsBox = OttawaWorkUi.CheckBoxItem("Wall-aligned sections per room");
    private readonly ComboBox _sectionTemplateBox = OttawaWorkUi.ComboBox();
    private readonly CheckBox _ceilingPlanBox = OttawaWorkUi.CheckBoxItem("Add reflected ceiling plan");
    private readonly ComboBox _ceilingPlanTemplateBox = OttawaWorkUi.ComboBox();

    private readonly ComboBox _levelFilterBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _deptFilterBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _searchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _roomsListPanel = new();
    private readonly TextBlock _footerText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 8, 0, 0) };

    private readonly TextBlock _selTitle = new() { FontSize = 18, FontWeight = System.Windows.FontWeights.Bold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary) };
    private readonly TextBlock _selSub = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _selArea = new() { FontSize = 20, FontWeight = System.Windows.FontWeights.Bold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent) };
    private readonly TextBlock _selDetails = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _selStatusHost = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBox _floorFinishBox = OttawaWorkUi.TextBox();
    private readonly TextBox _wallFinishBox = OttawaWorkUi.TextBox();
    private readonly TextBox _ceilingFinishBox = OttawaWorkUi.TextBox();
    private readonly TextBox _baseFinishBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _selectedRoomPanel = new() { Width = 260 };

    private RoomEntry? _detailRoom;

    public bool Generated { get; private set; }
    public List<RoomEntry> SelectedRooms { get; private set; } = new();
    public ViewTypeOptions ViewTypes { get; private set; } = null!;
    public OutputOptions Output { get; private set; } = null!;

    public RoomPlansWindow(Document doc, List<RoomEntry> rooms) : base("Ottawa Tools — Plans Per Room", minWidth: 980)
    {
        _doc = doc;
        _allRooms = rooms;

        _titleBlocksByName = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .GroupBy(s => $"{s.FamilyName}: {s.Name}")
            .ToDictionary(g => g.Key, g => g.First().Id);

        _viewTemplatesByName = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.FloorPlan)
            .GroupBy(v => v.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // A Floor Plan view template can't be applied to a Ceiling Plan
        // view (Revit's View.ViewTemplateId requires a matching ViewType),
        // so this needs its own list filtered to ViewType.CeilingPlan.
        _ceilingPlanTemplatesByName = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.CeilingPlan)
            .GroupBy(v => v.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);

        _elevationTemplatesByName = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.Elevation)
            .GroupBy(v => v.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);

        _sectionTemplatesByName = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.Section)
            .GroupBy(v => v.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🗺️", "Plans Per Room", "Create cropped plan views per room and place them on individual sheets."));
        root.Children.Add(BuildStatRow());

        var columns = new StackPanel { Orientation = Orientation.Horizontal };
        columns.Children.Add(BuildOutputColumn());
        columns.Children.Add(BuildViewTypesColumn());
        columns.Children.Add(BuildRoomsColumn());
        columns.Children.Add(BuildSelectedRoomColumn());
        root.Children.Add(columns);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var generateButton = OttawaWorkUi.PrimaryButton("Generate");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        generateButton.Click += (_, _) => Finish();
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(generateButton);
        root.Children.Add(buttonRow);

        SetContent(root);

        BuildRoomRows();
        RefreshVisibility();
        ShowDetailPlaceholder();
    }

    private StackPanel BuildStatRow()
    {
        var total = _allRooms.Count;
        var valid = _allRooms.Count(r => r.Status == RoomPlanStatus.Valid);
        var unplaced = _allRooms.Count(r => r.Status == RoomPlanStatus.Unplaced);
        var notEnclosed = _allRooms.Count(r => r.Status == RoomPlanStatus.NotEnclosed);
        var levels = _allRooms.Select(r => r.LevelId).Distinct().Count();

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        void Add(string value, string label, System.Windows.Media.Color? color)
        {
            var tile = OttawaWorkUi.StatTile(value, label, color);
            tile.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(tile);
        }
        Add(total.ToString(), "TOTAL", null);
        Add(valid.ToString(), "VALID", OttawaWorkUi.Success);
        Add(unplaced.ToString(), "UNPLACED", OttawaWorkUi.Danger);
        Add(notEnclosed.ToString(), "NOT ENCLOSED", OttawaWorkUi.Warning);
        Add(levels.ToString(), "LEVELS", null);

        return row;
    }

    private StackPanel BuildOutputColumn()
    {
        var col = new StackPanel { Width = 230, Margin = new Thickness(0, 0, 14, 0) };
        col.Children.Add(OttawaWorkUi.SectionHeader("Output"));

        col.Children.Add(OttawaWorkUi.FieldLabel("Title block"));
        _titleBlockBox.Items.AddRange(_titleBlocksByName.Keys.Cast<object>());
        if (_titleBlockBox.Items.Count > 0) _titleBlockBox.SelectedIndex = 0;
        col.Children.Add(_titleBlockBox);

        col.Children.Add(OttawaWorkUi.FieldLabel("Sheet number"));
        _sheetNumberBox.Text = "RDS-{Num}";
        col.Children.Add(_sheetNumberBox);

        col.Children.Add(OttawaWorkUi.FieldLabel("Sheet name"));
        _sheetNameBox.Text = "Room Data - {Num} - {Name}";
        col.Children.Add(_sheetNameBox);

        col.Children.Add(OttawaWorkUi.FieldLabel("View name"));
        _viewNameBox.Text = "RDS-{Num}-{Name}-Plan";
        col.Children.Add(_viewNameBox);
        col.Children.Add(new TextBlock { Text = "Tokens: {Num} {Name} {Level} {Dept}", FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10), TextWrapping = TextWrapping.Wrap });

        col.Children.Add(OttawaWorkUi.FieldLabel("1st sheet number (optional)"));
        _firstSheetNumberBox.Text = "";
        col.Children.Add(_firstSheetNumberBox);
        col.Children.Add(new TextBlock { Text = "e.g. A-301 — overrides the sheet-number template with sequential numbers starting here.", FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10), TextWrapping = TextWrapping.Wrap });

        col.Children.Add(OttawaWorkUi.FieldLabel("Browser sort parameter"));
        _browserSortBox.Items.AddRange(new object[] { "(None)", "Level", "Department", "Custom" });
        _browserSortBox.SelectedIndex = 0;
        col.Children.Add(_browserSortBox);

        col.Children.Add(OttawaWorkUi.FieldLabel("Sort value (used when Custom)"));
        _sortValueBox.Text = "Room Drawings";
        col.Children.Add(_sortValueBox);

        col.Children.Add(OttawaWorkUi.FieldLabel("Scale"));
        _scaleBox.Items.AddRange(new object[] { "1:20", "1:25", "1:50", "1:100", "1:200" });
        _scaleBox.SelectedIndex = 2;
        col.Children.Add(_scaleBox);

        col.Children.Add(OttawaWorkUi.FieldLabel($"Crop margin ({LengthUnitSymbol()})"));
        // Default text shows ~3 ft (the old fixed value) converted into
        // whatever unit the project actually uses — confirmed live (user-
        // reported), the field stayed labeled "(ft)" and the typed number
        // was always read as feet, regardless of the project's own unit
        // settings (e.g. metric); a metric user typing "3" meaning 3 m
        // would have gotten roughly a third of that. Read back and
        // converted the same way in Finish() below.
        _cropMarginBox.Text = Math.Round(UnitUtils.ConvertFromInternalUnits(3.0, LengthUnitTypeId()), 2).ToString(System.Globalization.CultureInfo.CurrentCulture);
        col.Children.Add(_cropMarginBox);

        col.Children.Add(_cropAnnotationsBox);
        col.Children.Add(_autoFitBox);
        col.Children.Add(_showCropBox);
        col.Children.Add(_overwriteBox);
        col.Children.Add(_autoFillBox);

        return col;
    }

    private StackPanel BuildViewTypesColumn()
    {
        var col = new StackPanel { Width = 210, Margin = new Thickness(0, 0, 14, 0) };
        col.Children.Add(OttawaWorkUi.SectionHeader("View types"));

        col.Children.Add(ViewTypeCard(OttawaWorkUi.Success, _floorPlanBox, "Cropped plan view per room", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(OttawaWorkUi.FieldLabel("View template"));
            _floorPlanTemplateBox.Items.Clear();
            _floorPlanTemplateBox.Items.Add("(None)");
            _floorPlanTemplateBox.Items.AddRange(_viewTemplatesByName.Keys.Cast<object>());
            _floorPlanTemplateBox.SelectedIndex = 0;
            sub.Children.Add(_floorPlanTemplateBox);
            return sub;
        }));

        col.Children.Add(ViewTypeCard(OttawaWorkUi.Accent, _keyPlanBox, "Location reference on sheet", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(OttawaWorkUi.FieldLabel("Corner position"));
            _keyPlanCornerBox.Items.AddRange(new object[] { "Top-Right", "Top-Left", "Bottom-Right", "Bottom-Left" });
            _keyPlanCornerBox.SelectedIndex = 0;
            sub.Children.Add(_keyPlanCornerBox);
            return sub;
        }));

        col.Children.Add(ViewTypeCard(OttawaWorkUi.Warning, _elevationsBox, "Interior elevations, cropped to the room's height, aligned to the room's own walls", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(OttawaWorkUi.FieldLabel("View template"));
            _elevationTemplateBox.Items.Clear();
            _elevationTemplateBox.Items.Add("(None)");
            _elevationTemplateBox.Items.AddRange(_elevationTemplatesByName.Keys.Cast<object>());
            _elevationTemplateBox.SelectedIndex = 0;
            sub.Children.Add(_elevationTemplateBox);
            return sub;
        }));
        col.Children.Add(ViewTypeCard(OttawaWorkUi.Danger, _wallSectionsBox, "One section per boundary wall, longest first", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(OttawaWorkUi.FieldLabel("View template"));
            _sectionTemplateBox.Items.Clear();
            _sectionTemplateBox.Items.Add("(None)");
            _sectionTemplateBox.Items.AddRange(_sectionTemplatesByName.Keys.Cast<object>());
            _sectionTemplateBox.SelectedIndex = 0;
            sub.Children.Add(_sectionTemplateBox);
            return sub;
        }));
        col.Children.Add(ViewTypeCard(System.Windows.Media.Color.FromRgb(0x8B, 0x5C, 0xF6), _ceilingPlanBox, "RCP cropped to room boundary", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(OttawaWorkUi.FieldLabel("View template"));
            _ceilingPlanTemplateBox.Items.Clear();
            _ceilingPlanTemplateBox.Items.Add("(None)");
            _ceilingPlanTemplateBox.Items.AddRange(_ceilingPlanTemplatesByName.Keys.Cast<object>());
            _ceilingPlanTemplateBox.SelectedIndex = 0;
            sub.Children.Add(_ceilingPlanTemplateBox);
            return sub;
        }));

        return col;
    }

    private Border ViewTypeCard(System.Windows.Media.Color barColor, CheckBox box, string subtitle, Func<StackPanel>? extra = null)
    {
        var stack = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new Border { Width = 3, Height = 16, Background = OttawaWorkUi.BrushOf(barColor), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 8, 0) });
        header.Children.Add(box);
        stack.Children.Add(header);
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(11, 0, 0, 0), TextWrapping = TextWrapping.Wrap });
        if (extra is not null) stack.Children.Add(extra());

        var card = OttawaWorkUi.Card(stack, padding: 10);
        card.Margin = new Thickness(0, 0, 0, 10);
        return card;
    }

    private StackPanel BuildRoomsColumn()
    {
        var col = new StackPanel { Width = 340, Margin = new Thickness(0, 0, 14, 0) };

        col.Children.Add(OttawaWorkUi.SectionHeader("Rooms"));

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal };
        _levelFilterBox.Width = 155;
        _levelFilterBox.Margin = new Thickness(0, 4, 8, 10);
        _levelFilterBox.Items.Add("All Levels");
        _levelFilterBox.Items.AddRange(_allRooms.Select(r => r.LevelName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).Cast<object>());
        _levelFilterBox.SelectedIndex = 0;
        _levelFilterBox.SelectionChanged += (_, _) => RefreshVisibility();
        filterRow.Children.Add(_levelFilterBox);

        _deptFilterBox.Width = 155;
        _deptFilterBox.Margin = new Thickness(0, 4, 0, 10);
        _deptFilterBox.Items.Add("All Departments");
        _deptFilterBox.Items.AddRange(_allRooms.Select(r => r.Department).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d).Cast<object>());
        _deptFilterBox.SelectedIndex = 0;
        _deptFilterBox.SelectionChanged += (_, _) => RefreshVisibility();
        filterRow.Children.Add(_deptFilterBox);
        col.Children.Add(filterRow);

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var allButton = OttawaWorkUi.SecondaryButton("All");
        var noneButton = OttawaWorkUi.SecondaryButton("None");
        var validButton = OttawaWorkUi.SecondaryButton("Valid only");
        allButton.Margin = new Thickness(0, 0, 6, 0);
        noneButton.Margin = new Thickness(0, 0, 6, 0);
        allButton.Click += (_, _) => { foreach (var row in VisibleRows()) row.Box.IsChecked = true; RefreshVisibility(); };
        noneButton.Click += (_, _) => { foreach (var row in VisibleRows()) row.Box.IsChecked = false; RefreshVisibility(); };
        validButton.Click += (_, _) => { foreach (var row in VisibleRows()) row.Box.IsChecked = row.Entry.Status == RoomPlanStatus.Valid; RefreshVisibility(); };
        quickRow.Children.Add(allButton);
        quickRow.Children.Add(noneButton);
        quickRow.Children.Add(validButton);
        col.Children.Add(quickRow);

        _searchBox.Margin = new Thickness(0, 0, 0, 8);
        _searchBox.ToolTip = "Search by room name, number, or level...";
        _searchBox.TextChanged += (_, _) => RefreshVisibility();
        col.Children.Add(_searchBox);

        var scroll = new ScrollViewer { MaxHeight = 360, Content = _roomsListPanel };
        col.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        col.Children.Add(_footerText);

        return col;
    }

    private StackPanel BuildSelectedRoomColumn()
    {
        _selectedRoomPanel.Children.Add(OttawaWorkUi.SectionHeader("Selected room"));
        var card = new StackPanel();
        card.Children.Add(_selTitle);
        card.Children.Add(_selSub);
        card.Children.Add(new Border { Height = 1, Background = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor), Margin = new Thickness(0, 10, 0, 10) });
        card.Children.Add(new TextBlock { Text = "AREA", FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        card.Children.Add(_selArea);
        card.Children.Add(_selDetails);
        card.Children.Add(_selStatusHost);

        card.Children.Add(OttawaWorkUi.SectionHeader("Parameters"));
        AddParamField(card, "Floor finish", _floorFinishBox);
        AddParamField(card, "Wall finish", _wallFinishBox);
        AddParamField(card, "Ceiling finish", _ceilingFinishBox);
        AddParamField(card, "Base finish", _baseFinishBox);

        var saveButton = OttawaWorkUi.SecondaryButton("Save parameters");
        saveButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        saveButton.Click += (_, _) => SaveDetailParameters();
        card.Children.Add(saveButton);

        _selectedRoomPanel.Children.Add(OttawaWorkUi.Card(card, padding: 14));
        return _selectedRoomPanel;
    }

    private void AddParamField(StackPanel host, string label, TextBox box)
    {
        host.Children.Add(OttawaWorkUi.FieldLabel(label));
        host.Children.Add(box);
    }

    private void BuildRoomRows()
    {
        foreach (var entry in _allRooms)
        {
            var checkbox = new CheckBox
            {
                // Starts UNCHECKED for every room, valid or not — confirmed
                // live (user-reported): defaulting every valid room to
                // pre-checked meant clicking 1-2 rows to look at them (which
                // only opens the "Selected room" detail panel, it doesn't
                // touch the checkbox) generated a sheet for every OTHER
                // valid room too, since they were still checked from the
                // default and Generate correctly processes whatever's
                // checked. Nothing pre-selected forces an explicit choice —
                // the "All"/"Valid only" quick buttons above still cover
                // the bulk-select case in one click.
                IsChecked = false,
                IsEnabled = entry.Status == RoomPlanStatus.Valid,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            checkbox.Click += (_, _) => RefreshFooterOnly();

            var label = string.IsNullOrWhiteSpace(entry.Name) ? entry.Number : $"{entry.Number}  {entry.Name}";
            var rowContent = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            rowContent.Children.Add(checkbox);
            rowContent.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 168, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            rowContent.Children.Add(new TextBlock { Text = FormatArea(entry.AreaSqFt), FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 56 });

            var (badgeText, badgeColor) = entry.Status switch
            {
                RoomPlanStatus.Valid => ("Valid", OttawaWorkUi.Success),
                RoomPlanStatus.NotEnclosed => ("Open", OttawaWorkUi.Warning),
                _ => ("Unplaced", OttawaWorkUi.Danger),
            };
            rowContent.Children.Add(OttawaWorkUi.Badge(badgeText, badgeColor));

            var container = new Border { Padding = new Thickness(4, 2, 4, 2), CornerRadius = new CornerRadius(6), Cursor = System.Windows.Input.Cursors.Hand, Child = rowContent };
            var row = new RoomRow(entry, checkbox, container);
            container.MouseLeftButtonUp += (_, _) => SelectForDetail(row);

            _rows.Add(row);
            _roomsListPanel.Children.Add(container);
        }
    }

    private IEnumerable<RoomRow> VisibleRows() => _rows.Where(r => r.Container.Visibility == System.Windows.Visibility.Visible);

    private void RefreshVisibility()
    {
        var level = _levelFilterBox.SelectedItem as string;
        var dept = _deptFilterBox.SelectedItem as string;
        var search = _searchBox.Text?.Trim() ?? "";

        foreach (var row in _rows)
        {
            var matchesLevel = level is null or "All Levels" || row.Entry.LevelName == level;
            var matchesDept = dept is null or "All Departments" || row.Entry.Department == dept;
            var matchesSearch = string.IsNullOrEmpty(search)
                || row.Entry.Number.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Entry.LevelName.Contains(search, StringComparison.OrdinalIgnoreCase);

            row.Container.Visibility = matchesLevel && matchesDept && matchesSearch
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        RefreshFooterOnly();
    }

    private void RefreshFooterOnly()
    {
        var visible = VisibleRows().ToList();
        var selected = _rows.Count(r => r.Box.IsChecked == true);
        _footerText.Text = $"Showing {visible.Count}/{_rows.Count} rooms  |  {selected} selected";
    }

    private void SelectForDetail(RoomRow row)
    {
        _detailRoom = row.Entry;
        var entry = row.Entry;

        _selTitle.Text = entry.Number;
        _selSub.Text = string.IsNullOrWhiteSpace(entry.Name) ? entry.LevelName : $"{entry.Name}\nLevel: {entry.LevelName}";
        _selArea.Text = FormatArea(entry.AreaSqFt);
        _selDetails.Text =
            $"Perimeter: {FormatLength(entry.PerimeterFeet)}\n" +
            $"Volume: {FormatVolume(entry.VolumeCubicFeet)}\n" +
            $"Upper limit: {(string.IsNullOrWhiteSpace(entry.UpperLimitName) ? "—" : entry.UpperLimitName)}\n" +
            $"Limit offset: {FormatLength(entry.LimitOffsetFeet)}";

        var (statusText, statusColor) = entry.Status switch
        {
            RoomPlanStatus.Valid => ("Valid", OttawaWorkUi.Success),
            RoomPlanStatus.NotEnclosed => ("Not enclosed", OttawaWorkUi.Warning),
            _ => ("Unplaced", OttawaWorkUi.Danger),
        };
        _selStatusHost.Children.Clear();
        _selStatusHost.Children.Add(OttawaWorkUi.Badge($"Status: {statusText}", statusColor));

        _floorFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_FLOOR)?.AsString() ?? "";
        _wallFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_WALL)?.AsString() ?? "";
        _ceilingFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_CEILING)?.AsString() ?? "";
        _baseFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_BASE)?.AsString() ?? "";
    }

    /// <summary>The project's real configured length unit (e.g. Meters, not always Feet) — used both to
    /// label the crop margin field correctly and to convert what's typed there into Revit's internal
    /// feet, instead of always assuming/treating that field's number as feet regardless of the project's
    /// own unit settings.</summary>
    private ForgeTypeId LengthUnitTypeId() => _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();

    private string LengthUnitSymbol()
    {
        var options = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
        var symbolTypeId = options.GetSymbolTypeId();
        return symbolTypeId.Empty() ? LabelUtils.GetLabelForUnit(options.GetUnitTypeId()) : LabelUtils.GetLabelForSymbol(symbolTypeId);
    }

    /// <summary>RoomEntry's Area/Perimeter/Volume/LimitOffset are Revit's raw INTERNAL values — always
    /// feet/square-feet/cubic-feet under the hood, regardless of what units the project is actually set
    /// up to display in (Revit's documented internal unit system, unrelated to the project's own Units
    /// settings). Confirmed live (user-reported): a metric project showed these labeled "sf"/"ft"/"cf"
    /// with the raw internal number, meaningless to someone working in m²/m — UnitFormatUtils.Format
    /// converts to and formats in the project's real configured display units (with the right symbol,
    /// precision, and decimal separator) instead of assuming the internal unit is what should be shown.</summary>
    private string FormatArea(double internalValue) => UnitFormatUtils.Format(_doc.GetUnits(), SpecTypeId.Area, internalValue, false);
    private string FormatLength(double internalValue) => UnitFormatUtils.Format(_doc.GetUnits(), SpecTypeId.Length, internalValue, false);
    private string FormatVolume(double internalValue) => UnitFormatUtils.Format(_doc.GetUnits(), SpecTypeId.Volume, internalValue, false);

    private void ShowDetailPlaceholder()
    {
        _selTitle.Text = "—";
        _selSub.Text = "Click a room in the list to see its details.";
        _selArea.Text = "";
        _selDetails.Text = "";
    }

    private void SaveDetailParameters()
    {
        if (_detailRoom is null) return;
        RoomPlanGenerator.SaveFinishParameters(_doc, _detailRoom.Room, _floorFinishBox.Text, _wallFinishBox.Text, _ceilingFinishBox.Text, _baseFinishBox.Text);
    }

    private void Finish()
    {
        SelectedRooms = _rows.Where(r => r.Box.IsChecked == true).Select(r => r.Entry).ToList();

        var floorPlanTemplateId = _floorPlanTemplateBox.SelectedItem is string templateName && _viewTemplatesByName.TryGetValue(templateName, out var tId) ? tId : (ElementId?)null;
        var ceilingPlanTemplateId = _ceilingPlanTemplateBox.SelectedItem is string ceilingTemplateName && _ceilingPlanTemplatesByName.TryGetValue(ceilingTemplateName, out var ctId) ? ctId : (ElementId?)null;
        var elevationTemplateId = _elevationTemplateBox.SelectedItem is string elevTemplateName && _elevationTemplatesByName.TryGetValue(elevTemplateName, out var etId) ? etId : (ElementId?)null;
        var sectionTemplateId = _sectionTemplateBox.SelectedItem is string sectTemplateName && _sectionTemplatesByName.TryGetValue(sectTemplateName, out var stId) ? stId : (ElementId?)null;
        var corner = (_keyPlanCornerBox.SelectedItem as string) switch
        {
            "Top-Left" => KeyPlanCorner.TopLeft,
            "Bottom-Right" => KeyPlanCorner.BottomRight,
            "Bottom-Left" => KeyPlanCorner.BottomLeft,
            _ => KeyPlanCorner.TopRight,
        };
        ViewTypes = new ViewTypeOptions(
            _floorPlanBox.IsChecked == true,
            floorPlanTemplateId,
            _keyPlanBox.IsChecked == true,
            corner,
            _elevationsBox.IsChecked == true,
            _wallSectionsBox.IsChecked == true,
            _ceilingPlanBox.IsChecked == true,
            ceilingPlanTemplateId,
            elevationTemplateId,
            sectionTemplateId);

        var titleBlockId = _titleBlockBox.SelectedItem is string tbName && _titleBlocksByName.TryGetValue(tbName, out var tbId) ? tbId : ElementId.InvalidElementId;
        var scale = int.Parse((_scaleBox.SelectedItem as string ?? "1:50").Split(':')[1]);
        var lengthUnitTypeId = LengthUnitTypeId();
        var cropMarginDisplay = double.TryParse(_cropMarginBox.Text, out var m) ? m : UnitUtils.ConvertFromInternalUnits(3.0, lengthUnitTypeId);
        var cropMargin = UnitUtils.ConvertToInternalUnits(cropMarginDisplay, lengthUnitTypeId);
        var sortMode = (_browserSortBox.SelectedItem as string) switch { "Level" => "Level", "Department" => "Department", "Custom" => "Custom", _ => "None" };

        Output = new OutputOptions(
            titleBlockId,
            _sheetNumberBox.Text,
            _sheetNameBox.Text,
            _viewNameBox.Text,
            _firstSheetNumberBox.Text,
            sortMode,
            _sortValueBox.Text,
            scale,
            cropMargin,
            _cropAnnotationsBox.IsChecked == true,
            _autoFitBox.IsChecked == true,
            _showCropBox.IsChecked == true,
            _overwriteBox.IsChecked == true,
            _autoFillBox.IsChecked == true);

        if (titleBlockId == ElementId.InvalidElementId)
        {
            System.Windows.MessageBox.Show("Load at least one title block family into the project first.", "Ottawa Tools — Plans Per Room");
            return;
        }
        if (SelectedRooms.Count == 0)
        {
            System.Windows.MessageBox.Show("Check at least one room to generate.", "Ottawa Tools — Plans Per Room");
            return;
        }

        Generated = true;
        DialogResult = true;
        Close();
    }

    private sealed record RoomRow(RoomEntry Entry, CheckBox Box, Border Container);
}
