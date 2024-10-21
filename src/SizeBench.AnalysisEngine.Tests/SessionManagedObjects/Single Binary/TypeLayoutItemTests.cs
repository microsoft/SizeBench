using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public sealed class TypeLayoutItemTests : IDisposable
{
    Mock<ISession>? MockSession;
    SessionDataCache? DataCache;
    UserDefinedTypeSymbol? UDT;
    UserDefinedTypeSymbol? DerivedUDT1;

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataCache = new SessionDataCache();
        this.MockSession = new Mock<ISession>();
        var diaAdapter = new TestDIAAdapter();
        this.UDT = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CBase", instanceSize: 100, symIndexId: 1, udtKind: UserDefinedTypeKind.UdtClass);
        var baseclasses = new List<(uint, uint)>(capacity: 1)
            {
                (this.UDT.SymIndexId, 0)
            };
        this.DerivedUDT1 = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CDerived1", instanceSize: 120, symIndexId: 2, udtKind: UserDefinedTypeKind.UdtClass);
        diaAdapter.BaseTypeIDsToFindByUDT.Add(this.DerivedUDT1, baseclasses);
    }

    [TestMethod]
    public void AlignmentWasteCorrectlyIncludesBaseTypes()
    {
        var baseType = new TypeLayoutItem(this.UDT!, alignmentWasteExclusive: 10, usedForVFPtrsExclusive: 0, baseTypeLayouts: null, memberLayouts: null);
        var derivedType = new TypeLayoutItem(this.DerivedUDT1!, alignmentWasteExclusive: 6, usedForVFPtrsExclusive: 0, baseTypeLayouts: new TypeLayoutItem[] { baseType }, memberLayouts: null);

        Assert.AreEqual(10, baseType.AlignmentWasteExclusive);
        Assert.AreEqual(10, baseType.AlignmentWasteIncludingBaseTypes);

        Assert.AreEqual(6, derivedType.AlignmentWasteExclusive);
        Assert.AreEqual(16, derivedType.AlignmentWasteIncludingBaseTypes);
    }

    [TestMethod]
    public void UsedForVFPtrsCorrectlyIncludesBaseTypes()
    {
        var baseType = new TypeLayoutItem(this.UDT!, alignmentWasteExclusive: 0, usedForVFPtrsExclusive: 16, baseTypeLayouts: null, memberLayouts: null);
        var derivedType = new TypeLayoutItem(this.DerivedUDT1!, alignmentWasteExclusive: 0, usedForVFPtrsExclusive: 8, baseTypeLayouts: new TypeLayoutItem[] { baseType }, memberLayouts: null);

        Assert.AreEqual(16u, baseType.UsedForVFPtrsExclusive);
        Assert.AreEqual(16u, baseType.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual(8u, derivedType.UsedForVFPtrsExclusive);
        Assert.AreEqual(24u, derivedType.UsedForVFPtrsIncludingBaseTypes);
    }

    [TestMethod]
    public void MembersAndUDTPassThrough()
    {
        var memberLayouts = new TypeLayoutItemMember[2]
        {
                TypeLayoutItemMember.CreateAlignmentMember(2m, 2m, false, 0, false),
                TypeLayoutItemMember.CreateVfptrMember(0, 8)
        };
        var typeLayoutItem = new TypeLayoutItem(this.UDT!, alignmentWasteExclusive: 0, usedForVFPtrsExclusive: 8, baseTypeLayouts: null, memberLayouts: memberLayouts);

        Assert.IsTrue(ReferenceEquals(this.UDT, typeLayoutItem.UserDefinedType));
        Assert.IsNotNull(typeLayoutItem.MemberLayouts);
        Assert.AreEqual(memberLayouts.Length, typeLayoutItem.MemberLayouts.Count);
        Assert.IsTrue(ReferenceEquals(memberLayouts[0], typeLayoutItem.MemberLayouts[0]));
        Assert.IsTrue(ReferenceEquals(memberLayouts[1], typeLayoutItem.MemberLayouts[1]));
    }

    public void Dispose() => this.DataCache?.Dispose();
}
