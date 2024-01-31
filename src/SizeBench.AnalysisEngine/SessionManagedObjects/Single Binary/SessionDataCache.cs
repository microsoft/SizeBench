using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

// Looking stuff up can be a pretty slow operation, each Session maintains a cache of things
// it has already looked up.
internal sealed class SessionDataCache : IDisposable
{
    public uint BytesPerWord { get; set; }

    internal Linker LinkerDetected { get; set; } = Linker.Unknown;

    private List<BinarySection>? _binarySectionsConstructedEver = new List<BinarySection>();
    public IReadOnlyList<BinarySection> BinarySectionsConstructedEver
        => this._binarySectionsConstructedEver ?? new List<BinarySection>();

    private List<COFFGroup>? _coffGroupsConstructedEver = new List<COFFGroup>();
    public IReadOnlyList<COFFGroup> COFFGroupsConstrutedEver
        => this._coffGroupsConstructedEver ?? new List<COFFGroup>();

    public List<Compiland>? _compilandsConstructedEver = new List<Compiland>(capacity: 100);
    private Dictionary<uint, Compiland>? _compilandsBySymIndexId = new Dictionary<uint, Compiland>(capacity: 100);
    public IReadOnlyList<Compiland> CompilandsConstructedEver => this._compilandsConstructedEver ?? new List<Compiland>();
    public Compiland? FindCompilandBySymIndexId(uint symIndexId)
    {
        if (this._compilandsBySymIndexId != null &&
            this._compilandsBySymIndexId.TryGetValue(symIndexId, out var compiland) == true)
        {
            return compiland;
        }
        else
        {
            return null;
        }
    }

    private Dictionary<string, SourceFile>? _sourceFilesByFilename = new Dictionary<string, SourceFile>();
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

    public void RecordBinarySectionConstructed(BinarySection section)
        => this._binarySectionsConstructedEver!.Add(section);
    public void RecordCOFFGroupConstructed(COFFGroup coffGroup)
        => this._coffGroupsConstructedEver!.Add(coffGroup);
    public void RecordCompilandConstructed(Compiland compiland)
    {
        this._compilandsConstructedEver!.Add(compiland);
        this._compilandsBySymIndexId!.Add(compiland.SymIndexId, compiland);
    }
    public void RecordSourceFileConstructed(SourceFile sourceFile)
    {
        this._sourceFilesConstructedEver!.Add(sourceFile);
        this._sourceFilesByFilename!.Add(sourceFile.Name, sourceFile);
    }

    internal List<BinarySection>? AllBinarySections { get; set; }
    internal List<COFFGroup>? AllCOFFGroups { get; set; }
    internal List<Compiland>? AllCompilands { get; set; }
    internal List<SourceFile>? AllSourceFiles { get; set; }
    internal List<Library>? AllLibs { get; set; }
    internal List<DuplicateDataItem>? AllDuplicateDataItems { get; set; }
    internal List<WastefulVirtualItem>? AllWastefulVirtualItems { get; set; }
    internal List<TemplateFoldabilityItem>? AllTemplateFoldabilityItems { get; set; }
    internal List<AnnotationSymbol>? AllAnnotations { get; set; }

    internal SortedList<uint, NameCanonicalization>? AllCanonicalNames { get; set; }

    #region Symbols of specific types, and the big cache with all symbols

    public SortedList<uint, TypeSymbol> AllTypesBySymIndexId { get; } = new SortedList<uint, TypeSymbol>(capacity: 1000);
    public SortedList<uint, AnnotationSymbol> AllAnnotationsBySymIndexId { get; } = new SortedList<uint, AnnotationSymbol>();
    public SortedList<uint, ISymbol> AllSymbolsBySymIndexId { get; } = new SortedList<uint, ISymbol>(capacity: 10000);
    public SortedList<uint, MemberDataSymbol> AllMemberDataSymbolsBySymIndexId { get; } = new SortedList<uint, MemberDataSymbol>(capacity: 1000);
    public SortedList<uint, ParameterDataSymbol> AllParameterDataSymbolsbySymIndexId { get; } = new SortedList<uint, ParameterDataSymbol>(capacity: 1000);
    public SortedList<uint, IFunctionCodeSymbol> AllFunctionSymbolsBySymIndexIdOfPrimaryBlock { get; } = new SortedList<uint, IFunctionCodeSymbol>(capacity: 1000);

    internal UserDefinedTypeSymbol[]? AllUserDefinedTypes { get; set; }
    internal List<UserDefinedTypeGrouping>? AllUserDefinedTypeGroupings { get; set; }

    internal SortedList<uint, List<string>>? AllDisambiguatingVTablePublicSymbolNamesByRVA { get; set; }

    #endregion

    #region Special kinds of ranges that DIA can't deal with - PDATA, XDATA, and RSRC

    public RVARange? PDataRVARange { get; internal set; }

    //TODO: WastefulVirtual: consider replacing these with an Array that's sorted by RVA which may be even faster.
    public SortedList<uint, PDataSymbol>? PDataSymbolsByRVA { get; internal set; }

    public RVARangeSet? XDataRVARanges { get; internal set; }
    public SortedList<uint, XDataSymbol>? XDataSymbolsByRVA { get; internal set; }

    // TODO: determine if there's a way to use this in source file attribution?  The source files exist (with ".res" extension) and section contribs do...but unclear if this can be correlated
    public RVARange? RsrcRVARange { get; internal set; }
    public SortedList<uint, RsrcSymbolBase>? RsrcSymbolsByRVA { get; internal set; }

    public SortedList<uint, ISymbol>? OtherPESymbolsByRVA { get; internal set; }
    public RVARangeSet? OtherPESymbolsRVARanges { get; internal set; }

    #endregion

    internal RVARangeSet? RVARangesThatAreOnlyVirtualSize { get; set; }

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

            this.PDataRVARange = null;
            this.PDataSymbolsByRVA = null;
            this.XDataRVARanges = null;
            this.XDataSymbolsByRVA = null;
            this.RsrcRVARange = null;
            this.RsrcSymbolsByRVA = null;
            this.OtherPESymbolsRVARanges = null;
            this.OtherPESymbolsByRVA = null;

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
