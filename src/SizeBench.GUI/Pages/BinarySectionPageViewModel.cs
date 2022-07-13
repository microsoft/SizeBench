using System.Collections.ObjectModel;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Converters;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class BinarySectionPageViewModel : SingleBinaryViewModelBase
{
    private readonly IExcelExporter _excelExporter;
    private readonly IUITaskScheduler _uiTaskScheduler;

    private BinarySection? _binarySection;
    public BinarySection? BinarySection
    {
        get => this._binarySection;
        private set { this._binarySection = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<Library>? _libList;
    public IReadOnlyList<Library>? Libs
    {
        get => this._libList;
        private set { this._libList = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<Compiland>? _compilands;
    public IReadOnlyList<Compiland>? Compilands
    {
        get => this._compilands;
        private set { this._compilands = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<ISymbol>? _symbols;
    public IReadOnlyList<ISymbol>? Symbols
    {
        get => this._symbols;
        private set { this._symbols = value; RaisePropertyChanged(); }
    }

    public ObservableCollection<DataGridColumnDescription> DataGridColumnDescriptions { get; } = new ObservableCollection<DataGridColumnDescription>();

    public Func<object, bool> CompilandFilter => (item) =>
    {
        if (item is not Compiland c || this.BinarySection is null)
        {
            return false;
        }

        return c.SectionContributions.ContainsKey(this.BinarySection);
    };

    public Func<object, bool> LibFilter => (item) =>
    {
        if (item is not Library l || this.BinarySection is null)
        {
            return false;
        }

        return l.SectionContributions.ContainsKey(this.BinarySection);
    };

    public enum BinarySectionPageTabIndex
    {
        COFFGroupsTab = 0,
        LibsTab = 1,
        CompilandsTab = 2,
        SymbolsTab
    }
    private BinarySectionPageTabIndex _selectedTab = BinarySectionPageTabIndex.COFFGroupsTab;
    public int SelectedTab
    {
        get => (int)this._selectedTab;
        set
        {
            this._selectedTab = (BinarySectionPageTabIndex)value;
            StartLoadingStuffIfNecessary();
        }
    }

    public DelegateCommand ExportCOFFGroupsToExcelCommand { get; }
    public DelegateCommand ExportLibsToExcelCommand { get; }
    public DelegateCommand ExportCompilandsToExcelCommand { get; }
    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public BinarySectionPageViewModel(IUITaskScheduler uiTaskScheduler,
                                      IExcelExporter excelExporter,
                                      ISession session) : base(session)
    {
        this._excelExporter = excelExporter;
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportCOFFGroupsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.BinarySection?.COFFGroups));
        this.ExportLibsToExcelCommand = new DelegateCommand(ExportLibsToExcel);
        this.ExportCompilandsToExcelCommand = new DelegateCommand(ExportCompilandsToExcel);
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.Symbols));
    }

    protected internal override async Task InitializeAsync()
        => await LoadBinarySection();

    private async Task LoadBinarySection()
    {
        var sectionName = this.QueryString["BinarySection"];

        this.DataGridColumnDescriptions.Clear();
        this.DataGridColumnDescriptions.Add(new DataGridColumnDescription(
            header: $"Size on Disk in {sectionName}",
            propertyPath: $"SectionContributionsByName[{sectionName}].Size",
            valueConverter: SizeToFriendlySizeConverter.Instance,
            isRightAligned: true));

        this.DataGridColumnDescriptions.Add(new DataGridColumnDescription(
            header: $"Size in Memory in {sectionName}",
            propertyPath: $"SectionContributionsByName[{sectionName}].VirtualSize",
            valueConverter: SizeToFriendlySizeConverter.Instance,
            isRightAligned: true));

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding {sectionName} Binary Section",
            async (token) => this.BinarySection = await this.Session.LoadBinarySectionByName(sectionName, token));
    }

    private async void StartLoadingStuffIfNecessary()
    {
        if (this._selectedTab == BinarySectionPageTabIndex.LibsTab && this.Libs is null)
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating libs in the {this.QueryString["BinarySection"]} Binary Section",
                async (token) => this.Libs = await this.Session.EnumerateLibs(token));
        }
        else if (this._selectedTab == BinarySectionPageTabIndex.CompilandsTab && this.Compilands is null)
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating libs in the {this.QueryString["BinarySection"]} Binary Section",
                async (token) => this.Compilands = await this.Session.EnumerateCompilands(token));
        }
        else if (this._selectedTab == BinarySectionPageTabIndex.SymbolsTab && this.Symbols is null && this.BinarySection != null)
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in the {this.QueryString["BinarySection"]} Binary Section",
                async (token) => this.Symbols = await this.Session.EnumerateSymbolsInBinarySection(this.BinarySection, token));
        }
    }

    private async void ExportLibsToExcel()
    {
        if (this.BinarySection is null || this.Libs is null)
        {
            return;
        }

        var columnHeaders = new string[]
        {
                "Lib Name",
                "Lib Short Name",
                $"Size on Disk in {this.BinarySection.Name}",
                $"Size in Memory in {this.BinarySection.Name}",
        };

        var preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>();

        foreach (var lib in this.Libs.Where(l => l.SectionContributions.ContainsKey(this.BinarySection)))
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Lib Name", lib.Name },
                    { "Lib Short Name", lib.ShortName },
                    { $"Size on Disk in {this.BinarySection.Name}", lib.SectionContributions[this.BinarySection].Size },
                    { $"Size in Memory in {this.BinarySection.Name}", lib.SectionContributions[this.BinarySection].VirtualSize },
                };
            preformattedData.Add(formattedData);
        }

        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    private async void ExportCompilandsToExcel()
    {
        if (this.Compilands is null || this.BinarySection is null)
        {
            return;
        }

        var columnHeaders = new string[]
        {
                "Compiland Name",
                "Compiland Short Name",
                "Lib Name",
                "Lib Short Name",
                $"Size on Disk in {this.BinarySection.Name}",
                $"Size in Memory in {this.BinarySection.Name}",
        };

        var preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>();

        foreach (var compiland in this.Compilands.Where(c => c.SectionContributions.ContainsKey(this.BinarySection)))
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Compiland Name", compiland.Name },
                    { "Compiland Short Name", compiland.ShortName },
                    { "Lib Name", compiland.Lib.Name },
                    { "Lib Short Name", compiland.Lib.ShortName },
                    { $"Size on Disk in {this.BinarySection.Name}", compiland.SectionContributions[this.BinarySection].Size },
                    { $"Size in Memory in {this.BinarySection.Name}", compiland.SectionContributions[this.BinarySection].VirtualSize },
                };
            preformattedData.Add(formattedData);
        }

        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }
}
