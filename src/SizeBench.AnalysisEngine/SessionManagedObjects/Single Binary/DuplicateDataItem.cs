using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Duplicate Data: {Symbol.Name}, Size = {Symbol.Size}, Wasted Size = {WastedSize}")]
public sealed class DuplicateDataItem
{
    [DisplayFormat(DataFormatString = "{0:N0}")]
    public uint WastedSize =>
            // The first instance isn't considered wasted, so the number of copies is (ReferencedIn - 1).
            (uint)(this.Symbol.Size * (this.ReferencedIn.Count - 1));

    [DisplayFormat(DataFormatString = "{0:N0}")]
    public uint TotalSize => (uint)(this.Symbol.Size * this.ReferencedIn.Count);

    public StaticDataSymbol Symbol { get; }

    private readonly List<uint> _rvas = new List<uint>();
    public IReadOnlyList<uint> RVAs => this._rvas;

    private readonly List<Compiland> _referencedIn = new List<Compiland>();
    internal void AddReferencedCompilandIfNecessary(Compiland compiland, uint rvaInThatCompiland)
    {
        if (!this._referencedIn.Contains(compiland))
        {
            this._referencedIn.Add(compiland);
            this._rvas.Add(rvaInThatCompiland);
        }
    }

    internal void SortReferencedIn() => this._referencedIn.Sort((c1, c2) => String.CompareOrdinal(c1.Name, c2.Name));
    public IReadOnlyList<Compiland> ReferencedIn => this._referencedIn;

    internal DuplicateDataItem(StaticDataSymbol symbol, Compiland firstCompilandFoundIn)
    {
        this.Symbol = symbol;
        this._referencedIn.Add(firstCompilandFoundIn);
        this._rvas.Add(symbol.RVA);
    }
}
