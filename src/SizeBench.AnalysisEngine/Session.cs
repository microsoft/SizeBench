using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using SizeBench.AnalysisEngine.DebuggerInterop;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Helpers;
using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.SessionTasks;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.Threading.Tasks.Schedulers;

namespace SizeBench.AnalysisEngine;

public sealed class Session : ISession
{
    private readonly string _originalPDBPathMayBeRemote;
    private GuaranteedLocalFile? _guaranteedLocalPDBFile;
    public string PdbPath => this._guaranteedLocalPDBFile?.OriginalPath ?? "No pdb opened yet";

    private readonly string _originalBinaryPathMayBeRemote;
    public string BinaryPath => this.PEFile?.GuaranteedLocalCopyOfBinary.OriginalPath ?? "No binary opened yet";

    public byte BytesPerWord => this.PEFile!.BytesPerWord;

    private readonly ILogger _logger;
    internal SessionDataCache DataCache { get; } = new SessionDataCache();

    internal PEFile? PEFile { get; private set; }

    #region Progress Reporting

    private int _tasksQueued;

    private void IncrementTasksInBusyQueue()
    {
        Interlocked.Increment(ref this._tasksQueued);
        FirePropertyChanged(nameof(this.IsBusy));
    }

    private void DecrementTasksInBusyQueue()
    {
        Interlocked.Decrement(ref this._tasksQueued);
        FirePropertyChanged(nameof(this.IsBusy));
    }

    public bool IsBusy => this._tasksQueued > 0;

    public IProgress<SessionTaskProgress>? ProgressReporter { get; set; }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void FirePropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Debugger Interop

    private IDebuggerAdapter? _debuggerAdapter;

    private async Task EnsureDebuggerAdapter(CancellationToken token)
    {
        if (this._debuggerAdapter != null)
        {
            return;
        }

        const string logEntry = "Initializing debugging engine";
        using var taskLog = this._logger.StartTaskLog(logEntry);
        this.ProgressReporter?.Report(new SessionTaskProgress(logEntry, 0, null));
        this._debuggerAdapter = new DebuggerAdapter();
        //TODO: TemplateFoldability: this is loading the binary and PDB from their original locations, which may be over the network and thus quite slow.
        //                           The problem is we can't just switch to the GuaranteedLocalPath for the binary and PDB, since they have DeleteOnClose
        //                           streams open which prevent the debugger from being able to load them apparently...
        await this._debuggerAdapter.OpenAsync(this.BinaryPath, this.PdbPath, this.PEFile!.MachineType, token, taskLog).ConfigureAwait(true);
    }

    #endregion

    #region Background Thread stuff

    // Because DIA is thread-affinitive, we'll need to do all our parsing and analysis on our own thread.
    // The QueuedTaskScheduler will be initialized to have only one thread to appease DIA.
    private readonly QueuedTaskScheduler _taskScheduler;
    private readonly TaskFactory _taskFactory;
    private int _diaManagedThreadId;
    private DIAAdapter? _diaAdapter;

    #endregion

    #region Create and Open Session

    public static async Task<Session> Create(string binaryPath, string pdbPath, ILogger sessionLogger)
    {
        var s = new Session(binaryPath, pdbPath, sessionLogger);

        try
        {
            await s.Open().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Disposing of this here will ensure we clean up any GuaranteedLocalFile copies we did, even if we fail to open the session, and
            // that this will happen promptly instead of whenever a GC happens or the process exits.
            await s.DisposeAsync().ConfigureAwait(true);
            throw;
        }

        return s;
    }

    private Task Open()
    {
        ThrowIfDisposingOrDisposed();
        return this._taskFactory.StartNew(InitializeDIAThread);
    }

