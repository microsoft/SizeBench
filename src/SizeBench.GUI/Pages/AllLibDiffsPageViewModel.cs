using System.Collections.ObjectModel;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Converters;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllLibDiffsPageViewModel : BinaryDiffViewModelBase
{
    private IReadOnlyList<LibDiff>? _libDiffList;
    public ObservableCollection<DataGridColumnDescription> DataGridSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();
    public ObservableCollection<DataGridColumnDescription> DataGridVirtualSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public IReadOnlyList<LibDiff>? LibDiffs
    {
        get => this._libDiffList;
        private set { this._libDiffList = value; RaisePropertyChanged(); }
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

    public AllLibDiffsPageViewModel(IUITaskScheduler uiTaskScheduler,
                                    IDiffSession diffSession,
                                    IExcelExporter excelExporter) : base(diffSession)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Lib Diffs",
            async (token) =>
            {
                this.LibDiffs = await this.DiffSession.EnumerateLibDiffs(token);
                CalculateColumnDescriptions();
            });
    }

    public void CalculateColumnDescriptions()
    {
        var binarySections = BinarySectionsInLibs().OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInLibs().OrderBy(s => s.Name).ToList();
        this.DataGridSizeColumnDescriptions.Clear();
        this.DataGridVirtualSizeColumnDescriptions.Clear();

        if (this.LibDiffs == null)
        {
            return;
        }

        foreach (var section in binarySections)
        {
            if (this.LibDiffs.Any(ld => ld.SectionContributionDiffs.ContainsKey(section) && ld.SectionContributionDiffs[section].SizeDiff != 0))
            {
                this.DataGridSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"Section: {section.Name}",
                    propertyPath: $"SectionContributionDiffsByName[{section.Name}].SizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
            if (this.LibDiffs.Any(ld => ld.SectionContributionDiffs.ContainsKey(section) && ld.SectionContributionDiffs[section].VirtualSizeDiff != 0))
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
            if (this.LibDiffs.Any(ld => ld.COFFGroupContributionDiffs.ContainsKey(coffGroup) && ld.COFFGroupContributionDiffs[coffGroup].SizeDiff != 0))
            {
                this.DataGridSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"COFF Group: {coffGroup.Name}",
                    propertyPath: $"COFFGroupContributionDiffsByName[{coffGroup.Name}].SizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
            if (this.LibDiffs.Any(ld => ld.COFFGroupContributionDiffs.ContainsKey(coffGroup) && ld.COFFGroupContributionDiffs[coffGroup].VirtualSizeDiff != 0))
            {
                this.DataGridVirtualSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"COFF Group: {coffGroup.Name}",
                    propertyPath: $"COFFGroupContributionDiffsByName[{coffGroup.Name}].VirtualSizeDiff",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
        }
    }

    public IEnumerable<BinarySectionDiff> BinarySectionsInLibs()
    {
        var sectionsAlreadySeen = new List<BinarySectionDiff>();
        if (this.LibDiffs != null)
        {
            foreach (var libDiff in this.LibDiffs)
            {
                foreach (var section in libDiff.SectionContributionDiffs.Keys)
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

    public IEnumerable<COFFGroupDiff> COFFGroupsInLibs()
    {
        var coffGroupsAlreadySeen = new List<COFFGroupDiff>();
        if (this.LibDiffs != null)
        {
            foreach (var libDiff in this.LibDiffs)
            {
                foreach (var coffGroup in libDiff.COFFGroupContributionDiffs.Keys)
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
                "Lib Name",
                "Lib Short Name",
                this.ShouldDisplaySize ? "Total Before Size on Disk" : "Total Before Size in Memory",
                this.ShouldDisplaySize ? "Total After Size on Disk" : "Total After Size in Memory",
                this.ShouldDisplaySize ? "Total Size on Disk Diff" : "Total Size in Memory Diff",
            };

        var binarySections = BinarySectionsInLibs().OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInLibs().OrderBy(s => s.Name).ToList();

        foreach (var section in binarySections)
        {
            columnHeadersList.Add($"Section: {section.Name}");
        }

        foreach (var coffGroup in coffGroups)
        {
            columnHeadersList.Add($"COFF Group: {coffGroup.Name}");
        }

        columnHeaders = columnHeadersList.ToArray();

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.LibDiffs?.Count ?? 1);
        if (this.LibDiffs != null)
        {
            foreach (var lib in this.LibDiffs)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Lib Name", lib.Name },
                    { "Lib Short Name", lib.ShortName },
                    { this.ShouldDisplaySize ? "Total Before Size on Disk" : "Total Before Size in Memory", this.ShouldDisplaySize ? lib.BeforeLib?.Size ?? 0 : lib.BeforeLib?.VirtualSize ?? 0 },
                    { this.ShouldDisplaySize ? "Total After Size on Disk" : "Total After Size in Memory", this.ShouldDisplaySize ? lib.AfterLib?.Size ?? 0 : lib.AfterLib?.VirtualSize ?? 0 },
                    { this.ShouldDisplaySize ? "Total Size on Disk Diff" : "Total Size in Memory Diff", this.ShouldDisplaySize ? lib.SizeDiff : lib.VirtualSizeDiff }
                };

                foreach (var section in lib.SectionContributionDiffs.Keys)
                {
                    formattedData.Add($"Section: {section.Name}", this.ShouldDisplaySize ? lib.SectionContributionDiffs[section].SizeDiff : lib.SectionContributionDiffs[section].VirtualSizeDiff);
                }
                foreach (var coffGroup in lib.COFFGroupContributionDiffs.Keys)
                {
                    formattedData.Add($"COFF Group: {coffGroup.Name}", this.ShouldDisplaySize ? lib.COFFGroupContributionDiffs[coffGroup].SizeDiff : lib.COFFGroupContributionDiffs[coffGroup].VirtualSizeDiff);
                }

                preformattedData.Add(formattedData);
            }
        }
    }
}
