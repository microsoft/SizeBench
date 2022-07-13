using System.Windows;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class NullToCollapsedConverterTests
{
    [TestMethod]
    public void ConvertNonNullToVisible()
    {
        var converter = new NullToCollapsedConverter();
        var result = converter.Convert(new object(), typeof(Visibility), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(Visibility.Visible, result);
    }

    [TestMethod]
    public void ConvertNullToCollapsed()
    {
        var converter = new NullToCollapsedConverter();
        var result = converter.Convert(null, typeof(Visibility), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(Visibility.Collapsed, result);
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
    {
        NullToCollapsedConverter.Instance.ConvertBack(Visibility.Visible, typeof(object), null /* ConverterParameter */, null /* CultureInfo */);
    }
}
