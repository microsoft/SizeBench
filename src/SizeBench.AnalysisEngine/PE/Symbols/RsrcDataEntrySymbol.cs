using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcDataEntrySymbol : RsrcSymbolBase
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes), Depth={this._depth}, Type={this._win32ResourceType}, DirectoryName={this._directoryName}";

    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.RsrcDirectoryEntry;

    private readonly uint _depth;
    private readonly string _language;
    private readonly uint _entryIndex;
    private readonly Win32ResourceType? _win32ResourceType;
    private readonly string _directoryName;

    internal RsrcDataEntrySymbol(uint rva, uint size, uint depth, string language, Win32ResourceType rsrcType, string rsrcTypeName, string? directoryName, uint entryIndex)
        : base(rva, size, ConjureName(depth, language, rsrcTypeName, directoryName))
    {
        if (depth > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        this._depth = depth;
        this._language = language;
        this._entryIndex = entryIndex;
        this._win32ResourceType = rsrcType;
        this._directoryName = directoryName ?? String.Empty;
    }

    private static string ConjureName(uint depth, string language, string rsrcTypeName, string? directoryName)
        => $"[rsrc data entry] L{depth} ({rsrcTypeName}, {directoryName ?? "unknown name"}, {language})";

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not RsrcDataEntrySymbol otherRsrcSymbol)
        {
            return false;
        }

        return this._win32ResourceType == otherRsrcSymbol._win32ResourceType &&
               this._depth == otherRsrcSymbol._depth &&
               this._entryIndex == otherRsrcSymbol._entryIndex &&
               base.IsVeryLikelyTheSameAs(otherSymbol) &&
               String.Equals(this._directoryName, otherRsrcSymbol._directoryName, StringComparison.Ordinal) &&
               String.Equals(this._language, otherRsrcSymbol._language, StringComparison.Ordinal);
    }
}
