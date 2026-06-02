using System.Diagnostics.CodeAnalysis;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

// Looking stuff up can be a pretty slow operation, each Session maintains a cache of things
// it has already looked up.
internal sealed class SessionDataCache : IDisposable
{
    public uint BytesPerWord { get; set; }
    public SymbolSourcesSupported SymbolSourcesSupported { get; }

    internal Linker LinkerDetected { get; set; } = Linker.Unknown;

    private List<BinarySection>? _binarySectionsConstructedEver = new List<BinarySection>();
    public IReadOnlyList<BinarySection> BinarySectionsConstructedEver
        => this._binarySectionsConstructedEver ?? new List<BinarySection>();

    private List<COFFGroup>? _coffGroupsConstructedEver = new List<COFFGroup>();
    public IReadOnlyList<COFFGroup> COFFGroupsConstrutedEver
        => this._coffGroupsConstructedEver ?? new List<COFFGroup>();

    public List<Compiland>? _compilandsConstructedEver = new List<Compiland>(capacity: 100);
    // Note that the same Compiland may appear in this dictionary multiple times, with different SymIndexIds, because
    // for example we map the null name and the empty string name to "...no name found..." as a single Compiland entity
    // in SizeBench.
    private Dictionary<uint, Compiland>? _compilandsBySymIndexId = new Dictionary<uint, Compiland>(capacity: 100);
    internal IReadOnlyDictionary<uint, Compiland> CompilandsBySymIndexId => this._compilandsBySymIndexId!;
    public IReadOnlyList<Compiland> CompilandsConstructedEver => this._compilandsConstructedEver ?? new List<Compiland>();
    public Compiland? FindCompilandBySymIndexId(uint symIndexId)
    {
        if (this._compilandsBySymIndexId is not null &&
            this._compilandsBySymIndexId.TryGetValue(symIndexId, out var compiland) == true)
        {
            return compiland;
        }
        else
        {
            return null;
        }
    }

    private Dictionary<string, SourceFile>? _sourceFilesByFilename = new Dictionary<string, SourceFile>(StringComparer.OrdinalIgnoreCase);
    private List<SourceFile>? _sourceFilesConstructedEver = new List<SourceFile>(capacity: 100);
    public IReadOnlyList<SourceFile> SourceFilesConstructedEver => this._sourceFilesConstructedEver ?? new List<SourceFile>();
    public SourceFile? FindSourceFileByFilename(string? sourceFileName)
    {
        if (this.AllSourceFiles is null)
        {
            throw new InvalidOperationException("Don't try to find a source file by name unless you're sure all source files have been enumerated!");
        }

        if (sourceFileName is null)
        {
            return null;
        }

        this._sourceFilesByFilename!.TryGetValue(sourceFileName, out var foundFile);
        return foundFile;
    }

    internal Dictionary<string, SourceFile> UnsafeSourceFilesByFilename_UsedOnlyDuringConstruction => this._sourceFilesByFilename!;

    public void RecordBinarySectionConstructed(BinarySection section)
        => this._binarySectionsConstructedEver!.Add(section);
    public void RecordCOFFGroupConstructed(COFFGroup coffGroup)
        => this._coffGroupsConstructedEver!.Add(coffGroup);
    public void RecordCompilandConstructed(Compiland compiland, uint symIndexId)
    {
        this._compilandsConstructedEver!.Add(compiland);
        this._compilandsBySymIndexId!.Add(symIndexId, compiland);
    }
    public void RecordAdditionalSymIndexIdForCompiland(Compiland compiland, uint additionalSymIndexId)
    {
        compiland.AddSymIndexId(additionalSymIndexId);
        this._compilandsBySymIndexId!.TryAdd(additionalSymIndexId, compiland);
    }
    public void RecordSourceFileConstructed(SourceFile sourceFile)
    {
        this._sourceFilesConstructedEver!.Add(sourceFile);
        this._sourceFilesByFilename!.Add(sourceFile.Name, sourceFile);
    }

    internal List<BinarySection>? AllBinarySections { get; set; }
    internal List<COFFGroup>? AllCOFFGroups { get; set; }
    internal HashSet<Compiland>? AllCompilands { get; set; }
    internal List<SourceFile>? AllSourceFiles { get; set; }
    internal HashSet<Library>? AllLibs { get; set; }
    internal List<DuplicateDataItem>? AllDuplicateDataItems { get; set; }
    internal List<WastefulVirtualItem>? AllWastefulVirtualItems { get; set; }
    internal List<TemplateFoldabilityItem>? AllTemplateFoldabilityItems { get; set; }
    internal List<AnnotationSymbol>? AllAnnotations { get; set; }

