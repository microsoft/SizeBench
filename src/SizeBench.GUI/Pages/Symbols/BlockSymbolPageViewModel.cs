using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class BlockSymbolPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private CodeBlockSymbol? _block;
    public CodeBlockSymbol? Block
    {
        get => this._block;
        private set
        {
            this._block = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.ParentFunction));
            RaisePropertyChanged(nameof(this.IsSeparatedBlock));
        }
    }

    public bool IsSeparatedBlock => this._block is SeparatedCodeBlockSymbol;

    public IFunctionCodeSymbol? ParentFunction => this._block?.ParentFunction;

    public bool IsBlockCodeUsedForMultipleBlocks => this.FoldedBlocks?.Count > 1;

    private IReadOnlyList<ISymbol>? _foldedBlocks;
    public IReadOnlyList<ISymbol>? FoldedBlocks
    {
        get => this._foldedBlocks;
        set
        {
            this._foldedBlocks = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsBlockCodeUsedForMultipleBlocks));
        }
    }

    private BinarySection? _section;
    public BinarySection? BinarySection
    {
        get => this._section;
        private set { this._section = value; RaisePropertyChanged(); }
    }

    private COFFGroup? _coffGroup;
    public COFFGroup? COFFGroup
    {
        get => this._coffGroup;
        private set { this._coffGroup = value; RaisePropertyChanged(); }
    }

    private Compiland? _compiland;
    public Compiland? Compiland
    {
        get => this._compiland;
        private set { this._compiland = value; RaisePropertyChanged(); }
    }

    private Library? _lib;
    public Library? Lib
    {
        get => this._lib;
        private set { this._lib = value; RaisePropertyChanged(); }
    }

    private SourceFile? _sourceFile;
    public SourceFile? SourceFile
    {
        get => this._sourceFile;
        private set { this._sourceFile = value; RaisePropertyChanged(); }
    }

    private string _pageTitle = "Block Symbol";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public BlockSymbolPageViewModel(IUITaskScheduler uiTaskScheduler,
                                    ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var symbolRVA = Convert.ToUInt32(this.QueryString["RVA"], CultureInfo.InvariantCulture);

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up block symbol at {symbolRVA:X}",
            async (token) => this.Block = await this.Session.LoadSymbolByRVA(symbolRVA) as CodeBlockSymbol);

        // It's possible no symbol was found.  For example, if the Symbol was removed by /OPT:REF or /Gw, then it 
        // will have an RVA of 0 which will then cause the lookup above to fail.  Or it's possible that someone
        // tries to type an RVA into the address bar and fat-fingers it.
        // So in cases where we can't find a Symbol, let's just stop and show an empty UI, not crash trying to deref
        // the Symbol below.
        if (this.Block is null)
        {
            return;
        }

        this.PageTitle = $"Block Symbol: {this.Block.Name}";

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up the location of {this.Block.Name}", async (token) =>
        {
            var placement = await this.Session.LookupSymbolPlacementInBinary(this.Block, token);
            this.BinarySection = placement.BinarySection;
            this.COFFGroup = placement.COFFGroup;
            this.Lib = placement.Lib;
            this.Compiland = placement.Compiland;
            this.SourceFile = placement.SourceFile;
        });

        await this._uiTaskScheduler.StartLongRunningUITask("Looking up any blocks that may have folded with this one", async (token) =>
        {
            var foldedBlocks = (await this.Session.EnumerateAllSymbolsFoldedAtRVA(symbolRVA, token)).ToList();

            // The block we started with will of course be found, so we only care if at least 2 things were found.
            if (foldedBlocks.Count > 1)
            {
                this.FoldedBlocks = foldedBlocks.OrderBy(b => b.Name).ToList();
            }
        });
    }
}
