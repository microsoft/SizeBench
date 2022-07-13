using System.Collections.ObjectModel;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Converters;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllLibsPageViewModel : SingleBinaryViewModelBase
{
    private IReadOnlyList<Library>? _libList;

    public ObservableCollection<DataGridColumnDescription> DataGridSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public ObservableCollection<DataGridColumnDescription> DataGridVirtualSizeColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public IReadOnlyList<Library>? Libs
    {
        get => this._libList;
        private set { this._libList = value; RaisePropertyChanged(); }
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

    public AllLibsPageViewModel(IUITaskScheduler uiTaskScheduler,
                                ISession session,
                                IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Libs",
            async (token) =>
            {
                this.Libs = await this.Session.EnumerateLibs(token);
                CalculateColumnDescriptions();
            });
    }

    public void CalculateColumnDescriptions()
    {
        var binarySections = BinarySectionsInLibs().OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInLibs().OrderBy(s => s.Name).ToList();

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

    public IEnumerable<BinarySection> BinarySectionsInLibs()
    {
        var sectionsAlreadySeen = new List<BinarySection>();
        if (this.Libs != null)
        {
            foreach (var lib in this.Libs)
            {
                if (lib.SectionContributions is null)
                {
                    continue;
                }

                foreach (var section in lib.SectionContributions.Keys)
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

    public IEnumerable<COFFGroup> COFFGroupsInLibs()
    {
        var coffGroupsAlreadySeen = new List<COFFGroup>();
        if (this.Libs != null)
        {
            foreach (var lib in this.Libs)
            {
                if (lib.COFFGroupContributions is null)
                {
                    continue;
                }

                foreach (var coffGroup in lib.COFFGroupContributions.Keys)
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
                this.ShouldDisplaySize ? "Lib Total Size on Disk" : "Lib Total Size in Memory"
            };

        var binarySections = BinarySectionsInLibs().Where(s => this.ShouldDisplaySize ? s.Size > 0 : s.VirtualSize > 0).OrderBy(s => s.Name).ToList();
        var coffGroups = COFFGroupsInLibs().Where(cg => this.ShouldDisplaySize ? cg.Size > 0 : cg.VirtualSize > 0).OrderBy(cg => cg.Name).ToList();

        foreach (var section in binarySections)
        {
            columnHeadersList.Add($"Section: {section.Name}");
        }

        foreach (var coffGroup in coffGroups)
        {
            columnHeadersList.Add($"COFF Group: {coffGroup.Name}");
        }

        columnHeaders = columnHeadersList.ToArray();

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.Libs?.Count ?? 1);
        if (this.Libs != null)
        {
            foreach (var lib in this.Libs)
            {
                if (this.ShouldDisplaySize && lib.Size == 0)
                {
                    continue;
                }

                if (this.ShouldDisplayVirtualSize && lib.VirtualSize == 0)
                {
                    continue;
                }

                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Lib Name", lib.Name },
                    { "Lib Short Name", lib.ShortName },
                    { this.ShouldDisplaySize ? "Lib Total Size on Disk" : "Lib Total Size in Memory", this.ShouldDisplaySize ? lib.Size : lib.VirtualSize }
                };

                foreach (var contrib in lib.SectionContributions.Where(sc => this.ShouldDisplayVirtualSize ? sc.Value.VirtualSize > 0 : sc.Value.Size > 0))
                {
                    formattedData.Add($"Section: {contrib.Key.Name}",
                                      this.ShouldDisplaySize ? lib.SectionContributions[contrib.Key].Size : lib.SectionContributions[contrib.Key].VirtualSize);
                }

                foreach (var contrib in lib.COFFGroupContributions.Where(sc => this.ShouldDisplayVirtualSize ? sc.Value.VirtualSize > 0 : sc.Value.Size > 0))
                {
                    formattedData.Add($"COFF Group: {contrib.Key.Name}",
                                      this.ShouldDisplaySize ? lib.COFFGroupContributions[contrib.Key].Size : lib.COFFGroupContributions[contrib.Key].VirtualSize);
                }

                preformattedData.Add(formattedData);
            }
        }
    }
}
