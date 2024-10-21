using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class StaticStaticDataSymbolTests : IDisposable
{
    private SessionDataCache SessionDataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize() => this.SessionDataCache = new SessionDataCache()
    {
        AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
    };

    [TestMethod]
    public void StaticDataSymbolsNotVeryLikelyTheSameWhenNamesDiffer()
    {
        uint nextSymIndexId = 0;
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo");
        var data2 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo2");

        Assert.IsFalse(data1.IsVeryLikelyTheSameAs(data2));
        Assert.IsFalse(data2.IsVeryLikelyTheSameAs(data1));
    }

    [TestMethod]
    public void StaticDataSymbolsNotVeryLikelyTheSameWhenOtherIsNotData()
    {
        uint nextSymIndexId = 0;
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo");
        var thunk = new ThunkSymbol(this.SessionDataCache, "[thunk] test thunk", rva: 100, size: 4, symIndexId: nextSymIndexId++);

        Assert.IsFalse(data1.IsVeryLikelyTheSameAs(thunk));
    }

    [TestMethod]
    public void StaticDataSymbolsNotVeryLikelyTheSameWhenDataKindDiffers()
    {
        // This test may be overly restrictive - if the data kind changes, it could change from static to non-static, for example, but we
        // may want to consider this 'equal' for diffing.  For now, being restrictive until more cases are found to figure out a better
        // heuristic.

        uint nextSymIndexId = 0;
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", dataKind: DataKind.DataIsFileStatic);
        var data2 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", dataKind: DataKind.DataIsStaticLocal);

        Assert.IsFalse(data1.IsVeryLikelyTheSameAs(data2));
        Assert.IsFalse(data2.IsVeryLikelyTheSameAs(data1));
    }

    [TestMethod]
    public void StaticDataSymbolsNotVeryLikelyTheSameWhenOnlyOneHasType()
    {
        uint nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++);
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo");
        var data2 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", type: intBasicType);

        Assert.IsFalse(data1.IsVeryLikelyTheSameAs(data2));
        Assert.IsFalse(data2.IsVeryLikelyTheSameAs(data1));
    }

    [TestMethod]
    public void StaticDataSymbolsNotVeryLikelyTheSameWhenTypeNamesDifferBeforeColonColon()
    {
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 0;
        var udt1 = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "UDT1::Stuff", 20, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", type: udt1);
        var udt2 = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "UDT2::Stuff", 20, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var data2 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", type: udt2);

        Assert.IsFalse(data1.IsVeryLikelyTheSameAs(data2));
        Assert.IsFalse(data2.IsVeryLikelyTheSameAs(data1));
    }

    [TestMethod]
    public void StaticDataSymbolsVeryLikelyTheSameWhenTypeNamesDifferAfterColonColon()
    {
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 0;
        var udt1 = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "TraceForFailFast::__l7::<unnamed-type-_TlgEvent>", 20, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", type: udt1);
        var udt2 = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "TraceForFailFast::__l8::<unnamed-type-_TlgEvent>", 20, nextSymIndexId++, UserDefinedTypeKind.UdtClass);
        var data2 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", type: udt2);

        Assert.IsTrue(data1.IsVeryLikelyTheSameAs(data2));
        Assert.IsTrue(data2.IsVeryLikelyTheSameAs(data1));
    }

    [TestMethod]
    public void StaticDataSymbolsNotVeryLikelyTheSameWhenFunctionParentDiffers()
    {
        uint nextSymIndexId = 0;
        var function1 = new SimpleFunctionCodeSymbol(this.SessionDataCache, "CFoo::DoTheThing", rva: 123, size: 10, symIndexId: nextSymIndexId++);
        var data1 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", dataKind: DataKind.DataIsStaticLocal, functionParent: function1);
        var function2 = new SimpleFunctionCodeSymbol(this.SessionDataCache, "CFoo::DoTheThing", rva: 456, size: 10, symIndexId: nextSymIndexId++, isStatic: true);
        var data2 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", dataKind: DataKind.DataIsStaticLocal, functionParent: function2);

        Assert.IsFalse(data1.IsVeryLikelyTheSameAs(data2));
        Assert.IsFalse(data2.IsVeryLikelyTheSameAs(data1));

        // But if the functions are very likely the same, then the data can be too
        var function3 = new SimpleFunctionCodeSymbol(this.SessionDataCache, "CFoo::DoTheThing", rva: 789, size: 10, symIndexId: nextSymIndexId++);
        var data3 = BuildSimpleStaticDataSymbol(ref nextSymIndexId, "s_foo", dataKind: DataKind.DataIsStaticLocal, functionParent: function3);

        Assert.IsTrue(data1.IsVeryLikelyTheSameAs(data3));
        Assert.IsTrue(data3.IsVeryLikelyTheSameAs(data1));
        Assert.IsFalse(data2.IsVeryLikelyTheSameAs(data3));
        Assert.IsFalse(data3.IsVeryLikelyTheSameAs(data2));
    }

    private StaticDataSymbol BuildSimpleStaticDataSymbol(ref uint nextSymIndexId,
                                                         string name,
                                                         SessionDataCache? sessionDataCache = null,
                                                         DataKind dataKind = DataKind.DataIsFileStatic,
                                                         TypeSymbol? type = null,
                                                         IFunctionCodeSymbol? functionParent = null)
    {
        return new StaticDataSymbol(sessionDataCache ?? this.SessionDataCache,
                                    name: name,
                                    rva: 0,
                                    size: 10,
                                    isVirtualSize: false,
                                    symIndexId: nextSymIndexId++,
                                    dataKind: dataKind,
                                    type: type,
                                    referencedIn: null,
                                    functionParent: functionParent);
    }

    public void Dispose() => this.SessionDataCache.Dispose();
}
