namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class FullTypeNameToToolTipTypeNameConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotAString()
    {
        Assert.ThrowsException<ArgumentException>(() => FullTypeNameToToolTipTypeNameConverter.Instance.Convert(3, typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => FullTypeNameToToolTipTypeNameConverter.Instance.Convert(true, typeof(string), null, null));
    }

    [TestMethod]
    public void NullInputIsNullOutput()
    {
        Assert.AreEqual(null, FullTypeNameToToolTipTypeNameConverter.Instance.Convert(null, typeof(string), null, null));
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
        Assert.AreEqual(null, FullTypeNameToToolTipTypeNameConverter.Instance.Convert("int", typeof(string), null, null));
        Assert.AreEqual(null, FullTypeNameToToolTipTypeNameConverter.Instance.Convert("MyCustomType<int, char*>", typeof(string), null, null));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
    {
        FullTypeNameToToolTipTypeNameConverter.Instance.ConvertBack("string", typeof(string), null, null);
    }
}
