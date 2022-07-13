using System.Globalization;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class SizeToFriendlySizeConverterTests
{
    [TestMethod]
    public void SmallSizesStayAsBytes()
    {
        var output = SizeToFriendlySizeConverter.Instance.Convert(123, typeof(string), null, null) as string;

        Assert.AreEqual("123 bytes", output);
    }

    [TestMethod]
    public void MediumSizedStuffIsInKB()
    {
        var output = SizeToFriendlySizeConverter.Instance.Convert(1024 * 1.5f, typeof(string), null, null) as string;

        Assert.AreEqual("1.5 KB", output);

        output = SizeToFriendlySizeConverter.Instance.Convert(1024 * 999.7f, typeof(string), null, null) as string;
        Assert.AreEqual("999.7 KB", output);
    }

    [TestMethod]
    public void LargerStuffBecomesMB()
    {
        var output = SizeToFriendlySizeConverter.Instance.Convert(1024 * 1024 * 1.5f, typeof(string), null, null) as string;

        Assert.AreEqual("1.5 MB", output);

        output = SizeToFriendlySizeConverter.Instance.Convert(1024 * 1024 * 999.7f, typeof(string), null, null) as string;
        Assert.AreEqual("999.7 MB", output);
    }

    [TestMethod]
    public void ReallyLargeStuffBecomesGB()
    {
        var output = SizeToFriendlySizeConverter.Instance.Convert(1024 * 1024 * 1024 * 1.5f, typeof(string), null, null) as string;

        Assert.AreEqual("1.5 GB", output);

        output = SizeToFriendlySizeConverter.Instance.Convert(1024 * 1024 * 1024 * 999.7f, typeof(string), null, null) as string;
        Assert.AreEqual("999.7 GB", output);
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackShouldThrow()
    {
        SizeToFriendlySizeConverter.Instance.ConvertBack(123, typeof(string), null, CultureInfo.CurrentCulture);
    }
}
