using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class CanLoadLayoutTests
{
    [TestMethod]
    public void CanLoadLayoutIsCorrectForAllTypeSymbols()
    {
        using var dataCache = new SessionDataCache();
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 0;

        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);

        // Basic types can never have their layout loaded, they are a primitive
        var basicType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        Assert.IsFalse(basicType.CanLoadLayout);

        // Same for enums
        var enumType = new EnumTypeSymbol(dataCache, "enum Foo", 4, nextSymIndexId++);
        Assert.IsFalse(enumType.CanLoadLayout);

        // FunctionType doesn't represent a thing with data members and so on, so it has no layout to load
        var functionType = new FunctionTypeSymbol(dataCache, "int (*function)(float)", 100, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: voidType);
        Assert.IsFalse(functionType.CanLoadLayout);

        // UDTs can always have their layout loaded, they're custom so we can't know anything about them.  Some of them won't have data members or functions or either, but that distinction is
        // finer than we care to distinguish so far.
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "MyCustomType", 10, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        Assert.IsTrue(udt.CanLoadLayout);

        // Arrays can have their layout loaded, but only if the element type has a layout - then what we can do is just load
        // the layout of the element type, as the array is basically the same.
        var arrayOfBasicTypes = new ArrayTypeSymbol(dataCache, "int[5]", 20, nextSymIndexId++, basicType, elementCount: 5);
        Assert.IsFalse(arrayOfBasicTypes.CanLoadLayout);
        var arrayOfUDTs = new ArrayTypeSymbol(dataCache, "MyCustomType[3]", udt.InstanceSize * 3, nextSymIndexId++, udt, elementCount: 3);
        Assert.IsTrue(arrayOfUDTs.CanLoadLayout);

        // Modified types are like arrays - they have a layout if their unmodified type has a layout
        var modifiedInt = new ModifiedTypeSymbol(dataCache, basicType, "const int", 4, nextSymIndexId++);
        Assert.IsFalse(modifiedInt.CanLoadLayout);
        var modifiedUDT = new ModifiedTypeSymbol(dataCache, udt, "const MyCustomType", udt.InstanceSize, nextSymIndexId++);
        Assert.IsTrue(modifiedUDT.CanLoadLayout);

        // Pointers are also like arrays and modified types - if the thing they point to has a layout, we can chase through to that and load it
        var pointerToInt = new PointerTypeSymbol(dataCache, basicType, "int*", 4, nextSymIndexId++);
        Assert.IsFalse(pointerToInt.CanLoadLayout);
        var pointerToUDT = new PointerTypeSymbol(dataCache, udt, "MyCustomType*", udt.InstanceSize, nextSymIndexId++);
        Assert.IsTrue(pointerToUDT.CanLoadLayout);
    }
}
