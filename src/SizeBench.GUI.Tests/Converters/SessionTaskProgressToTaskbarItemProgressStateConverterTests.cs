using System.Windows.Shell;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class SessionTaskProgressToTaskbarItemProgressStateConverterTests
{
    [TestMethod]
    public void ConvertOnlyConvertsFromSessionTaskProgress()
        => Assert.ThrowsExactly<ArgumentException>(() => SessionTaskProgressToTaskbarItemProgressStateConverter.Instance.Convert(new object(), typeof(TaskbarItemProgressState), null /* ConverterParameter */, null /* CultureInfo */));

    [TestMethod]
    public void ConvertThrowsForNull()
    {
        var converter = new SessionTaskProgressToTaskbarItemProgressStateConverter();
        Assert.ThrowsExactly<ArgumentException>(() => converter.Convert(null, typeof(TaskbarItemProgressState), null /* ConverterParaeter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void ConvertWorksForIndeterminateProgress()
    {
        var progress = new SessionTaskProgress("dummy message", 5, null);
        var converter = new SessionTaskProgressToTaskbarItemProgressStateConverter();
        var result = converter.Convert(progress, typeof(TaskbarItemProgressState), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(TaskbarItemProgressState.Indeterminate, result);
    }

    [TestMethod]
    public void ConvertWorksForDeterminateProgress()
    {
        var progress = new SessionTaskProgress("dummy message", 5, 100);
        var converter = new SessionTaskProgressToTaskbarItemProgressStateConverter();
        var result = converter.Convert(progress, typeof(TaskbarItemProgressState), null /* ConverterParameter */, null /* CultureInfo */);

        Assert.AreEqual(TaskbarItemProgressState.Normal, result);
    }

    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => Assert.ThrowsExactly<NotImplementedException>(() => SessionTaskProgressToTaskbarItemProgressStateConverter.Instance.ConvertBack(TaskbarItemProgressState.Indeterminate, typeof(SessionTaskProgress), null /* ConverterParameter */, null /* CultureInfo */));
}
