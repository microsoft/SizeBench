using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class FunctionToFormattedNameConverterTests
{
    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyTakesIFunctionCodeSymbolValue()
        => FunctionToFormattedNameConverter.Instance.Convert(5, typeof(string), FunctionCodeNameFormatting.IncludeParentType /* ConverterParameter */, Thread.CurrentThread.CurrentCulture);

    [TestMethod]
    public void ConvertOnlyTakesFunctionCodeNameFormattingAsConverterParameter()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: new TypeSymbol[] { intType }, returnValueType: constUDTRef);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AMemberFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: udt,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: true, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.ThrowsException<ArgumentException>(() => FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), null /* ConverterParameter */, Thread.CurrentThread.CurrentCulture));
        Assert.ThrowsException<ArgumentException>(() => FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), "test" /* ConverterParameter */, Thread.CurrentThread.CurrentCulture));
        Assert.ThrowsException<ArgumentException>(() => FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), 5 /* ConverterParameter */, Thread.CurrentThread.CurrentCulture));
        Assert.ThrowsException<ArgumentException>(() => FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), UserDefinedTypeKind.UdtClass /* ConverterParameter */, Thread.CurrentThread.CurrentCulture));
    }

    [TestMethod]
    public void ConvertWithVariousFormattingFlagsWorks()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: new TypeSymbol[] { intType }, returnValueType: constUDTRef);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AMemberFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: udt,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: true, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const volatile", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.All, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeArgumentNames, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction(int)", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeArgumentTypes, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction const volatile", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeCVQualifiers, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeParentType, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("const ANamespace::SomeUDT& AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeReturnType, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeSealed, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("static AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeStatic, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) const volatile", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeUniqueSignature, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeVirtualOverride, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.None, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction(int)", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("AMemberFunction(int) const volatile", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers, Thread.CurrentThread.CurrentCulture));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", FunctionToFormattedNameConverter.Instance.Convert(function, typeof(string), FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride, Thread.CurrentThread.CurrentCulture));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
    {
        var converter = new FunctionToFormattedNameConverter();
        converter.ConvertBack("MyFunction()", typeof(IFunctionCodeSymbol), null /* ConverterParameter */, Thread.CurrentThread.CurrentCulture);
    }
}
