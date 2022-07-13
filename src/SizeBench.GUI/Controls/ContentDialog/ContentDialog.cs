using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SizeBench.GUI.Controls;

// Very hard to unit test UI stuff like this
[ExcludeFromCodeCoverage]
[TemplatePart(Name = "CommandSpace")]
[TemplatePart(Name = "Container")]
public sealed class ContentDialog : ContentControl
{
    private UIElement? _commandSpace;
    private bool IsUsingCommandSpace => this._commandSpace != null && !String.IsNullOrEmpty(this.PrimaryButtonText);

    static ContentDialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentDialog), new FrameworkPropertyMetadata(typeof(ContentDialog)));
    }


    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(object), typeof(ContentDialog), new PropertyMetadata(null));
    public object Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleTemplateProperty = DependencyProperty.Register("TitleTemplate", typeof(DataTemplate), typeof(ContentDialog), new PropertyMetadata(null));
    public DataTemplate TitleTemplate
    {
        get => (DataTemplate)GetValue(TitleTemplateProperty);
        set => SetValue(TitleTemplateProperty, value);
    }

    public static readonly DependencyProperty PrimaryButtonTextProperty = DependencyProperty.Register("PrimaryButtonText", typeof(string), typeof(ContentDialog), new PropertyMetadata(String.Empty, OnPrimaryButtonTextChanged));
    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    private static void OnPrimaryButtonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ContentDialog)d).UpdateVisualStates();

    public static readonly DependencyProperty PrimaryButtonCommandProperty = DependencyProperty.Register("PrimaryButtonCommand", typeof(ICommand), typeof(ContentDialog), new PropertyMetadata(null));
    public ICommand PrimaryButtonCommand
    {
        get => (ICommand)GetValue(PrimaryButtonCommandProperty);
        set => SetValue(PrimaryButtonCommandProperty, value);
    }

    public static readonly DependencyProperty ClosedByUserCommandProperty = DependencyProperty.Register("ClosedByUserCommand", typeof(ICommand), typeof(ContentDialog), new PropertyMetadata(null));
    public ICommand ClosedByUserCommand
    {
        get => (ICommand)GetValue(ClosedByUserCommandProperty);
        set => SetValue(ClosedByUserCommandProperty, value);
    }

    public static readonly DependencyProperty CloseButtonVisibilityProperty = DependencyProperty.Register("CloseButtonVisibility", typeof(Visibility), typeof(ContentDialog), new PropertyMetadata(Visibility.Visible));
    public Visibility CloseButtonVisibility
    {
        get => (Visibility)GetValue(CloseButtonVisibilityProperty);
        set => SetValue(CloseButtonVisibilityProperty, value);
    }

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(ContentDialog), new PropertyMetadata(false, OnIsOpenChanged));
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var that = (ContentDialog)d;
        if ((bool)e.NewValue == true)
        {
            that.OpenDialog();
        }
        else if ((bool)e.NewValue == false)
        {
            VisualStateManager.GoToState(that, "DialogHidden", useTransitions: true);
        }
    }

    private void OpenDialog()
    {
        // We need to tick once to ensure all the bindings are updated
        this.Dispatcher.BeginInvoke(new Action(() =>
        {
            // Force everything to lay out so the dialog transitions in nicely with the right size.
            UpdateLayout();

            // Now begin to go to the new state - the transitions will happen (if defined in the template), which may take some time, so
            // we need to wait for the CurrentStateChanged event to fire on the VisualStateGroup - only then can we know the dialog and/or
            // its command space are focusable.  So the final logic for opening the dialog is in DialogShowingStatesGroup_CurrentStateChanged
            VisualStateManager.GoToState(this, "DialogShowing", useTransitions: true);
        }));
    }

    private void DialogShowingStatesGroup_CurrentStateChanged(object? sender, VisualStateChangedEventArgs e)
    {
        if (e.NewState.Name == "DialogShowing")
        {
            if (this._commandSpace != null && this.IsUsingCommandSpace)
            {
                this._commandSpace.Focus();
            }
            else
            {
                Focus();
            }
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        this._commandSpace = GetTemplateChild("CommandSpace") as UIElement;

        if (GetTemplateChild("Container") is FrameworkElement container)
        {
            var vsGroups = VisualStateManager.GetVisualStateGroups(container);
            if (vsGroups != null)
            {
                foreach (var group in vsGroups)
                {
                    var vsg = group as VisualStateGroup;
                    if (vsg?.Name == "DialogShowingStates")
                    {
                        vsg.CurrentStateChanged += DialogShowingStatesGroup_CurrentStateChanged;
                    }
                }
            }
        }

        UpdateVisualStates();
    }

    private void UpdateVisualStates()
    {
        // Command Space
        if (this.IsUsingCommandSpace)
        {
            VisualStateManager.GoToState(this, "AllVisible", useTransitions: true);
        }
        else
        {
            VisualStateManager.GoToState(this, "NoneVisible", useTransitions: true);
        }
    }
}
