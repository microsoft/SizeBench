using Dia2Lib;

namespace SizeBench.AnalysisEngine.Symbols;

// These types are for how we attempt to deal with folding in the binary - two types of things can fold: DIA symbols, and XDATA symbols.
// DIA symbols fold when /OPT:ICF is in use, and what results is multiple names and SymIndexIDs for the same RVA.
// XDATA symbols fold when the same unwinding data can be used for multiple functions, and what results is multiple TargetSymbolRVAs for the same XDATA record RVA.
internal sealed class NameCanonicalization
{
    private bool _hasEverHadAnyNonPublicSymbolNameAdded;
    private readonly List<KeyValuePair<uint, string>> _namesBySymIndexId = new List<KeyValuePair<uint, string>>();
    public IReadOnlyList<KeyValuePair<uint, string>> NamesBySymIndexID => this._namesBySymIndexId;
    public uint CanonicalSymIndexID { get; private set; }
    public string CanonicalName { get; private set; } = String.Empty;

    public void AddName(uint symIndexId, string name, SymTagEnum symTag)
    {
        // PublicSymbols have ugly names that don't sort well because they start with things like "public:" and "virtual" and include the function's
        // return type.  We don't want any of that, so if we are getting a PublicSymbol but we've seen any non-public symbol we don't want this name,
        // just throw it away.
        if (symTag == SymTagEnum.SymTagPublicSymbol && this._hasEverHadAnyNonPublicSymbolNameAdded)
        {
            return;
        }

        // If we are seeing the same name a second time, it's not going to help later so we'll discard it.  Sometimes the same name appears with multiple
        // SymIndexIDs which is annoying but as far as I know harmless.
        for (var i = 0; i < this._namesBySymIndexId.Count; i++)
        {
            if (this._namesBySymIndexId[i].Value.Equals(name, StringComparison.Ordinal))
            {
                return;
            }
        }

        this._namesBySymIndexId.Add(new KeyValuePair<uint, string>(symIndexId, name));
        this._hasEverHadAnyNonPublicSymbolNameAdded |= symTag != SymTagEnum.SymTagPublicSymbol;
    }

    public void Canonicalize()
    {
        uint? canonicalSymIndexId = null;
        string? canonicalName = null;

        foreach (var names in this._namesBySymIndexId)
        {
            if (canonicalName is null || String.CompareOrdinal(canonicalName, names.Value) > 0)
            {
                canonicalSymIndexId = names.Key;
                canonicalName = names.Value;
            }
        }

        if (canonicalSymIndexId != null && canonicalName != null)
        {
            this.CanonicalSymIndexID = canonicalSymIndexId.Value;
            this.CanonicalName = canonicalName;
        }
    }
}
