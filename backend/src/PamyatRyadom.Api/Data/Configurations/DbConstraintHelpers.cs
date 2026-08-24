namespace PamyatRyadom.Api.Data.Configurations;

internal static class DbConstraintHelpers
{
    public static string InListCheck(string column, IEnumerable<string> values)
    {
        var quoted = string.Join(", ", values.Select(v => $"'{v}'"));
        return $"{column} IN ({quoted})";
    }

    public static string InListOrNullCheck(string column, IEnumerable<string> values)
    {
        var quoted = string.Join(", ", values.Select(v => $"'{v}'"));
        return $"{column} IS NULL OR {column} IN ({quoted})";
    }
}
