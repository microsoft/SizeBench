using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using SizeBench.GUI.Commands;

namespace SizeBench.GUI.Controls;

// It's difficult to unit test a UI type
[ExcludeFromCodeCoverage]
internal sealed class SizeBenchDataGridHyperlinkColumn : DataGridColumn
{
    static SizeBenchDataGridHyperlinkColumn()
    {
        SortMemberPathProperty.OverrideMetadata(typeof(SizeBenchDataGridHyperlinkColumn), new FrameworkPropertyMetadata(null, OnCoerceSortMemberPath));
    }

    public SizeBenchDataGridHyperlinkColumn()
    {
        this.IsReadOnly = true;
    }

    public override BindingBase? ClipboardContentBinding
    {
        get => base.ClipboardContentBinding ?? this.HyperlinkTextBinding;

        set => base.ClipboardContentBinding = value;
    }

    #region HyperlinkTextBinding

    private Binding? _hyperlinkTextBinding;

    public Binding? HyperlinkTextBinding
    {
        get => this._hyperlinkTextBinding;
        set
        {
            if (this._hyperlinkTextBinding != value)
            {
                this._hyperlinkTextBinding = value;
                CoerceValue(DataGridColumn.SortMemberPathProperty);
                OnHyperlinkTextBindingChanged();
            }
        }
    }

    private void OnHyperlinkTextBindingChanged() => NotifyPropertyChanged(nameof(this.HyperlinkTextBinding));

    private static object OnCoerceSortMemberPath(DependencyObject d, object baseValue)
    {
        var column = (SizeBenchDataGridHyperlinkColumn)d;
        var sortMemberPath = (string)baseValue;

        if (String.IsNullOrEmpty(sortMemberPath))
        {
            var bindingSortMemberPath = column.HyperlinkTextBinding?.Path.Path;
            if (!String.IsNullOrEmpty(bindingSortMemberPath))
            {
                sortMemberPath = bindingSortMemberPath;
            }
        }

        return sortMemberPath;
    }

    private void ApplyHyperlinkTextBinding(DependencyObject target, DependencyProperty property)
    {
        var binding = this.HyperlinkTextBinding;
        if (binding != null)
        {
            BindingOperations.SetBinding(target, property, binding);
        }
        else
        {
            BindingOperations.ClearBinding(target, property);
        }
    }

    #endregion

    #region HyperlinkToolTipBinding

    private Binding _commandParameterBinding = new Binding();

    public Binding CommandParameterBinding
    {
        get => this._commandParameterBinding;
        set
        {
            if (this._commandParameterBinding != value)
            {
                this._commandParameterBinding = value;
                OnCommandParameterBindingChanged();
            }
        }
    }

    private void OnCommandParameterBindingChanged() => NotifyPropertyChanged(nameof(this.CommandParameterBinding));

    #endregion

    #region HyperlinkToolTipBinding

    private Binding? _hyperlinkToolTipBinding;

    public Binding? HyperlinkToolTipBinding
    {
        get => this._hyperlinkToolTipBinding;
        set
        {
            if (this._hyperlinkToolTipBinding != value)
            {
                this._hyperlinkToolTipBinding = value;
                OnHyperlinkToolTipBindingChanged();
            }
        }
    }

    private void OnHyperlinkToolTipBindingChanged() => NotifyPropertyChanged(nameof(this.HyperlinkToolTipBinding));

    private void ApplyHyperlinkToolTipBinding(DependencyObject target, DependencyProperty property)
    {
        var binding = this.HyperlinkToolTipBinding;
        if (binding != null)
        {
            BindingOperations.SetBinding(target, property, binding);
        }
        else
        {
            BindingOperations.ClearBinding(target, property);
        }
    }

    #endregion

    #region Element Generation

    private static readonly SolidColorBrush BlueBrush = new SolidColorBrush(Colors.Blue);

    /// <summary>
    ///     Creates the visual tree for cells.
    /// </summary>
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        cell.Foreground = BlueBrush;

        var outerBlock = new TextBlock();
        var link = new Hyperlink()
        {
            Command = AppCommands.NavigateToModel
        };
        var inlineContainer = new InlineUIContainer();
        var innerContentPresenter = new ContentPresenter();

        outerBlock.Inlines.Add(link);
        link.Inlines.Add(inlineContainer);
        inlineContainer.Child = innerContentPresenter;

        var foregroundBinding = new Binding()
        {
            Path = new PropertyPath("Foreground"),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, ancestorType: typeof(DataGridCell), ancestorLevel: 1)
        };
        BindingOperations.SetBinding(link, Hyperlink.ForegroundProperty, foregroundBinding);

        BindingOperations.SetBinding(link, Hyperlink.CommandParameterProperty, this.CommandParameterBinding);

        ApplyHyperlinkTextBinding(innerContentPresenter, ContentPresenter.ContentProperty);
        ApplyHyperlinkToolTipBinding(link, Hyperlink.ToolTipProperty);

        return outerBlock;
    }

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem) => throw new NotImplementedException();

    #endregion
}
