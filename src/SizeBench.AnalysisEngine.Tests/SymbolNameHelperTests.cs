using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public sealed class SymbolNameHelperTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();

        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
    }

    [TestMethod]
    public void FunctionNamesCanBeGenericized()
    {
        uint nextSymIndexId = 0;
        var voidType = new BasicTypeSymbol(this.DataCache, "void", 0, nextSymIndexId++);
        var boolType = new BasicTypeSymbol(this.DataCache, "bool", size: 1, symIndexId: nextSymIndexId++);
        var constBoolType = new ModifiedTypeSymbol(this.DataCache, boolType, "const bool", size: 1, symIndexId: nextSymIndexId++);
        var intType = new BasicTypeSymbol(this.DataCache, "int", size: 1, symIndexId: nextSymIndexId++);
        var intPointerType = new PointerTypeSymbol(this.DataCache, intType, "int*", instanceSize: 8, symIndexId: nextSymIndexId++);
        var constBoolPointerType = new PointerTypeSymbol(this.DataCache, constBoolType, "const bool*", instanceSize: 8, symIndexId: nextSymIndexId++);
        var aComplexTypeOfSomeUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "AComplex::Type<SomeUDT>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);

        Assert.AreEqual("MyType::MyFunction<T1>(bool, T1)",
            SymbolNameHelper.FunctionToGenericTemplatedName(
            // MyType::MyFunction<int>(bool, int) -> MyType::MyFunction<T1>(bool, T1)
            new SimpleFunctionCodeSymbol(this.DataCache, "MyType::MyFunction<int>", rva: 100, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(this.DataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, intType }, returnValueType: voidType))
            ));

        Assert.AreEqual("FoldableVolatile<T1>(T1*) volatile",
            SymbolNameHelper.FunctionToGenericTemplatedName(
            // FoldableVolatile<int>(int*) volatile -> FoldableVolatile<T1>(T1*) volatile
            new SimpleFunctionCodeSymbol(this.DataCache, "FoldableVolatile<int>", rva: 800, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(this.DataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: new TypeSymbol[] { intPointerType }, returnValueType: voidType))
        ));

        Assert.AreEqual("FoldableVolatile<T1>(T1*) volatile",
            SymbolNameHelper.FunctionToGenericTemplatedName(
            // FoldableVolatile<const bool>(const bool*) volatile -> FoldableVolatile<T1>(T1*) volatile
            new SimpleFunctionCodeSymbol(this.DataCache, "FoldableVolatile<const bool>", rva: 900, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(this.DataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: new TypeSymbol[] { constBoolPointerType }, returnValueType: voidType))
        ));

        Assert.AreEqual("FoldableWithDuplicateType<T1,T2,T1>(T1) const",
            SymbolNameHelper.FunctionToGenericTemplatedName(
            // FoldableWithDuplicateType<AComplex::Type<SomeUDT>,bool,AComplex::Type<SomeUDT>>(AComplex::Type<SomeUDT>) const -> FoldableWithDuplicateType<T1,T2,T1>(T1) const
            new SimpleFunctionCodeSymbol(this.DataCache, "FoldableWithDuplicateType<AComplex::Type<SomeUDT>,bool,AComplex::Type<SomeUDT>>", rva: 700, size: 10, symIndexId: nextSymIndexId++,
                                     functionType: new FunctionTypeSymbol(this.DataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: new TypeSymbol[] { aComplexTypeOfSomeUDT }, returnValueType: voidType))
        ));

        Assert.AreEqual("SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)",
            SymbolNameHelper.FunctionToGenericTemplatedName(
            // SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool(bool, AComplex::Type<SomeUDT>) -> SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)
            new SimpleFunctionCodeSymbol(this.DataCache, "SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool>", rva: 400, size: 10, symIndexId: nextSymIndexId++,
                                     functionType: new FunctionTypeSymbol(this.DataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, aComplexTypeOfSomeUDT }, returnValueType: voidType))
        ));
    }

    [TestMethod]
    public void UDTNamesCanBeGenericized()
    {
        uint nextSymIndexId = 0;
        var simpleType = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "SomeNamespace::ASimpleTypeName", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var aComplexTypeOfInt = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "AComplex::Type<int>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var aComplexTypeOfFloat = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "AComplex::Type<float>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var aComplexTypeOfSomeUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "AComplex::Type<SomeUDT>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var aComplexTypeInANamespace = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "ANamespace::AComplex::Type<int>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);

        Assert.AreEqual("SomeNamespace::ASimpleTypeName", SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(simpleType));
        Assert.AreEqual("AComplex::Type<T1>", SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(aComplexTypeOfInt));
        Assert.AreEqual("AComplex::Type<T1>", SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(aComplexTypeOfFloat));
        Assert.AreEqual("AComplex::Type<T1>", SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(aComplexTypeOfSomeUDT));
        Assert.AreEqual("ANamespace::AComplex::Type<T1>", SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(aComplexTypeInANamespace));
    }

    public void Dispose() => this.DataCache.Dispose();
}
