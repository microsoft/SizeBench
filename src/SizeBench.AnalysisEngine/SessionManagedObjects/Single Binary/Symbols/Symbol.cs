using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Symbol : ISymbol
{
    [ExcludeFromCodeCoverage] // Only used by the debugger
    private string DebuggerDisplay
    {
        get
        {
            if (this.IsCOMDATFolded)
            {
                return $"Symbol Name={this.Name}, Size={this.Size} (COMDAT folded with {this.CanonicalName})";
            }
            else
            {
                return $"Symbol Name={this.Name}, Size={this.Size}";
            }
        }
    }

    private protected SessionDataCache DataCache { get; }

    private bool _nameFinalized;
    private string _name = String.Empty;
    public string Name
    {
        get
        {
            if (!this._nameFinalized)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._name;
        }
        private protected set
        {
            if (this._nameFinalized)
            {
                throw new ObjectFullyConstructedAlreadyException();
            }

            this._name = value;
            this._nameFinalized = true;
        }
    }

    public virtual SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.Unknown;

    private bool _canonicalNameFinalized;
    private string _canonicalName = String.Empty;
    public string CanonicalName
    {
        get
        {
            if (!this._canonicalNameFinalized)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._canonicalName;
        }
        private protected set
        {
            if (this._canonicalNameFinalized)
            {
                throw new ObjectFullyConstructedAlreadyException();
            }

            this._canonicalName = value;
            this._canonicalNameFinalized = true;
        }
    }

    internal virtual bool CanBeFolded => false;

    public uint RVA { get; }

    // The 'end' RVA is the last byte occupied by this symbol - so we subtract 1 from the size because
    // (RVA + Size) would point to the byte *after* the symbol.
    // If the Size is 0 (which can happen with some sentinel symbols that mark the beginning/end of a
    // range, like .CRT$XCA and such), then the RVAEnd is the same as the RVA, thus the Math.Max.
    public uint RVAEnd => this.RVA + Math.Max(this.VirtualSize, 1) - 1;

    public uint Size { get; }

    public uint VirtualSize { get; }

    public bool IsCOMDATFolded { get; }

    internal uint SymIndexId { get; }

    internal Symbol(SessionDataCache cache, string name, uint rva, uint size, bool isVirtualSize, uint symIndexId, bool namesAreFinalized = true)
    {
#if DEBUG
        if (cache.AllSymbolsBySymIndexId.ContainsKey(symIndexId))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif
        this.DataCache = cache;

        this._canonicalName = name;
        this._canonicalNameFinalized = namesAreFinalized;
        this._name = name;
        this._nameFinalized = namesAreFinalized;
        this.RVA = rva;
        this.SymIndexId = symIndexId;

        if (this.DataCache.AllCanonicalNames!.TryGetValue(rva, out var nameCanonicalization))
        {
            this._canonicalName = nameCanonicalization.CanonicalName;
            this.IsCOMDATFolded = nameCanonicalization.CanonicalSymIndexID != symIndexId; // If we're a symIndexId *other than* the canonical one, we're COMDAT folded to that canonical one
        }
        else
        {
            this.IsCOMDATFolded = false;
        }

        if (isVirtualSize)
        {
            this.VirtualSize = size;
            this.Size = 0;
        }
        else
        {
            this.Size = size;
            this.VirtualSize = size;
        }

        if (this.IsCOMDATFolded)
        {
            this.VirtualSize = this.Size = 0;
        }

        cache.AllSymbolsBySymIndexId.Add(symIndexId, this);
    }

#pragma warning disable CA1062 // Validate arguments of public methods - this function is just too hot for perf to afford to null-check each time, so we'll let it crash if anyone passes null in.
    public virtual bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (this.SymbolComparisonClass != otherSymbol.SymbolComparisonClass)
        {
            return false;
        }

        if (String.Equals(this.Name, otherSymbol.Name, StringComparison.Ordinal))
        {
            return true;
        }
        else if (this.CanBeFolded && this.RVA != 0 && otherSymbol.RVA != 0)
        {
            var other = (Symbol)otherSymbol;

            if (String.Equals(this.CanonicalName, other.CanonicalName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
#pragma warning restore CA1062
}
