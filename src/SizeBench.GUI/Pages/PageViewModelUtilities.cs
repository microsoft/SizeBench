namespace SizeBench.GUI.Pages;

internal static class PageViewModelUtilities
{
    // Some things like Zig can generate COFF Group names that are very weird and include commas and function parameter types
    // and all sorts of bizarre things.  We need to escape these so XAML data binding works.
    internal static string EscapeXAMLIndexer(string? input)
    {
        if (input is null)
        {
            return "null";
        }

        return input.Replace(@"\", @"^\", StringComparison.Ordinal)
                    .Replace(",", @"^,", StringComparison.Ordinal)
                    .Replace("=", @"^=", StringComparison.Ordinal)
                    .Replace("{", @"^{", StringComparison.Ordinal)
                    .Replace("}", @"^}", StringComparison.Ordinal)
                    .Replace("'", @"^'", StringComparison.Ordinal)
                    .Replace("(", @"^(", StringComparison.Ordinal)
                    .Replace(")", @"^)", StringComparison.Ordinal);
    }
}
