using System.ComponentModel;
using System.Runtime.CompilerServices;
using SizeBench.AnalysisEngine.DiffSessionTasks;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.Threading.Tasks.Schedulers;

namespace SizeBench.AnalysisEngine;

public sealed class DiffSession : IDiffSession
{
    public ISession BeforeSession { get; }

    public ISession AfterSession { get; }

    private readonly ILogger _logger;
    private readonly DiffSessionDataCache _dataCache = new DiffSessionDataCache();

    public static async Task<DiffSession> Create(string beforeBinaryPath, string beforePdbPath,
                                                 string afterBinaryPath, string afterPdbPath,
                                                 ILogger sessionLogger)
    {
        var beforeOpenTask = Session.Create(beforeBinaryPath, beforePdbPath, sessionLogger);
        var afterOpenTask = Session.Create(afterBinaryPath, afterPdbPath, sessionLogger);

        var sessions = await Task.WhenAll(beforeOpenTask, afterOpenTask).ConfigureAwait(true);

        var diffSession = new DiffSession(sessions[0], sessions[1], sessionLogger);

        await diffSession.Open().ConfigureAwait(true);

        return diffSession;
    }

    private DiffSession(Session before, Session after, ILogger logger)
    {
        this.BeforeSession = before;
        this.BeforeSession.PropertyChanged += BeforeOrAfterSession_PropertyChanged;
        this.AfterSession = after;
        this.AfterSession.PropertyChanged += BeforeOrAfterSession_PropertyChanged;

        this._logger = logger;
        this._taskParameters = new DiffSessionTaskParameters(diffSession: this, dataCache: this._dataCache);

        this._taskScheduler = new QueuedTaskScheduler(threadCount: 1);
        this._taskFactory = new TaskFactory(this._taskScheduler);
    }

    private Task Open()
    {
        ThrowIfDisposingOrDisposed();
        return this._taskFactory.StartNew(SetupDiffThreadId);
    }

    private void SetupDiffThreadId()
        => this._diffManagedThreadId = Environment.CurrentManagedThreadId;

