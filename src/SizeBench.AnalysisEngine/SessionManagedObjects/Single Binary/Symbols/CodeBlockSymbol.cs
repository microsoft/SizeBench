using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

public abstract class CodeBlockSymbol : Symbol
{
    internal const string BlockOfCodePrefix = "Block of code in ";

    // TODO: this isn't quite right. A  primary block has just one parent function, but separated blocks can be shared among many functions.
    //       How should we represent this in the SizeBench API?
    private IFunctionCodeSymbol? _parentFunction;

    public IFunctionCodeSymbol ParentFunction
    {
        get => this._parentFunction!;
        internal set
        {
            if (this._parentFunction != null)
            {
                throw new ObjectFullyConstructedAlreadyException();
            }

            this._parentFunction = value;
            OnParentFunctionSet();
        }
    }

    internal override bool CanBeFolded => true;

    internal CodeBlockSymbol(SessionDataCache cache,
                             uint rva,
                             uint size,
                             uint symIndexId) : base(cache, System.String.Empty, rva, size, isVirtualSize: false, symIndexId: symIndexId, namesAreFinalized: false)
    {
        Debug.Assert(cache.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.Code));
    }

    protected virtual void OnParentFunctionSet() { }
}
