namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class FullTypeNameToToolTipTypeNameConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotAString()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FullTypeNameToToolTipTypeNameConverter.Instance.Convert(3, typeof(string), null, null));
        Assert.ThrowsExactly<ArgumentException>(() => FullTypeNameToToolTipTypeNameConverter.Instance.Convert(true, typeof(string), null, null));
    }

    [TestMethod]
    public void NullInputIsNullOutput()
    {
        Assert.IsNull(FullTypeNameToToolTipTypeNameConverter.Instance.Convert(null, typeof(string), null, null));
    }

    [TestMethod]
    public void NamespacedNamesPassThroughEvenIfShort()
    {
        Assert.AreEqual("std::string", FullTypeNameToToolTipTypeNameConverter.Instance.Convert("std::string", typeof(string), null, null));
    }

    [TestMethod]
    public void LongNamesPassThrough()
    {
        var input = "AReallyReallyRidiculouslyLongTypeName_WhyWouldAnyoneHaveATypeNameThisLong";
        Assert.AreEqual(input, FullTypeNameToToolTipTypeNameConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void ShortNamesReturnNull()
    {
        Assert.IsNull(FullTypeNameToToolTipTypeNameConverter.Instance.Convert("int", typeof(string), null, null));
        Assert.IsNull(FullTypeNameToToolTipTypeNameConverter.Instance.Convert("MyCustomType<int, char*>", typeof(string), null, null));
    }

    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => Assert.ThrowsExactly<NotImplementedException>(() => FullTypeNameToToolTipTypeNameConverter.Instance.ConvertBack("string", typeof(string), null, null));
}
