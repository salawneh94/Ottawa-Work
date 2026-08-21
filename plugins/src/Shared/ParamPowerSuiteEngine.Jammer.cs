using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

public static partial class ParamPowerSuiteEngine
{
    /// <summary>
    /// "Jammer": converts a LOCAL (non-shared) family parameter into a
    /// SHARED one, in place, preserving every instance's current value and
    /// any formula — the standard Revit technique for retrofitting a shared
    /// parameter onto families that already have a same-purpose local
    /// parameter, via FamilyManager.ReplaceParameter rather than deleting
    /// and recreating the parameter (which would lose every value).
    ///
    /// This operates on FAMILIES, not individual elements: it edits every
    /// distinct loadable family among the currently loaded elements
    /// (system-family categories like Walls/Floors have no family document
    /// to edit and are silently excluded, not skipped-and-counted, since
    /// "local family parameter" doesn't apply to them at all) that has a
    /// local — not already shared — parameter with the given name, replaces
    /// it with a shared definition from the given file/group, then reloads
    /// the family into the project.
    ///
    /// Each family edit needs its own Transaction on that family's own
    /// (temporary, in-memory) Document — a family document is a distinct
    /// Document object from the project, so unlike every other tab this
    /// can't be one single transaction; the reload back into the project is
    /// a second, ordinary transaction on the project document itself. The
    /// family keeps whichever of instance/type its local parameter already
    /// was — Jammer preserves that rather than exposing it as a choice,
    /// since flipping it while jamming would be a behavior change riding
    /// along with what's supposed to be a pure storage-mechanism swap.
    /// </summary>
    public static (int Jammed, int Skipped, int Failed) Jam(
        Document doc,
        List<Element> elements,
        string parameterName,
        string sharedParameterFilePath,
        string groupName,
        ForgeTypeId dataType,
        ForgeTypeId parameterGroup)
    {
        var families = elements
            .Select(e => (e as FamilyInstance)?.Symbol?.Family)
            .Where(f => f is not null)
            .Cast<Family>()
            .GroupBy(f => f.Id)
            .Select(g => g.First())
            .ToList();

        if (families.Count == 0) return (0, 0, 0);

        var definition = OpenOrCreateDefinition(doc, sharedParameterFilePath, groupName, parameterName, dataType);
        if (definition is null) return (0, 0, families.Count);

        int jammed = 0, skipped = 0, failed = 0;
        foreach (var family in families)
        {
            if (!family.IsEditable) { skipped++; continue; }

            Document? familyDoc = null;
            try
            {
                familyDoc = doc.EditFamily(family);
                var manager = familyDoc.FamilyManager;
                var existing = manager.GetParameters().FirstOrDefault(p => p.Definition.Name == parameterName);

                if (existing is null || existing.IsShared) { skipped++; continue; }

                using (var familyTransaction = new Transaction(familyDoc, "Jam parameter to shared"))
                {
                    familyTransaction.Start();
                    manager.ReplaceParameter(existing, definition, parameterGroup, existing.IsInstance);
                    familyTransaction.Commit();
                }

                using (var reloadTransaction = new Transaction(doc, "Reload jammed family"))
                {
                    reloadTransaction.Start();
                    familyDoc.LoadFamily(doc, new JammerLoadOptions());
                    reloadTransaction.Commit();
                }

                jammed++;
            }
            catch (Exception)
            {
                failed++;
            }
            finally
            {
                familyDoc?.Close(false);
            }
        }

        return (jammed, skipped, failed);
    }

    private class JammerLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
