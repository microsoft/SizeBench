using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class MemberToBitsOrBytesSuffixConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotAMemberViewModel()
    {
        Assert.ThrowsException<ArgumentException>(() => MemberToBitsOrBytesSuffixConverter.Instance.Convert(3, typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => MemberToBitsOrBytesSuffixConverter.Instance.Convert("test", typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => MemberToBitsOrBytesSuffixConverter.Instance.Convert(null, typeof(string), null, null));
    }

    [TestMethod]
    public void ConvertThrowsIfTargetTypeNotString()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 4, nextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.ThrowsException<ArgumentException>(() => MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(bool), null, null));
        Assert.ThrowsException<ArgumentException>(() => MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(int), null, null));
    }

    [TestMethod]
    public void NotBitfieldIsInBytes()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 4, nextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("4 bytes", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void NotBitfieldSingleByteIsInBytesWithSingularSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 1, nextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("1 byte", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void SingleBitHasSingularSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 1, nextSymIndexId++, isStaticMember: false, isBitField: true, bitStartPosition: 3, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("1 bit", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MultiBitHasPluralSuffixSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 4, nextSymIndexId++, isStaticMember: false, isBitField: true, bitStartPosition: 3, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("4 bits", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MultiBitHasBytesSuffixWhenItsFullBytesSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 16, nextSymIndexId++, isStaticMember: false, isBitField: true, bitStartPosition: 3, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("2 bytes", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void EightBitBitfieldHasSingularByteSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 8, nextSymIndexId++, isStaticMember: false, isBitField: true, bitStartPosition: 3, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("1 byte", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MemberDiffPrefersAfterForStringifyingWhenBothPresent()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var type = tliDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout != null && tlid.MemberDiffs.Any(md => md.BeforeMember != null && md.AfterMember != null));
        var typeVM = new TypeLayoutItemDiffViewModel(type, testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.BeforeMember != null && mdvm.Member.AfterMember != null);

        Assert.AreEqual("1 byte", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MemberDiffUsesBeforeIfOnlyBeforePresent()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var type = tliDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout is null);
        var typeVM = new TypeLayoutItemDiffViewModel(type, testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.BeforeMember != null && mdvm.Member.AfterMember is null);

        Assert.AreEqual("1 byte", MemberToBitsOrBytesSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => MemberToBitsOrBytesSuffixConverter.Instance.ConvertBack("4 bytes", typeof(TypeLayoutItemViewModel.MemberViewModel), null, null);
}
