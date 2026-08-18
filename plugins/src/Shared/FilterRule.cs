using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

public enum RuleOperator
{
    Equals,
    Contains,
    GreaterThan,
    LessThan,
    IsEmpty,
    IsNotEmpty,
}

public record FilterRule(string ParameterName, RuleOperator Operator, string Value)
{
    public bool Matches(Element element)
    {
        var parameter = element.LookupParameter(ParameterName);
        if (parameter is null) return false;

        if (Operator == RuleOperator.IsEmpty) return !parameter.HasValue || GetText(parameter).Length == 0;
        if (Operator == RuleOperator.IsNotEmpty) return parameter.HasValue && GetText(parameter).Length > 0;

        var text = GetText(parameter);

        return Operator switch
        {
            RuleOperator.Equals => string.Equals(text, Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.Contains => text.Contains(Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.GreaterThan => TryCompareNumeric(parameter, out var cmp) && cmp > 0,
            RuleOperator.LessThan => TryCompareNumeric(parameter, out var cmp2) && cmp2 < 0,
            _ => false,
        };
    }

    private bool TryCompareNumeric(Parameter parameter, out int comparison)
    {
        comparison = 0;
        if (!double.TryParse(Value, out var target)) return false;

        double actual = parameter.StorageType switch
        {
            StorageType.Double => parameter.AsDouble(),
            StorageType.Integer => parameter.AsInteger(),
            _ => double.NaN,
        };

        if (double.IsNaN(actual)) return false;
        comparison = actual.CompareTo(target);
        return true;
    }

    private static string GetText(Parameter parameter) =>
        parameter.StorageType switch
        {
            StorageType.String => parameter.AsString() ?? string.Empty,
            StorageType.ElementId => parameter.AsValueString() ?? string.Empty,
            _ => parameter.AsValueString() ?? string.Empty,
        };
}
