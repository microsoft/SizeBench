using System.ComponentModel;
using System.Windows.Data;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class COFFGroupPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;

    private COFFGroup? _coffGroup;
    public COFFGroup? COFFGroup
    {
        get => this._coffGroup;
        private set
        {
            this._coffGroup = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.ContributionSizeSortMemberPath));
            RaisePropertyChanged(nameof(this.ContributionVirtualSizeSortMemberPath));
        }
    }

    public string ContributionSizeSortMemberPath => $"COFFGroupContributionsByName[{this.COFFGroup?.Name}].Size";
    public string ContributionVirtualSizeSortMemberPath => $"COFFGroupContributionsByName[{this.COFFGroup?.Name}].VirtualSize";

    private IReadOnlyList<ISymbol>? _symbols;
    public IReadOnlyList<ISymbol>? Symbols
    {
        get => this._symbols;
        private set { this._symbols = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<Library>? _libs;
    private CollectionView? _libsCV;
    public CollectionView? Libs
    {
        get => this._libsCV;
        set { this._libsCV = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<Compiland>? _compilands;
    private CollectionView? _compilandsCV;
    public CollectionView? Compilands
    {
        get => this._compilandsCV;
        set { this._compilandsCV = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public COFFGroupPageViewModel(IUITaskScheduler uiTaskScheduler,
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
        var coffGroupName = this.QueryString["COFFGroup"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Loading COFF Group {coffGroupName}", async (token) =>
        {
            this.COFFGroup = (from bs in await this.Session.EnumerateBinarySectionsAndCOFFGroups(token)
                              from cg in bs.COFFGroups
                              where cg.Name == coffGroupName
                              select cg).FirstOrDefault();
        });

        // It's possible for this to be null, if the user cancels.
        if (this.COFFGroup is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in COFF Group {this.COFFGroup.Name}",
            async (token) => this.Symbols = await this.Session.EnumerateSymbolsInCOFFGroup(this.COFFGroup, token));

        await this._uiTaskScheduler.StartLongRunningUITask("Enumerating libs", async (token) =>
        {
            this._libs = await this.Session.EnumerateLibs(token);
            this.Libs = (CollectionView)CollectionViewSource.GetDefaultView(this._libs);
            this.Libs.Filter = (lib) => ((Library)lib).COFFGroupContributions.ContainsKey(this.COFFGroup);
            this.Libs.SortDescriptions.Add(new SortDescription(this.ContributionSizeSortMemberPath, ListSortDirection.Descending));
            this.Libs.SortDescriptions.Add(new SortDescription(this.ContributionVirtualSizeSortMemberPath, ListSortDirection.Descending));
        });

        await this._uiTaskScheduler.StartLongRunningUITask("Enumerating compilands", async (token) =>
        {
            this._compilands = await this.Session.EnumerateCompilands(token);
            this.Compilands = (CollectionView)CollectionViewSource.GetDefaultView(this._compilands);
            this.Compilands.Filter = (compiland) => ((Compiland)compiland).COFFGroupContributions.ContainsKey(this.COFFGroup);
            this.Compilands.SortDescriptions.Add(new SortDescription(this.ContributionSizeSortMemberPath, ListSortDirection.Descending));
            this.Compilands.SortDescriptions.Add(new SortDescription(this.ContributionVirtualSizeSortMemberPath, ListSortDirection.Descending));
        });
    }

    private async void ExportToExcel()
        => await this._uiTaskScheduler.StartExcelExport(this._excelExporter, this.Symbols);
}
