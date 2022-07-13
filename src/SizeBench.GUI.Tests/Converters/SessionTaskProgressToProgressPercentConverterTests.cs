using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class SessionTaskProgressToProgressPercentConverterTests
{
    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyConvertsToDouble()
    {
        SessionTaskProgressToProgressPercentConverter.Instance.Convert(new object(), typeof(int), null /* ConverterParameter */, null /* CultureInfo */);
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyConvertsFromSessionTaskProgress()
    {
        SessionTaskProgressToProgressPercentConverter.Instance.Convert(new object(), typeof(double), null /* ConverterParameter */, null /* CultureInfo */);
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertThrowsForNull()
    {
        var converter = new SessionTaskProgressToProgressPercentConverter();
        converter.Convert(null, typeof(double), null /* ConverterParaeter */, null /* CultureInfo */);
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

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
    {
        SessionTaskProgressToProgressPercentConverter.Instance.ConvertBack(100.0d, typeof(SessionTaskProgress), null /* ConverterParameter */, null /* CultureInfo */);
    }
}
