using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public enum NumberingSortOrder
{
    LevelLeftToRightThenTopToBottom,
    LevelTopToBottomThenLeftToRight,
    LevelThenTypeName,
    ElementIdOrder,
    SelectionOrder,
}

public record NumberingRule(string ParameterName, string Prefix, string Separator, int Start, int Step);

/// <summary>One row per element in sort order. ExistingValues/NewValues are parallel to the rule
/// list passed into BuildPreview — index i is rule i's existing/new value for this element. A null
/// entry in NewValues means that rule skips this element (already numbered, and skip-already-numbered
/// is on) — its existing value is left untouched.</summary>
public record NumberingPreviewRow(
    ElementId ElementId,
    string Descriptor,
    string Level,
    IReadOnlyList<string> ExistingValues,
    IReadOnlyList<string?> NewValues);

/// <summary>
/// Core logic for the Unique Numbering tool: order a batch of elements (from
/// any category, not just Rooms), then assign sequential values — one or
/// more rules at once, each its own target parameter/prefix/separator/start/
/// step — with a skip option for elements that already carry a value, and
/// a preview the caller can inspect before committing anything.
/// </summary>
public static class UniqueNumberingEngine
{
    public static List<Element> OrderElements(Document doc, List<Element> elements, NumberingSortOrder sortOrder, IReadOnlyList<ElementId> selectionOrder)
    {
        if (sortOrder == NumberingSortOrder.SelectionOrder)
        {
            var indexOf = selectionOrder
                .Select((id, i) => (id, i))
                .GroupBy(t => t.id)
                .ToDictionary(g => g.Key, g => g.First().i);
            return elements.OrderBy(e => indexOf.TryGetValue(e.Id, out var i) ? i : int.MaxValue).ThenBy(e => e.Id.Value).ToList();
        }

        if (sortOrder == NumberingSortOrder.ElementIdOrder)
            return elements.OrderBy(e => e.Id.Value).ToList();

        if (sortOrder == NumberingSortOrder.LevelThenTypeName)
        {
            return elements
                .OrderBy(e => LevelSortKey(doc, e))
                .ThenBy(e => TypeNameOf(doc, e), StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Id.Value)
                .ToList();
        }

        // Position-based orders need a representative point; every one of
        // the common categories this tool offers (walls, floors, roofs...)
        // has SOME way to derive one — a LocationPoint (doors, furniture),
        // a LocationCurve (walls — midpoint), or failing both, its bounding
        // box center (floors, ceilings, roofs are often sketch-based with
        // no simple Location) — so nothing silently drops out of a
        // position-based sort just because it isn't a point-placed family.
        var located = elements.Select(e => (Element: e, Point: LocationOf(e))).ToList();
        var withPoint = located.Where(t => t.Point is not null).ToList();
        var withoutPoint = located.Where(t => t.Point is null).OrderBy(t => t.Element.Id.Value).Select(t => t.Element);

        IEnumerable<Element> ordered = sortOrder == NumberingSortOrder.LevelLeftToRightThenTopToBottom
            ? withPoint.OrderBy(t => LevelSortKey(doc, t.Element))
                       .ThenByDescending(t => Math.Round(t.Point!.Y, 1))
                       .ThenBy(t => Math.Round(t.Point!.X, 1))
                       .Select(t => t.Element)
            : withPoint.OrderBy(t => LevelSortKey(doc, t.Element))
                       .ThenBy(t => Math.Round(t.Point!.X, 1))
                       .ThenByDescending(t => Math.Round(t.Point!.Y, 1))
                       .Select(t => t.Element);

        return ordered.Concat(withoutPoint).ToList();
    }

    public static (List<NumberingPreviewRow> Rows, int Assigns, int Skips) BuildPreview(
        Document doc, List<Element> orderedElements, List<NumberingRule> rules, bool skipAlreadyNumbered)
    {
        // Counted first so the zero-padding width (below) reflects how many
        // values a rule will actually reach, not the raw element count —
        // skipped elements never consume a counter slot, matching the
        // "renumber only what's missing" behavior a real office numbering
        // pass wants (re-running the tool after adding 5 new doors picks up
        // right where the last real assignment left off, not restarted from
        // element #1 of the whole batch again).
        var existingByRule = rules
            .Select(rule => orderedElements.Select(e => e.LookupParameter(rule.ParameterName)?.AsString() ?? "").ToList())
            .ToList();

        var counters = rules.Select(r => r.Start).ToArray();
        var finalCounters = new int[rules.Count];
        var assignedFlags = new List<bool[]>();

        for (var elementIndex = 0; elementIndex < orderedElements.Count; elementIndex++)
        {
            var flags = new bool[rules.Count];
            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                var alreadyHasValue = !string.IsNullOrWhiteSpace(existingByRule[ruleIndex][elementIndex]);
                var willAssign = !(skipAlreadyNumbered && alreadyHasValue);
                flags[ruleIndex] = willAssign;
                if (willAssign)
                {
                    finalCounters[ruleIndex] = counters[ruleIndex];
                    counters[ruleIndex] += rules[ruleIndex].Step;
                }
            }
            assignedFlags.Add(flags);
        }

