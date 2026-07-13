using System.Globalization;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class SymbolNameToFriendlyNameConverterTests
{
    [TestMethod]
    public void NullInputThrows()
        => Assert.ThrowsExactly<ArgumentException>(() => SymbolNameToFriendlyNameConverter.Instance.Convert(null, typeof(string), null, CultureInfo.CurrentCulture));

    [TestMethod]
    public void AccessModifiersGetStripped()
    {
        var expectedFriendlyName = "CUIElement::Dummy(int)";
        var converter = new SymbolNameToFriendlyNameConverter();
        Assert.AreEqual(expectedFriendlyName, converter.Convert("public: " + expectedFriendlyName, typeof(string), null, CultureInfo.CurrentCulture));
        Assert.AreEqual(expectedFriendlyName, converter.Convert("protected: " + expectedFriendlyName, typeof(string), null, CultureInfo.CurrentCulture));
        Assert.AreEqual(expectedFriendlyName, converter.Convert("private: " + expectedFriendlyName, typeof(string), null, CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void CallingConventionsGetStripped()
    {
        var expectedFriendlyName = "CUIElement::Dummy(int)";
        var converter = new SymbolNameToFriendlyNameConverter();
        Assert.AreEqual(expectedFriendlyName, converter.Convert("public: __cdecl " + expectedFriendlyName, typeof(string), null, CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void Ptr64GetsStripped()
    {
        var expectedFriendlyName = "CUIElement::Dummy(int)";
        var symbolName = "public: __cdecl CUIElement::Dummy(int __ptr64) __ptr64";
        var converter = new SymbolNameToFriendlyNameConverter();
        Assert.AreEqual(expectedFriendlyName, converter.Convert(symbolName, typeof(string), null, CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void ConvertBackShouldThrow()
        => Assert.ThrowsExactly<NotImplementedException>(() => SymbolNameToFriendlyNameConverter.Instance.ConvertBack("symbol friendly name", typeof(string), null, CultureInfo.CurrentCulture));
}
