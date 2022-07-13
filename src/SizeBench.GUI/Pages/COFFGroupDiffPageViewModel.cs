using System.ComponentModel;
using System.Windows.Data;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class COFFGroupDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private COFFGroupDiff? _coffGroupDiff;
    public COFFGroupDiff? COFFGroupDiff
    {
        get => this._coffGroupDiff;
        private set { this._coffGroupDiff = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<SymbolDiff>? _symbolDiffs;
    public IReadOnlyList<SymbolDiff>? SymbolDiffs
    {
        get => this._symbolDiffs;
        private set { this._symbolDiffs = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<LibDiff>? _libDiffs;
    private CollectionView? _libDiffsCV;
    public CollectionView? LibDiffs
    {
        get => this._libDiffsCV;
        set { this._libDiffsCV = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<CompilandDiff>? _compilandDiffs;
    private CollectionView? _compilandDiffsCV;
    public CollectionView? CompilandDiffs
    {
        get => this._compilandDiffsCV;
        set { this._compilandDiffsCV = value; RaisePropertyChanged(); }
    }

    public string ContributionSizeSortMemberPath => this.COFFGroupDiff != null ? $"COFFGroupContributionDiffsByName[{this.COFFGroupDiff.Name}].SizeDiff" : String.Empty;
    public string ContributionVirtualSizeSortMemberPath => this.COFFGroupDiff != null ? $"COFFGroupContributionDiffsByName[{this.COFFGroupDiff.Name}].VirtualSizeDiff" : String.Empty;

    public DelegateCommand ExportSymbolsToExcelCommand { get; }
    public DelegateCommand ExportLibsToExcelCommand { get; }
    public DelegateCommand ExportCompilandsToExcelCommand { get; }

    public COFFGroupDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                      IDiffSession session,
                                      IExcelExporter excelExporter)
        : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportSymbolsToExcelCommand = new DelegateCommand(ExportSymbolsToExcel);
        this.ExportLibsToExcelCommand = new DelegateCommand(ExportLibsToExcel);
        this.ExportCompilandsToExcelCommand = new DelegateCommand(ExportCompilandsToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        var coffGroupName = this.QueryString["COFFGroup"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Loading Diff of COFF Group {coffGroupName}", async (token) =>
        {
            this.COFFGroupDiff = (from bs in await this.DiffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(token)
                                  from cg in bs.COFFGroupDiffs
                                  where cg.Name == coffGroupName
                                  select cg).FirstOrDefault();
        });

        // It's possible for this to be null, if the user cancels.
        if (this.COFFGroupDiff is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in COFF Group {this.COFFGroupDiff.Name}",
            async (token) => this.SymbolDiffs = await this.DiffSession.EnumerateSymbolDiffsInCOFFGroupDiff(this.COFFGroupDiff, token));

        await this._uiTaskScheduler.StartLongRunningUITask("Enumerating libs", async (token) =>
        {
            this._libDiffs = await this.DiffSession.EnumerateLibDiffs(token);
            this.LibDiffs = (CollectionView)CollectionViewSource.GetDefaultView(this._libDiffs);
            this.LibDiffs.Filter = (lib) => ((LibDiff)lib).COFFGroupContributionDiffs.ContainsKey(this.COFFGroupDiff);
            this.LibDiffs.SortDescriptions.Add(new SortDescription(this.ContributionSizeSortMemberPath, ListSortDirection.Descending));
            this.LibDiffs.SortDescriptions.Add(new SortDescription(this.ContributionVirtualSizeSortMemberPath, ListSortDirection.Descending));
        });

        await this._uiTaskScheduler.StartLongRunningUITask("Enumerating compilands", async (token) =>
        {
            this._compilandDiffs = await this.DiffSession.EnumerateCompilandDiffs(token);
            this.CompilandDiffs = (CollectionView)CollectionViewSource.GetDefaultView(this._compilandDiffs);
            this.CompilandDiffs.Filter = (compiland) => ((CompilandDiff)compiland).COFFGroupContributionDiffs.ContainsKey(this.COFFGroupDiff);
            this.CompilandDiffs.SortDescriptions.Add(new SortDescription(this.ContributionSizeSortMemberPath, ListSortDirection.Descending));
            this.CompilandDiffs.SortDescriptions.Add(new SortDescription(this.ContributionVirtualSizeSortMemberPath, ListSortDirection.Descending));
        });
    }

    private async void ExportLibsToExcel()
    {
        if (this.COFFGroupDiff is null || this.LibDiffs is null)
        {
            return;
        }

        var cgName = this.COFFGroupDiff.Name;

        var columnHeaders = new string[]
        {
                "Lib Name",
                "Lib Short Name",
                $"Before Size on Disk in {cgName}",
                $"Before Size in Memory in {cgName}",
                $"After Size on Disk in {cgName}",
                $"After Size in Memory in {cgName}",
                $"Size on Disk Diff in {cgName}",
                $"Size in Memory Diff in {cgName}",
        };

        var preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.LibDiffs.Count);
        foreach (var libDiff in this.LibDiffs.Cast<LibDiff>())
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Lib Name", libDiff.Name },
                    { "Lib Short Name", libDiff.ShortName },
                    { $"Size on Disk Diff in {cgName}", libDiff.COFFGroupContributionDiffs[this.COFFGroupDiff].SizeDiff },
                    { $"Size in Memory Diff in {cgName}", libDiff.COFFGroupContributionDiffs[this.COFFGroupDiff].VirtualSizeDiff },
                };

            if (libDiff.BeforeLib != null && this.COFFGroupDiff.BeforeCOFFGroup != null &&
                libDiff.BeforeLib.COFFGroupContributions.ContainsKey(this.COFFGroupDiff.BeforeCOFFGroup))
            {
                formattedData.Add($"Before Size on Disk in {cgName}", libDiff.BeforeLib.COFFGroupContributions[this.COFFGroupDiff.BeforeCOFFGroup].Size);
                formattedData.Add($"Before Size in Memory in {cgName}", libDiff.BeforeLib.COFFGroupContributions[this.COFFGroupDiff.BeforeCOFFGroup].VirtualSize);
            }

            if (libDiff.AfterLib != null && this.COFFGroupDiff.AfterCOFFGroup != null &&
                libDiff.AfterLib.COFFGroupContributions.ContainsKey(this.COFFGroupDiff.AfterCOFFGroup))
            {
                formattedData.Add($"After Size on Disk in {cgName}", libDiff.AfterLib.COFFGroupContributions[this.COFFGroupDiff.AfterCOFFGroup].Size);
                formattedData.Add($"After Size in Memory in {cgName}", libDiff.AfterLib.COFFGroupContributions[this.COFFGroupDiff.AfterCOFFGroup].VirtualSize);
            }

            preformattedData.Add(formattedData);
        }

        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    private async void ExportCompilandsToExcel()
    {
        if (this.COFFGroupDiff is null || this.CompilandDiffs is null)
        {
            return;
        }

        var cgName = this.COFFGroupDiff.Name;

        var columnHeaders = new string[]
        {
                "Compiland Name",
                "Compiland ShortName",
                "Lib Name",
                "Lib Short Name",
                $"Before Size on Disk in {cgName}",
                $"Before Size in Memory in {cgName}",
                $"After Size on Disk in {cgName}",
                $"After Size in Memory in {cgName}",
                $"Size on Disk Diff in {cgName}",
                $"Size in Memory Diff in {cgName}",
        };

        var preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.CompilandDiffs.Count);
        foreach (var compilandDiff in this.CompilandDiffs.Cast<CompilandDiff>())
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Compiland Name", compilandDiff.Name },
                    { "Compiland Short Name", compilandDiff.ShortName },
                    { "Lib Name", compilandDiff.LibDiff.Name },
                    { "Lib Short Name", compilandDiff.LibDiff.ShortName },
                    { $"Size on Disk Diff in {cgName}", compilandDiff.COFFGroupContributionDiffs[this.COFFGroupDiff].SizeDiff },
                    { $"Size in Memory Diff in {cgName}", compilandDiff.COFFGroupContributionDiffs[this.COFFGroupDiff].VirtualSizeDiff },
                };

            if (compilandDiff.BeforeCompiland != null && this.COFFGroupDiff.BeforeCOFFGroup != null &&
                compilandDiff.BeforeCompiland.COFFGroupContributions.ContainsKey(this.COFFGroupDiff.BeforeCOFFGroup))
            {
                formattedData.Add($"Before Size on Disk in {cgName}", compilandDiff.BeforeCompiland.COFFGroupContributions[this.COFFGroupDiff.BeforeCOFFGroup].Size);
                formattedData.Add($"Before Size in Memory in {cgName}", compilandDiff.BeforeCompiland.COFFGroupContributions[this.COFFGroupDiff.BeforeCOFFGroup].VirtualSize);
            }

            if (compilandDiff.AfterCompiland != null && this.COFFGroupDiff.AfterCOFFGroup != null &&
                compilandDiff.AfterCompiland.COFFGroupContributions.ContainsKey(this.COFFGroupDiff.AfterCOFFGroup))
            {
                formattedData.Add($"After Size on Disk in {cgName}", compilandDiff.AfterCompiland.COFFGroupContributions[this.COFFGroupDiff.AfterCOFFGroup].Size);
                formattedData.Add($"After Size in Memory in {cgName}", compilandDiff.AfterCompiland.COFFGroupContributions[this.COFFGroupDiff.AfterCOFFGroup].VirtualSize);
            }

            preformattedData.Add(formattedData);
        }

        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    private async void ExportSymbolsToExcel()
    {
        if (this.SymbolDiffs is null)
        {
            return;
        }

        var columnHeaders = new string[]
        {
                "Symbol Name",
                $"Before Size on Disk",
                $"Before Size in Memory",
                $"After Size on Disk",
                $"After Size in Memory",
                $"Size on Disk Diff",
                $"Size in Memory Diff",
        };

        var preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.SymbolDiffs.Count);
        foreach (var symbolDiff in this.SymbolDiffs)
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Symbol Name", symbolDiff.Name },
                    { $"Size on Disk Diff", symbolDiff.SizeDiff },
                    { $"Size in Memory Diff", symbolDiff.VirtualSizeDiff },
                };

            if (symbolDiff.BeforeSymbol != null)
            {
                formattedData.Add($"Before Size on Disk", symbolDiff.BeforeSymbol.Size);
                formattedData.Add($"Before Size in Memory", symbolDiff.BeforeSymbol.VirtualSize);
            }

            if (symbolDiff.AfterSymbol != null)
            {
                formattedData.Add($"After Size on Disk", symbolDiff.AfterSymbol.Size);
                formattedData.Add($"After Size in Memory", symbolDiff.AfterSymbol.VirtualSize);
            }

            preformattedData.Add(formattedData);
        }

        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }
}
