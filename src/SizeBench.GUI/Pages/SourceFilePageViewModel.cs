using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class SourceFilePageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private SourceFile? _sourceFile;
    public SourceFile? SourceFile
    {
        get => this._sourceFile;
        private set { this._sourceFile = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<ISymbol>? _symbols;
    public IReadOnlyList<ISymbol>? Symbols
    {
        get => this._symbols;
        set { this._symbols = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportSymbolsToExcelCommand { get; }

    public SourceFilePageViewModel(IUITaskScheduler uiTaskScheduler,
                                   IExcelExporter excelExporter,
                                   ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportSymbolsToExcelCommand = new DelegateCommand(async () => await uiTaskScheduler.StartExcelExport(excelExporter, this.Symbols));
    }

    protected internal override async Task InitializeAsync()
    {
        var sourceFileName = this.QueryString["SourceFile"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding {sourceFileName} SourceFile",
            async (token) =>
            {
                var sourceFiles = await this.Session.EnumerateSourceFiles(token);

                this.SourceFile = (from c in sourceFiles
                                   where c.Name == sourceFileName
                                   select c).FirstOrDefault();
            });

        // It's possible for this to be null, if the user cancels.
        if (this.SourceFile is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in SourceFile {this.SourceFile.ShortName}",
            async (token) => this.Symbols = await this.Session.EnumerateSymbolsInSourceFile(this.SourceFile, token));
    }
}
