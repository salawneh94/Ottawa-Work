using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.MaterialSwap;

/// <summary>
/// Reassigns a material across every wall/floor/roof/ceiling type compound-
/// structure layer that uses it, plus the Structural Material parameter on
/// any type that has one — the batch alternative to opening every type's
/// structure editor by hand. Paint overrides on individual faces aren't
/// touched — that's a separate, per-instance API surface.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "materialswap";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var materials = new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .OrderBy(m => m.Name)
            .ToList();

        if (materials.Count < 2)
        {
            TaskDialog.Show("BIMFlow — MaterialSwap", "This project needs at least two materials to swap between.");
            return Result.Succeeded;
        }

        var window = new MaterialSwapWindow(materials);
        if (window.ShowDialog() != true || window.FromMaterial is null || window.ToMaterial is null)
            return Result.Cancelled;

        if (window.FromMaterial.Id == window.ToMaterial.Id)
        {
            TaskDialog.Show("BIMFlow — MaterialSwap", "Pick two different materials.");
            return Result.Cancelled;
        }

        var fromId = window.FromMaterial.Id;
        var toId = window.ToMaterial.Id;

        var layersChanged = 0;
        var typesChanged = 0;
        var structuralParamsChanged = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Swap Material");
        transaction.Start();
        try
        {
            var hostTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(HostObjAttributes))
                .Cast<HostObjAttributes>()
                .ToList();

            foreach (var type in hostTypes)
            {
                var cs = type.GetCompoundStructure();
                if (cs is null) continue;

                var layers = cs.GetLayers();
                var changedThisType = false;
                for (var i = 0; i < layers.Count; i++)
                {
                    if (layers[i].MaterialId != fromId) continue;
                    layers[i].MaterialId = toId;
                    layersChanged++;
                    changedThisType = true;
                }

                if (changedThisType)
                {
                    cs.SetLayers(layers);
                    type.SetCompoundStructure(cs);
                    typesChanged++;
                }
            }

            var elementTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ElementType))
                .Cast<ElementType>()
                .ToList();

            foreach (var type in elementTypes)
            {
                var param = type.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (param is null || param.IsReadOnly || param.AsElementId() != fromId) continue;
                param.Set(toId);
                structuralParamsChanged++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — MaterialSwap",
            $"Swapped {window.FromMaterial.Name} -> {window.ToMaterial.Name} in {layersChanged} layer(s) across " +
            $"{typesChanged} type(s), plus {structuralParamsChanged} Structural Material parameter(s).");

        return Result.Succeeded;
    }
}