    internal Session(string binaryPath, string pdbPath, ILogger sessionLogger)
    {
        this._logger = sessionLogger;

        Debug.Assert(File.Exists(pdbPath));
        this._originalPDBPathMayBeRemote = pdbPath;
        Debug.Assert(File.Exists(binaryPath));
        this._originalBinaryPathMayBeRemote = binaryPath;

        this._taskScheduler = new QueuedTaskScheduler(threadCount: 1, threadApartmentState: ApartmentState.STA);
        this._taskFactory = new TaskFactory(this._taskScheduler);
    }

    private void InitializeDIAThread()
    {
        //TODO: We should take in the ProgressReporter when calling Create on the Session, so we can report progress *during*
        //      the Open/InitializeDIAThread.  Then the UI would not set the ProgressReporter late, it would be during
        //      startup.
        //      We should also use that opportunity to plumb through a CancellationToken so we can cancel opening a binary?

        using var initializeDiaThreadLog = this._logger.StartTaskLog("Setting up initial data needed to open the session");
        this._diaManagedThreadId = Environment.CurrentManagedThreadId;

        this.ProgressReporter?.Report(new SessionTaskProgress("Copying PDB file locally if necessary.", 0, null));
        this._guaranteedLocalPDBFile = new GuaranteedLocalFile(this._originalPDBPathMayBeRemote, initializeDiaThreadLog);

        this._diaAdapter = new DIAAdapter(this, this._guaranteedLocalPDBFile.GuaranteedLocalPath);
        this._taskParameters = new SessionTaskParameters(this, this._diaAdapter, this.DataCache);

        this.PEFile = new PEFile(this._originalBinaryPathMayBeRemote, initializeDiaThreadLog);
        this.DataCache.BytesPerWord = this.PEFile.BytesPerWord;
        this.DataCache.RsrcRVARange = this.PEFile.RsrcRange;

        this._diaAdapter.Initialize(this.PEFile, initializeDiaThreadLog);
    }

    #endregion

    public async Task<ISymbol?> LoadSymbolForVTableSlotAsync(uint vtableRVA, uint slotIndex)
    {
        var vtableTargetRva = EHSymbolTable.GetAdjustedRva(this.PEFile!.LoadUInt32ByRVAThatIsPreferredBaseRelative(vtableRVA + (this.BytesPerWord * slotIndex)), this.PEFile.MachineType);
        return await LoadSymbolByRVA(vtableTargetRva).ConfigureAwait(true);
    }

    public bool CompareData(long RVA1, long RVA2, uint length)
        => this.PEFile!.CompareData(RVA1, RVA2, length);

    public float CompareSimilarityOfCodeBytesInBinary(IFunctionCodeSymbol firstSymbol, IFunctionCodeSymbol secondSymbol)
    {
        ArgumentNullException.ThrowIfNull(firstSymbol);
        ArgumentNullException.ThrowIfNull(secondSymbol);

        var firstRanges = new List<RVARange>();
        var secondRanges = new List<RVARange>();

        foreach (var block in firstSymbol.Blocks)
        {
            firstRanges.Add(new RVARange(block.RVA, block.RVAEnd));
        }

        foreach (var block in secondSymbol.Blocks)
        {
            secondRanges.Add(new RVARange(block.RVA, block.RVAEnd));
        }

        if (firstRanges.Count != secondRanges.Count)
        {
            return 0.0f;
        }

        return this.PEFile!.CompareSimilarityOfBytesInBinary(firstRanges, secondRanges);
    }

    #region Debug Helpers

    // Things in this region are meant to help when debugging SizeBench, since the debugger can't do LINQ queries so it is hard
    // to 'look around' sometimes.
    [ExcludeFromCodeCoverage] // No need to test things only meant to be used in the debugger
    internal Compiland? FindCompilandThatContributesRVA(uint RVA)
    {
        return this.DataCache.AllCompilands!.SingleOrDefault(c => c.SectionContributions
                                                                   .Values
                                                                   .Any(csc => csc.RVARanges
                                                                                  .Any(range => range.Contains(RVA))));
    }

    #endregion

    #region IAsyncDisposable Support

