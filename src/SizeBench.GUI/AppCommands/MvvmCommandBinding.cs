using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;

namespace SizeBench.GUI.Commands;

// Taken from https://gist.github.com/vbfox/1445370
[ExcludeFromCodeCoverage]
internal class MvvmCommandBinding : Freezable
{
    private UIElement? _uiElement;
    private readonly CommandBinding _commandBinding;

    public MvvmCommandBinding()
    {
        this._commandBinding = new CommandBinding();

        this._commandBinding.CanExecute += OnCanExecute;
        this._commandBinding.Executed += OnExecute;
    }

    #region Command

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        "Command", typeof(ICommand), typeof(MvvmCommandBinding),
        new PropertyMetadata(null, OnCommandChanged));

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MvvmCommandBinding)d).OnCommandChanged((ICommand)e.NewValue);

    private void OnCommandChanged(ICommand newValue)
        => this._commandBinding.Command = newValue;

    [Bindable(true)]
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    #endregion

    #region Target

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        "Target", typeof(ICommand), typeof(MvvmCommandBinding),
        new PropertyMetadata(null, OnTargetChanged));

    [Bindable(true)]
    public ICommand Target
    {
        get => (ICommand)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MvvmCommandBinding)d).OnTargetChanged((ICommand)e.OldValue, (ICommand)e.NewValue);

    private void OnTargetChanged(ICommand oldValue, ICommand newValue)
    {
        if (oldValue != null)
        {
            oldValue.CanExecuteChanged -= OnTargetCanExecuteChanged;
        }

        if (newValue != null)
        {
            newValue.CanExecuteChanged += OnTargetCanExecuteChanged;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    #endregion

    #region CanExecuteChangedSuggestRequery

    public static readonly DependencyProperty CanExecuteChangedSuggestRequeryProperty
        = DependencyProperty.Register(
            "CanExecuteChangedSuggestRequery", typeof(bool), typeof(MvvmCommandBinding),
            new PropertyMetadata(false, OnCanExecuteChangedSuggestRequeryChanged));

    [Bindable(true)]
    public bool CanExecuteChangedSuggestRequery
    {
        get => (bool)GetValue(CanExecuteChangedSuggestRequeryProperty);
        set => SetValue(CanExecuteChangedSuggestRequeryProperty, value);
    }

    private static void OnCanExecuteChangedSuggestRequeryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    #endregion

    #region On event

    private void OnTargetCanExecuteChanged(object? _, EventArgs e)
    {
        if (this.CanExecuteChangedSuggestRequery)
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OnExecute(object sender, ExecutedRoutedEventArgs e)
    {
        if (this.Target is null)
        {
            return;
        }

        e.Handled = true;
        this.Target.Execute(e.Parameter);
    }

    private void OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (this.Target is null)
        {
            return;
        }

        e.Handled = true;
        e.CanExecute = false;

        e.CanExecute = this.Target.CanExecute(e.Parameter);
    }

    #endregion

    #region Attach / Detach

    internal void DetachFrom(UIElement uiDependencyObject)
    {
        ArgumentNullException.ThrowIfNull(uiDependencyObject);

        WritePreamble();

        if (uiDependencyObject != this._uiElement)
        {
            return;
        }

        Detach();
    }

    private void Detach()
    {
        if (this._uiElement is null)
        {
            return;
        }

        this._uiElement.CommandBindings.Remove(this._commandBinding);
        this._uiElement = null;
    }

    internal void AttachTo(UIElement uiDependencyObject)
    {
        ArgumentNullException.ThrowIfNull(uiDependencyObject);

        WritePreamble();

        if (this._uiElement != null)
        {
            Detach();
        }

        this._uiElement = uiDependencyObject;
        uiDependencyObject.CommandBindings.Add(this._commandBinding);
    }

    #endregion

    protected override Freezable CreateInstanceCore()
        => new MvvmCommandBinding();
}
