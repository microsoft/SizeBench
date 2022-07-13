using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class TemplatedUserDefinedTypeSymbolPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    public sealed class UDTViewModel
    {
        public uint TotalSizeOfFunctions { get; }
        public UserDefinedTypeSymbol UDT { get; }

        public UDTViewModel(UserDefinedTypeSymbol udt, IReadOnlyList<IFunctionCodeSymbol> udtFunctions)
        {
            this.UDT = udt;
            this.TotalSizeOfFunctions = (uint)udtFunctions.Sum(fn => fn.Size);
        }
    }

    private List<UDTViewModel> _udts = new List<UDTViewModel>();
    public List<UDTViewModel> UDTs
    {
        get => this._udts;
        set { this._udts = value; RaisePropertyChanged(); }
    }

    private TemplatedUserDefinedTypeSymbol? _templatedUDT;
    public TemplatedUserDefinedTypeSymbol? TemplatedUDT
    {
        get => this._templatedUDT;
        set { this._templatedUDT = value; RaisePropertyChanged(); }
    }

    private async Task SetTemplatedUDT(TemplatedUserDefinedTypeSymbol? templatedUDT, CancellationToken token)
    {
        this.TemplatedUDT = templatedUDT;

        if (templatedUDT != null)
        {
            var udts = new List<UDTViewModel>(capacity: templatedUDT.UserDefinedTypes.Count);

            foreach (var udt in templatedUDT.UserDefinedTypes)
            {
                udts.Add(new UDTViewModel(udt, await udt.GetFunctionsAsync(token)));
            }

            this.UDTs = udts.OrderByDescending(udtVM => udtVM.TotalSizeOfFunctions).ToList();
        }
    }

    private string _pageTitle = "Templated User Defined Type";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public TemplatedUserDefinedTypeSymbolPageViewModel(IUITaskScheduler uiTaskScheduler,
                                                       ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var templateName = this.QueryString["TemplateName"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up templated type '{templateName}'",
            async (token) =>
            {
                var allUDTGroupings = await this.Session.EnumerateAllUserDefinedTypeGroupings(token);
                await SetTemplatedUDT(allUDTGroupings.FirstOrDefault(group => group.TemplatedUserDefinedType?.TemplateName == templateName)?.TemplatedUserDefinedType, token);
            });

        if (this.TemplatedUDT != null)
        {
            this.PageTitle = $"Templated User Defined Type: {this.TemplatedUDT.TemplateName}";
        }
    }
}
