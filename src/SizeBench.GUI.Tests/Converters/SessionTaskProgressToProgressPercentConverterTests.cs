using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class SessionTaskProgressToProgressPercentConverterTests
{
    [TestMethod]
    public void ConvertOnlyConvertsToDouble()
        => Assert.ThrowsExactly<ArgumentException>(() => SessionTaskProgressToProgressPercentConverter.Instance.Convert(new object(), typeof(int), null /* ConverterParameter */, null /* CultureInfo */));

    [TestMethod]
    public void ConvertOnlyConvertsFromSessionTaskProgress()
        => Assert.ThrowsExactly<ArgumentException>(() => SessionTaskProgressToProgressPercentConverter.Instance.Convert(new object(), typeof(double), null /* ConverterParameter */, null /* CultureInfo */));

    [TestMethod]
    public void ConvertThrowsForNull()
    {
        var converter = new SessionTaskProgressToProgressPercentConverter();
        Assert.ThrowsExactly<ArgumentException>(() => converter.Convert(null, typeof(double), null /* ConverterParaeter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void ConvertWorksForIndeterminateProgress()
    {
        var progress = new SessionTaskProgress("dummy message", 5, null);
        var converter = new SessionTaskProgressToProgressPercentConverter();
        var result = converter.Convert(progress, typeof(double), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(0.0d, result);
    }

    [TestMethod]
    public void ConvertWorksForNormalPercentages()
    {
        var progress = new SessionTaskProgress("dummy message", 5, 100);
        var converter = new SessionTaskProgressToProgressPercentConverter();
        var result = converter.Convert(progress, typeof(double), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(0.05d, result);
    }

    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => Assert.ThrowsExactly<NotImplementedException>(() => SessionTaskProgressToProgressPercentConverter.Instance.ConvertBack(100.0d, typeof(SessionTaskProgress), null /* ConverterParameter */, null /* CultureInfo */));
}
