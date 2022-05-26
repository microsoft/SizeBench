using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcDirectorySymbol : RsrcSymbolBase
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.RsrcDirectory;

    private readonly uint _depth;
    private readonly Win32ResourceType? _win32ResourceType;
    private readonly string _directoryName;

    internal RsrcDirectorySymbol(uint rva, uint size, uint depth, Win32ResourceType rsrcType, string rsrcTypeName, string? directoryName)
        : base(rva, size, ConjureName(depth, rsrcTypeName, directoryName))
    {
        if (depth > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        this._depth = depth;
        this._win32ResourceType = rsrcType;
        this._directoryName = directoryName ?? String.Empty;
    }

    private static string ConjureName(uint depth, string rsrcTypeName, string? directoryName)
    {
        return depth switch
        {
            0 => $"[rsrc directory] L0 (Root)",
            1 => $"[rsrc directory] L1 ({rsrcTypeName})",
            2 => $"[rsrc directory] L2 ({rsrcTypeName}, {directoryName ?? "unknown name"})",
            _ => throw new ArgumentOutOfRangeException(nameof(depth)),
        };
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not RsrcDirectorySymbol otherRsrcSymbol)
        {
            return false;
        }

        return this._win32ResourceType == otherRsrcSymbol._win32ResourceType &&
               this._depth == otherRsrcSymbol._depth &&
               base.IsVeryLikelyTheSameAs(otherSymbol) &&
               String.Equals(this._directoryName, otherRsrcSymbol._directoryName, StringComparison.Ordinal);
    }
}
