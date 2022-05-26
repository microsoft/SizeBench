using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal abstract class EHSymbolBase : ISymbol
{
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name}";

    // This may target a function, a block, or a thunk - so don't assume this is a function!
    private Symbol? _targetSymbol;

    private string? _name;
    public string Name => this._name ?? throw new ObjectNotYetFullyConstructedException();

    public abstract SymbolComparisonClass SymbolComparisonClass { get; }

    public uint TargetStartRVA { get; }
    public uint RVA { get; }

    // The 'end' RVA is the last byte occupied by this symbol - so we subtract 1 from the size because
    // (RVA + Size) would point to the byte *after* the symbol.
    public uint RVAEnd => this.RVA + this.Size - 1;
    public uint Size { get; }

    // All the PESymbol types take up real space, so their Size == VirtualSize always
    public uint VirtualSize => this.Size;

    public bool IsCOMDATFolded => false;

    internal abstract string SymbolPrefix { get; }

    internal EHSymbolBase(uint targetStartRVA, uint rva, uint size)
    {
        this.TargetStartRVA = targetStartRVA;
        this.RVA = rva;
        this.Size = size;
    }

    internal EHSymbolBase(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : this(targetStartRVA, rva, size)
    {
        this._targetSymbol = targetSymbol;
        ConjureName();
    }

    internal void UpdateTargetSymbol(Symbol? symbol)
    {
        if (this._name != null)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        Debug.Assert(symbol is null || (this.TargetStartRVA >= symbol.RVA && this.TargetStartRVA <= symbol.RVAEnd));

        this._targetSymbol = symbol;
        ConjureName();
    }

    internal void ConjureName()
    {
        if (this._targetSymbol is IFunctionCodeSymbol targetFunction)
        {
            this._name = $"{this.SymbolPrefix} {targetFunction.FormattedName.UniqueSignatureWithNoPrefixes}";
        }
        else
        {
            this._name = $"{this.SymbolPrefix} {this._targetSymbol?.CanonicalName ?? $"<unnamed code at 0x{this.TargetStartRVA:X}>"}";
        }
    }

    public bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (this._name is null)
        {
            throw new ObjectNotYetFullyConstructedException();
        }

        if (otherSymbol is not EHSymbolBase otherEHSymbol)
        {
            return false;
        }

        return otherEHSymbol.SymbolComparisonClass == this.SymbolComparisonClass &&
               this._targetSymbol != null &&
               otherEHSymbol._targetSymbol != null &&
               this._targetSymbol.IsVeryLikelyTheSameAs(otherEHSymbol._targetSymbol);
    }
}
