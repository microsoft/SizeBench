using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class LibDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private LibDiff? _libDiff;
    public LibDiff? LibDiff
    {
        get => this._libDiff;
        private set { this._libDiff = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<SymbolDiff>? _symbolDiffs;
    public IReadOnlyList<SymbolDiff>? SymbolDiffs
    {
        get => this._symbolDiffs;
        private set { this._symbolDiffs = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public LibDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                IExcelExporter excelExporter,
                                IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.SymbolDiffs));
    }

    protected internal override async Task InitializeAsync()
    {
        var libName = this.QueryString["Lib"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Diff of Lib {libName}",
            async (token) =>
            {
                var libs = await this.DiffSession.EnumerateLibDiffs(token);

                var results = from lib in libs
                              where lib.Name == libName
                              select lib;

                this.LibDiff = results.FirstOrDefault();
            });

        // It's possible for this to be null, if the user cancels.
        if (this.LibDiff is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in {libName}",
            async (token) => this.SymbolDiffs = await this.DiffSession.EnumerateSymbolDiffsInLibDiff(this.LibDiff, token));
    }
}
