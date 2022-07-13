using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class LibPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private Library? _lib;
    public Library? Lib
    {
        get => this._lib;
        private set { this._lib = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<ISymbol>? _symbols;
    public IReadOnlyList<ISymbol>? Symbols
    {
        get => this._symbols;
        private set { this._symbols = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public LibPageViewModel(IUITaskScheduler uiTaskScheduler,
                            IExcelExporter excelExporter,
                            ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.Symbols));
    }

    protected internal override async Task InitializeAsync()
    {
        var libName = this.QueryString["Lib"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Lib {libName}",
            async (token) =>
            {
                var libs = await this.Session.EnumerateLibs(token);

                var results = from lib in libs
                              where lib.Name == libName
                              select lib;

                this.Lib = results.FirstOrDefault();
            });

        // It's possible the user canceled loading the Lib or something, so don't try to load symbols if the lib isn't around.
        if (this.Lib is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in {libName}",
            async (token) => this.Symbols = await this.Session.EnumerateSymbolsInLib(this.Lib, token));
    }
}
