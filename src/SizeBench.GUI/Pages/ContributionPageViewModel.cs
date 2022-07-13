using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class ContributionPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private Contribution? _contribution;
    public Contribution? Contribution
    {
        get => this._contribution;
        private set { this._contribution = value; RaisePropertyChanged(); }
    }

    private IReadOnlyList<ISymbol>? _symbols;
    public IReadOnlyList<ISymbol>? Symbols
    {
        get => this._symbols;
        private set { this._symbols = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public ContributionPageViewModel(IUITaskScheduler uiTaskScheduler,
                                     IExcelExporter excelExporter,
                                     ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this.ExportToExcelCommand = new DelegateCommand(async () =>
        {
            if (this.Symbols != null)
            {
                await uiTaskScheduler.StartExcelExport(excelExporter, this.Symbols.ToList());
            }
        });
    }

    protected internal override async Task InitializeAsync()
    {
        var libName = this.QueryString.ContainsKey("Lib") ? this.QueryString["Lib"] : null;
        var binarySectionName = this.QueryString.ContainsKey("BinarySection") ? this.QueryString["BinarySection"] : null;
        var compilandName = this.QueryString.ContainsKey("Compiland") ? this.QueryString["Compiland"] : null;
        var coffGroupName = this.QueryString.ContainsKey("COFFGroup") ? this.QueryString["COFFGroup"] : null;
        var sourceFileName = this.QueryString.ContainsKey("SourceFile") ? this.QueryString["SourceFile"] : null;

        if (String.IsNullOrEmpty(libName) && String.IsNullOrEmpty(compilandName) && String.IsNullOrEmpty(sourceFileName))
        {
            throw new InvalidOperationException("A Contribution must have either a Lib, a Compiland, or a SourceFile!");
        }

        if (String.IsNullOrEmpty(binarySectionName) && String.IsNullOrEmpty(coffGroupName) &&
            (String.IsNullOrEmpty(sourceFileName) == true || (String.IsNullOrEmpty(sourceFileName) == false && String.IsNullOrEmpty(compilandName))))
        {
            throw new InvalidOperationException("A Contribution must have either a BinarySection or a COFFGroup, or be a SourceFile + Compiland!");
        }

        if (!String.IsNullOrEmpty(sourceFileName))
        {
            if (!String.IsNullOrEmpty(binarySectionName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {sourceFileName} to {binarySectionName}", async (token) =>
                {
                    var sourceFile = (from c in await this.Session.EnumerateSourceFiles(token)
                                      where c.Name == sourceFileName
                                      select c).FirstOrDefault();
                    this.Contribution = sourceFile?.SectionContributionsByName[binarySectionName];
                });
            }
            else if (!String.IsNullOrEmpty(coffGroupName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {sourceFileName} to {coffGroupName}", async (token) =>
                {
                    var sourceFile = (from c in await this.Session.EnumerateSourceFiles(token)
                                      where c.Name == sourceFileName
                                      select c).FirstOrDefault();
                    this.Contribution = sourceFile?.COFFGroupContributionsByName[coffGroupName];
                });
            }
            else if (!String.IsNullOrEmpty(compilandName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {sourceFileName} to {compilandName}", async (token) =>
                {
                    var sourceFile = (from c in await this.Session.EnumerateSourceFiles(token)
                                      where c.Name == sourceFileName
                                      select c).FirstOrDefault();

                    var compiland = sourceFile?.CompilandContributions.Keys.FirstOrDefault(c => c.Name == compilandName && c.Lib.Name == libName);

                    if (sourceFile != null && compiland != null)
                    {
                        this.Contribution = sourceFile.CompilandContributions[compiland];
                    }
                });
            }
        }
        else if (!String.IsNullOrEmpty(compilandName))
        {
            if (!String.IsNullOrEmpty(binarySectionName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {compilandName} to {binarySectionName}", async (token) =>
                {
                    var compiland = (from c in await this.Session.EnumerateCompilands(token)
                                     where c.Name == compilandName && c.Lib.Name == libName
                                     select c).FirstOrDefault();
                    this.Contribution = compiland?.SectionContributionsByName[binarySectionName];
                });
            }
            else if (!String.IsNullOrEmpty(coffGroupName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {compilandName} to {coffGroupName}", async (token) =>
                {
                    var compiland = (from c in await this.Session.EnumerateCompilands(token)
                                     where c.Name == compilandName && c.Lib.Name == libName
                                     select c).FirstOrDefault();
                    this.Contribution = compiland?.COFFGroupContributionsByName[coffGroupName];
                });
            }
        }
        else if (!String.IsNullOrEmpty(libName))
        {
            if (!String.IsNullOrEmpty(binarySectionName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {libName} to {binarySectionName}", async (token) =>
                {
                    var lib = (from l in await this.Session.EnumerateLibs(token)
                               where l.Name == libName
                               select l).FirstOrDefault();
                    this.Contribution = lib?.SectionContributionsByName[binarySectionName];
                });
            }
            else if (!String.IsNullOrEmpty(coffGroupName))
            {
                await this._uiTaskScheduler.StartLongRunningUITask($"Loading contribution of {libName} to {coffGroupName}", async (token) =>
                {
                    var lib = (from l in await this.Session.EnumerateLibs(token)
                               where l.Name == libName
                               select l).FirstOrDefault();
                    this.Contribution = lib?.COFFGroupContributionsByName[coffGroupName];
                });
            }
        }

        if (this.Contribution is null)
        {
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating symbols in {this.Contribution.Name}",
            async (token) => this.Symbols = await this.Session.EnumerateSymbolsInContribution(this.Contribution, token));
    }
}