        var padWidths = rules.Select((r, i) => Math.Max(2, finalCounters[i].ToString().Length)).ToArray();

        counters = rules.Select(r => r.Start).ToArray();
        var rows = new List<NumberingPreviewRow>();
        var assigns = 0;
        var skips = 0;

        for (var elementIndex = 0; elementIndex < orderedElements.Count; elementIndex++)
        {
            var element = orderedElements[elementIndex];
            var existingValues = new List<string>();
            var newValues = new List<string?>();

            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                var rule = rules[ruleIndex];
                var existing = existingByRule[ruleIndex][elementIndex];
                existingValues.Add(existing);

                if (!assignedFlags[elementIndex][ruleIndex])
                {
                    newValues.Add(null);
                    skips++;
                    continue;
                }

                var number = counters[ruleIndex].ToString().PadLeft(padWidths[ruleIndex], '0');
                newValues.Add($"{rule.Prefix}{rule.Separator}{number}");
                counters[ruleIndex] += rule.Step;
                assigns++;
            }

            rows.Add(new NumberingPreviewRow(element.Id, Descriptor(doc, element), LevelNameOf(doc, element), existingValues, newValues));
        }

        return (rows, assigns, skips);
    }

    /// <summary>Writes every rule's assigned values in one two-pass batch (see TwoPassRenamer) so
    /// numbers that happen to swap between two elements never collide with each other mid-write.
    /// Rules on a StorageType.String parameter are set directly; anything else is skipped per-element
    /// rather than attempted, since a sequential text/number value like "D-001" only round-trips
    /// safely through a String-storage field (Comments, Mark, Room Number, and similar are all String).</summary>
    public static int Apply(Document doc, List<NumberingPreviewRow> rows, List<NumberingRule> rules)
    {
        var renames = new List<(Action<string> setValue, string newValue)>();

        foreach (var row in rows)
        {
            var element = doc.GetElement(row.ElementId);
            if (element is null) continue;

            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                var newValue = row.NewValues[ruleIndex];
                if (newValue is null) continue;

                var parameter = element.LookupParameter(rules[ruleIndex].ParameterName);
                if (parameter is not { StorageType: StorageType.String, IsReadOnly: false }) continue;

                renames.Add((value => parameter.Set(value), newValue));
            }
        }

        TwoPassRenamer.Apply(renames);
        return renames.Count;
    }

    /// <summary>A short human-readable label for the preview table — Family: Type for anything with
    /// a type (doors, furniture, ...), else the element's own Name (rooms, generic named elements).</summary>
    public static string Descriptor(Document doc, Element element)
    {
        if (doc.GetElement(element.GetTypeId()) is ElementType type)
        {
            var familyName = type.FamilyName;
            return string.IsNullOrWhiteSpace(familyName) ? type.Name : $"{familyName}: {type.Name}";
        }
        return element.Name;
    }

    private static string TypeNameOf(Document doc, Element element) =>
        (doc.GetElement(element.GetTypeId()) as ElementType)?.Name ?? "";

    private static double LevelSortKey(Document doc, Element element) =>
        element.LevelId != ElementId.InvalidElementId && doc.GetElement(element.LevelId) is Level level
            ? level.Elevation
            : double.MaxValue;

    private static string LevelNameOf(Document doc, Element element) =>
        element.LevelId != ElementId.InvalidElementId && doc.GetElement(element.LevelId) is Level level
            ? level.Name
            : "";

    private static XYZ? LocationOf(Element element)
    {
        if (element.Location is LocationPoint point) return point.Point;
        if (element.Location is LocationCurve curve) return curve.Curve.Evaluate(0.5, true);

        var bbox = element.get_BoundingBox(null);
        return bbox is null ? null : (bbox.Min + bbox.Max) * 0.5;
    }
}
