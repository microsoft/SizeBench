using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class TypeLayoutPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;

    private IReadOnlyList<TypeLayoutItem> _typeLayoutItems = new List<TypeLayoutItem>();
    public IReadOnlyList<TypeLayoutItem> TypeLayoutItems
    {
        get => this._typeLayoutItems;
        private set
        {
            this._typeLayoutItems = value.Where(LayoutIsWorthViewingInTreeView).ToList();
            RaisePropertyChanged();
            this.PageTitle = this._typeLayoutItems.Count > 0 ? $"Type Layout: {this.TypeNameToLoad}" : "Type Layout";
            this.ExportToExcelCommand.RaiseCanExecuteChanged();
        }
    }

    private string? _typeNameToLoad;
    public string? TypeNameToLoad
    {
        get => this._typeNameToLoad;
        set
        {
            this._typeNameToLoad = value;
            RaisePropertyChanged();
        }
    }

    private string _pageTitle = "Type Layout";
    public string PageTitle
    {
        get => this._pageTitle;
        set
        {
            this._pageTitle = value;
            RaisePropertyChanged();
        }
    }

    public DelegateCommand ViewLayoutsOfSpecificTypesCommand { get; }

    public DelegateCommand<TypeSymbol> LoadTypeCommand { get; }

    public DelegateCommand ExportToExcelCommand { get; }

    public TypeLayoutPageViewModel(IUITaskScheduler taskScheduler,
                                   IExcelExporter excelExporter,
                                   ISession session) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ViewLayoutsOfSpecificTypesCommand = new DelegateCommand(async () => await LoadTypeLayoutsByName());
        this.LoadTypeCommand = new DelegateCommand<TypeSymbol>(LoadTypeLayoutByType);
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel, () => this.TypeLayoutItems?.Count > 0);
    }

    protected override Task OnCurrentFragmentChanged()
    {
        this.TypeNameToLoad = this.CurrentFragment;
        return LoadTypeLayoutsByName();
    }

    private static string ResolveProperTypeName(TypeSymbol typeSymbol)
    {
        if (typeSymbol is UserDefinedTypeSymbol)
        {
            return typeSymbol.Name;
        }
        else if (typeSymbol is PointerTypeSymbol ptrType)
        {
            return ResolveProperTypeName(ptrType.PointerTargetType);
        }
        else if (typeSymbol is ModifiedTypeSymbol modType)
        {
            return ResolveProperTypeName(modType.UnmodifiedTypeSymbol);
        }
        else if (typeSymbol is ArrayTypeSymbol arrType)
        {
            return ResolveProperTypeName(arrType.ElementType);
        }
        else
        {
            throw new InvalidOperationException("We shouldn't be trying to do this - how did we get here?");
        }
    }

    private async void LoadTypeLayoutByType(TypeSymbol typeSymbol)
    {
        this.TypeNameToLoad = ResolveProperTypeName(typeSymbol);
        await this._uiTaskScheduler.StartLongRunningUITask($"Loading Type Layout for {typeSymbol.Name}",
            async (token) =>
            {
                OnRequestFragmentNavigation(this.TypeNameToLoad);
                this.TypeLayoutItems = new List<TypeLayoutItem>() { await this.Session.LoadTypeLayout(typeSymbol, token) };
            });
    }

    private async Task LoadTypeLayoutsByName()
    {
        if (this.TypeNameToLoad == "*")
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Loading Type Layouts for all types",
            async (token) =>
            {
                OnRequestFragmentNavigation(this.TypeNameToLoad);
                this.TypeLayoutItems = await this.Session.LoadAllTypeLayouts(token);
            });
        }
        else if (!String.IsNullOrWhiteSpace(this.TypeNameToLoad))
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Loading Type Layout(s) for {this.TypeNameToLoad}",
                async (token) =>
                {
                    OnRequestFragmentNavigation(this.TypeNameToLoad);
                    this.TypeLayoutItems = await this.Session.LoadTypeLayoutsByName(this.TypeNameToLoad, token);
                });
        }
    }

    private static bool LayoutIsWorthViewingInTreeView(TypeLayoutItem typeLayout)
    {
        // We go ahead and hide types that are 0-sized as they would clog up the tree view,
        // and we hide types that have size == 1 with no data members except the default
        // tail slop alignment, since that's what things like an empty base class may show
        // up as, but again they're not very interesting at least as top-level entities for
        // a tree view UI.
        if (typeLayout.UserDefinedType.InstanceSize == 0)
        {
            return false;
        }

        if (typeLayout.UserDefinedType.InstanceSize == 1 &&
            typeLayout.MemberLayouts != null &&
            typeLayout.MemberLayouts.Count == 1 &&
            typeLayout.MemberLayouts[0].IsAlignmentMember)
        {
            return false;
        }

        return true;
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
        columnHeaders = new string[6]
        {
                "Type Name",
                "Instance Size",
                "Alignment Waste (exclusive)",
                "Alignment Waste (including base types)",
                "Used For vfptr (exclusive)",
                "Used For vfptr (including base types)",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.TypeLayoutItems.Count);
        foreach (var tli in this.TypeLayoutItems)
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Type Name", tli.UserDefinedType.Name },
                    { "Instance Size", tli.UserDefinedType.InstanceSize },
                    { "Alignment Waste (exclusive)", tli.AlignmentWasteExclusive },
                    { "Alignment Waste (including base types)", tli.AlignmentWasteIncludingBaseTypes },
                    { "Used For vfptr (exclusive)", tli.UsedForVFPtrsExclusive },
                    { "Used For vfptr (including base types)", tli.UsedForVFPtrsIncludingBaseTypes }
                };
            preformattedData.Add(formattedData);
        }
    }

    #endregion
}
