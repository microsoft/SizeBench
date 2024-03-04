using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcGroupStringTablesDataSymbol : RsrcDataSymbol
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public IReadOnlyList<RsrcStringTableDataSymbol> StringTables { get; }
    internal RsrcGroupStringTablesDataSymbol(List<RsrcStringTableDataSymbol> stringTables, SymbolSourcesSupported symbolSourcesSupported)
        : base(stringTables[0].RVA, (uint)stringTables.Sum(st => st.Size), stringTables[0].Language, Win32ResourceType.STRINGTABLE,
               "STRINGTABLE", "<strings>", "", symbolSourcesSupported)
    {
        this.StringTables = stringTables;
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        //TODO: This may not be right, if a binary has multiple of these string table groups, how will we know which one to compare to which
        //      other one?  Maybe by looking at the StringTables inside?
        return otherSymbol is RsrcGroupStringTablesDataSymbol &&
               base.IsVeryLikelyTheSameAs(otherSymbol);
    }
}
