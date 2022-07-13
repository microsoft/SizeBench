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
internal class TypeLayoutItemDiffViewModel : INotifyPropertyChanged
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"Type Layout Diff: {this.TypeLayoutItemDiff?.UserDefinedType.Name ?? "placeholder item"}";

    public TypeLayoutItemDiffViewModel(TypeLayoutItemDiff item, IDiffSession session)
    {
        this.TypeLayoutItemDiff = item ?? throw new ArgumentNullException(nameof(item));

        foreach (var member in item.MemberDiffs)
        {
            this.Members.Add(new MemberDiffViewModel(member, session));
        }

        if (item.BaseTypeDiffs != null)
        {
            foreach (var baseType in item.BaseTypeDiffs)
            {
                this.BaseTypes.Add(new TypeLayoutItemDiffViewModel(baseType, session));
            }
        }
    }

    internal static TypeLayoutItemDiffViewModel PlaceholderLoadingItem = new TypeLayoutItemDiffViewModel();

    private TypeLayoutItemDiffViewModel()
    {
        this.IsPlaceholderLoadingItem = true;
    }

    public bool IsPlaceholderLoadingItem { get; }

    public TypeLayoutItemDiff? TypeLayoutItemDiff { get; }
    public ObservableCollection<TypeLayoutItemDiffViewModel.MemberDiffViewModel> Members { get; } = new ObservableCollection<MemberDiffViewModel>();
    public ObservableCollection<TypeLayoutItemDiffViewModel> BaseTypes { get; } = new ObservableCollection<TypeLayoutItemDiffViewModel>();

    public decimal FirstOffsetOfBaseTypeOrMember
    {
        get
        {
            var minMemberOffset = this.Members?.Count > 0 ? this.Members.Min(m => m.Member.AfterMember?.Offset ?? 0) : 0;
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

            var membersInOrder = this.Members?.OrderBy(m => m.Member.AfterMember?.Offset ?? m.Member.BeforeMember?.Offset ?? 0)
                                             ?.ThenBy(m => m.Member.AfterMember?.IsBitField == true ? 0 : 1)
                                             ?.ToList();
            var nextMemberIndex = 0;

            if (this.BaseTypes != null)
            {
                foreach (var bt in this.BaseTypes)
                {
                    while (membersInOrder != null &&
                           membersInOrder.Count > 0 &&
                           nextMemberIndex < membersInOrder.Count &&
                           membersInOrder[nextMemberIndex].Member.AfterMember?.Offset <= bt.FirstOffsetOfBaseTypeOrMember)
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
    internal class MemberDiffViewModel : INotifyPropertyChanged
    {
        [ExcludeFromCodeCoverage]
        private string DebuggerDisplay => $"Member Layout Diff: {this.Member.Name}";

        private readonly IDiffSession _session;

        public MemberDiffViewModel(TypeLayoutItemMemberDiff member, IDiffSession session)
        {
            ArgumentNullException.ThrowIfNull(member);
            ArgumentNullException.ThrowIfNull(session);

            this.Member = member;
            this._session = session;
            if (this.Member.BeforeMember?.Type?.CanLoadLayout == true ||
                this.Member.AfterMember?.Type?.CanLoadLayout == true)
            {
                this.ChildrenOfThisType.Add(TypeLayoutItemDiffViewModel.PlaceholderLoadingItem);
            }
            else
            {
                this._childrenOfThisTypePopulated = true;
            }
        }

        public TypeLayoutItemMemberDiff Member { get; }

        public TypeSymbol MemberType => this.Member.Type?.AfterSymbol ?? this.Member.Type?.BeforeSymbol!;

        private bool _childrenOfThisTypePopulated;
        public ObservableCollection<TypeLayoutItemDiffViewModel> ChildrenOfThisType { get; } = new ObservableCollection<TypeLayoutItemDiffViewModel>();

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
            // This should be pretty fast at this point, so let's skip plumbing cancellation through fully for now.  If this is a bad
            // UX later, go ahead and plumb this to a progress window with cancellation if necessary.

            var childrenLayout = await this._session.LoadMemberTypeLayoutDiff(this.Member, CancellationToken.None);

            this.ChildrenOfThisType.Clear(); // Remove the 'loading...' item
            this.ChildrenOfThisType.Add(new TypeLayoutItemDiffViewModel(childrenLayout, this._session));
        }

        // In the UI I think it's more desirable to show bitfields as at the same offset (to make it clear they're occupying the same bytes)
        // so this strips away the part of the offset that's due to the bitfield's bits.
        public uint OffsetExcludingBitfield
        {
            get
            {
                var memberToLookAt = this.Member.AfterMember ?? this.Member.BeforeMember!;

                if (memberToLookAt.IsBitField)
                {
                    return (uint)(memberToLookAt.Offset - (0.125m * memberToLookAt.BitStartPosition));
                }
                else
                {
                    return (uint)memberToLookAt.Offset;
                }
            }
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
internal partial class TypeLayoutDiffTreeView : UserControl
{
    #region ItemsSource Property

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TypeLayoutDiffTreeView), new PropertyMetadata(null /* default value */, ItemsSourceChanged));

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void ItemsSourceChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        => ((TypeLayoutDiffTreeView)depObj).ItemsSourceChanged(e);

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
        var itemsSourceAsViewModel = new ObservableCollection<TypeLayoutItemDiffViewModel>();
        foreach (TypeLayoutItemDiff? item in this.ItemsSource)
        {
            itemsSourceAsViewModel.Add(new TypeLayoutItemDiffViewModel(item!, this.DiffSessionForTypeExpansion));
        }

        // If there's just one, the user is clearly trying to look at this in detail, let's pre-expand it.
        if (itemsSourceAsViewModel.Count == 1)
        {
            itemsSourceAsViewModel[0].Expanded = true;
        }

        this.ItemsSourceAsViewModel = itemsSourceAsViewModel;
    }

    private void ItemsSourceINCC(object? sender, NotifyCollectionChangedEventArgs args)
        => SetupItemViewModels();

    public static readonly DependencyProperty ItemsSourceAsViewModelProperty = DependencyProperty.Register(nameof(ItemsSourceAsViewModel), typeof(ObservableCollection<TypeLayoutItemDiffViewModel>), typeof(TypeLayoutDiffTreeView), new PropertyMetadata(null /* default value */));

    public ObservableCollection<TypeLayoutItemDiffViewModel> ItemsSourceAsViewModel
    {
        get => (ObservableCollection<TypeLayoutItemDiffViewModel>)GetValue(ItemsSourceAsViewModelProperty);
        private set => SetValue(ItemsSourceAsViewModelProperty, value);
    }

    #endregion ItemsSource Property

    #region DiffSessionForTypeExpansion Property

    public static readonly DependencyProperty DiffSessionForTypeExpansionProperty = DependencyProperty.Register(nameof(DiffSessionForTypeExpansion), typeof(DiffSession), typeof(TypeLayoutDiffTreeView), new PropertyMetadata(null /* default value */));

    public DiffSession DiffSessionForTypeExpansion
    {
        get => (DiffSession)GetValue(DiffSessionForTypeExpansionProperty);
        set => SetValue(DiffSessionForTypeExpansionProperty, value);
    }

    #endregion DiffSessionForTypeExpansion Property

    #region TypeLinkClickedCommand Property

    public static readonly DependencyProperty TypeLinkClickedCommandProperty = DependencyProperty.Register(nameof(TypeLinkClickedCommand), typeof(DelegateCommand<SymbolDiff>), typeof(TypeLayoutDiffTreeView), new PropertyMetadata(null /* default value */));

    public DelegateCommand<SymbolDiff> TypeLinkClickedCommand
    {
        get => (DelegateCommand<SymbolDiff>)GetValue(TypeLinkClickedCommandProperty);
        set => SetValue(TypeLinkClickedCommandProperty, value);
    }

    #endregion TypeLinkClickedCommand Property

    public TypeLayoutDiffTreeView()
    {
        InitializeComponent();
        this.LayoutRoot.DataContext = this;
    }
}