    private void BeforeOrAfterSession_PropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        // If either our Before or After session has changed that it's busy, we should re-evaluate ours.
        if (e.PropertyName == nameof(this.IsBusy))
        {
            FirePropertyChanged(nameof(this.IsBusy));
        }
    }

    #region Progress Reporting

    public bool IsBusy => this.BeforeSession.IsBusy || this.AfterSession.IsBusy;

    public IProgress<SessionTaskProgress>? ProgressReporter { get; set; }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void FirePropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Background Thread stuff

    // Because DIA is thread-affinitive, we'll need to do all our parsing and analysis on our own thread.
    // The QueuedTaskScheduler will be initialized to have only one thread to appease DIA.
    private readonly QueuedTaskScheduler _taskScheduler;
    private readonly TaskFactory _taskFactory;
    internal int _diffManagedThreadId;

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

        this._dataCache.Dispose();
        await this.BeforeSession.DisposeAsync().ConfigureAwait(true);
        await this.AfterSession.DisposeAsync().ConfigureAwait(true);
        this._taskScheduler.Dispose();
        this._logger.Dispose();

        // We need to let GC totally finish, otherwise we can have COM objects still hanging around
        // which will become invalid and weird when we release the module, causing watsons during
        // test runs and probably just freaking DIA out.
        if (disposing)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }

        this.IsDisposing = false;
        this.IsDisposed = true;
    }

    // This code added to correctly implement the disposable pattern.
    public ValueTask DisposeAsync() =>
        // Do not change this code. Put cleanup code in DisposeAsync(bool disposing) above.
        DisposeAsync(true);

    #endregion IAsyncDisposable Support

    #region Single Binary objects -> Diff objects

    public BinarySectionDiff? GetBinarySectionDiffFromBinarySection(BinarySection section)
    {
        if (this._dataCache.AllBinarySectionDiffs is null)
        {
            throw new InvalidOperationException("You must first enumerate all binary section diffs before calling this function.");
        }

        return this._dataCache.AllBinarySectionDiffs.SingleOrDefault(diff => diff.BeforeSection == section || diff.AfterSection == section);
    }

    public COFFGroupDiff? GetCOFFGroupDiffFromCOFFGroup(COFFGroup coffGroup)
    {
        if (this._dataCache.AllCOFFGroupDiffs is null)
        {
            throw new InvalidOperationException("You must first enumerate all COFFGroup diffs before calling this function.");
        }

        return this._dataCache.AllCOFFGroupDiffs.SingleOrDefault(diff => diff.BeforeCOFFGroup == coffGroup || diff.AfterCOFFGroup == coffGroup);
    }

    public LibDiff? GetLibraryDiffFromLibrary(Library library)
    {
        if (this._dataCache.AllLibDiffsInList is null)
        {
            throw new InvalidOperationException("You must first enumerate all lib diffs before calling this function.");
        }

        return this._dataCache.AllLibDiffsInList.SingleOrDefault(diff => diff.BeforeLib == library || diff.AfterLib == library);
    }

    public CompilandDiff? GetCompilandDiffFromCompiland(Compiland compiland)
    {
        if (this._dataCache.AllCompilandDiffs is null)
        {
            throw new InvalidOperationException("You must first enumerate all compiland diffs before calling this function.");
        }

        return this._dataCache.AllCompilandDiffs.SingleOrDefault(diff => diff.BeforeCompiland == compiland || diff.AfterCompiland == compiland);
    }

    public DuplicateDataItemDiff? GetDuplicateDataItemDiffFromDuplicateDataItem(DuplicateDataItem duplicateDataItem)
    {
        if (this._dataCache.AllDuplicateDataItemDiffs is null)
        {
            throw new InvalidOperationException("You must first enumerate all duplicate data item diffs before calling this function.");
        }

        return this._dataCache.AllDuplicateDataItemDiffs.SingleOrDefault(diff => diff.BeforeDuplicate == duplicateDataItem || diff.AfterDuplicate == duplicateDataItem);
    }

    public WastefulVirtualItemDiff? GetWastefulVirtualItemDiffFromWastefulVirtualItem(WastefulVirtualItem wastefulVirtualItem)
    {
        if (this._dataCache.AllWastefulVirtualItemDiffs is null)
        {
            throw new InvalidOperationException("You must first enumerate all wasteful virtual item diffs before calling this function.");
        }

        return this._dataCache.AllWastefulVirtualItemDiffs.SingleOrDefault(diff => diff.BeforeWastefulVirtual == wastefulVirtualItem || diff.AfterWastefulVirtual == wastefulVirtualItem);
    }

    public TemplateFoldabilityItemDiff? GetTemplateFoldabilityItemDiffFromTemplateFoldabilityItem(TemplateFoldabilityItem templateFoldabilityItem)
    {
        if (this._dataCache.AllTemplateFoldabilityItemDiffs is null)
        {
            throw new InvalidOperationException("You must first enumerate all template foldability item diffs before calling this function.");
        }

        return this._dataCache.AllTemplateFoldabilityItemDiffs.SingleOrDefault(diff => diff.BeforeTemplateFoldabilityItem == templateFoldabilityItem || diff.AfterTemplateFoldabilityItem == templateFoldabilityItem);
    }

    public SymbolDiff? GetSymbolDiffFromSymbol(ISymbol symbol)
    {
        this._dataCache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.TryGetValue(symbol, out var found);
        return found;
    }

    #endregion

    #region Enumerate Binary Sections and COFF Groups

    public async Task<IReadOnlyList<BinarySectionDiff>> EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken token)
    {
        if (this._dataCache.AllBinarySectionDiffs is null)
        {
            var task = new EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask(
                this._taskParameters,
                (logger) => this.BeforeSession.EnumerateBinarySectionsAndCOFFGroups(token, logger),
                (logger) => this.AfterSession.EnumerateBinarySectionsAndCOFFGroups(token, logger),
                token);

            this._dataCache.AllBinarySectionDiffs = await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
        }

        return this._dataCache.AllBinarySectionDiffs;
    }

    public async Task<BinarySectionDiff?> LoadBinarySectionDiffByName(string name,
                                                                      CancellationToken token)
    {
        var sectionDiffs = await EnumerateBinarySectionsAndCOFFGroupDiffs(token).ConfigureAwait(true);

        return sectionDiffs.FirstOrDefault(bsd => bsd.Name == name);
    }

    #endregion

    #region Enumerate Libs

    public async Task<IReadOnlyList<LibDiff>> EnumerateLibDiffs(CancellationToken token)
    {
        await (this._dataCache.AllLibDiffs ?? EnumerateLibsAndCompilandsAndUpdateCaches(token)).ConfigureAwait(true);

        return this._dataCache.AllLibDiffsInList!;
    }

    private async Task EnumerateLibsAndCompilandsAndUpdateCaches(CancellationToken token)
    {
        if (this._dataCache.AllLibDiffs is null)
        {
            var task = new EnumerateLibsAndCompilandDiffsSessionTask(
                            this._taskParameters,
                            (logger) => this.BeforeSession.EnumerateLibs(token, logger),
                            (logger) => this.AfterSession.EnumerateLibs(token, logger),
                            token,
                            this.ProgressReporter);

            this._dataCache.AllLibDiffs = PerformSessionDiffTaskOnBackgroundThread(task, token);
        }

        await this._dataCache.AllLibDiffs.ConfigureAwait(true);

        var compilandDiffs = (from libDiff in this._dataCache.AllLibDiffsInList!
                              select libDiff.CompilandDiffs.Values).SelectMany(compDiff => compDiff).ToList();

        this._dataCache.AllCompilandDiffs = compilandDiffs;
    }

    #endregion

    #region Enumerate Compilands

    public async Task<IReadOnlyList<CompilandDiff>> EnumerateCompilandDiffs(CancellationToken token)
    {
        if (this._dataCache.AllCompilandDiffs is null)
        {
            await EnumerateLibsAndCompilandsAndUpdateCaches(token).ConfigureAwait(true);
        }

        return this._dataCache.AllCompilandDiffs!;
    }

    #endregion

    #region Enumerating symbols in a Binary Section / COFF Group / Lib / etc.

    public async Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInBinarySectionDiff(BinarySectionDiff sectionDiff, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(sectionDiff);

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInSectionBeforeFunc(ILogger logger)
        {
            if (sectionDiff.BeforeSection is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns a IReadOnlyList<ISymbol>?, but we can return a non-null IReadOnlyList<ISymbol> here, so the guarantee is stronger, meaning this is safe.
                return this.BeforeSession.EnumerateSymbolsInBinarySection(sectionDiff.BeforeSection, token, logger);
#pragma warning restore CS8619
            }
        }

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInSectionAfterFunc(ILogger logger)
        {
            if (sectionDiff.AfterSection is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns a IReadOnlyList<ISymbol>?, but we can return a non-null IReadOnlyList<ISymbol> here, so the guarantee is stronger, meaning this is safe.
                return this.AfterSession.EnumerateSymbolsInBinarySection(sectionDiff.AfterSection, token, logger);
#pragma warning restore CS8619
            }
        }

        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._taskParameters,
            enumSymbolsInSectionBeforeFunc,
            enumSymbolsInSectionAfterFunc,
            $"Binary Section '{sectionDiff.Name}'",
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInLibDiff(LibDiff libDiff, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(libDiff);

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInLibBeforeFunc(ILogger logger)
        {
            if (libDiff.BeforeLib is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.BeforeSession.EnumerateSymbolsInLib(libDiff.BeforeLib, token, logger);
#pragma warning restore CS8619
            }
        }

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInLibAfterFunc(ILogger logger)
        {
            if (libDiff.AfterLib is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.AfterSession.EnumerateSymbolsInLib(libDiff.AfterLib, token, logger);
#pragma warning restore CS8619
            }
        }

        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._taskParameters,
            enumSymbolsInLibBeforeFunc,
            enumSymbolsInLibAfterFunc,
            $"Lib '{libDiff.ShortName}'",
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInCOFFGroupDiff(COFFGroupDiff coffGroupDiff, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(coffGroupDiff);

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInCOFFGroupBeforeFunc(ILogger logger)
        {
            if (coffGroupDiff.BeforeCOFFGroup is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.BeforeSession.EnumerateSymbolsInCOFFGroup(coffGroupDiff.BeforeCOFFGroup, token, logger);
#pragma warning restore CS8619
            }
        }

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInCOFFGroupAfterFunc(ILogger logger)
        {
            if (coffGroupDiff.AfterCOFFGroup is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.AfterSession.EnumerateSymbolsInCOFFGroup(coffGroupDiff.AfterCOFFGroup, token, logger);
#pragma warning restore CS8619
            }
        }

        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._taskParameters,
            enumSymbolsInCOFFGroupBeforeFunc,
            enumSymbolsInCOFFGroupAfterFunc,
            $"COFF Group '{coffGroupDiff.Name}'",
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInCompilandDiff(CompilandDiff compilandDiff, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(compilandDiff);

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInCompilandBeforeFunc(ILogger logger)
        {
            if (compilandDiff.BeforeCompiland is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.BeforeSession.EnumerateSymbolsInCompiland(compilandDiff.BeforeCompiland, new SymbolEnumerationOptions(), token, logger);
#pragma warning restore CS8619
            }
        }

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInCompilandAfterFunc(ILogger logger)
        {
            if (compilandDiff.AfterCompiland is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.AfterSession.EnumerateSymbolsInCompiland(compilandDiff.AfterCompiland, new SymbolEnumerationOptions(), token, logger);
#pragma warning restore CS8619
            }
        }

        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._taskParameters,
            enumSymbolsInCompilandBeforeFunc,
            enumSymbolsInCompilandAfterFunc,
            $"Compiland '{compilandDiff.ShortName}'",
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInContributionDiff(ContributionDiff contributionDiff,
                                                                                          CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(contributionDiff);

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInContribBeforeFunc(ILogger logger)
        {
            if (contributionDiff.BeforeContribution is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.BeforeSession.EnumerateSymbolsInContribution(contributionDiff.BeforeContribution, token, logger);
#pragma warning restore CS8619
            }
        }

        Task<IReadOnlyList<ISymbol>?> enumSymbolsInContribAfterFunc(ILogger logger)
        {
            if (contributionDiff.AfterContribution is null)
            {
                return Task.FromResult<IReadOnlyList<ISymbol>?>(null);
            }
            else
            {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  This function returns IReadOnlyList<ISymbol>?, but here we return a non-null IReadOnlyList<ISymbol>, which means we're being stricter and thus this is safe.
                return this.AfterSession.EnumerateSymbolsInContribution(contributionDiff.AfterContribution, token, logger);
#pragma warning restore CS8619
            }
        }

        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._taskParameters,
            enumSymbolsInContribBeforeFunc,
            enumSymbolsInContribAfterFunc,
            $"Contribution '{contributionDiff.Name}'",
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    #endregion

    #region Duplicate Data

    public async Task<IReadOnlyList<DuplicateDataItemDiff>> EnumerateDuplicateDataItemDiffs(CancellationToken token)
    {
        if (this._dataCache.AllDuplicateDataItemDiffs is null)
        {
            var task = new EnumerateDuplicateDataDiffsSessionTask(
                            this._taskParameters,
                (logger) => this.BeforeSession.EnumerateDuplicateDataItems(token, logger),
                (logger) => this.AfterSession.EnumerateDuplicateDataItems(token, logger),
                this.ProgressReporter,
                token);

            this._dataCache.AllDuplicateDataItemDiffs = await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
        }

        return this._dataCache.AllDuplicateDataItemDiffs;
    }

    #endregion

    #region Wasteful Virtuals

    public async Task<IReadOnlyList<WastefulVirtualItemDiff>> EnumerateWastefulVirtualItemDiffs(CancellationToken token)
    {
        if (this._dataCache.AllWastefulVirtualItemDiffs is null)
        {
            var task = new EnumerateWastefulVirtualDiffsSessionTask(
                            this._taskParameters,
                (logger) => this.BeforeSession.EnumerateWastefulVirtuals(token, logger),
                (logger) => this.AfterSession.EnumerateWastefulVirtuals(token, logger),
                this.ProgressReporter,
                token);

            this._dataCache.AllWastefulVirtualItemDiffs = await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
        }

        return this._dataCache.AllWastefulVirtualItemDiffs;
    }

    #endregion

    #region Template Foldability

    public async Task<IReadOnlyList<TemplateFoldabilityItemDiff>> EnumerateTemplateFoldabilityItemDiffs(CancellationToken token)
    {
        if (this._dataCache.AllTemplateFoldabilityItemDiffs is null)
        {
            var task = new EnumerateTemplateFoldabilityDiffsSessionTask(
                            this._taskParameters,
                (logger) => this.BeforeSession.EnumerateTemplateFoldabilityItems(token, logger),
                (logger) => this.AfterSession.EnumerateTemplateFoldabilityItems(token, logger),
                this.ProgressReporter,
                token);

            this._dataCache.AllTemplateFoldabilityItemDiffs = await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
        }

        return this._dataCache.AllTemplateFoldabilityItemDiffs;
    }

    #endregion

    #region Type Layouts

    public async Task<IReadOnlyList<TypeLayoutItemDiff>> LoadAllTypeLayoutDiffs(CancellationToken token)
    {
        var task = new LoadTypeLayoutDiffsSessionTask(
            this._taskParameters,
            (logger) => this.BeforeSession.LoadAllTypeLayouts(token, logger),
            (logger) => this.AfterSession.LoadAllTypeLayouts(token, logger),
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<TypeLayoutItemDiff>> LoadTypeLayoutDiffsByName(string typeName, CancellationToken token)
    {
        var task = new LoadTypeLayoutDiffsSessionTask(
                        this._taskParameters,
            (logger) => this.BeforeSession.LoadTypeLayoutsByName(typeName, token, logger),
            (logger) => this.AfterSession.LoadTypeLayoutsByName(typeName, token, logger),
            this.ProgressReporter,
            token);

        return await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true);
    }

    public async Task<TypeLayoutItemDiff> LoadTypeLayoutDiff(TypeSymbolDiff typeSymbol, CancellationToken token)
    {
        var task = new LoadTypeLayoutDiffsSessionTask(this._taskParameters,
            async (logger) =>
            {
                if (typeSymbol.BeforeSymbol is null)
                {
                    return new List<TypeLayoutItem>();
                }
                else
                {
                    return new List<TypeLayoutItem>() { await this.BeforeSession.LoadTypeLayout(typeSymbol.BeforeSymbol, token, logger).ConfigureAwait(true) };
                }
            },
            async (logger) =>
            {
                if (typeSymbol.AfterSymbol is null)
                {
                    return new List<TypeLayoutItem>();
                }
                else
                {
                    return new List<TypeLayoutItem>() { await this.AfterSession.LoadTypeLayout(typeSymbol.AfterSymbol, token, logger).ConfigureAwait(true) };
                }
            },
            this.ProgressReporter,
            token);

        return (await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true)).First();
    }

    public async Task<TypeLayoutItemDiff> LoadMemberTypeLayoutDiff(TypeLayoutItemMemberDiff member, CancellationToken token)
    {
        var task = new LoadTypeLayoutDiffsSessionTask(this._taskParameters,
            async (logger) =>
            {
                if (member.BeforeMember is null)
                {
                    return new List<TypeLayoutItem>();
                }
                else
                {
                    return new List<TypeLayoutItem>() { await this.BeforeSession.LoadMemberTypeLayout(member.BeforeMember, token, logger).ConfigureAwait(true) };
                }
            },
            async (logger) =>
            {
                if (member.AfterMember is null)
                {
                    return new List<TypeLayoutItem>();
                }
                else
                {
                    return new List<TypeLayoutItem>() { await this.AfterSession.LoadMemberTypeLayout(member.AfterMember, token, logger).ConfigureAwait(true) };
                }
            },
            this.ProgressReporter,
            token);

        return (await PerformSessionDiffTaskOnBackgroundThread(task, token).ConfigureAwait(true)).First();
    }



    #endregion

    #region Load Symbol Diff by RVAs

    public Task<SymbolDiff?> LoadSymbolDiffByBeforeAndAfterRVA(uint? beforeRVA, uint? afterRVA, CancellationToken token)
    {
        var task = new LoadSymbolDiffByBeforeAndAfterRVAsSessionTask(this._taskParameters,
            (logger) =>
            {
                if (beforeRVA is null)
                {
                    return Task.FromResult<ISymbol?>(null);
                }
                else
                {
                    return this.BeforeSession.LoadSymbolByRVA(beforeRVA.Value, token, logger);
                }
            },
            (logger) =>
            {
                if (afterRVA is null)
                {
                    return Task.FromResult<ISymbol?>(null);
                }
                else
                {
                    return this.AfterSession.LoadSymbolByRVA(afterRVA.Value, token, logger);
                }
            },
            this.ProgressReporter,
            token);

        return PerformSessionDiffTaskOnBackgroundThreadWhichMayReturnNull(task, token);
    }

    #endregion

    #region Getting on a background thread to do work

    private readonly DiffSessionTaskParameters _taskParameters;

    private async Task<T> PerformSessionDiffTaskOnBackgroundThread<T>(DiffSessionTask<T> task, CancellationToken token)
        where T : class
    {
        T? results = default;
        await this._taskFactory.StartNew(async () => results = await task.ExecuteAsync(this._logger).ConfigureAwait(true), token).Unwrap().ConfigureAwait(true);

#pragma warning disable CA1508 // Avoid dead conditional code - "results" is set in the lambda via the task factory, this is definitely not dead code but code analysis can't see that
        if (results is null)
#pragma warning restore CA1508 // Avoid dead conditional code
        {
            throw new InvalidOperationException("DiffSessionTask returned null results, this should be impossible and the calling code is not resilient to null return values.");
        }

        return results;
    }

    private async Task<T?> PerformSessionDiffTaskOnBackgroundThreadWhichMayReturnNull<T>(DiffSessionTask<T?> task, CancellationToken token)
        where T : class
    {
        T? results = default;
        await this._taskFactory.StartNew(async () => results = await task.ExecuteAsync(this._logger).ConfigureAwait(true), token).Unwrap().ConfigureAwait(true);

        return results;
    }

    #endregion
}
