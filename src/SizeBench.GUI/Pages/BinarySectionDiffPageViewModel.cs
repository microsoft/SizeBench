using System.Collections.ObjectModel;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Converters;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class BinarySectionDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;

    private BinarySectionDiff? _binarySectionDiff;

    public BinarySectionDiff? BinarySectionDiff
    {
        get => this._binarySectionDiff;
        private set { this._binarySectionDiff = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<LibDiff>? _libDiffsList;
    private ObservableCollection<LibDiff>? _libDiffsForUI;

    public ObservableCollection<LibDiff>? LibDiffs
    {
        get => this._libDiffsForUI;
        private set { this._libDiffsForUI = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<CompilandDiff>? _compilandDiffsList;
    private ObservableCollection<CompilandDiff>? _compilandDiffsForUI;

    public ObservableCollection<CompilandDiff>? CompilandDiffs
    {
        get => this._compilandDiffsForUI;
        private set { this._compilandDiffsForUI = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<SymbolDiff>? _symbolsList;
    private ObservableCollection<SymbolDiff>? _symbolsForUI;

    public ObservableCollection<SymbolDiff>? Symbols
    {
        get => this._symbolsForUI;
        private set { this._symbolsForUI = value; RaisePropertyChanged(); }
    }

    public ObservableCollection<DataGridColumnDescription> DataGridColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public enum BinarySectionDiffPageTabIndex
    {
        COFFGroupsTab = 0,
        LibsTab = 1,
        CompilandsTab = 2,
        SymbolsTab
    }

    private BinarySectionDiffPageTabIndex _selectedTab = BinarySectionDiffPageTabIndex.COFFGroupsTab;

    public int SelectedTab
    {
        get => (int)this._selectedTab;
        set
        {
            this._selectedTab = (BinarySectionDiffPageTabIndex)value;
            StartLoadingStuffIfNecessary();
        }
    }

    public DelegateCommand ExportCOFFGroupsToExcelCommand { get; }
    public DelegateCommand ExportLibsToExcelCommand { get; }
    public DelegateCommand ExportCompilandsToExcelCommand { get; }
    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public BinarySectionDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                          IExcelExporter excelExporter,
                                          IDiffSession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportCOFFGroupsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.BinarySectionDiff?.COFFGroupDiffs));
        this.ExportLibsToExcelCommand = new DelegateCommand(ExportLibsToExcel);
        this.ExportCompilandsToExcelCommand = new DelegateCommand(ExportCompilandsToExcel);
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.Symbols));
    }

    protected internal override async Task InitializeAsync()
    {
        var sectionName = this.QueryString["BinarySection"];

        this.DataGridColumnDescriptions.Clear();
        this.DataGridColumnDescriptions.Add(new DataGridColumnDescription(
            header: $"Size on Disk Diff in {sectionName}",
            propertyPath: $"SectionContributionDiffsByName[{sectionName}].SizeDiff",
            valueConverter: SizeToFriendlySizeConverter.Instance,
            isRightAligned: true));

        this.DataGridColumnDescriptions.Add(new DataGridColumnDescription(
            header: $"Size in Memory Diff in {sectionName}",
            propertyPath: $"SectionContributionDiffsByName[{sectionName}].VirtualSizeDiff",
            valueConverter: SizeToFriendlySizeConverter.Instance,
            isRightAligned: true));

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding {sectionName} Binary Section Diff",
            async (token) => this.BinarySectionDiff = await this.DiffSession.LoadBinarySectionDiffByName(sectionName, token));
    }

    private async void StartLoadingStuffIfNecessary()
    {
        if (this._selectedTab == BinarySectionDiffPageTabIndex.LibsTab)
        {
            if (this._libDiffsList is null)
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating diffs of libs in the {this.BinarySectionDiff?.Name ?? this.QueryString["BinarySection"]} Binary Section",
                    async (token) => this._libDiffsList = await this.DiffSession.EnumerateLibDiffs(token));
            }

            var newDiffsForUI = new ObservableCollection<LibDiff>();
            // Can be null if the user cancels
            if (this._libDiffsList != null && this.BinarySectionDiff != null)
            {
                newDiffsForUI.AddRange(this._libDiffsList.Where(l => l.SectionContributionDiffs.ContainsKey(this.BinarySectionDiff)));
            }
            this.LibDiffs = newDiffsForUI;
        }
        else if (this._selectedTab == BinarySectionDiffPageTabIndex.CompilandsTab)
        {
            if (this._compilandDiffsList is null)
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating diffs of compilands in the {this.BinarySectionDiff?.Name ?? this.QueryString["BinarySection"]} Binary Section",
                    async (token) => this._compilandDiffsList = await this.DiffSession.EnumerateCompilandDiffs(token));
            }

            var newDiffsForUI = new ObservableCollection<CompilandDiff>();
            // Can be null if the user cancels
            if (this._compilandDiffsList != null && this.BinarySectionDiff != null)
            {
                newDiffsForUI.AddRange(this._compilandDiffsList.Where(c => c.SectionContributionDiffs.ContainsKey(this.BinarySectionDiff)));
            }
            this.CompilandDiffs = newDiffsForUI;
        }
        else if (this._selectedTab == BinarySectionDiffPageTabIndex.SymbolsTab)
        {
            if (this._symbolsList is null && this.BinarySectionDiff != null)
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating diffs of symbols in the {this.BinarySectionDiff.Name} Binary Section",
                    async (token) => this._symbolsList = await this.DiffSession.EnumerateSymbolDiffsInBinarySectionDiff(this.BinarySectionDiff, token));
            }

            // It's possible for these to be null if the user cancels out of the loading process.
            if (this._symbolsList != null)
            {
                this.Symbols = new ObservableCollection<SymbolDiff>(this._symbolsList);
            }
            else
            {
                this.Symbols = new ObservableCollection<SymbolDiff>();
            }
        }
    }

    #region Excel Export

    private async void ExportLibsToExcel()
    {
        GenerateFormattedDataForLibsForExcelExport(out var columnHeaders, out var preformattedData);
        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    public void GenerateFormattedDataForLibsForExcelExport(out string[] columnHeaders,
                                                           out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        if (this.BinarySectionDiff is null || this.LibDiffs is null)
        {
            columnHeaders = new string[1] { "Unable to export" };
            preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>();
            return;
        }

        columnHeaders = new string[4]
        {
                "Name",
                "Short Name",
                $"Size on Disk Diff in {this.BinarySectionDiff.Name}",
                $"Size in Memory Diff in {this.BinarySectionDiff.Name}",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.LibDiffs.Count);
        foreach (var libDiff in this.LibDiffs)
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Name", libDiff.Name },
                    { "Short Name", libDiff.ShortName },
                    { $"Size on Disk Diff in {this.BinarySectionDiff.Name}", libDiff.SectionContributionDiffs[this.BinarySectionDiff].SizeDiff },
                    { $"Size in Memory Diff in {this.BinarySectionDiff.Name}", libDiff.SectionContributionDiffs[this.BinarySectionDiff].VirtualSizeDiff },
                };
            preformattedData.Add(formattedData);
        }
    }

    private async void ExportCompilandsToExcel()
    {
        GenerateFormattedDataForCompilandsForExcelExport(out var columnHeaders, out var preformattedData);
        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    public void GenerateFormattedDataForCompilandsForExcelExport(out string[] columnHeaders,
                                                                 out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        if (this.BinarySectionDiff is null || this.CompilandDiffs is null)
        {
            columnHeaders = new string[1] { "Unable to export" };
            preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>();
            return;
        }

        columnHeaders = new string[4]
        {
                "Name",
                "Short Name",
                $"Size on Disk Diff in {this.BinarySectionDiff.Name}",
                $"Size in Memory Diff in {this.BinarySectionDiff.Name}",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.CompilandDiffs.Count);
        foreach (var compilandDiff in this.CompilandDiffs)
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Name", compilandDiff.Name },
                    { "Short Name", compilandDiff.ShortName },
                    { $"Size on Disk Diff in {this.BinarySectionDiff.Name}", compilandDiff.SectionContributionDiffs[this.BinarySectionDiff].SizeDiff },
                    { $"Size in Memory Diff in {this.BinarySectionDiff.Name}", compilandDiff.SectionContributionDiffs[this.BinarySectionDiff].VirtualSizeDiff },
                };
            preformattedData.Add(formattedData);
        }
    }

    #endregion Excel Export
}
