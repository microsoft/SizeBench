using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class ContributionDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private ContributionDiff? _contributionDiff;
    public ContributionDiff? ContributionDiff
    {
        get => this._contributionDiff;
        private set { this._contributionDiff = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<SymbolDiff>? _symbolDiffs;
    public IReadOnlyList<SymbolDiff>? SymbolDiffs
    {
        get => this._symbolDiffs;
        private set { this._symbolDiffs = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public ContributionDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                         IExcelExporter excelExporter,
                                         IDiffSession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportToExcelCommand = new DelegateCommand(async () =>
        {
            if (this.SymbolDiffs != null)
            {
                await uiTaskScheduler.StartExcelExport(excelExporter, this.SymbolDiffs.ToList());
            }
        });
    }

    protected internal override async Task InitializeAsync()
    {
        var libName = this.QueryString.ContainsKey("Lib") ? this.QueryString["Lib"] : null;
        var binarySectionName = this.QueryString.ContainsKey("BinarySection") ? this.QueryString["BinarySection"] : null;
        var compilandName = this.QueryString.ContainsKey("Compiland") ? this.QueryString["Compiland"] : null;
        var coffGroupName = this.QueryString.ContainsKey("COFFGroup") ? this.QueryString["COFFGroup"] : null;

        if (String.IsNullOrEmpty(libName) && String.IsNullOrEmpty(compilandName))
        {
            throw new InvalidOperationException("A ContributionDiff must have either a Lib or a Compiland!");
        }

        if (String.IsNullOrEmpty(binarySectionName) && String.IsNullOrEmpty(coffGroupName))
        {
            throw new InvalidOperationException("A ContributionDiff must have either a BinarySection or a COFFGroup!");
        }

        if (!String.IsNullOrEmpty(compilandName))
        {
            if (!String.IsNullOrEmpty(binarySectionName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution diff of {compilandName} to {binarySectionName}", async (token) =>
                {
                    var compiland = (from c in await this.DiffSession.EnumerateCompilandDiffs(token)
                                     where c.Name == compilandName && c.LibDiff.Name == libName
                                     select c).FirstOrDefault();
                    this.ContributionDiff = compiland?.SectionContributionDiffsByName[binarySectionName];
                });
            }
            else if (!String.IsNullOrEmpty(coffGroupName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution diff of {compilandName} to {coffGroupName}", async (token) =>
                {
                    var compiland = (from c in await this.DiffSession.EnumerateCompilandDiffs(token)
                                     where c.Name == compilandName && c.LibDiff.Name == libName
                                     select c).FirstOrDefault();
                    this.ContributionDiff = compiland?.COFFGroupContributionDiffsByName[coffGroupName];
                });
            }
        }
        else if (!String.IsNullOrEmpty(libName))
        {
            if (!String.IsNullOrEmpty(binarySectionName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution diff of {libName} to {binarySectionName}", async (token) =>
                {
                    var lib = (from l in await this.DiffSession.EnumerateLibDiffs(token)
                               where l.Name == libName
                               select l).FirstOrDefault();
                    this.ContributionDiff = lib?.SectionContributionDiffsByName[binarySectionName];
                });
            }
            else if (!String.IsNullOrEmpty(coffGroupName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution diff of {libName} to {coffGroupName}", async (token) =>
                {
                    var lib = (from l in await this.DiffSession.EnumerateLibDiffs(token)
                               where l.Name == libName
                               select l).FirstOrDefault();
                    this.ContributionDiff = lib?.COFFGroupContributionDiffsByName[coffGroupName];
                });
            }
        }

        // It's possible for this to be null, if the user cancels.
        if (this.ContributionDiff is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbol diffs in {this.ContributionDiff.Name}",
            async (token) => this.SymbolDiffs = await this.DiffSession.EnumerateSymbolDiffsInContributionDiff(this.ContributionDiff, token));
    }
}
