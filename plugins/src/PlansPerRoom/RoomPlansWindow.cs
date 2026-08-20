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
using BIMFlow.Shared;

namespace BIMFlow.PlansPerRoom;

/// <summary>
/// The "specify exactly what you want, per room" dialog: naming/numbering
/// templates with tokens, which view types to generate (floor plan / key
/// plan / elevations / wall sections / RCP), a filterable checkable room
/// list, and a detail panel for the currently-focused room including its
/// editable finish parameters. Generate builds one sheet per checked room
/// via RoomPlanGenerator; Save Parameters writes the finish fields back to
/// the focused room immediately, independent of Generate.
/// </summary>
public class RoomPlansWindow : BimFlowWindow
{
    private readonly Document _doc;
    private readonly List<RoomEntry> _allRooms;
    private readonly List<RoomRow> _rows = new();
    private readonly Dictionary<string, ElementId> _titleBlocksByName;
    private readonly Dictionary<string, ElementId> _viewTemplatesByName;

    private readonly ComboBox _titleBlockBox = BimFlowUi.ComboBox();
    private readonly TextBox _sheetNumberBox = BimFlowUi.TextBox();
    private readonly TextBox _sheetNameBox = BimFlowUi.TextBox();
    private readonly TextBox _viewNameBox = BimFlowUi.TextBox();
    private readonly TextBox _firstSheetNumberBox = BimFlowUi.TextBox();
    private readonly ComboBox _browserSortBox = BimFlowUi.ComboBox();
    private readonly TextBox _sortValueBox = BimFlowUi.TextBox();
    private readonly ComboBox _scaleBox = BimFlowUi.ComboBox();
    private readonly TextBox _cropMarginBox = BimFlowUi.TextBox();
    private readonly CheckBox _cropAnnotationsBox = BimFlowUi.CheckBoxItem("Crop annotations tight to the room", isChecked: true);
    private readonly CheckBox _autoFitBox = BimFlowUi.CheckBoxItem("Auto-fit scale to room size");
    private readonly CheckBox _showCropBox = BimFlowUi.CheckBoxItem("Show crop region");
    private readonly CheckBox _overwriteBox = BimFlowUi.CheckBoxItem("Overwrite existing sheets");
    private readonly CheckBox _autoFillBox = BimFlowUi.CheckBoxItem("Auto-fill sheet params", isChecked: true);

    private readonly CheckBox _floorPlanBox = BimFlowUi.CheckBoxItem("Create floor plans", isChecked: true);
    private readonly ComboBox _floorPlanTemplateBox = BimFlowUi.ComboBox();
    private readonly CheckBox _keyPlanBox = BimFlowUi.CheckBoxItem("Add key plan", isChecked: true);
    private readonly ComboBox _keyPlanCornerBox = BimFlowUi.ComboBox();
    private readonly CheckBox _elevationsBox = BimFlowUi.CheckBoxItem("Add 4 room elevations");
    private readonly CheckBox _wallSectionsBox = BimFlowUi.CheckBoxItem("Wall-aligned sections per room");
    private readonly CheckBox _ceilingPlanBox = BimFlowUi.CheckBoxItem("Add reflected ceiling plan");

