using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace BIMFlow.ParameterFormulaPropagator;

/// <summary>Resolves {Level}, {Category}, and {seq} / {seq:000} placeholders in a template string.</summary>
public static class TemplateResolver
{
    private static readonly Regex SeqPattern = new(@"\{seq(:(\d+))?\}", RegexOptions.IgnoreCase);

    public static string Resolve(string template, Element element, Document doc, int sequenceNumber)
    {
        var result = template;

        var levelName = doc.GetElement(element.LevelId)?.Name ?? "";
        result = result.Replace("{Level}", levelName, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{Category}", element.Category?.Name ?? "", StringComparison.OrdinalIgnoreCase);

        result = SeqPattern.Replace(result, match =>
        {
            var digits = match.Groups[2].Success ? match.Groups[2].Value.Length : 1;
            return sequenceNumber.ToString().PadLeft(digits, '0');
        });

        return result;
    }
}
