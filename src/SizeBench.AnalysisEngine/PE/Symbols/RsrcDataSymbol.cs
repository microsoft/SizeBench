using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal class RsrcDataSymbol : RsrcSymbolBase
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.RsrcData;

    public string Language { get; }
    public Win32ResourceType Win32ResourceType { get; }
    public string ResourceTypeName { get; }

    internal RsrcDataSymbol(uint rva, uint size, string language, Win32ResourceType rsrcType, string? rsrcTypeName, string dataName, string nameSuffix = "")
        : base(rva, size, ConjureName(language, rsrcType, rsrcTypeName, dataName, nameSuffix))
    {
        this.Language = language;
        this.Win32ResourceType = rsrcType;
        this.ResourceTypeName = rsrcTypeName ?? rsrcType.ToString();
    }

    private static string ConjureName(string language, Win32ResourceType rsrcType, string? rsrcTypeName, string? dataName, string nameSuffix)
        => $"Resource '{dataName ?? "unknown name"}' ({rsrcTypeName ?? rsrcType.ToString()}, {language}){nameSuffix}";

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not RsrcDataSymbol otherRsrcSymbol)
        {
            return false;
        }

        return this.Win32ResourceType == otherRsrcSymbol.Win32ResourceType &&
               base.IsVeryLikelyTheSameAs(otherSymbol) &&
               String.Equals(this.Language, otherRsrcSymbol.Language, StringComparison.Ordinal);
    }
}
