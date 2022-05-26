using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class UserDefinedTypeSymbolTests : IDisposable
{
    private SessionDataCache? DataCache;
    private UserDefinedTypeSymbol? Base1UDT;
    private UserDefinedTypeSymbol? Base2UDT;
    private UserDefinedTypeSymbol? Derived1And2UDT;
    private UserDefinedTypeSymbol? Derived2UDT;
    private TestDIAAdapter? TestDIAAdapter;

    [TestInitialize]
    public void TestInitialize()
    {
        var mockSession = new Mock<ISession>();
        this.DataCache = new SessionDataCache();
        this.TestDIAAdapter = new TestDIAAdapter();
        this.Base1UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, mockSession.Object, "TestBase1", 8, symIndexId: 123, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        this.Base2UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, mockSession.Object, "TestBase2", 8, symIndexId: 456, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var baseTypeIDs = new Dictionary<uint, uint>()
            {
                { this.Base1UDT.SymIndexId, 0 },
                { this.Base2UDT.SymIndexId, 8 }
            };
        this.Derived1And2UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, mockSession.Object, "TestDerived1And2", 10, symIndexId: 0, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseTypeIDs);
        baseTypeIDs = new Dictionary<uint, uint>()
            {
                { this.Base2UDT.SymIndexId, 0 }
            };
        this.Derived2UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, mockSession.Object, "TestDerived2", 10, symIndexId: 5, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseTypeIDs);

        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Base1UDT.SymIndexId, this.Base1UDT);
        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Base2UDT.SymIndexId, this.Base2UDT);
        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Derived1And2UDT.SymIndexId, this.Derived1And2UDT);
    }

    // Accessing BaseTypes before you load them should throw - it's up to the caller to do that right for now.  Though that design kinda sucks.
    [TestMethod]
    public void BaseTypesPropertyThrowsIfCalledWithoutCallingLoadBaseTypes() => Assert.ThrowsException<InvalidOperationException>(() => this.Derived1And2UDT!.BaseTypes);

    [TestMethod]
    public void LoadBaseTypesWorksIfTypesAreNotInCacheAlready()
    {
        this.DataCache!.AllTypesBySymIndexId.Clear();
        this.Derived1And2UDT!.LoadBaseTypes(this.DataCache, this.TestDIAAdapter!, CancellationToken.None);

        Assert.IsNotNull(this.Derived1And2UDT.BaseTypes);
        Assert.AreEqual(2, this.Derived1And2UDT.BaseTypes.Count);
        Assert.IsTrue(this.Derived1And2UDT.BaseTypes.Any(bt => bt._baseTypeSymbol == this.Base1UDT && bt._offset == 0));
        Assert.IsTrue(this.Derived1And2UDT.BaseTypes.Any(bt => bt._baseTypeSymbol == this.Base2UDT && bt._offset == 8));
    }

    [TestMethod]
    public void LoadBaseTypesUsesCacheIfItCan()
    {
        this.TestDIAAdapter!.TypeSymbolsToFindBySymIndexId.Clear();
        // double-check we set up the cache right when we constructed everything
        Assert.IsTrue(ReferenceEquals(this.DataCache!.AllTypesBySymIndexId[this.Base1UDT!.SymIndexId], this.Base1UDT));
        Assert.IsTrue(ReferenceEquals(this.DataCache.AllTypesBySymIndexId[this.Base2UDT!.SymIndexId], this.Base2UDT));

        this.Derived1And2UDT!.LoadBaseTypes(this.DataCache, this.TestDIAAdapter, CancellationToken.None);

        Assert.IsNotNull(this.Derived1And2UDT.BaseTypes);
        Assert.AreEqual(2, this.Derived1And2UDT.BaseTypes.Count);
        Assert.IsTrue(this.Derived1And2UDT.BaseTypes.Any(bt => bt._baseTypeSymbol == this.Base1UDT && bt._offset == 0));
        Assert.IsTrue(this.Derived1And2UDT.BaseTypes.Any(bt => bt._baseTypeSymbol == this.Base2UDT && bt._offset == 8));
    }



    [TestMethod]
    public void DerivedTypesPropertyThrowsIfCalledWithoutMarkingDerivedTypesAsCompletelyLoaded() => Assert.ThrowsException<InvalidOperationException>(() => this.Base1UDT!.DerivedTypesBySymIndexId);

    [TestMethod]
    public void AddingTheSameDerivedTypeTwiceIsOK()
    {
        this.Base1UDT!.AddDerivedType(this.Derived1And2UDT!);
        this.Base1UDT.AddDerivedType(this.Derived1And2UDT!);
        this.Base1UDT.MarkDerivedTypesLoaded();

        Assert.IsNotNull(this.Base1UDT.DerivedTypesBySymIndexId);
        Assert.AreEqual(1, this.Base1UDT.DerivedTypesBySymIndexId.Count);
        Assert.IsTrue(ReferenceEquals(this.Base1UDT.DerivedTypesBySymIndexId.Values[0], this.Derived1And2UDT));
        Assert.IsTrue(ReferenceEquals(this.Base1UDT.DerivedTypesBySymIndexId[this.Derived1And2UDT!.SymIndexId], this.Derived1And2UDT));
    }

    [TestMethod]
    public void AddingMultipleDerivedTypesWorks()
    {
        this.Base2UDT!.AddDerivedType(this.Derived1And2UDT!);
        this.Base2UDT.AddDerivedType(this.Derived2UDT!);
        this.Base2UDT.MarkDerivedTypesLoaded();

        Assert.IsNotNull(this.Base2UDT.DerivedTypesBySymIndexId);
        Assert.AreEqual(2, this.Base2UDT.DerivedTypesBySymIndexId.Count);
        Assert.IsTrue(this.Base2UDT.DerivedTypesBySymIndexId.Any(kvp => kvp.Key == this.Derived1And2UDT!.SymIndexId && ReferenceEquals(kvp.Value, this.Derived1And2UDT)));
        Assert.IsTrue(this.Base2UDT.DerivedTypesBySymIndexId.Any(kvp => kvp.Key == this.Derived2UDT!.SymIndexId && ReferenceEquals(kvp.Value, this.Derived2UDT)));
    }

    [TestMethod]
    public void AddingDerivedTypeAfterMarkingDoneThrows()
    {
        this.Base2UDT!.AddDerivedType(this.Derived1And2UDT!);
        this.Base2UDT.MarkDerivedTypesLoaded();

        Assert.ThrowsException<InvalidOperationException>(() => this.Base2UDT.AddDerivedType(this.Derived2UDT!));
    }

    public void Dispose() => this.DataCache?.Dispose();
}
