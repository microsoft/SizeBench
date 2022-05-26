using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

internal sealed class DiffSessionDataCache : IDisposable
{
    private List<BinarySectionDiff>? _binarySectionDiffsConstructedEver = new List<BinarySectionDiff>();
    public IReadOnlyList<BinarySectionDiff> BinarySectionDiffsConstructedEver => this._binarySectionDiffsConstructedEver ?? new List<BinarySectionDiff>();
    private List<COFFGroupDiff>? _coffGroupDiffsConstructedEver = new List<COFFGroupDiff>();
    public IReadOnlyList<COFFGroupDiff> COFFGroupDiffsConstrutedEver => this._coffGroupDiffsConstructedEver ?? new List<COFFGroupDiff>();
    private List<CompilandDiff>? _compilandDiffsConstructedEver = new List<CompilandDiff>(capacity: 100);
    public IReadOnlyList<CompilandDiff> CompilandDiffsConstructedEver => this._compilandDiffsConstructedEver ?? new List<CompilandDiff>();

    public void RecordBinarySectionDiffConstructed(BinarySectionDiff sectionDiff)
        => this._binarySectionDiffsConstructedEver!.Add(sectionDiff);
    public void RecordCOFFGroupDiffConstructed(COFFGroupDiff coffGroupDiff)
        => this._coffGroupDiffsConstructedEver!.Add(coffGroupDiff);
    public void RecordCompilandDiffConstructed(CompilandDiff compilandDiff)
        => this._compilandDiffsConstructedEver!.Add(compilandDiff);

    internal List<BinarySectionDiff>? AllBinarySectionDiffs { get; set; }
    internal List<COFFGroupDiff>? AllCOFFGroupDiffs { get; set; }

    //TODO: Diff: these 3 things are gross and a terrible pattern, we need
    //            something better to scale...but this is enough to make
    //            progress for now (ugh)
    internal bool _allLibDiffsCreationInProgress;
    internal Task? AllLibDiffs { get; set; }
    internal List<LibDiff>? AllLibDiffsInList { get; set; }

    internal List<CompilandDiff>? AllCompilandDiffs { get; set; }

    internal List<DuplicateDataItemDiff>? AllDuplicateDataItemDiffs { get; set; }

    internal List<WastefulVirtualItemDiff>? AllWastefulVirtualItemDiffs { get; set; }

    internal List<TemplateFoldabilityItemDiff>? AllTemplateFoldabilityItemDiffs { get; set; }

    internal Dictionary<uint, SymbolDiff> AllSymbolDiffsAlreadyConstructedWithNullBeforeSymbolByAfterRVA { get; } = new Dictionary<uint, SymbolDiff>();
    internal Dictionary<uint, SymbolDiff> AllSymbolDiffsAlreadyConstructedWithNullAfterSymbolByBeforeRVA { get; } = new Dictionary<uint, SymbolDiff>();
    internal Dictionary<uint, Dictionary<uint, SymbolDiff>> AllSymbolDiffsAlreadyConstructedByBeforeRVAThenByAfterRVA { get; } = new Dictionary<uint, Dictionary<uint, SymbolDiff>>();
    internal Dictionary<ISymbol, SymbolDiff> AllSymbolDiffsBySymbolFromEitherBeforeOrAfter { get; } = new Dictionary<ISymbol, SymbolDiff>();

    #region IDisposable Support

    private bool _isDisposed; // To detect redundant calls

    private void Dispose(bool _)
    {
        if (!this._isDisposed)
        {
            // Set large fields to null.
            this._binarySectionDiffsConstructedEver = null;
            this._coffGroupDiffsConstructedEver = null;
            this._compilandDiffsConstructedEver = null;

            this.AllBinarySectionDiffs = null;
            this.AllCOFFGroupDiffs = null;
            this.AllLibDiffs = null;
            this.AllLibDiffsInList = null;
            this.AllCompilandDiffs = null;

            this.AllDuplicateDataItemDiffs = null;
            this.AllWastefulVirtualItemDiffs = null;
            this.AllTemplateFoldabilityItemDiffs = null;

            this.AllSymbolDiffsAlreadyConstructedWithNullBeforeSymbolByAfterRVA.Clear();
            this.AllSymbolDiffsAlreadyConstructedWithNullAfterSymbolByBeforeRVA.Clear();
            this.AllSymbolDiffsAlreadyConstructedByBeforeRVAThenByAfterRVA.Clear();
            this.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.Clear();

            this._isDisposed = true;
        }
    }

    ~DiffSessionDataCache()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
