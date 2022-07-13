using System.Collections.ObjectModel;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Converters;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllCompilandDiffsPageViewModel : BinaryDiffViewModelBase
{
    private IReadOnlyList<CompilandDiff>? _compilandDiffList;

    public ObservableCollection<DataGridColumnDescription> DataGridSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();
    public ObservableCollection<DataGridColumnDescription> DataGridVirtualSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public IReadOnlyList<CompilandDiff>? CompilandDiffs
    {
        get => this._compilandDiffList;
        private set { this._compilandDiffList = value; RaisePropertyChanged(); }
    }

    public List<string> DisplayModes { get; } = new List<string>() { "Size on disk", "Size in memory" };

    private int _selectedDisplayModeIndex;

    public int SelectedDisplayModeIndex
    {
        get => this._selectedDisplayModeIndex;
        set
        {
            this._selectedDisplayModeIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.ShouldDisplaySize));
            RaisePropertyChanged(nameof(this.ShouldDisplayVirtualSize));
        }
    }

    public bool ShouldDisplaySize => this.SelectedDisplayModeIndex == 0;

    public bool ShouldDisplayVirtualSize => this.SelectedDisplayModeIndex == 1;

    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    public DelegateCommand ExportToExcelCommand { get; }

    public AllCompilandDiffsPageViewModel(IUITaskScheduler uiTaskScheduler,
                                          IDiffSession diffSession,
                                          IExcelExporter excelExporter) : base(diffSession)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Compiland Diffs",
            async (token) =>
            {
                this.CompilandDiffs = await this.DiffSession.EnumerateCompilandDiffs(token);
                CalculateColumnDescriptions();
            });
    }

    public void CalculateColumnDescriptions()
    {
        var binarySections = BinarySectionsInCompilands().OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInCompilands().OrderBy(s => s.Name).ToList();
        this.DataGridSizeColumnDescriptions.Clear();
        this.DataGridVirtualSizeColumnDescriptions.Clear();

        if (this.CompilandDiffs == null)
        {
            return;
        }

        foreach (var section in binarySections)
        {
            if (this.CompilandDiffs.Any(cd => cd.SectionContributionDiffs.ContainsKey(section) && cd.SectionContributionDiffs[section].SizeDiff != 0))
            {
                this.DataGridSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"Section: {section.Name}",
                    propertyPath: $"SectionContributionDiffsByName[{section.Name}].SizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
            if (this.CompilandDiffs.Any(cd => cd.SectionContributionDiffs.ContainsKey(section) && cd.SectionContributionDiffs[section].VirtualSizeDiff != 0))
            {
                this.DataGridVirtualSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"Section: {section.Name}",
                    propertyPath: $"SectionContributionDiffsByName[{section.Name}].VirtualSizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
        }

        foreach (var coffGroup in coffGroups)
        {
            if (this.CompilandDiffs.Any(cd => cd.COFFGroupContributionDiffs.ContainsKey(coffGroup) && cd.COFFGroupContributionDiffs[coffGroup].SizeDiff != 0))
            {
                this.DataGridSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"COFF Group: {coffGroup.Name}",
                    propertyPath: $"COFFGroupContributionDiffsByName[{coffGroup.Name}].SizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
            if (this.CompilandDiffs.Any(cd => cd.COFFGroupContributionDiffs.ContainsKey(coffGroup) && cd.COFFGroupContributionDiffs[coffGroup].VirtualSizeDiff != 0))
            {
                this.DataGridVirtualSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"COFF Group: {coffGroup.Name}",
                    propertyPath: $"COFFGroupContributionDiffsByName[{coffGroup.Name}].VirtualSizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
        }
    }

    public IEnumerable<BinarySectionDiff> BinarySectionsInCompilands()
    {
        var sectionsAlreadySeen = new List<BinarySectionDiff>();
        if (this.CompilandDiffs != null)
        {
            foreach (var compilandDiff in this.CompilandDiffs)
            {
                foreach (var section in compilandDiff.SectionContributionDiffs.Keys)
                {
                    if (!sectionsAlreadySeen.Contains(section))
                    {
                        sectionsAlreadySeen.Add(section);
                        yield return section;
                    }
                }
            }
        }
    }

    public IEnumerable<COFFGroupDiff> COFFGroupsInCompilands()
    {
        var coffGroupsAlreadySeen = new List<COFFGroupDiff>();
        if (this.CompilandDiffs != null)
        {
            foreach (var compilandDiff in this.CompilandDiffs)
            {
                foreach (var coffGroup in compilandDiff.COFFGroupContributionDiffs.Keys)
                {
                    if (!coffGroupsAlreadySeen.Contains(coffGroup))
                    {
                        coffGroupsAlreadySeen.Add(coffGroup);
                        yield return coffGroup;
                    }
                }
            }
        }
    }

    private async void ExportToExcel()
    {
        GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);
        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    public void GenerateFormattedDataForExcelExport(out string[] columnHeaders,
                                                    out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        var columnHeadersList = new List<string>
            {
                "Compiland Name",
                "Compiland Short Name",
                "Lib Name",
                "Lib Short Name",
                this.ShouldDisplaySize ? "Total Before Size on Disk" : "Total Before Size in Memory",
                this.ShouldDisplaySize ? "Total After Size on Disk" : "Total After Size in Memory",
                this.ShouldDisplaySize ? "Total Size on Disk Diff" : "Total Size in Memory Diff",
            };

        var binarySections = BinarySectionsInCompilands().OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInCompilands().OrderBy(s => s.Name).ToList();

        foreach (var section in binarySections)
        {
            columnHeadersList.Add($"Section: {section.Name}");
        }

        foreach (var coffGroup in coffGroups)
        {
            columnHeadersList.Add($"COFF Group: {coffGroup.Name}");
        }

        columnHeaders = columnHeadersList.ToArray();

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.CompilandDiffs?.Count ?? 1);
        if (this.CompilandDiffs != null)
        {
            foreach (var compiland in this.CompilandDiffs)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Compiland Name", compiland.Name },
                    { "Compiland Short Name", compiland.ShortName },
                    { "Lib Name", compiland.LibDiff.Name },
                    { "Lib Short Name", compiland.LibDiff.ShortName },
                    { this.ShouldDisplaySize ? "Total Before Size on Disk" : "Total Before Size in Memory", this.ShouldDisplaySize ? compiland.BeforeCompiland?.Size ?? 0 : compiland.BeforeCompiland?.VirtualSize ?? 0 },
                    { this.ShouldDisplaySize ? "Total After Size on Disk" : "Total After Size in Memory", this.ShouldDisplaySize ? compiland.AfterCompiland?.Size ?? 0 : compiland.AfterCompiland?.VirtualSize ?? 0 },
                    { this.ShouldDisplaySize ? "Total Size on Disk Diff" : "Total Size in Memory Diff", this.ShouldDisplaySize ? compiland.SizeDiff : compiland.VirtualSizeDiff }
                };
                foreach (var section in compiland.SectionContributionDiffs.Keys)
                {
                    formattedData.Add($"Section: {section.Name}", this.ShouldDisplaySize ? compiland.SectionContributionDiffs[section].SizeDiff : compiland.SectionContributionDiffs[section].VirtualSizeDiff);
                }
                foreach (var coffGroup in compiland.COFFGroupContributionDiffs.Keys)
                {
                    formattedData.Add($"COFF Group: {coffGroup.Name}", this.ShouldDisplaySize ? compiland.COFFGroupContributionDiffs[coffGroup].SizeDiff : compiland.COFFGroupContributionDiffs[coffGroup].VirtualSizeDiff);
                }
                preformattedData.Add(formattedData);
            }
        }
    }
}
