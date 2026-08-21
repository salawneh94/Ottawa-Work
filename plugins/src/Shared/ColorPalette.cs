using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>Named, visually distinct color sets for color-by-value overrides. Cycles if there are more values than colors.</summary>
public static class ColorPalette
{
    public static readonly Autodesk.Revit.DB.Color[] Vivid =
    {
        new(230, 25, 75), new(60, 180, 75), new(0, 130, 200), new(245, 130, 48),
        new(145, 30, 180), new(70, 240, 240), new(240, 50, 230), new(210, 245, 60),
        new(250, 190, 212), new(0, 128, 128), new(220, 190, 255), new(170, 110, 40),
    };

    public static readonly Autodesk.Revit.DB.Color[] Pastel =
    {
        new(255, 179, 186), new(255, 223, 186), new(255, 255, 186), new(186, 255, 201),
        new(186, 225, 255), new(201, 186, 255), new(255, 186, 246), new(186, 255, 255),
        new(223, 255, 186), new(255, 214, 165), new(214, 193, 255), new(193, 255, 214),
    };

    public static readonly Autodesk.Revit.DB.Color[] Grayscale =
    {
        new(40, 40, 40), new(75, 75, 75), new(110, 110, 110), new(145, 145, 145),
        new(180, 180, 180), new(60, 60, 70), new(95, 95, 105), new(130, 130, 140),
        new(165, 165, 175), new(25, 25, 25), new(200, 200, 200), new(115, 115, 125),
    };

    public static readonly Autodesk.Revit.DB.Color[] Ocean =
    {
        new(0, 105, 148), new(0, 150, 136), new(0, 188, 212), new(38, 166, 154),
        new(3, 155, 229), new(0, 121, 107), new(77, 208, 225), new(0, 96, 100),
        new(129, 212, 250), new(0, 77, 64), new(178, 235, 242), new(0, 172, 193),
    };

    public static readonly Dictionary<string, Autodesk.Revit.DB.Color[]> Named = new()
    {
        ["Vivid"] = Vivid,
        ["Pastel"] = Pastel,
        ["Grayscale"] = Grayscale,
        ["Ocean"] = Ocean,
    };

    public static Autodesk.Revit.DB.Color ForIndex(int index) => Vivid[index % Vivid.Length];

    public static Autodesk.Revit.DB.Color ForIndex(int index, string paletteName)
    {
        var colors = Named.TryGetValue(paletteName, out var found) ? found : Vivid;
        return colors[index % colors.Length];
    }
}
