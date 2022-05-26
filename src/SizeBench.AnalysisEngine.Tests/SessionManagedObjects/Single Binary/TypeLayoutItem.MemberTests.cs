using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public class TypeLayoutItem_MemberTests
{
    [TestMethod]
    public void VfptrMemberConstructedCorrectly()
    {
        var vfptr = TypeLayoutItemMember.CreateVfptrMember(baseOffset: 16, size: 8);

        Assert.IsFalse(vfptr.IsAlignmentMember);
        Assert.IsFalse(vfptr.IsBitField);
        Assert.AreEqual("vfptr", vfptr.Name);
        Assert.AreEqual(0u, vfptr.BitStartPosition);
        Assert.AreEqual(0u, vfptr.NumberOfBits);
        Assert.AreEqual(16u, vfptr.Offset);
        Assert.AreEqual(8u, vfptr.Size);
        Assert.IsNull(vfptr.Type);
    }

    [TestMethod]
    public void RegularAlignmentMemberConstructedCorrectly()
    {
        var alignmentMember = TypeLayoutItemMember.CreateAlignmentMember(amountOfAlignment: 3, offsetOfAlignment: 1, isBitfield: false, bitStartPosition: 0, isTailSlop: false);

        Assert.IsTrue(alignmentMember.IsAlignmentMember);
        Assert.IsFalse(alignmentMember.IsBitField);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, alignmentMember.Name);
        Assert.AreEqual(0u, alignmentMember.NumberOfBits);
        Assert.AreEqual(0u, alignmentMember.BitStartPosition);
        Assert.AreEqual(1u, alignmentMember.Offset);
        Assert.AreEqual(3u, alignmentMember.Size);
        Assert.IsNull(alignmentMember.Type);

        var bitfieldAlignment = TypeLayoutItemMember.CreateAlignmentMember(amountOfAlignment: 0.875m, offsetOfAlignment: 1.125m, isBitfield: true, bitStartPosition: 9, isTailSlop: false);

        Assert.IsTrue(bitfieldAlignment.IsAlignmentMember);
        Assert.IsTrue(bitfieldAlignment.IsBitField);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, bitfieldAlignment.Name);
        Assert.AreEqual(7u, bitfieldAlignment.NumberOfBits);
        Assert.AreEqual(9u, bitfieldAlignment.BitStartPosition);
        Assert.AreEqual(1.125m, bitfieldAlignment.Offset);
        Assert.AreEqual(0.875m, bitfieldAlignment.Size);
        Assert.IsNull(bitfieldAlignment.Type);
    }

    [TestMethod]
    public void TailSlopAlignmentConstructedCorrectly()
    {
        var alignmentMember = TypeLayoutItemMember.CreateAlignmentMember(amountOfAlignment: 3, offsetOfAlignment: 1, isBitfield: false, bitStartPosition: 0, isTailSlop: true);

        Assert.IsTrue(alignmentMember.IsAlignmentMember);
        Assert.IsFalse(alignmentMember.IsBitField);
        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, alignmentMember.Name);
        Assert.AreEqual(0u, alignmentMember.NumberOfBits);
        Assert.AreEqual(0u, alignmentMember.BitStartPosition);
        Assert.AreEqual(1u, alignmentMember.Offset);
        Assert.AreEqual(3u, alignmentMember.Size);
        Assert.IsNull(alignmentMember.Type);

        var bitfieldAlignment = TypeLayoutItemMember.CreateAlignmentMember(amountOfAlignment: 0.875m, offsetOfAlignment: 1.125m, isBitfield: true, bitStartPosition: 9, isTailSlop: true);

        Assert.IsTrue(bitfieldAlignment.IsAlignmentMember);
        Assert.IsTrue(bitfieldAlignment.IsBitField);
        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, bitfieldAlignment.Name);
        Assert.AreEqual(7u, bitfieldAlignment.NumberOfBits);
        Assert.AreEqual(9u, bitfieldAlignment.BitStartPosition);
        Assert.AreEqual(1.125m, bitfieldAlignment.Offset);
        Assert.AreEqual(0.875m, bitfieldAlignment.Size);
        Assert.IsNull(bitfieldAlignment.Type);
    }

    [TestMethod]
    public void DataSymbolMemberConstructedCorrectly()
    {
        using var cache = new SessionDataCache();
        var dataType = new BasicTypeSymbol(cache, "int", size: 4, symIndexId: 0);
        var data = new MemberDataSymbol(cache, "test_member", size: 4, symIndexId: 1, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 16, type: dataType);
        var bitfieldData = new MemberDataSymbol(cache, "test_bitfield", size: 3, symIndexId: 2, isStaticMember: false, isBitField: true, bitStartPosition: 4, offset: 20, type: dataType);

        var dataMember = TypeLayoutItemMember.FromDataSymbol(data, baseOffset: 16);
        Assert.IsFalse(dataMember.IsAlignmentMember);
        Assert.IsFalse(dataMember.IsBitField);
        Assert.AreEqual("test_member", dataMember.Name);
        Assert.AreEqual(0u, dataMember.NumberOfBits);
        Assert.AreEqual(0u, dataMember.BitStartPosition);
        Assert.AreEqual(32u, dataMember.Offset); // 16 bytes of offset from the base class + 16 bytes within this type (from the DataSymbol) = 32
        Assert.AreEqual(4u, dataMember.Size);
        Assert.AreEqual(dataType, dataMember.Type);

        var bitfieldDataMember = TypeLayoutItemMember.FromDataSymbol(bitfieldData, baseOffset: 16);
        Assert.IsFalse(bitfieldDataMember.IsAlignmentMember);
        Assert.IsTrue(bitfieldDataMember.IsBitField);
        Assert.AreEqual("test_bitfield", bitfieldDataMember.Name);
        Assert.AreEqual(3u, bitfieldDataMember.NumberOfBits);
        Assert.AreEqual(4u, bitfieldDataMember.BitStartPosition);
        Assert.AreEqual(36.5m, bitfieldDataMember.Offset); // 16 bytes of offset from the base class + 20.5 bytes within this type (from the DataSymbol's BitStartPosition and Offset) = 36.5
        Assert.AreEqual(0.375m, bitfieldDataMember.Size);
        Assert.AreEqual(dataType, bitfieldDataMember.Type);
    }
}
