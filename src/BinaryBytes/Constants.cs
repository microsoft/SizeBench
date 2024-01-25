namespace BinaryBytes;

internal static class Constants
{
    // Distinguishable names for padding bytes based on where they appear in the PE section
    internal const string SectionStartPadding = "SectionStartPadding";
    internal const string SectionEndPadding = "SectionEndPadding";
    internal const string CoffgroupStartPadding = "CoffgroupStartPadding";
    internal const string CoffgroupEndPadding = "CoffgroupEndPadding";
    internal const string SymbolPadding = "SymbolPadding";

    // For identifying PE Sections without any COFF groups and COFF groups without symbols, example .reloc, .rsrc
    // until we figure out a better way to handle it, we will put a special string for the whole section
    internal const string SpecialSection = "SpecialSection";
    internal const string SpecialCoffGroup = "SpecialCoffGroup";
}
