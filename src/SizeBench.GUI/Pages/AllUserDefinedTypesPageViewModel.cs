using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllUserDefinedTypesPageViewModel : SingleBinaryViewModelBase
{
    public sealed class UDTUIGrouping
    {
        public string Name { get; }
        public int CountOfTypes { get; }
        public uint TotalSizeOfFunctions { get; }
        public object LinkTarget { get; }

        public UDTUIGrouping(TemplatedUserDefinedTypeSymbol templatedUDT, uint totalSizeOfFunctions)
        {
            this.LinkTarget = templatedUDT;
            this.Name = templatedUDT.TemplateName;
            this.CountOfTypes = templatedUDT.UserDefinedTypes.Count;
            this.TotalSizeOfFunctions = totalSizeOfFunctions;
        }

        public UDTUIGrouping(UserDefinedTypeSymbol udt, IReadOnlyList<IFunctionCodeSymbol> udtFunctions)
        {
            this.LinkTarget = udt;
            this.Name = udt.Name;
            this.CountOfTypes = 1;
            this.TotalSizeOfFunctions = (uint)udtFunctions.Sum(fn => fn.Size);
        }
    }

    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<UserDefinedTypeGrouping>? _udtGroupings;

    private List<UDTUIGrouping> _udtUIGroupings = new List<UDTUIGrouping>();
    public List<UDTUIGrouping> UDTGroupings
    {
        get => this._udtUIGroupings;
        private set { this._udtUIGroupings = value; RaisePropertyChanged(); }
    }

    private bool _showEachTemplateExpansionSeparately;

    public bool ShowEachTemplateExpansionSeparately
    {
        get => this._showEachTemplateExpansionSeparately;
        set
        {
            this._showEachTemplateExpansionSeparately = value;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed - we cannot await in a property setter, and it's ok if this happens async and the fragment gets updated 'early'
            GroupUDTs(CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            OnRequestFragmentNavigation(this._showEachTemplateExpansionSeparately ? nameof(this.ShowEachTemplateExpansionSeparately) : String.Empty);
            RaisePropertyChanged();
        }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllUserDefinedTypesPageViewModel(IUITaskScheduler taskScheduler,
                                            ISession session,
                                            IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected override Task OnCurrentFragmentChanged()
    {
        this.ShowEachTemplateExpansionSeparately = this.CurrentFragment == nameof(this.ShowEachTemplateExpansionSeparately);
        return Task.CompletedTask;
    }

    protected internal override async Task InitializeAsync()
    {
        if (this.CurrentFragment == nameof(this.ShowEachTemplateExpansionSeparately))
        {
            this.ShowEachTemplateExpansionSeparately = true;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating User-Defined Types",
            async (token) =>
            {
                this._udtGroupings = await this.Session.EnumerateAllUserDefinedTypeGroupings(token);
                await GroupUDTs(token);
            });
    }

    private async Task GroupUDTs(CancellationToken token)
    {
        if (this._udtGroupings is null)
        {
            // We haven't yet loaded the UDTs, we'll do that eventually and then call this again
            return;
        }

        var uiGroupings = new List<UDTUIGrouping>();
        if (this.ShowEachTemplateExpansionSeparately)
        {
            foreach (var grouping in this._udtGroupings)
            {
                if (grouping.UserDefinedType != null)
                {
                    uiGroupings.Add(new UDTUIGrouping(grouping.UserDefinedType, await grouping.UserDefinedType.GetFunctionsAsync(token)));
                }
                else if (grouping.TemplatedUserDefinedType != null)
                {
                    // This is a template group, but we want to show each instantiation separately, so expand them
                    foreach (var udt in grouping.TemplatedUserDefinedType.UserDefinedTypes)
                    {
                        uiGroupings.Add(new UDTUIGrouping(udt, await udt.GetFunctionsAsync(token)));
                    }
                }
            }
        }
        else
        {
            foreach (var grouping in this._udtGroupings)
            {
                if (grouping.UserDefinedType != null)
                {
                    uiGroupings.Add(new UDTUIGrouping(grouping.UserDefinedType, await grouping.UserDefinedType.GetFunctionsAsync(token)));
                }
                else if (grouping.TemplatedUserDefinedType != null)
                {
                    uint totalSizeOfFunctions = 0;
                    foreach (var udt in grouping.TemplatedUserDefinedType.UserDefinedTypes)
                    {
                        totalSizeOfFunctions += (uint)(await udt.GetFunctionsAsync(token)).Sum(fn => fn.Size);
                    }

                    uiGroupings.Add(new UDTUIGrouping(grouping.TemplatedUserDefinedType, totalSizeOfFunctions));
                }
            }
        }

        this.UDTGroupings = uiGroupings.OrderByDescending(uiGrouping => uiGrouping.TotalSizeOfFunctions).ToList();
    }

    #region Excel Export

    private async void ExportToExcel()
    {
        GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);
        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    public void GenerateFormattedDataForExcelExport(out string[] columnHeaders,
                                                    out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        if (this.ShowEachTemplateExpansionSeparately)
        {
            columnHeaders = new string[2]
            {
                "Type Name",
                "Total Size of Member Functions",
            };
        }
        else
        {
            columnHeaders = new string[3]
            {
                "Type Name",
                "# Types",
                "Total Size of Member Functions",
            };
        }

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.UDTGroupings.Count);
        foreach (var group in this.UDTGroupings)
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Type Name",  group.Name },
                    { "# Types", group.CountOfTypes },
                    { "Total Size of Member Functions", group.TotalSizeOfFunctions },
                };

            preformattedData.Add(formattedData);
        }
    }

    #endregion Excel Export
}
