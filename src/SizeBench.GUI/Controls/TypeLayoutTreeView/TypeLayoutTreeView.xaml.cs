using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Commands;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal class TypeLayoutItemViewModel : INotifyPropertyChanged
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"Type Layout: {this.TypeLayoutItem?.UserDefinedType.Name ?? "placeholder item"}";

    public TypeLayoutItemViewModel(TypeLayoutItem item, ISession session, bool shouldGenerateHyperlinks, bool initiallyExpanded = false)
    {
        this.TypeLayoutItem = item ?? throw new ArgumentNullException(nameof(item));
        this.ShouldGenerateHyperlinks = shouldGenerateHyperlinks;
        this.Expanded = initiallyExpanded;

        if (item.MemberLayouts != null)
        {
            foreach (var member in item.MemberLayouts)
            {
                this.Members.Add(new MemberViewModel(member!, session, shouldGenerateHyperlinks, initiallyExpanded));
            }
        }

        if (item.BaseTypeLayouts != null)
        {
            foreach (var baseType in item.BaseTypeLayouts)
            {
                this.BaseTypes.Add(new TypeLayoutItemViewModel(baseType, session, shouldGenerateHyperlinks, initiallyExpanded));
            }
        }
    }

    internal static TypeLayoutItemViewModel PlaceholderLoadingItem = new TypeLayoutItemViewModel();

    private TypeLayoutItemViewModel()
    {
        this.IsPlaceholderLoadingItem = true;
    }

    public bool IsPlaceholderLoadingItem { get; }

    public bool ShouldGenerateHyperlinks { get; }

    public TypeLayoutItem? TypeLayoutItem { get; }
    public ObservableCollection<TypeLayoutItemViewModel.MemberViewModel> Members { get; } = new ObservableCollection<MemberViewModel>();
    public ObservableCollection<TypeLayoutItemViewModel> BaseTypes { get; } = new ObservableCollection<TypeLayoutItemViewModel>();

    public decimal FirstOffsetOfBaseTypeOrMember
    {
        get
        {
            var minMemberOffset = this.Members?.Count > 0 ? this.Members.Min(m => m.Member.Offset) : 0;
            var minBaseTypeOffset = this.BaseTypes?.Count > 0 ? this.BaseTypes.Min(bt => bt.FirstOffsetOfBaseTypeOrMember) : 0;
            return Math.Min(minMemberOffset, minBaseTypeOffset);
        }
    }

    public IEnumerable BaseTypesAndMembers
    {
        get
        {
            // This may look like an unnecessarily complex order to return these in - but it's important for cases where a derived type is the first
            // one that introduces a virtual. In this case, the derived type inserts the vfptr, but it ends up at offset 0 in the type, before the
            // data members of the base type.  So we can't just do the simple thing of "return all base types in order, then all data members in order"
            // as one might expect.
            // A good test case for this is InterspersedBitfieldsTest_Derived_Withvfptr in CppTestCasesBefore.

            var membersInOrder = this.Members?.OrderBy(m => m.Member.Offset)
                                             ?.ThenBy(m => m.Member.IsBitField ? 0 : 1)
                                             ?.ToList();
            var nextMemberIndex = 0;

            if (this.BaseTypes != null)
            {
                foreach (var bt in this.BaseTypes)
                {
                    while (membersInOrder != null &&
                           membersInOrder.Count > 0 &&
                           nextMemberIndex < membersInOrder.Count &&
                           membersInOrder[nextMemberIndex].Member.Offset <= bt.FirstOffsetOfBaseTypeOrMember)
                    {
                        yield return membersInOrder[nextMemberIndex++];
                    }

                    // We've now returned all members that go before this type, so good to return this type now and continue on.
                    yield return bt;
                }
            }

            if (membersInOrder != null)
            {
                for (; nextMemberIndex < membersInOrder.Count; nextMemberIndex++)
                {
                    yield return membersInOrder[nextMemberIndex];
                }
            }
        }
    }

    private bool _expanded;
    public bool Expanded { get => this._expanded; set { this._expanded = value; FirePropertyChanged(); } }

    #region INPC Support

    public event PropertyChangedEventHandler? PropertyChanged;

    private void FirePropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion INPC Support

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal class MemberViewModel : INotifyPropertyChanged
    {
        [ExcludeFromCodeCoverage]
        private string DebuggerDisplay => $"Member Layout: {this.Member.Name}";

        private readonly ISession _session;

        public MemberViewModel(TypeLayoutItemMember member, ISession session, bool shouldGenerateHyperlinks, bool initiallyExpanded = false)
        {
            ArgumentNullException.ThrowIfNull(member);
            ArgumentNullException.ThrowIfNull(session);

            this.Member = member;
            this._session = session;
            this.ShouldGenerateHyperlinks = shouldGenerateHyperlinks;
            this.Expanded = initiallyExpanded;
            if (this.Member.Type?.CanLoadLayout == true)
            {
                this.ChildrenOfThisType.Add(TypeLayoutItemViewModel.PlaceholderLoadingItem);
            }
            else
            {
                this._childrenOfThisTypePopulated = true;
            }
        }

        // In the UI I think it's more desirable to show bitfields as at the same offset (to make it clear they're occupying the same bytes)
        // so this strips away the part of the offset that's due to the bitfield's bits.
        public uint OffsetExcludingBitfield
        {
            get
            {
                if (this.Member.IsBitField)
                {
                    return (uint)(this.Member.Offset - (0.125m * this.Member.BitStartPosition));
                }
                else
                {
                    return (uint)this.Member.Offset;
                }
            }
        }

        public TypeLayoutItemMember Member { get; }

        private bool _childrenOfThisTypePopulated;
        public ObservableCollection<TypeLayoutItemViewModel> ChildrenOfThisType { get; } = new ObservableCollection<TypeLayoutItemViewModel>();

        public bool ShouldGenerateHyperlinks { get; }

        private bool _expanded;

        public bool Expanded
        {
            get => this._expanded;
            set
            {
                this._expanded = value; FirePropertyChanged();
                if (this._expanded && !this._childrenOfThisTypePopulated)
                {
                    PopulateChildrenOfThisType();
                    this._childrenOfThisTypePopulated = true;
                }
            }
        }

        private async void PopulateChildrenOfThisType()
        {
            // When we are expanding an entire tree by default (like on the UserDefinedTypeSymbolPage) we may try to set Expanded=true on a primitive type like an int.
            // So if this is not a type that can load a layout, we won't attempt it, there's nothing to find anyway.
            if (this.Member.Type?.CanLoadLayout != true)
            {
                return;
            }

            // This should be pretty fast at this point, so let's skip plumbing cancellation through fully for now.  If this is a bad
            // UX later, go ahead and plumb this to a progress window with cancellation if necessary.
            var childrenLayout = await this._session.LoadMemberTypeLayout(this.Member, CancellationToken.None);

            this.ChildrenOfThisType.Clear(); // Remove the 'loading...' item
            this.ChildrenOfThisType.Add(new TypeLayoutItemViewModel(childrenLayout, this._session, this.ShouldGenerateHyperlinks, initiallyExpanded: false));
        }

        #region INPC Support

        public event PropertyChangedEventHandler? PropertyChanged;

        private void FirePropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion INPC Support
    }
}

