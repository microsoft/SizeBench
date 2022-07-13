using System.Collections.ObjectModel;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Converters;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllCompilandsPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<Compiland>? _compilandList;

    public IReadOnlyList<Compiland>? Compilands
    {
        get => this._compilandList;
        private set { this._compilandList = value; RaisePropertyChanged(); }
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

    public ObservableCollection<DataGridColumnDescription> DataGridSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public ObservableCollection<DataGridColumnDescription> DataGridVirtualSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public DelegateCommand ExportToExcelCommand { get; }

    public AllCompilandsPageViewModel(IUITaskScheduler uiTaskScheduler,
                                      ISession session,
                                      IExcelExporter excelExporter)
        : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Compilands",
            async (token) =>
            {
                this.Compilands = await this.Session.EnumerateCompilands(token);
                CalculateColumnDescriptions();
            });
    }

    private void CalculateColumnDescriptions()
    {
        var binarySections = BinarySectionsInCompilands().OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInCompilands().OrderBy(s => s.Name).ToList();
        this.DataGridSizeColumnDescriptions.Clear();
        this.DataGridVirtualSizeColumnDescriptions.Clear();

        foreach (var section in binarySections)
        {
            if (section.Size > 0)
            {
                this.DataGridSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"Section: {section.Name}",
                    propertyPath: $"SectionContributionsByName[{section.Name}].Size",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
            if (section.VirtualSize > 0)
            {
                this.DataGridVirtualSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"Section: {section.Name}",
                    propertyPath: $"SectionContributionsByName[{section.Name}].VirtualSize",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
        }

        foreach (var coffGroup in coffGroups)
        {
            if (coffGroup.Size > 0)
            {
                this.DataGridSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"COFF Group: {coffGroup.Name}",
                    propertyPath: $"COFFGroupContributionsByName[{coffGroup.Name}].Size",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
            }
            if (coffGroup.VirtualSize > 0)
            {
                this.DataGridVirtualSizeColumnDescriptions.Add(new DataGridColumnDescription(
                    header: $"COFF Group: {coffGroup.Name}",
                    propertyPath: $"COFFGroupContributionsByName[{coffGroup.Name}].VirtualSize",
                    valueConverter: SizeToFriendlySizeConverter.Instance,
                    isRightAligned: true));
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
        var binarySections = BinarySectionsInCompilands().Where(s => this.ShouldDisplayVirtualSize ? s.VirtualSize > 0 : s.Size > 0).OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInCompilands().Where(cg => this.ShouldDisplayVirtualSize ? cg.VirtualSize > 0 : cg.Size > 0).OrderBy(cg => cg.Name).ToList();
        var columnHeadersList = new List<string>
            {
                "Compiland Name",
                "Compiland Short Name",
                "Lib Name",
                "Lib Short Name",
                this.ShouldDisplayVirtualSize ? "Compiland Total Size in Memory" : "Compiland Total Size on Disk"
            };

        foreach (var section in binarySections)
        {
            columnHeadersList.Add($"Section: {section.Name}");
        }

        foreach (var cg in coffGroups)
        {
            columnHeadersList.Add($"COFF Group: {cg.Name}");
        }

        columnHeaders = columnHeadersList.ToArray();
        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.Compilands?.Count ?? 1);
        if (this.Compilands != null)
        {
            foreach (var compiland in this.Compilands)
            {
                if (this.ShouldDisplaySize && compiland.Size == 0)
                {
                    continue;
                }

                if (this.ShouldDisplayVirtualSize && compiland.VirtualSize == 0)
                {
                    continue;
                }

                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Compiland Name", compiland.Name },
                    { "Compiland Short Name", compiland.ShortName },
                    { "Lib Name", compiland.Lib.Name },
                    { "Lib Short Name", compiland.Lib.ShortName },
                    { this.ShouldDisplayVirtualSize ? "Compiland Total Size in Memory" : "Compiland Total Size on Disk", this.ShouldDisplayVirtualSize ? compiland.VirtualSize : compiland.Size }
                };
                foreach (var contrib in compiland.SectionContributions.Where(sc => this.ShouldDisplayVirtualSize ? sc.Value.VirtualSize > 0 : sc.Value.Size > 0))
                {
                    formattedData.Add($"Section: {contrib.Key.Name}",
                                      this.ShouldDisplayVirtualSize ? compiland.SectionContributions[contrib.Key].VirtualSize : compiland.SectionContributions[contrib.Key].Size);
                }
                foreach (var contrib in compiland.COFFGroupContributions.Where(sc => this.ShouldDisplayVirtualSize ? sc.Value.VirtualSize > 0 : sc.Value.Size > 0))
                {
                    formattedData.Add($"COFF Group: {contrib.Key.Name}",
                                      this.ShouldDisplayVirtualSize ? compiland.COFFGroupContributions[contrib.Key].VirtualSize : compiland.COFFGroupContributions[contrib.Key].Size);
                }
                preformattedData.Add(formattedData);
            }
        }
    }

    public IEnumerable<BinarySection> BinarySectionsInCompilands()
    {
        var sectionsAlreadySeen = new List<BinarySection>();
        if (this.Compilands != null)
        {
            foreach (var Compiland in this.Compilands)
            {
                foreach (var section in Compiland.SectionContributions.Keys)
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

    public IEnumerable<COFFGroup> COFFGroupsInCompilands()
    {
        var coffGroupsAlreadySeen = new List<COFFGroup>();
        if (this.Compilands != null)
        {
            foreach (var Compiland in this.Compilands)
            {
                foreach (var coffGroup in Compiland.COFFGroupContributions.Keys)
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
}