    internal SortedList<uint, NameCanonicalization>? AllCanonicalNames { get; set; }

    #region Symbols of specific types, and the big cache with all symbols

    public Dictionary<uint, TypeSymbol> AllTypesBySymIndexId { get; } = new Dictionary<uint, TypeSymbol>(capacity: 1_000);
    public Dictionary<uint, AnnotationSymbol> AllAnnotationsBySymIndexId { get; } = new Dictionary<uint, AnnotationSymbol>();
    public Dictionary<uint, ISymbol> AllSymbolsBySymIndexId { get; } = new Dictionary<uint, ISymbol>(capacity: 10_000);
    public Dictionary<uint, MemberDataSymbol> AllMemberDataSymbolsBySymIndexId { get; } = new Dictionary<uint, MemberDataSymbol>(capacity: 1_000);
    public Dictionary<uint, ParameterDataSymbol> AllParameterDataSymbolsbySymIndexId { get; } = new Dictionary<uint, ParameterDataSymbol>(capacity: 1_000);
    public Dictionary<uint, IFunctionCodeSymbol> AllFunctionSymbolsBySymIndexIdOfPrimaryBlock { get; } = new Dictionary<uint, IFunctionCodeSymbol>(capacity: 1_000);
    public Dictionary<uint, InlineSiteSymbol> AllInlineSiteSymbolsBySymIndexId { get; } = new Dictionary<uint, InlineSiteSymbol>(capacity: 100);

    internal UserDefinedTypeSymbol[]? AllUserDefinedTypes { get; set; }
    internal List<UserDefinedTypeGrouping>? AllUserDefinedTypeGroupings { get; set; }

    internal SortedList<uint, List<string>>? AllDisambiguatingVTablePublicSymbolNamesByRVA { get; set; }

    #endregion

    #region Special kinds of ranges that DIA can't deal with - PDATA, XDATA, and RSRC

    internal bool PDataHasBeenInitialized { get; set; }
    public RVARange PDataRVARange { get; internal set; } = new RVARange(0, 0);

    //TODO: WastefulVirtual: consider replacing these with an Array that's sorted by RVA which may be even faster.
    public SortedList<uint, PDataSymbol> PDataSymbolsByRVA { get; internal set; } = new SortedList<uint, PDataSymbol>();

    internal bool XDataHasBeenInitialized { get; set; }
    public RVARangeSet XDataRVARanges { get; internal set; } = new RVARangeSet();
    public SortedList<uint, XDataSymbol> XDataSymbolsByRVA { get; internal set; } = new SortedList<uint, XDataSymbol>();

    // TODO: determine if there's a way to use this in source file attribution?  The source files exist (with ".res" extension) and section contribs do...but unclear if this can be correlated
    internal bool RsrcHasBeenInitialized { get; set; }
    public RVARange RsrcRVARange { get; internal set; } = new RVARange(0, 0);
    public SortedList<uint, RsrcSymbolBase> RsrcSymbolsByRVA { get; internal set; } = new SortedList<uint, RsrcSymbolBase>();

    internal bool OtherPESymbolsHaveBeenInitialized { get; set; }
    public SortedList<uint, ISymbol> OtherPESymbolsByRVA { get; internal set; } = new SortedList<uint, ISymbol>();
    public RVARangeSet OtherPESymbolsRVARanges { get; internal set; } = new RVARangeSet();

    #endregion

    #region Pre-Processed Symbol Info

    private List<(uint rva, List<uint> symIndices)>? _allSymIndexIDsByRVA;
    private HashSet<uint>? _rvasOfLabelSymbols;

    public bool LabelExistsAtRVA(uint rva) => this._rvasOfLabelSymbols?.Contains(rva) ?? false;

    internal void InitializeRVARanges(
        Dictionary<uint, List<uint>> symIndexIDsByRVA,
        HashSet<uint> rvasOfLabelSymbols)
    {
        this._rvasOfLabelSymbols = rvasOfLabelSymbols;

        if (symIndexIDsByRVA.Count == 0)
        {
            // We'll leave it at null internally since we have nothing to find.
            return;
        }

        this._allSymIndexIDsByRVA = new List<(uint, List<uint>)>(symIndexIDsByRVA.Count);
        foreach (var kvp in symIndexIDsByRVA.OrderBy(x => x.Key))
        {
            this._allSymIndexIDsByRVA!.Add((kvp.Key, kvp.Value));
        }
    }

