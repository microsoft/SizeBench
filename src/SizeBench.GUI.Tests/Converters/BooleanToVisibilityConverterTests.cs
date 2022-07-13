using System.Windows;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class BooleanToVisibilityConverterTests
{
    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyTakesBoolValue()
    {
        BooleanToVisibilityConverter.Instance.Convert(5, typeof(Visibility), null /* ConverterParameter */, null /* CultureInfo */);
    }

    [TestMethod]
    public void ConvertTrueToVisible()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(Visibility.Visible, result);
    }

    [TestMethod]
    public void ConvertFalseToCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(Visibility.Collapsed, result);
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackOnlyTakesVisibilityValue()
    {
        var converter = new BooleanToVisibilityConverter();
        converter.ConvertBack(true, typeof(bool), null /* ConverterParameter */, null /* CultureInfo */);
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackOnlyConvertsToBool()
    {
        var converter = new BooleanToVisibilityConverter();
        converter.ConvertBack(Visibility.Visible, typeof(int), null /* ConverterParameter */, null /* CultureInfo */);
    }

    [TestMethod]
    public void ConvertBackVisibleToTrue()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void ConvertBackCollapsedToFalse()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.ConvertBack(Visibility.Collapsed, typeof(bool), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(false, result);
    }
}