// This is ok to exclude since it's just registering DependencyProperties and setting up data context, not exactly riveting stuff.
// This type should never contain real logic!
[ExcludeFromCodeCoverage]
internal partial class TypeLayoutTreeView : UserControl
{
    #region InitiallyExpanded Property

    // Being lazy and not doing all the correct change notifications or using ISupportInitialize or any of that - this must be set before ItemsSource for it to work.
    public static readonly DependencyProperty InitiallyExpandedProperty = DependencyProperty.Register(nameof(InitiallyExpandedProperty), typeof(bool), typeof(TypeLayoutTreeView), new PropertyMetadata(false));

    public bool InitiallyExpanded
    {
        get => (bool)GetValue(InitiallyExpandedProperty);
        set => SetValue(InitiallyExpandedProperty, value);
    }

    #endregion

    #region ItemsSource Property

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TypeLayoutTreeView), new PropertyMetadata(null /* default value */, ItemsSourceChanged));

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void ItemsSourceChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        => ((TypeLayoutTreeView)depObj).ItemsSourceChanged(e);

    private void ItemsSourceChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged oldINCC)
        {
            oldINCC.CollectionChanged -= ItemsSourceINCC;
        }
        if (e.NewValue is INotifyCollectionChanged newINCC)
        {
            newINCC.CollectionChanged += ItemsSourceINCC;
        }

        SetupItemViewModels();
    }

    private void SetupItemViewModels()
    {
        var itemsSourceAsViewModel = new ObservableCollection<TypeLayoutItemViewModel>();
        foreach (TypeLayoutItem? item in this.ItemsSource)
        {
            itemsSourceAsViewModel.Add(new TypeLayoutItemViewModel(item!, this.SessionForTypeExpansion, this.TypeLinkClickedCommand != null, this.InitiallyExpanded));
        }

        if (itemsSourceAsViewModel.Count == 1)
        {
            itemsSourceAsViewModel[0].Expanded = true;
        }

        this.ItemsSourceAsViewModel = itemsSourceAsViewModel;
    }

    private void ItemsSourceINCC(object? sender, NotifyCollectionChangedEventArgs args)
        => SetupItemViewModels();

    public static readonly DependencyProperty ItemsSourceAsViewModelProperty = DependencyProperty.Register(nameof(ItemsSourceAsViewModel), typeof(ObservableCollection<TypeLayoutItemViewModel>), typeof(TypeLayoutTreeView), new PropertyMetadata(null /* default value */));

    public ObservableCollection<TypeLayoutItemViewModel> ItemsSourceAsViewModel
    {
        get => (ObservableCollection<TypeLayoutItemViewModel>)GetValue(ItemsSourceAsViewModelProperty);
        private set => SetValue(ItemsSourceAsViewModelProperty, value);
    }

    #endregion ItemsSource Property

    #region SessionForTypeExpansion Property

    public static readonly DependencyProperty SessionForTypeExpansionProperty = DependencyProperty.Register(nameof(SessionForTypeExpansion), typeof(Session), typeof(TypeLayoutTreeView), new PropertyMetadata(null /* default value */));

    public Session SessionForTypeExpansion
    {
        get => (Session)GetValue(SessionForTypeExpansionProperty);
        set => SetValue(SessionForTypeExpansionProperty, value);
    }

    #endregion SessionForTypeExpansion Property

    #region TypeLinkClickedCommand Property

    public static readonly DependencyProperty TypeLinkClickedCommandProperty = DependencyProperty.Register(nameof(TypeLinkClickedCommand), typeof(DelegateCommand<TypeSymbol>), typeof(TypeLayoutTreeView), new PropertyMetadata(null /* default value */));

    public DelegateCommand<TypeSymbol> TypeLinkClickedCommand
    {
        get => (DelegateCommand<TypeSymbol>)GetValue(TypeLinkClickedCommandProperty);
        set => SetValue(TypeLinkClickedCommandProperty, value);
    }

    #endregion TypeLinkClickedCommand Property

    public TypeLayoutTreeView()
    {
        InitializeComponent();
        this.LayoutRoot.DataContext = this;
    }
}
