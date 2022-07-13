using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class CompilandPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private Compiland? _compiland;
    public Compiland? Compiland
    {
        get => this._compiland;
        private set { this._compiland = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<ISymbol>? _symbols;
    public IReadOnlyList<ISymbol>? Symbols
    {
        get => this._symbols;
        set { this._symbols = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public CompilandPageViewModel(IUITaskScheduler uiTaskScheduler,
                                  IExcelExporter excelExporter,
                                  ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.Symbols));
    }

    protected internal override async Task InitializeAsync()
    {
        // Compilands are not guaranteed to be unique by their name alone, as two static libs can contain a compiland with the same name
        // in each one, so we need a combination of the compiland and lib names to uniquely identify a Compiland in a URI-friendly/deeplink-friendly way.
        var compilandName = this.QueryString["Compiland"];
        var libName = this.QueryString["Lib"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding {compilandName} Compiland",
            async (token) =>
            {
                var compilands = await this.Session.EnumerateCompilands(token);

                this.Compiland = (from c in compilands
                                  where c.Name == compilandName && c.Lib.Name == libName
                                  select c).FirstOrDefault();
            });

        // It's possible for this to be null, if the user cancels.
        if (this.Compiland is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in Compiland {this.Compiland.ShortName}",
            async (token) => this.Symbols = await this.Session.EnumerateSymbolsInCompiland(this.Compiland, token));
    }
}