    private readonly ComboBox _levelFilterBox = BimFlowUi.ComboBox();
    private readonly ComboBox _deptFilterBox = BimFlowUi.ComboBox();
    private readonly TextBox _searchBox = BimFlowUi.TextBox();
    private readonly StackPanel _roomsListPanel = new();
    private readonly TextBlock _footerText = new() { FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, 8, 0, 0) };

    private readonly TextBlock _selTitle = new() { FontSize = 18, FontWeight = System.Windows.FontWeights.Bold, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary) };
    private readonly TextBlock _selSub = new() { FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _selArea = new() { FontSize = 20, FontWeight = System.Windows.FontWeights.Bold, Foreground = BimFlowUi.BrushOf(BimFlowUi.Accent) };
    private readonly TextBlock _selDetails = new() { FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _selStatusHost = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBox _floorFinishBox = BimFlowUi.TextBox();
    private readonly TextBox _wallFinishBox = BimFlowUi.TextBox();
    private readonly TextBox _ceilingFinishBox = BimFlowUi.TextBox();
    private readonly TextBox _baseFinishBox = BimFlowUi.TextBox();
    private readonly StackPanel _selectedRoomPanel = new() { Width = 260 };

    private RoomEntry? _detailRoom;

    public bool Generated { get; private set; }
    public List<RoomEntry> SelectedRooms { get; private set; } = new();
    public ViewTypeOptions ViewTypes { get; private set; } = null!;
    public OutputOptions Output { get; private set; } = null!;

    public RoomPlansWindow(Document doc, List<RoomEntry> rooms) : base("BIMFlow — Plans Per Room", minWidth: 980)
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

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🗺️", "Plans Per Room", "Create cropped plan views per room and place them on individual sheets."));
        root.Children.Add(BuildStatRow());

        var columns = new StackPanel { Orientation = Orientation.Horizontal };
        columns.Children.Add(BuildOutputColumn());
        columns.Children.Add(BuildViewTypesColumn());
        columns.Children.Add(BuildRoomsColumn());
        columns.Children.Add(BuildSelectedRoomColumn());
        root.Children.Add(columns);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var generateButton = BimFlowUi.PrimaryButton("Generate");
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
            var tile = BimFlowUi.StatTile(value, label, color);
            tile.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(tile);
        }
        Add(total.ToString(), "TOTAL", null);
        Add(valid.ToString(), "VALID", BimFlowUi.Success);
        Add(unplaced.ToString(), "UNPLACED", BimFlowUi.Danger);
        Add(notEnclosed.ToString(), "NOT ENCLOSED", BimFlowUi.Warning);
        Add(levels.ToString(), "LEVELS", null);

        return row;
    }

    private StackPanel BuildOutputColumn()
    {
        var col = new StackPanel { Width = 230, Margin = new Thickness(0, 0, 14, 0) };
        col.Children.Add(BimFlowUi.SectionHeader("Output"));

        col.Children.Add(BimFlowUi.FieldLabel("Title block"));
        _titleBlockBox.Items.AddRange(_titleBlocksByName.Keys.Cast<object>());
        if (_titleBlockBox.Items.Count > 0) _titleBlockBox.SelectedIndex = 0;
        col.Children.Add(_titleBlockBox);

        col.Children.Add(BimFlowUi.FieldLabel("Sheet number"));
        _sheetNumberBox.Text = "RDS-{Num}";
        col.Children.Add(_sheetNumberBox);

        col.Children.Add(BimFlowUi.FieldLabel("Sheet name"));
        _sheetNameBox.Text = "Room Data - {Num} - {Name}";
        col.Children.Add(_sheetNameBox);

        col.Children.Add(BimFlowUi.FieldLabel("View name"));
        _viewNameBox.Text = "RDS-{Num}-{Name}-Plan";
        col.Children.Add(_viewNameBox);
        col.Children.Add(new TextBlock { Text = "Tokens: {Num} {Name} {Level} {Dept}", FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10), TextWrapping = TextWrapping.Wrap });

        col.Children.Add(BimFlowUi.FieldLabel("1st sheet number (optional)"));
        _firstSheetNumberBox.Text = "";
        col.Children.Add(_firstSheetNumberBox);
        col.Children.Add(new TextBlock { Text = "e.g. A-301 — overrides the sheet-number template with sequential numbers starting here.", FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10), TextWrapping = TextWrapping.Wrap });

        col.Children.Add(BimFlowUi.FieldLabel("Browser sort parameter"));
        _browserSortBox.Items.AddRange(new object[] { "(None)", "Level", "Department", "Custom" });
        _browserSortBox.SelectedIndex = 0;
        col.Children.Add(_browserSortBox);

        col.Children.Add(BimFlowUi.FieldLabel("Sort value (used when Custom)"));
        _sortValueBox.Text = "Room Drawings";
        col.Children.Add(_sortValueBox);

        col.Children.Add(BimFlowUi.FieldLabel("Scale"));
        _scaleBox.Items.AddRange(new object[] { "1:20", "1:25", "1:50", "1:100", "1:200" });
        _scaleBox.SelectedIndex = 2;
        col.Children.Add(_scaleBox);

        col.Children.Add(BimFlowUi.FieldLabel("Crop margin (ft)"));
        _cropMarginBox.Text = "3";
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
        col.Children.Add(BimFlowUi.SectionHeader("View types"));

        col.Children.Add(ViewTypeCard(BimFlowUi.Success, _floorPlanBox, "Cropped plan view per room", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(BimFlowUi.FieldLabel("View template"));
            _floorPlanTemplateBox.Items.Clear();
            _floorPlanTemplateBox.Items.Add("(None)");
            _floorPlanTemplateBox.Items.AddRange(_viewTemplatesByName.Keys.Cast<object>());
            _floorPlanTemplateBox.SelectedIndex = 0;
            sub.Children.Add(_floorPlanTemplateBox);
            return sub;
        }));

        col.Children.Add(ViewTypeCard(BimFlowUi.Accent, _keyPlanBox, "Location reference on sheet", () =>
        {
            var sub = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sub.Children.Add(BimFlowUi.FieldLabel("Corner position"));
            _keyPlanCornerBox.Items.AddRange(new object[] { "Top-Right", "Top-Left", "Bottom-Right", "Bottom-Left" });
            _keyPlanCornerBox.SelectedIndex = 0;
            sub.Children.Add(_keyPlanCornerBox);
            return sub;
        }));

        col.Children.Add(ViewTypeCard(BimFlowUi.Warning, _elevationsBox, "Interior elevations, cropped to the room's height"));
        col.Children.Add(ViewTypeCard(BimFlowUi.Danger, _wallSectionsBox, "One section per boundary wall, longest first"));
        col.Children.Add(ViewTypeCard(System.Windows.Media.Color.FromRgb(0x8B, 0x5C, 0xF6), _ceilingPlanBox, "RCP cropped to room boundary"));

        return col;
    }

    private Border ViewTypeCard(System.Windows.Media.Color barColor, CheckBox box, string subtitle, Func<StackPanel>? extra = null)
    {
        var stack = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new Border { Width = 3, Height = 16, Background = BimFlowUi.BrushOf(barColor), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 8, 0) });
        header.Children.Add(box);
        stack.Children.Add(header);
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(11, 0, 0, 0), TextWrapping = TextWrapping.Wrap });
        if (extra is not null) stack.Children.Add(extra());

        var card = BimFlowUi.Card(stack, padding: 10);
        card.Margin = new Thickness(0, 0, 0, 10);
        return card;
    }

    private StackPanel BuildRoomsColumn()
    {
        var col = new StackPanel { Width = 340, Margin = new Thickness(0, 0, 14, 0) };

        col.Children.Add(BimFlowUi.SectionHeader("Rooms"));

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
        var allButton = BimFlowUi.SecondaryButton("All");
        var noneButton = BimFlowUi.SecondaryButton("None");
        var validButton = BimFlowUi.SecondaryButton("Valid only");
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
        col.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        col.Children.Add(_footerText);

        return col;
    }

    private StackPanel BuildSelectedRoomColumn()
    {
        _selectedRoomPanel.Children.Add(BimFlowUi.SectionHeader("Selected room"));
        var card = new StackPanel();
        card.Children.Add(_selTitle);
        card.Children.Add(_selSub);
        card.Children.Add(new Border { Height = 1, Background = BimFlowUi.BrushOf(BimFlowUi.BorderColor), Margin = new Thickness(0, 10, 0, 10) });
        card.Children.Add(new TextBlock { Text = "AREA", FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary) });
        card.Children.Add(_selArea);
        card.Children.Add(_selDetails);
        card.Children.Add(_selStatusHost);

        card.Children.Add(BimFlowUi.SectionHeader("Parameters"));
        AddParamField(card, "Floor finish", _floorFinishBox);
        AddParamField(card, "Wall finish", _wallFinishBox);
        AddParamField(card, "Ceiling finish", _ceilingFinishBox);
        AddParamField(card, "Base finish", _baseFinishBox);

        var saveButton = BimFlowUi.SecondaryButton("Save parameters");
        saveButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        saveButton.Click += (_, _) => SaveDetailParameters();
        card.Children.Add(saveButton);

        _selectedRoomPanel.Children.Add(BimFlowUi.Card(card, padding: 14));
        return _selectedRoomPanel;
    }

    private void AddParamField(StackPanel host, string label, TextBox box)
    {
        host.Children.Add(BimFlowUi.FieldLabel(label));
        host.Children.Add(box);
    }

    private void BuildRoomRows()
    {
        foreach (var entry in _allRooms)
        {
            var checkbox = new CheckBox
            {
                IsChecked = entry.Status == RoomPlanStatus.Valid,
                IsEnabled = entry.Status == RoomPlanStatus.Valid,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            checkbox.Click += (_, _) => RefreshFooterOnly();

            var label = string.IsNullOrWhiteSpace(entry.Name) ? entry.Number : $"{entry.Number}  {entry.Name}";
            var rowContent = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            rowContent.Children.Add(checkbox);
            rowContent.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary), Width = 168, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            rowContent.Children.Add(new TextBlock { Text = $"{entry.AreaSqFt:0.0} sf", FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Width = 56 });

            var (badgeText, badgeColor) = entry.Status switch
            {
                RoomPlanStatus.Valid => ("Valid", BimFlowUi.Success),
                RoomPlanStatus.NotEnclosed => ("Open", BimFlowUi.Warning),
                _ => ("Unplaced", BimFlowUi.Danger),
            };
            rowContent.Children.Add(BimFlowUi.Badge(badgeText, badgeColor));

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
        _selArea.Text = $"{entry.AreaSqFt:0.0} sf";
        _selDetails.Text =
            $"Perimeter: {entry.PerimeterFeet:0.0} ft\n" +
            $"Volume: {entry.VolumeCubicFeet:0.0} cf\n" +
            $"Upper limit: {(string.IsNullOrWhiteSpace(entry.UpperLimitName) ? "—" : entry.UpperLimitName)}\n" +
            $"Limit offset: {entry.LimitOffsetFeet:0.0} ft";

        var (statusText, statusColor) = entry.Status switch
        {
            RoomPlanStatus.Valid => ("Valid", BimFlowUi.Success),
            RoomPlanStatus.NotEnclosed => ("Not enclosed", BimFlowUi.Warning),
            _ => ("Unplaced", BimFlowUi.Danger),
        };
        _selStatusHost.Children.Clear();
        _selStatusHost.Children.Add(BimFlowUi.Badge($"Status: {statusText}", statusColor));

        _floorFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_FLOOR)?.AsString() ?? "";
        _wallFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_WALL)?.AsString() ?? "";
        _ceilingFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_CEILING)?.AsString() ?? "";
        _baseFinishBox.Text = entry.Room.get_Parameter(BuiltInParameter.ROOM_FINISH_BASE)?.AsString() ?? "";
    }

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
            _ceilingPlanBox.IsChecked == true);

        var titleBlockId = _titleBlockBox.SelectedItem is string tbName && _titleBlocksByName.TryGetValue(tbName, out var tbId) ? tbId : ElementId.InvalidElementId;
        var scale = int.Parse((_scaleBox.SelectedItem as string ?? "1:50").Split(':')[1]);
        var cropMargin = double.TryParse(_cropMarginBox.Text, out var m) ? m : 3.0;
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
            System.Windows.MessageBox.Show("Load at least one title block family into the project first.", "BIMFlow — Plans Per Room");
            return;
        }
        if (SelectedRooms.Count == 0)
        {
            System.Windows.MessageBox.Show("Check at least one room to generate.", "BIMFlow — Plans Per Room");
            return;
        }

        Generated = true;
        DialogResult = true;
        Close();
    }

    private sealed record RoomRow(RoomEntry Entry, CheckBox Box, Border Container);
}
