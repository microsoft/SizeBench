using System.Globalization;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class PageTitleToWindowTitleConverterTests
{
    [TestMethod]
    public void NullInputReturnsAppTitle()
    {
        var converter = new PageTitleToWindowTitleConverter();
        var output = converter.Convert(null, typeof(string), null, CultureInfo.CurrentCulture) as string;
        Assert.AreEqual("SizeBench", output);
    }

    [TestMethod]
    public void NullSessionDisplaysPageNameAndAppName()
    {
        const string pageTitle = "Binary Section: .text";
        var converter = new PageTitleToWindowTitleConverter();
        var output = converter.Convert(new object[] { pageTitle }, typeof(string), null, CultureInfo.CurrentCulture).ToString();
        StringAssert.Contains(output, "SizeBench", StringComparison.Ordinal);
        StringAssert.Contains(output, pageTitle, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SessionPresentDisplaysPageNameAndAppNameAndSessionBinaryPath()
    {
        const string pageTitle = "Binary Section: .text";
        const string binaryPath = @"c:\blah\something.dll";
        var converter = new PageTitleToWindowTitleConverter();
        var output = converter.Convert(new object[] { pageTitle, binaryPath }, typeof(string), null, CultureInfo.CurrentCulture).ToString();
        StringAssert.Contains(output, "SizeBench", StringComparison.Ordinal);
        StringAssert.Contains(output, pageTitle, StringComparison.Ordinal);
        StringAssert.Contains(output, binaryPath, StringComparison.Ordinal);
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackShouldThrow() => PageTitleToWindowTitleConverter.Instance.ConvertBack(new object[] { "window title" }, new Type[] { typeof(string) }, null, CultureInfo.CurrentCulture);

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void MoreThanTwoInputParametersThrows()
    {
        const string pageTitle = "Binary Section: .text";
        const string binaryPath = @"c:\blah\something.dll";
        var converter = new PageTitleToWindowTitleConverter();
        _ = converter.Convert(new object[] { pageTitle, binaryPath, new object() }, typeof(string), null, CultureInfo.CurrentCulture).ToString();
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void FirstParameterMustBeString()
    {
        const string binaryPath = @"c:\blah\something.dll";
        var converter = new PageTitleToWindowTitleConverter();
        _ = converter.Convert(new object[] { 123, binaryPath }, typeof(string), null, CultureInfo.CurrentCulture).ToString();
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void SecondParameterMustBeString()
    {
        const string pageTitle = "Binary Section: .text";
        var converter = new PageTitleToWindowTitleConverter();
        _ = converter.Convert(new object[] { pageTitle, 123 }, typeof(string), null, CultureInfo.CurrentCulture).ToString();
    }
}
