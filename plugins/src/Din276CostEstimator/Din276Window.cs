using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.Din276CostEstimator;

public enum CostScope { WholeProject, ActiveView }

/// <summary>
/// Live DIN 276 cost estimate: pick a scope, see every Kostengruppe with
/// matched elements and its real quantity, type a €/unit rate per row and
/// watch subtotals and the grand total update immediately. No rates ship
/// built in — see Din276Engine's doc comment for why — so the table starts
/// at zero until you type rates in or import a rate sheet you already have.
/// Import/export are pure file I/O (no document changes), so both run
/// directly from here rather than needing a transaction in Command.cs.
/// Assign to elements is the one action that writes to the model — this
/// window only confirms and hands the resolved codes off via
/// PendingAssignments/AssignRequested; Command.cs does the actual writes
/// in its own transaction after this window closes.
/// </summary>
public class Din276Window : OttawaWorkWindow
{
    private readonly Document _doc;
    private readonly View _activeView;
    private readonly Button _projectButton = OttawaWorkUi.SecondaryButton("Whole project");
    private readonly Button _viewButton = OttawaWorkUi.SecondaryButton("Active view");
    private readonly StackPanel _tablePanel = new();
    private readonly TextBlock _grandTotalText = new() { FontSize = 20, FontWeight = System.Windows.FontWeights.Bold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent) };
    private readonly TextBlock _statusText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };

    private CostScope _scope = CostScope.WholeProject;
    private List<KostengruppeTotal> _totals = new();
    private List<ElementQuantity> _quantities = new();
    private readonly Dictionary<string, double> _rates = new();

    public List<ElementQuantity> PendingAssignments { get; private set; } = new();
    public bool AssignRequested { get; private set; }

    public Din276Window(Document doc, View activeView) : base("Ottawa Tools — DIN 276 Cost Estimator", minWidth: 640)
    {
        _doc = doc;
        _activeView = activeView;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("💶", "DIN 276 Cost Estimator", "Classify elements into Kostengruppen, enter your own unit rates, and get a live estimate.", Close));

        root.Children.Add(OttawaWorkUi.SectionHeader("Scope"));
        var scopeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        _projectButton.Margin = new Thickness(0, 0, 8, 0);
        _projectButton.Click += (_, _) => SetScope(CostScope.WholeProject);
        _viewButton.Click += (_, _) => SetScope(CostScope.ActiveView);
        scopeRow.Children.Add(_projectButton);
        scopeRow.Children.Add(_viewButton);
        root.Children.Add(scopeRow);

        root.Children.Add(OttawaWorkUi.SectionHeader("Kostengruppen"));
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 4) };
        headerRow.Children.Add(new TextBlock { Text = "KG", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 36 });
        headerRow.Children.Add(new TextBlock { Text = "BEZEICHNUNG", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 190 });
        headerRow.Children.Add(new TextBlock { Text = "MENGE", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 90 });
        headerRow.Children.Add(new TextBlock { Text = "€/EINHEIT", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 90 });
        headerRow.Children.Add(new TextBlock { Text = "SUMME", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 100 });
        root.Children.Add(headerRow);

        var scroll = new ScrollViewer { MaxHeight = 360, Content = _tablePanel };
        root.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        var totalRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        totalRow.Children.Add(new TextBlock { Text = "GESAMTSUMME  ", FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center });
        totalRow.Children.Add(_grandTotalText);
        root.Children.Add(totalRow);

        root.Children.Add(_statusText);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var closeButton = OttawaWorkUi.SecondaryButton("Close");
        var importButton = OttawaWorkUi.SecondaryButton("Import rates");
        var exportButton = OttawaWorkUi.SecondaryButton("Export report");
        var assignButton = OttawaWorkUi.SuccessButton("Assign to elements");
        closeButton.Margin = new Thickness(0, 0, 8, 0);
        importButton.Margin = new Thickness(0, 0, 8, 0);
        exportButton.Margin = new Thickness(0, 0, 8, 0);
        closeButton.Click += (_, _) => { DialogResult = true; Close(); };
        importButton.Click += (_, _) => ImportRates();
        exportButton.Click += (_, _) => ExportReport();
        assignButton.Click += (_, _) => RequestAssign();
        buttonRow.Children.Add(closeButton);
        buttonRow.Children.Add(importButton);
        buttonRow.Children.Add(exportButton);
        buttonRow.Children.Add(assignButton);
        root.Children.Add(buttonRow);
        root.Children.Add(new TextBlock
        {
            Text = "Assign to elements writes each element's resolved KG code onto its own 'Kostengruppe' parameter (project/shared parameter must already exist — elements without it are skipped).",
            FontSize = 9,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = System.Windows.TextAlignment.Right,
        });

        SetContent(root, padding: 24);

        SetScope(CostScope.WholeProject);
    }

    private void SetScope(CostScope scope)
    {
        _scope = scope;
        OttawaWorkUi.SetToggleActive(_projectButton, scope == CostScope.WholeProject);
        OttawaWorkUi.SetToggleActive(_viewButton, scope == CostScope.ActiveView);
        Recalculate();
    }

    private void Recalculate()
    {
        var elements = _scope == CostScope.WholeProject
            ? new FilteredElementCollector(_doc).WhereElementIsNotElementType().ToList()
            : new FilteredElementCollector(_doc, _activeView.Id).WhereElementIsNotElementType().ToList();

        _quantities = Din276Engine.Classify(elements);
        _totals = Din276Engine.Aggregate(_quantities, _rates);

        BuildTable();
        RefreshGrandTotal();

        _statusText.Text = _totals.Count == 0
            ? "No elements matched a known Kostengruppe in this scope. Tag elements with a 'Kostengruppe' parameter to include them."
            : $"{elements.Count} element(s) scanned, {_totals.Count} Kostengruppe(n) matched.";
    }

    private void BuildTable()
    {
        _tablePanel.Children.Clear();

        foreach (var total in _totals)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock { Text = total.Code, FontSize = 12, FontWeight = System.Windows.FontWeights.SemiBold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 36 });
            row.Children.Add(new TextBlock { Text = total.Name, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 190, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = $"{total.Quantity:0.0} {Din276Engine.UnitLabel(total.Unit)}", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 90, VerticalAlignment = VerticalAlignment.Center });

            var rateBox = OttawaWorkUi.TextBox();
            rateBox.Width = 80;
            rateBox.Margin = new Thickness(0, 0, 10, 0);
            rateBox.Text = _rates.TryGetValue(total.Code, out var rate) ? rate.ToString("0.##") : "0";

            var subtotalText = new TextBlock { Text = FormatEuro(total.Subtotal), FontSize = 11, FontWeight = System.Windows.FontWeights.SemiBold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Success), Width = 100, VerticalAlignment = VerticalAlignment.Center };

            var code = total.Code;
            rateBox.TextChanged += (_, _) => OnRateChanged(code, rateBox, subtotalText);

            row.Children.Add(rateBox);
            row.Children.Add(subtotalText);
            _tablePanel.Children.Add(row);
        }
    }

    private void OnRateChanged(string code, TextBox rateBox, TextBlock subtotalText)
    {
        var rate = double.TryParse(rateBox.Text, out var parsed) ? parsed : 0;
        _rates[code] = rate;

        var index = _totals.FindIndex(t => t.Code == code);
        if (index < 0) return;

        var updated = _totals[index] with { Rate = rate, Subtotal = _totals[index].Quantity * rate };
        _totals[index] = updated;
        subtotalText.Text = FormatEuro(updated.Subtotal);

        RefreshGrandTotal();
    }

    private void RefreshGrandTotal() => _grandTotalText.Text = FormatEuro(_totals.Sum(t => t.Subtotal));

    private static string FormatEuro(double value) => $"{value:N0} €";

    private void ImportRates()
    {
        using var dialog = new OpenFileDialog { Title = "Import Kostengruppe rates", Filter = "Excel Workbook (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;

        var table = BrandedXlsx.ReadTable(dialog.FileName);
        if (table is null)
        {
            System.Windows.MessageBox.Show("This file doesn't look like a rate sheet — no header row was found.", "Ottawa Tools — DIN 276 Cost Estimator");
            return;
        }

        var (headers, rows) = table.Value;
        var codeIndex = headers.IndexOf("KG");
        var rateIndex = headers.IndexOf("€/Einheit");
        if (codeIndex < 0 || rateIndex < 0)
        {
            System.Windows.MessageBox.Show("Expected columns 'KG' and '€/Einheit' — export a rate sheet from this tool first to see the expected format.", "Ottawa Tools — DIN 276 Cost Estimator");
            return;
        }

        var imported = 0;
        foreach (var row in rows)
        {
            var code = row.ElementAtOrDefault(codeIndex);
            var rateText = row.ElementAtOrDefault(rateIndex);
            if (string.IsNullOrWhiteSpace(code) || !double.TryParse(rateText, out var rate)) continue;
            _rates[code] = rate;
            imported++;
        }

        Recalculate();
        System.Windows.MessageBox.Show($"Imported {imported} rate(s).", "Ottawa Tools — DIN 276 Cost Estimator");
    }

    private void ExportReport()
    {
        if (_totals.Count == 0)
        {
            System.Windows.MessageBox.Show("Nothing to export — no Kostengruppen matched in this scope.", "Ottawa Tools — DIN 276 Cost Estimator");
            return;
        }

        var headers = new List<string> { "KG", "Bezeichnung", "Menge", "Einheit", "€/Einheit", "Summe" };
        var rows = _totals.Select(t => new List<string>
        {
            t.Code,
            t.Name,
            t.Quantity.ToString("0.00"),
            Din276Engine.UnitLabel(t.Unit),
            t.Rate.ToString("0.00"),
            t.Subtotal.ToString("0.00"),
        }).ToList();
        rows.Add(new List<string> { "", "Gesamtsumme", "", "", "", _totals.Sum(t => t.Subtotal).ToString("0.00") });

        var savedPath = BrandedXlsx.Save(
            "Export DIN 276 cost estimate",
            $"{_doc.Title}_DIN276.xlsx",
            "DIN 276 Kostenschätzung",
            _scope == CostScope.WholeProject ? "Whole project" : "Active view",
            headers,
            rows);

        if (savedPath is not null)
            System.Windows.MessageBox.Show($"Exported to:\n{savedPath}", "Ottawa Tools — DIN 276 Cost Estimator");
    }

    /// <summary>Confirms, then hands the currently-classified elements off to Command.cs to actually write —
    /// this window never touches the model itself, matching every other Ottawa Tools dialog's transaction split.</summary>
    private void RequestAssign()
    {
        if (_quantities.Count == 0)
        {
            System.Windows.MessageBox.Show("Nothing to assign — no Kostengruppen matched in this scope.", "Ottawa Tools — DIN 276 Cost Estimator");
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Write the resolved Kostengruppe code onto {_quantities.Count} element(s)' own 'Kostengruppe' parameter?\n\nElements without that parameter (or where it's read-only) are skipped, not created.",
            "Ottawa Tools — DIN 276 Cost Estimator",
            System.Windows.MessageBoxButton.YesNo);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        PendingAssignments = _quantities;
        AssignRequested = true;
        DialogResult = true;
        Close();
    }
}
