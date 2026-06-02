using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public sealed class WastefulVirtualItemTests : IDisposable
{
    Mock<ISession>? MockSession;
    SessionDataCache? DataCache;
    UserDefinedTypeSymbol? UDT;
    UserDefinedTypeSymbol? DerivedUDT1;
    UserDefinedTypeSymbol? DerivedUDT2;
    SimpleFunctionCodeSymbol? WastedFunction1;
    SimpleFunctionCodeSymbol? WastedFunction2;

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.MockSession = new Mock<ISession>();
        var diaAdapter = new TestDIAAdapter();
        this.UDT = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CBase", instanceSize: 100, symIndexId: 1, udtKind: UserDefinedTypeKind.UdtClass);
        var baseclasses = new List<(uint, uint)>(capacity: 1)
            {
                (this.UDT.SymIndexId, 0)
            };
        this.DerivedUDT1 = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CDerived1", instanceSize: 120, symIndexId: 2, udtKind: UserDefinedTypeKind.UdtClass);
        this.DerivedUDT2 = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CDerived2", instanceSize: 110, symIndexId: 3, udtKind: UserDefinedTypeKind.UdtClass);
        diaAdapter.BaseTypeIDsToFindByUDT.Add(this.DerivedUDT1, baseclasses);
        diaAdapter.BaseTypeIDsToFindByUDT.Add(this.DerivedUDT2, baseclasses);

        this.WastedFunction1 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction1", rva: 100, size: 50, symIndexId: 4);
        this.WastedFunction2 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction2", rva: 150, size: 100, symIndexId: 5);
    }

    [TestMethod]
    public void InitiallyConstructedItemHasZeroWastedSize()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 4);
        this.UDT!.MarkDerivedTypesLoaded();
        Assert.AreEqual<ulong>(0, item.WastedSize);
        Assert.IsFalse(item.IsCOMType);
        Assert.IsTrue(ReferenceEquals(this.UDT, item.UserDefinedType));
        Assert.AreEqual(0, item.WastePerSlot);
    }

    [TestMethod]
    public void NoDerivedClassesHasNoWastedSize()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 4);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        this.UDT!.MarkDerivedTypesLoaded();

        Assert.AreEqual<ulong>(0, item.WastedSize);
        Assert.AreEqual(0, item.WastePerSlot);
    }

    [TestMethod]
    public void OneDerivedClassWastedSizeIsCorrect()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 4);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        this.UDT!.AddDerivedType(this.DerivedUDT1!);
        this.UDT.MarkDerivedTypesLoaded();

        // 8 bytes are wasted, since there's 4 in the base type's vtable and 4 in DerivedUDT1's vtable.
        Assert.AreEqual<ulong>(8, item.WastedSize);
        Assert.AreEqual(8, item.WastePerSlot);
    }

    [TestMethod]
    public void TwoDerivedClassesWastedSizeIsCorrect()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 4);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        this.UDT!.AddDerivedType(this.DerivedUDT1!);
        this.UDT.AddDerivedType(this.DerivedUDT2!);
        this.UDT.MarkDerivedTypesLoaded();

        // Now it's 12 bytes because DerivedUDT2's vtable also has waste.
        Assert.AreEqual<ulong>(12, item.WastedSize);
        Assert.AreEqual(12, item.WastePerSlot);
    }

    [TestMethod]
    public void WastedOverridesIncreaseWastedSizeButNotWastePerSlot()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 4);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction2!);
        this.UDT!.AddDerivedType(this.DerivedUDT1!);
        this.UDT.AddDerivedType(this.DerivedUDT2!);
        this.UDT.MarkDerivedTypesLoaded();

        // Now it's 24 bytes because there's 2 functions * 12 bytes per slot
        Assert.AreEqual<ulong>(24, item.WastedSize);
        Assert.AreEqual(12, item.WastePerSlot);
    }

    //TODO: WastefulVirtual: SHOULD BytesPerWord influence this?  Aren't vtable entries RVA's which are always 4 bytes?
    [TestMethod]
    public void BytesPerWordInfluencesWastedSize()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 8);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        this.UDT!.MarkDerivedTypesLoaded();

        Assert.AreEqual<ulong>(0, item.WastedSize);
        Assert.AreEqual(0, item.WastePerSlot);
    }

    //TODO: WastefulVirtual: SHOULD BytesPerWord influence this?  Aren't vtable entries RVA's which are always 4 bytes?
    [TestMethod]
    public void BytesPerWordInfluencesWastedSizeCorrectlyWithOneDerivedClass()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 8);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        this.UDT!.AddDerivedType(this.DerivedUDT1!);
        this.UDT.MarkDerivedTypesLoaded();

        // 16 bytes are wasted, since there's 8 in the base type's vtable and 8 in DerivedUDT1's vtable.
        Assert.AreEqual<ulong>(16, item.WastedSize);
        Assert.AreEqual(16, item.WastePerSlot);

        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction2!);

        // Now there's 32 bytes of total waste, but the waste per slot has not changed - with no new derived
        // types, the slots cost just as much, there's just two slots wasted now.
        Assert.AreEqual<ulong>(32, item.WastedSize);
        Assert.AreEqual(16, item.WastePerSlot);
    }

    [TestMethod]
    public void BytesPerWordInfluencesWastedSizeCorrectlyWithTwoDerivedClasses()
    {
        var item = new WastefulVirtualItem(this.UDT!, isCOMType: false, bytesPerWord: 8);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1!);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction2!);
        this.UDT!.AddDerivedType(this.DerivedUDT1!);
        this.UDT.AddDerivedType(this.DerivedUDT2!);
        this.UDT.MarkDerivedTypesLoaded();

        // Now it's 48 bytes because there's 2 functions * 24 bytes per slot
        Assert.AreEqual<ulong>(48, item.WastedSize);
        Assert.AreEqual(24, item.WastePerSlot);
    }

    public void Dispose() => this.DataCache?.Dispose();
}
