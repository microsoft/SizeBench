using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class CompilandDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private CompilandDiff? _compilandDiff;
    public CompilandDiff? CompilandDiff
    {
        get => this._compilandDiff;
        private set { this._compilandDiff = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<SymbolDiff>? _symbolDiffs;
    public IReadOnlyList<SymbolDiff>? SymbolDiffs
    {
        get => this._symbolDiffs;
        set { this._symbolDiffs = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public CompilandDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                      IExcelExporter excelExporter,
                                      IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.SymbolDiffs));
    }

    protected internal override async Task InitializeAsync()
    {
        // Compilands are not guaranteed to be unique by their name alone, as two static libs can contain a compiland with the same name
        // in each one, so we need a combination of the compiland and lib names to uniquely identify a Compiland in a URI-friendly/deeplink-friendly way.
        var compilandName = this.QueryString["Compiland"];
        var libName = this.QueryString["Lib"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Diff of {compilandName} Compiland",
            async (token) =>
            {
                this.CompilandDiff = (from c in await this.DiffSession.EnumerateCompilandDiffs(token)
                                      where c.Name == compilandName && c.LibDiff.Name == libName
                                      select c).FirstOrDefault();
            });

        // It's possible for this to be null, if the user cancels.
        if (this.CompilandDiff is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in Compiland {this.CompilandDiff.ShortName}",
            async (token) => this.SymbolDiffs = await this.DiffSession.EnumerateSymbolDiffsInCompilandDiff(this.CompilandDiff, token));
    }
}
