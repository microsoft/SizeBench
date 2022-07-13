using System.Globalization;
using System.Windows;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class VisibilityReverseConverterTests
{
    [TestMethod]
    public void VisibleBecomesCollapsedAndViceVersaForConvert()
    {
        Assert.AreEqual(Visibility.Collapsed, VisibilityReverseConverter.Instance.Convert(Visibility.Visible, typeof(Visibility), null, CultureInfo.CurrentCulture));
        Assert.AreEqual(Visibility.Visible, VisibilityReverseConverter.Instance.Convert(Visibility.Collapsed, typeof(Visibility), null, CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void VisibleBecomesCollapsedAndViceVersaForConvertBack()
    {
        Assert.AreEqual(Visibility.Collapsed, VisibilityReverseConverter.Instance.ConvertBack(Visibility.Visible, typeof(Visibility), null, CultureInfo.CurrentCulture));
        Assert.AreEqual(Visibility.Visible, VisibilityReverseConverter.Instance.ConvertBack(Visibility.Collapsed, typeof(Visibility), null, CultureInfo.CurrentCulture));
    }
}
