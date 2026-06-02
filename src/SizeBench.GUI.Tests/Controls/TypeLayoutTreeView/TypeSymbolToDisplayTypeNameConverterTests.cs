using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class TypeSymbolToDisplayTypeNameConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotATypeSymbol()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 4, nextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 8, type: type);
        Assert.ThrowsException<ArgumentException>(() => TypeSymbolToDisplayTypeNameConverter.Instance.Convert(3, typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => TypeSymbolToDisplayTypeNameConverter.Instance.Convert("test", typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => TypeSymbolToDisplayTypeNameConverter.Instance.Convert(dataSymbol, typeof(string), null, null));
    }

    [TestMethod]
    public void ConvertThrowsIfTargetTypeNotString()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);

        Assert.ThrowsException<ArgumentException>(() => TypeSymbolToDisplayTypeNameConverter.Instance.Convert(type, typeof(bool), null, null));
        Assert.ThrowsException<ArgumentException>(() => TypeSymbolToDisplayTypeNameConverter.Instance.Convert(type, typeof(int), null, null));
    }

    [TestMethod]
    public void NullInputIsEmptyString()
    {
        Assert.AreEqual(String.Empty, TypeSymbolToDisplayTypeNameConverter.Instance.Convert(null, typeof(string), null, null));
    }

    [TestMethod]
    public void FunctionTypeSymbolNameDirectlyPassesThrough()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var functionTypeSymbol = new FunctionTypeSymbol(cache, "void (*function)()", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: null);

        Assert.AreEqual(functionTypeSymbol.Name, TypeSymbolToDisplayTypeNameConverter.Instance.Convert(functionTypeSymbol, typeof(string), null, null));
    }

    [TestMethod]
    public void SimpleTypeNamePassesThrough()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var basicType = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var enumType = new EnumTypeSymbol(cache, "enum Foo", 4, symIndexId: nextSymIndexId++);

        Assert.AreEqual("int", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(basicType, typeof(string), null, null));
        Assert.AreEqual("enum Foo", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(enumType, typeof(string), null, null));
    }

    [TestMethod]
    public void TemplatedNameIsSimplifiedCorrectly()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var udt = new UserDefinedTypeSymbol(cache, new Mock<IDIAAdapter>().Object, new Mock<ISession>().Object, "MyNamespace::MyTemplate<int, const std::string&>", 4, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var udt2 = new UserDefinedTypeSymbol(cache, new Mock<IDIAAdapter>().Object, new Mock<ISession>().Object, "AnotherTemplatedType<int, std::string>", 4, nextSymIndexId++, UserDefinedTypeKind.UdtClass);

        Assert.AreEqual("MyTemplate<int, const std::string&>", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(udt, typeof(string), null, null));
        Assert.AreEqual("AnotherTemplatedType<int, std::string>", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(udt2, typeof(string), null, null));
    }

    [TestMethod]
    public void DeepNamespaceIsSimplifiedCorrectly()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var udt = new UserDefinedTypeSymbol(cache, new Mock<IDIAAdapter>().Object, new Mock<ISession>().Object, "MyNamespace::IsDeep::LikeReallyDeep::MyTemplate<int, const std::string&>", 4, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var nonTemplatedUDT = new UserDefinedTypeSymbol(cache, new Mock<IDIAAdapter>().Object, new Mock<ISession>().Object, "MyNamespace::IsDeep::LikeReallyDeep::MyNonTemplatedType", 4, nextSymIndexId++, UserDefinedTypeKind.UdtClass);

        Assert.AreEqual("MyTemplate<int, const std::string&>", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(udt, typeof(string), null, null));
        Assert.AreEqual("MyNonTemplatedType", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(nonTemplatedUDT, typeof(string), null, null));
    }

    [TestMethod]
    public void LongNameIsTruncatedWithEllipsis()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var template = new UserDefinedTypeSymbol(cache, new Mock<IDIAAdapter>().Object, new Mock<ISession>().Object, "MyNamespace::MyTemplate<const unsigned int, const std::string&>", 4, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var nonTemplate = new UserDefinedTypeSymbol(cache, new Mock<IDIAAdapter>().Object, new Mock<ISession>().Object, "AReallyReallyRidiculouslyLongTypeName_WhyWouldAnyoneHaveATypeNameThisLong", 4, nextSymIndexId++, UserDefinedTypeKind.UdtClass);

        Assert.AreEqual("MyTemplate<const unsigned int, const std::...", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(template, typeof(string), null, null));
        Assert.AreEqual("AReallyReallyRidiculouslyLongTypeName_WhyW...", TypeSymbolToDisplayTypeNameConverter.Instance.Convert(nonTemplate, typeof(string), null, null));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
    {
        TypeSymbolToDisplayTypeNameConverter.Instance.ConvertBack("string", typeof(TypeSymbol), null, null);
    }
}
