using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ConnectorAlign;

/// <summary>
/// Finds unconnected duct/pipe/cable tray/conduit connectors that sit close
/// to another unconnected connector — the classic "should be joined but
/// isn't" near-miss. Reports candidates for review rather than moving
/// geometry automatically, since auto-aligning could shift elements in
/// ways that need an engineer's judgment call.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "connectoralign";
    private const double ToleranceFeet = 0.5;

    private static readonly BuiltInCategory[] Categories =
    {
        BuiltInCategory.OST_DuctCurves,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_CableTray,
        BuiltInCategory.OST_Conduit,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(Categories))
            .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ConnectorAlign", "No duct, pipe, cable tray, or conduit elements were found.");
            return Result.Succeeded;
        }

        var unconnected = new List<(Connector Connector, Element Owner)>();

        foreach (var element in elements)
        {
            var connectorManager = GetConnectorManager(element);
            if (connectorManager is null) continue;

            foreach (Connector connector in connectorManager.Connectors)
            {
                if (!connector.IsConnected)
                    unconnected.Add((connector, element));
            }
        }

        var rows = new List<ResultRow>();
        var reported = new HashSet<(ElementId, ElementId)>();

        for (var i = 0; i < unconnected.Count; i++)
        {
            for (var j = i + 1; j < unconnected.Count; j++)
            {
                var (connA, ownerA) = unconnected[i];
                var (connB, ownerB) = unconnected[j];
                if (ownerA.Id == ownerB.Id) continue;

                var key = ownerA.Id.Value < ownerB.Id.Value ? (ownerA.Id, ownerB.Id) : (ownerB.Id, ownerA.Id);
                if (reported.Contains(key)) continue;

                var distance = connA.Origin.DistanceTo(connB.Origin);
                if (distance > 0.001 && distance <= ToleranceFeet)
                {
                    reported.Add(key);
                    rows.Add(new ResultRow(
                        new[]
                        {
                            ownerA.Category?.Name ?? "?",
                            $"{ownerA.Id.Value} ↔ {ownerB.Id.Value}",
                            $"{distance * 12:F1}\"",
                        },
                        new List<ElementId> { ownerA.Id, ownerB.Id }));
                }
            }
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ConnectorAlign", $"Scanned {unconnected.Count} unconnected connector(s) — no near-miss pairs found within {ToleranceFeet * 12:F0}\".");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "BIMFlow — ConnectorAlign Results",
            $"{rows.Count} candidate misalignment(s) found.",
            new[] { "Category", "Element Ids", "Gap" },
            rows);

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }

    private static ConnectorManager? GetConnectorManager(Element element)
    {
        return element switch
        {
            MEPCurve mepCurve => mepCurve.ConnectorManager,
            FamilyInstance familyInstance => familyInstance.MEPModel?.ConnectorManager,
            _ => null,
        };
    }
}
