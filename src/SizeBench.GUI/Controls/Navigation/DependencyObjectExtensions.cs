using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
internal class DependencyObjectExtensionProperties : DependencyObject
{
    /// <summary>
    /// Tracks whether or not the event handlers of a particular object are currently suspended.
    /// Used by the SetValueNoCallback and AreHandlersSuspended extension methods.
    /// </summary>
    public static readonly DependencyProperty AreHandlersSuspended = DependencyProperty.RegisterAttached(
        "AreHandlersSuspended",
        typeof(bool),
        typeof(DependencyObjectExtensionProperties),
        new PropertyMetadata(false)
    );

    public static void SetAreHandlersSuspended(DependencyObject obj, bool value)
        => obj.SetValue(AreHandlersSuspended, value);

    public static bool GetAreHandlersSuspended(DependencyObject obj)
        => (bool)obj.GetValue(AreHandlersSuspended);
}

[ExcludeFromCodeCoverage]
internal static class DependencyObjectExtensions
{
    #region Static Methods

    public static void SetValueNoCallback(this DependencyObject obj, DependencyProperty property, object value)
    {
        DependencyObjectExtensionProperties.SetAreHandlersSuspended(obj, true);
        try
        {
            obj.SetValue(property, value);
        }
        finally
        {
            DependencyObjectExtensionProperties.SetAreHandlersSuspended(obj, false);
        }
    }

    public static bool AreHandlersSuspended(this DependencyObject obj)
        => DependencyObjectExtensionProperties.GetAreHandlersSuspended(obj);

    #endregion Static Methods
}
