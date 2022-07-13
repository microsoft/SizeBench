using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;

namespace SizeBench.GUI.Commands;

// No point in testing this type, it's just a holder for some static bits of data that are never mutated
[ExcludeFromCodeCoverage]
public static partial class AppCommands
{
    public static RoutedUICommand OpenSingleBinary { get; internal set; } = new RoutedUICommand("Open Single Binary", "OpenSingleBinaryCommand", typeof(AppCommands), new InputGestureCollection() { new KeyGesture(Key.O, ModifierKeys.Alt) });

    public static RoutedUICommand OpenBinaryDiff { get; internal set; } = new RoutedUICommand("Open Binary Diff", "OpenBinaryDiffCommand", typeof(AppCommands), new InputGestureCollection() { new KeyGesture(Key.D, ModifierKeys.Alt) });

    public static RoutedUICommand ShowLogWindow { get; internal set; } = new RoutedUICommand("Show Log Window", "ShowLogWindowCommand", typeof(AppCommands), new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Alt) });

    public static RoutedUICommand ShowHelpWindow { get; internal set; } = new RoutedUICommand("Show Help", "ShowHelpWindowCommand", typeof(AppCommands), new InputGestureCollection() { new KeyGesture(Key.F1, ModifierKeys.None) });

    public static RoutedUICommand ShowAboutBox { get; internal set; } = new RoutedUICommand("About SizeBench", "ShowAboutBoxCommand", typeof(AppCommands));

    public static RoutedCommand NavigateToModel { get; internal set; } = new RoutedCommand("Navigate To Model", typeof(AppCommands));
}
