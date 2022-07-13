using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class UserDefinedTypeSymbolPageViewModel : SingleBinaryViewModelBase
{
    internal sealed class FunctionViewModel
    {
        public IFunctionCodeSymbol FunctionCodeSymbol { get; }
        public bool IsInFinalBinary { get; }
        public FunctionViewModel(IFunctionCodeSymbol function)
        {
            this.FunctionCodeSymbol = function;
            this.IsInFinalBinary = function.PrimaryBlock.RVA != 0;
        }
    }

    private readonly IUITaskScheduler _uiTaskScheduler;

    private TypeLayoutItem? _typeLayout;
    private IReadOnlyList<TypeLayoutItem> _typeLayoutItems = new List<TypeLayoutItem>();
    public IReadOnlyList<TypeLayoutItem> TypeLayoutItems
    {
        get => this._typeLayoutItems;
        private set
        {
            this._typeLayoutItems = value;
            this._typeLayout = value.SingleOrDefault(_ => true);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.UDT));
        }
    }

    public UserDefinedTypeSymbol? UDT => this._typeLayout?.UserDefinedType;

    private List<FunctionViewModel> _functions = new List<FunctionViewModel>();
    public List<FunctionViewModel> Functions
    {
        get => this._functions;
        private set
        {
            this._functions = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.TotalSizeOfAllFunctions));
        }
    }

    public static FunctionCodeNameFormatting FunctionNameFormatting => FunctionCodeNameFormatting.IncludeCVQualifiers | FunctionCodeNameFormatting.IncludeArgumentTypes;

    public uint TotalSizeOfAllFunctions => (uint)this._functions.Sum(fvm => fvm.FunctionCodeSymbol.Size);

    private string _pageTitle = "User Defined Type";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public UserDefinedTypeSymbolPageViewModel(IUITaskScheduler uiTaskScheduler,
                                              ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var udtName = this.QueryString["Name"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up user defined type '{udtName}'",
            async (token) =>
            {
                this.TypeLayoutItems = await this.Session.LoadTypeLayoutsByName(udtName, token);

                if (this.UDT != null)
                {
                    this.PageTitle = $"User Defined Type: {this.UDT.Name}";

                    this.Functions = await CreateFunctionViewModels(this.UDT, token);
                }
            });
    }

    private static async Task<List<FunctionViewModel>> CreateFunctionViewModels(UserDefinedTypeSymbol udt, CancellationToken token)
    {
        var functionVMs = new List<FunctionViewModel>();
        foreach (var function in await udt.GetFunctionsAsync(token))
        {
            functionVMs.Add(new FunctionViewModel(function));
        }

        return functionVMs.OrderByDescending(fnVM => fnVM.FunctionCodeSymbol.Size).ToList();
    }
}
