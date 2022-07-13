using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace SizeBench.GUI.Controls;

// Taken from https://stackoverflow.com/a/45627524
// Very hard to unit test UI stuff like this
[ExcludeFromCodeCoverage]
public class SelectableTextBlock : TextBlock
{
    static SelectableTextBlock()
    {
        FocusableProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata(true));
        TextEditorWrapper.RegisterCommandHandlers(typeof(SelectableTextBlock), true, true, true);

        // remove the focus rectangle around the control
        FocusVisualStyleProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata((object?)null));
    }

#pragma warning disable IDE0052 // Remove unread private members - the mere act of creating this and holding onto it is what enables select-ability
    private readonly TextEditorWrapper _editor;
#pragma warning restore IDE0052 // Remove unread private members

    public SelectableTextBlock()
    {
        this._editor = TextEditorWrapper.CreateFor(this);
    }
}