    internal bool TryFindSymIndicesInRVARange(RVARange range, [NotNullWhen(true)] out List<(uint rva, List<uint> symIndices)>? symIndicesByRVA, out int minIdx, out int maxIdx)
    {
        // If we have nothing or the very first RVA is beyond the end of the range we're looking for, then we've found nothing.
        if (this._allSymIndexIDsByRVA is null || this._allSymIndexIDsByRVA[0].rva > range.RVAEnd)
        {
            symIndicesByRVA = null;
            minIdx = maxIdx = 0;
            return false;
        }

        var minIdxFound = false;
        minIdx = 0;
        maxIdx = this._allSymIndexIDsByRVA.Count - 1;

        // This is a linear walk, but we could probably make it even faster by using a binary search.
        for (var i = 0; i < this._allSymIndexIDsByRVA.Count; i++)
        {
            var r = this._allSymIndexIDsByRVA[i];
            if (!minIdxFound && range.Contains(r.rva))
            {
                minIdx = i;
                minIdxFound = true;
            }

            if (r.rva > range.RVAEnd)
            {
                maxIdx = i - 1;
                break;
            }
        }

        // We may find that our range is between two RVAs - if so, we found nothing.
        if (!minIdxFound || (minIdx == maxIdx && !range.Contains(this._allSymIndexIDsByRVA[minIdx].rva)))
        {
            symIndicesByRVA = null;
            minIdx = maxIdx = 0;
            return false;
        }

        System.Diagnostics.Debug.Assert(!minIdxFound || minIdx == 0 || this._allSymIndexIDsByRVA[minIdx - 1].rva <= range.RVAStart);
        System.Diagnostics.Debug.Assert(this._allSymIndexIDsByRVA[minIdx].rva >= range.RVAStart);
        System.Diagnostics.Debug.Assert(this._allSymIndexIDsByRVA[maxIdx].rva <= range.RVAEnd);
        System.Diagnostics.Debug.Assert(maxIdx == this._allSymIndexIDsByRVA.Count - 1 || this._allSymIndexIDsByRVA[maxIdx + 1].rva > range.RVAEnd);

        symIndicesByRVA = this._allSymIndexIDsByRVA;
        return true;
    }

    #endregion

    internal RVARangeSet? RVARangesThatAreOnlyVirtualSize { get; set; }

    internal SessionDataCache(SymbolSourcesSupported symbolSourcesSupported = SymbolSourcesSupported.All)
    {
        this.SymbolSourcesSupported = symbolSourcesSupported;
    }

    #region IDisposable Support

    private bool IsDisposed; // To detect redundant calls

    private void Dispose(bool _)
    {
        if (!this.IsDisposed)
        {
            // Set large fields to null.
            this._binarySectionsConstructedEver = null;
            this._coffGroupsConstructedEver = null;
            this._compilandsConstructedEver = null;
            this._compilandsBySymIndexId = null;
            this._sourceFilesConstructedEver = null;
            this._sourceFilesByFilename = null;
            this._allSymIndexIDsByRVA = null;
            this._rvasOfLabelSymbols = null;

            this.AllBinarySections = null;
            this.AllCOFFGroups = null;
            this.AllCompilands = null;
            this.AllSourceFiles = null;
            this.AllLibs = null;
            this.AllDuplicateDataItems = null;
            this.AllWastefulVirtualItems = null;
            this.AllTemplateFoldabilityItems = null;
            this.AllAnnotations = null;
            this.AllCanonicalNames = null;

            this.AllTypesBySymIndexId.Clear();
            this.AllAnnotationsBySymIndexId.Clear();
            this.AllSymbolsBySymIndexId.Clear();
            this.AllMemberDataSymbolsBySymIndexId.Clear();
            this.AllParameterDataSymbolsbySymIndexId.Clear();
            this.AllFunctionSymbolsBySymIndexIdOfPrimaryBlock.Clear();
            this.AllUserDefinedTypes = null;
            this.AllUserDefinedTypeGroupings = null;
            this.AllDisambiguatingVTablePublicSymbolNamesByRVA = null;

            this.PDataRVARange = new RVARange(0, 0);
            this.PDataSymbolsByRVA.Clear();
            this.PDataHasBeenInitialized = false;
            this.XDataRVARanges = new RVARangeSet();
            this.XDataSymbolsByRVA.Clear();
            this.XDataHasBeenInitialized = false;
            this.RsrcRVARange = new RVARange(0, 0);
            this.RsrcSymbolsByRVA.Clear();
            this.RsrcHasBeenInitialized = false;
            this.OtherPESymbolsRVARanges = new RVARangeSet();
            this.OtherPESymbolsByRVA.Clear();
            this.OtherPESymbolsHaveBeenInitialized = false;

            this.IsDisposed = true;
        }
    }

    ~SessionDataCache()
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
