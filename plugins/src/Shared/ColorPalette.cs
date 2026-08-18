using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>A small, visually distinct color set for color-by-value overrides. Cycles if there are more values than colors.</summary>
public static class ColorPalette
{
    public static readonly Autodesk.Revit.DB.Color[] Colors =
    {
        new(230, 25, 75), new(60, 180, 75), new(0, 130, 200), new(245, 130, 48),
        new(145, 30, 180), new(70, 240, 240), new(240, 50, 230), new(210, 245, 60),
        new(250, 190, 212), new(0, 128, 128), new(220, 190, 255), new(170, 110, 40),
    };

    public static Autodesk.Revit.DB.Color ForIndex(int index) => Colors[index % Colors.Length];
}