    private void ThrowIfDisposingOrDisposed()
    {
        if (this.IsDisposing || this.IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    // IsDisposing is set to true when we begin disposal, but once we begin we have to wait for the background
    // DIA thread to finish whatever it's doing before we can finish disposing, so IsDisposed is the way we
    // keep track of whether we've really finished disposing.
    private bool IsDisposed;
    private bool IsDisposing;

    private async ValueTask DisposeAsync(bool disposing)
    {
        if (this.IsDisposing || this.IsDisposed)
        {
            return;
        }

        this.IsDisposing = true;

        this._taskScheduler?.Dispose();

        this.PEFile?.Dispose();
        this.PEFile = null;

        this.DataCache.Dispose();

        if (this._debuggerAdapter != null)
        {
            await this._debuggerAdapter.DisposeAsync().ConfigureAwait(true);
        }

        if (disposing)
        {
            this._diaAdapter?.Dispose();
        }

        this._guaranteedLocalPDBFile?.Dispose();

        this.IsDisposing = false;
        this.IsDisposed = true;
    }

    // This code added to correctly implement the disposable pattern.
    public ValueTask DisposeAsync() =>
        // Do not change this code. Put cleanup code in DisposeAsync(bool disposing) above.
        DisposeAsync(true);

    #endregion IAsyncDisposable Support

    #region Enumerate Binary Sections and COFF Groups

    public Task<IReadOnlyList<BinarySection>> EnumerateBinarySectionsAndCOFFGroups(CancellationToken token)
        => EnumerateBinarySectionsAndCOFFGroups(token, null);

    public async Task<IReadOnlyList<BinarySection>> EnumerateBinarySectionsAndCOFFGroups(CancellationToken token, ILogger? parentLogger)
    {
        if (this.DataCache.AllBinarySections is null)
        {
            var task = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this._taskParameters!, token);
            this.DataCache.AllBinarySections = await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
        }

        return this.DataCache.AllBinarySections;
    }

    public async Task<BinarySection?> LoadBinarySectionByName(string name,
                                                              CancellationToken token)
    {
        await EnumerateBinarySectionsAndCOFFGroups(token).ConfigureAwait(true);

        return this.DataCache.AllBinarySections!.FirstOrDefault(bs => bs.Name == name);
    }

    #endregion

    #region Enumerate Symbols in Compiland

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCompiland(Compiland compiland, CancellationToken token)
        => EnumerateSymbolsInCompiland(compiland, new SymbolEnumerationOptions(), token, null);

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCompiland(Compiland compiland, SymbolEnumerationOptions options, CancellationToken token)
        => EnumerateSymbolsInCompiland(compiland, options, token, null);

    public async Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCompiland(Compiland compiland, SymbolEnumerationOptions options, CancellationToken token, ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(compiland);

        var task = new EnumerateSymbolsInCompilandSessionTask(this._taskParameters!,
                                                              token,
                                                              this.ProgressReporter,
                                                              compiland,
                                                              options);
        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    #endregion

    #region Enumerate Symbols in COFF Group

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCOFFGroup(COFFGroup coffGroup, CancellationToken token)
        => EnumerateSymbolsInCOFFGroup(coffGroup, token, null);

    public async Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCOFFGroup(COFFGroup coffGroup, CancellationToken token, ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(coffGroup);

        var task = new EnumerateSymbolsInCOFFGroupSessionTask(this._taskParameters!,
                                                              token,
                                                              this.ProgressReporter,
                                                              coffGroup);
        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    #endregion

    #region Enumerate Symbols in Binary Section

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInBinarySection(BinarySection section, CancellationToken token)
        => EnumerateSymbolsInBinarySection(section, token, null);

    public async Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInBinarySection(BinarySection section, CancellationToken token, ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(section);

        var task = new EnumerateSymbolsInBinarySectionSessionTask(this._taskParameters!,
                                                                  token,
                                                                  this.ProgressReporter,
                                                                  section);
        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    #endregion

    #region Enumerate Symbols in Lib

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInLib(Library library, CancellationToken token)
        => EnumerateSymbolsInLib(library, token, null);

    public async Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInLib(Library library, CancellationToken token, ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(library);

        var task = new EnumerateSymbolsInLibSessionTask(this._taskParameters!,
                                                        token,
                                                        this.ProgressReporter,
                                                        library);

        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    #endregion

    #region Enumerate Symbols in Source File

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInSourceFile(SourceFile sourceFile, CancellationToken token)
        => EnumerateSymbolsInSourceFile(sourceFile, token, null);

    public async Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInSourceFile(SourceFile sourceFile, CancellationToken token, ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        var task = new EnumerateSymbolsInSourceFileSessionTask(this._taskParameters!,
                                                               token,
                                                               this.ProgressReporter,
                                                               sourceFile);
        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    #endregion

    #region Enumerate Libs

    public Task<IReadOnlyList<Library>> EnumerateLibs(CancellationToken token)
        => EnumerateLibs(token, null);

    public async Task<IReadOnlyList<Library>> EnumerateLibs(CancellationToken token, ILogger? parentLogger)
    {
        if (this.DataCache.AllLibs is null)
        {
            var task = new EnumerateLibsAndCompilandsSessionTask(this._taskParameters!,
                                                                 token,
                                                                 this.ProgressReporter);
            this.DataCache.AllLibs = await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
        }

        return this.DataCache.AllLibs;
    }

    #endregion

    #region Enumerate Compilands

    public async Task<IReadOnlyList<Compiland>> EnumerateCompilands(CancellationToken token)
    {
        if (this.DataCache.AllCompilands is null)
        {
            var task = new EnumerateLibsAndCompilandsSessionTask(this._taskParameters!,
                                                                 token,
                                                                 this.ProgressReporter);
            //TODO: this has side effects on SessionDataCache that are weird.  We should follow the
            //      pattern of assigning to AllLibs and AllCompilands if a clean calling pattern 
            //      can be determined.
            await PerformSessionTaskOnDIAThread(task, token).ConfigureAwait(true);
        }

        return this.DataCache.AllCompilands!;
    }

    #endregion

    #region Enumerate Source Files

    public async Task<IReadOnlyList<SourceFile>> EnumerateSourceFiles(CancellationToken token)
    {
        if (this.DataCache.AllSourceFiles is null)
        {
            var task = new EnumerateSourceFilesSessionTask(this._taskParameters!,
                                                           token,
                                                           this.ProgressReporter);
            //TODO: this has side effects on SessionDataCache that are weird.  We should follow the
            //      pattern of assigning to AllLibs and AllCompilands if a clean calling pattern 
            //      can be determined.
            await PerformSessionTaskOnDIAThread(task, token).ConfigureAwait(true);
        }

        return this.DataCache.AllSourceFiles!;
    }

    #endregion

    #region Enumerate Symbols A Lib Contributes To a Section or a COFF Group

    public Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInContribution(Contribution contribution, CancellationToken token)
        => EnumerateSymbolsInContribution(contribution, token, null);

    public async Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInContribution(Contribution contribution, CancellationToken token, ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        var task = new EnumerateSymbolsInContributionSessionTask(this._taskParameters!,
                                                                 token,
                                                                 this.ProgressReporter,
                                                                 contribution);

        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    #endregion

    #region Lookup a symbol's placement in the binary

    public Task<SymbolPlacement> LookupSymbolPlacementInBinary(ISymbol symbol,
                                                               CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var task = new LookupSymbolPlacementInBinarySessionTask(symbol,
                                                                this._taskParameters!,
                                                                token,
                                                                this.ProgressReporter);

        return PerformSessionTaskOnDIAThread(task, token);
    }

    #endregion

    #region Load single symbol by RVA, or all the symbols folded to an RVA

    public Task<ISymbol?> LoadSymbolByRVA(uint rva)
        => LoadSymbolByRVA(rva, CancellationToken.None, null);

    public Task<ISymbol?> LoadSymbolByRVA(uint rva, CancellationToken token, ILogger? parentLogger)
    {
        var task = new LoadSymbolByRVASessionTask(this._taskParameters!,
            rva,
            this.ProgressReporter,
            token);

        // Don't report progress on this task, it can frequently be called thousands of times so it's spammy in the logs and makes them so large they can't be managed.
        return PerformSessionTaskOnDIAThreadWhichMayReturnNull(task, token, parentLogger, shouldReportProgress: false);
    }

    public async Task<IReadOnlyList<ISymbol>> EnumerateAllSymbolsFoldedAtRVA(uint rva, CancellationToken token)
    {
        var task = new EnumerateAllSymbolsFoldedAtRVASessionTask(this._taskParameters!,
                                                                 rva,
                                                                 this.ProgressReporter,
                                                                 token);

        return await PerformSessionTaskOnDIAThread(task, token, null).ConfigureAwait(true);
    }

    #endregion

    #region Duplicate Data

    public Task<IReadOnlyList<DuplicateDataItem>> EnumerateDuplicateDataItems(CancellationToken token)
        => EnumerateDuplicateDataItems(token, null);

    public async Task<IReadOnlyList<DuplicateDataItem>> EnumerateDuplicateDataItems(CancellationToken token, ILogger? parentLogger)
    {
        if (this.DataCache.AllDuplicateDataItems is null)
        {
            var task = new EnumerateDuplicateDataSessionTask(this._taskParameters!,
                                                             token,
                                                             this.ProgressReporter);

            this.DataCache.AllDuplicateDataItems = await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
        }

        return this.DataCache.AllDuplicateDataItems;
    }

    #endregion

    #region Wasteful Virtuals

    public Task<IReadOnlyList<WastefulVirtualItem>> EnumerateWastefulVirtuals(CancellationToken token)
        => EnumerateWastefulVirtuals(token, null);

    public async Task<IReadOnlyList<WastefulVirtualItem>> EnumerateWastefulVirtuals(CancellationToken token, ILogger? parentLogger)
    {
        if (this.DataCache.AllWastefulVirtualItems is null)
        {
            var task = new EnumerateWastefulVirtualsSessionTask(this._taskParameters!,
                                                                token,
                                                                this.ProgressReporter);

            this.DataCache.AllWastefulVirtualItems = await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
        }

        return this.DataCache.AllWastefulVirtualItems;
    }

    #endregion

    #region Template Foldability

    public Task<IReadOnlyList<TemplateFoldabilityItem>> EnumerateTemplateFoldabilityItems(CancellationToken token)
        => EnumerateTemplateFoldabilityItems(token, null);

    public async Task<IReadOnlyList<TemplateFoldabilityItem>> EnumerateTemplateFoldabilityItems(CancellationToken token, ILogger? parentLogger)
    {
        if (this.DataCache.AllTemplateFoldabilityItems is null)
        {
            var task = new EnumerateTemplateFoldabilitySessionTask(this._taskParameters!,
                this.ProgressReporter,
                token);

            this.DataCache.AllTemplateFoldabilityItems = await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
        }

        return this.DataCache.AllTemplateFoldabilityItems;
    }

    #endregion

    #region Type Layout

    public Task<IReadOnlyList<TypeLayoutItem>> LoadAllTypeLayouts(CancellationToken token)
        => LoadAllTypeLayouts(token, null);

    public async Task<IReadOnlyList<TypeLayoutItem>> LoadAllTypeLayouts(CancellationToken token, ILogger? parentLogger)
    {
        var task = new LoadTypeLayoutSessionTask(this._taskParameters!,
            null,
            null,
            0 /* baseOffset */,
            this.ProgressReporter,
            token);

        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    public Task<IReadOnlyList<TypeLayoutItem>> LoadTypeLayoutsByName(string typeName,
                                                            CancellationToken token)
        => LoadTypeLayoutsByName(typeName, token, null);

    public async Task<IReadOnlyList<TypeLayoutItem>> LoadTypeLayoutsByName(string typeName,
                                                                  CancellationToken token,
                                                                  ILogger? parentLogger)
    {
        var task = new LoadTypeLayoutSessionTask(this._taskParameters!,
            typeName,
            null,
            0 /* baseOffset */,
            this.ProgressReporter,
            token);

        return await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true);
    }

    public Task<TypeLayoutItem> LoadTypeLayout(TypeSymbol typeSymbol,
                                               CancellationToken token)
        => LoadTypeLayout(typeSymbol, token, null);

    public async Task<TypeLayoutItem> LoadTypeLayout(TypeSymbol typeSymbol,
                                                     CancellationToken token,
                                                     ILogger? parentLogger)
    {
        var task = new LoadTypeLayoutSessionTask(this._taskParameters!,
            null,
            typeSymbol,
            0 /* baseOffset */,
            this.ProgressReporter,
            token);

        return (await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true)).First();
    }

    public Task<TypeLayoutItem> LoadMemberTypeLayout(TypeLayoutItemMember member,
                                                     CancellationToken token)
        => LoadMemberTypeLayout(member, token, null);

    public async Task<TypeLayoutItem> LoadMemberTypeLayout(TypeLayoutItemMember member,
                                                           CancellationToken token,
                                                           ILogger? parentLogger)
    {
        ArgumentNullException.ThrowIfNull(member);

        // For now I'm taking a shortcut and casting member.Offset to a uint to reuse all the code that exists where
        // the baseOffset is from base classes (which will always be whole integers in size).  I don't know of any way
        // to end up here validly with a member at a bitfield offset, so for now just throw if that happens so this can
        // be properly coded and tested if it's important.
        if (member.Offset != Convert.ToUInt32(member.Offset))
        {
            throw new ArgumentOutOfRangeException(nameof(member), "member.Offset is not able to be represented as a uint - this is unexpected.  How did this happen?");
        }

        // When loading the layout of a member, if it's a pointer or an array, we'll "chase through" to find the UDT but we don't
        // want to use the member's offset as a base offset because it doesn't use up space in the class of the member that way.
        var effectiveMemberOffset = Convert.ToUInt32(member.Offset);
        if (member.Type is PointerTypeSymbol or ArrayTypeSymbol)
        {
            effectiveMemberOffset = 0;
        }

        var task = new LoadTypeLayoutSessionTask(this._taskParameters!,
            null,
            member.Type,
            effectiveMemberOffset,
            this.ProgressReporter,
            token);

        return (await PerformSessionTaskOnDIAThread(task, token, parentLogger).ConfigureAwait(true)).First();
    }

    #endregion

    #region User-Defined Types

    public async Task<IReadOnlyList<UserDefinedTypeSymbol>> EnumerateAllUserDefinedTypes(CancellationToken token)
    {
        var task = new EnumerateAllUserDefinedTypesSessionTask(this._taskParameters!,
                                                               token,
                                                               this.ProgressReporter);

        return await PerformSessionTaskOnDIAThread(task, token).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<UserDefinedTypeGrouping>> EnumerateAllUserDefinedTypeGroupings(CancellationToken token)
    {
        if (this.DataCache.AllUserDefinedTypeGroupings is null)
        {
            var udts = await EnumerateAllUserDefinedTypes(token).ConfigureAwait(true);

            var udtGroupings = new List<UserDefinedTypeGrouping>();

            foreach (var group in udts.GroupBy(udt => SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(udt)))
            {
                udtGroupings.Add(new UserDefinedTypeGrouping(group.Key, group.ToList()));
            }

            this.DataCache.AllUserDefinedTypeGroupings = udtGroupings;
        }

        return this.DataCache.AllUserDefinedTypeGroupings;
    }

    public async Task<IReadOnlyList<IFunctionCodeSymbol>> EnumerateFunctionsFromUserDefinedType(UserDefinedTypeSymbol udt, CancellationToken token)
    {
        var results = new List<IFunctionCodeSymbol>();
        await PerformWorkOnDIAThread(() => results = this._diaAdapter!.FindAllFunctionsWithinUDT(udt.SymIndexId, token).ToList(), token).ConfigureAwait(true);

        return results;
    }

    #endregion

    #region Disassemble a function

    //TODO: Consider making this a TryDisassembleFunction that returns false if the debugging engine
    //      fails to start.
    public async Task<string> DisassembleFunction(IFunctionCodeSymbol functionSymbol, DisassembleFunctionOptions options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(functionSymbol);

        try
        {
            await EnsureDebuggerAdapter(token).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Do not catch general exception types - returning an error here is better than crashing, the disassembly can't do any better.
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            return ex.GetFormattedTextForLogging("Unable to start debugger engine, disassembly could not be provided.Debugger exception: ", Environment.NewLine);
        }

        string disassembly;
        var logEntry = $"Disassembling {functionSymbol.FullName}";
        using (var taskLog = this._logger.StartTaskLog(logEntry))
        {
            this.ProgressReporter?.Report(new SessionTaskProgress(logEntry, 0, null));
            // Note - this doesn't need to be done on the DIA thread, so we don't go over there!
            disassembly = await this._debuggerAdapter!.DisassembleAsync(functionSymbol, options, taskLog, token).ConfigureAwait(true);
        }

        return disassembly;
    }

    #endregion

    #region Annotations

    public async Task<IReadOnlyList<AnnotationSymbol>> EnumerateAnnotations(CancellationToken token)
    {
        if (this.DataCache.AllAnnotations is null)
        {
            var task = new EnumerateAnnotationsSessionTask(this._taskParameters!,
                                                           token,
                                                           this.ProgressReporter);

            this.DataCache.AllAnnotations = await PerformSessionTaskOnDIAThread(task, token).ConfigureAwait(true);
        }

        return this.DataCache.AllAnnotations;
    }

    #endregion

    #region Getting back on the DIA thread to do work

    private SessionTaskParameters? _taskParameters;

    private async Task<T> PerformSessionTaskOnDIAThread<T>(SessionTask<T> task, CancellationToken token, ILogger? parentLogger = null)
        where T : class
    {
        T? results = default;
        await PerformWorkOnDIAThread(() => results = task.Execute(parentLogger ?? this._logger), token).ConfigureAwait(true);

#pragma warning disable CA1508 // Avoid dead conditional code - code analysis cannot see that "results" gets set by the lambda in PerformWorkOnDIAThread, so this is very much not dead code.
        if (results is null)
#pragma warning restore CA1508 // Avoid dead conditional code
        {
            throw new InvalidOperationException("SessionTask on DIA thread returned null results, this should be impossible.  The calling code is not resilient to null.");
        }

        return results;
    }

    private async Task<T?> PerformSessionTaskOnDIAThreadWhichMayReturnNull<T>(SessionTask<T?> task, CancellationToken token, ILogger? parentLogger = null, bool shouldReportProgress = true)
        where T : class
    {
        T? results = default;
        await PerformWorkOnDIAThread(() => results = task.Execute(parentLogger ?? this._logger, shouldReportProgress: shouldReportProgress), token).ConfigureAwait(true);

        return results;
    }

    private async Task PerformWorkOnDIAThread(Action action, CancellationToken token)
    {
        ThrowIfDisposingOrDisposed();

        if (Environment.CurrentManagedThreadId == this._diaManagedThreadId)
        {
            action();
            return;
        }
        else
        {
            IncrementTasksInBusyQueue();
            try
            {
                await this._taskFactory.StartNew(action, token).ConfigureAwait(true);

                token.ThrowIfCancellationRequested();

                return;
            }
            finally
            {
                DecrementTasksInBusyQueue();
            }
        }
    }

    #endregion
}
