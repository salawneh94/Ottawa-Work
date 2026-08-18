using Autodesk.Revit.DB;

namespace BIMFlow.BatchExporter;

public static class NamingTemplate
{
    /// <summary>Supports {SheetNumber}, {SheetName}, {ViewName}, {Index} placeholders.</summary>
    public static string Resolve(string template, View view, int index)
    {
        var sheetNumber = view is ViewSheet sheet ? sheet.SheetNumber : string.Empty;
        var sheetName = view is ViewSheet sheet2 ? sheet2.Name : string.Empty;

        var result = template
            .Replace("{SheetNumber}", sheetNumber)
            .Replace("{SheetName}", sheetName)
            .Replace("{ViewName}", view.Name)
            .Replace("{Index}", index.ToString());

        foreach (var invalidChar in System.IO.Path.GetInvalidFileNameChars())
            result = result.Replace(invalidChar, '_');

        return result;
    }
}
