using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class MemberToBitfieldSuffixConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotAMemberViewModel()
    {
        Assert.ThrowsException<ArgumentException>(() => MemberToBitfieldSuffixConverter.Instance.Convert(3, typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => MemberToBitfieldSuffixConverter.Instance.Convert("test", typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => MemberToBitfieldSuffixConverter.Instance.Convert(null, typeof(string), null, null));
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

        Assert.ThrowsException<ArgumentException>(() => MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(bool), null, null));
        Assert.ThrowsException<ArgumentException>(() => MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(int), null, null));
    }

    [TestMethod]
    public void NotBitfieldIsEmptyString()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 4, nextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual(String.Empty, MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void SingleBitHasTheRightSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 1, nextSymIndexId++, isStaticMember: false, isBitField: true, bitStartPosition: 3, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("(bit 3)", MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MultiBitHasTheRightSuffix()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var type = new BasicTypeSymbol(cache, "int", 4, symIndexId: nextSymIndexId++);
        var dataSymbol = new MemberDataSymbol(cache, "test", size: 4, nextSymIndexId++, isStaticMember: false, isBitField: true, bitStartPosition: 3, offset: 8, type: type);
        var member = TypeLayoutItemMember.FromDataSymbol(dataSymbol, baseOffset: 0);
        var MockSession = new Mock<ISession>();
        var input = new TypeLayoutItemViewModel.MemberViewModel(member, MockSession.Object, true);

        Assert.AreEqual("(bits 3-6)", MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MemberDiffPrefersAfterForStringifyingWhenBothPresent()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var type = tliDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout != null && tlid.MemberDiffs.Any(md => md.BeforeMember != null && md.AfterMember != null && md.AfterMember.IsBitField));
        var typeVM = new TypeLayoutItemDiffViewModel(type, testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.BeforeMember != null && mdvm.Member.AfterMember != null && mdvm.Member.AfterMember.IsBitField);

        Assert.AreEqual("(bits 0-1)", MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [TestMethod]
    public void MemberDiffUsesBeforeIfOnlyBeforePresent()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var type = tliDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout is null && tlid.MemberDiffs.Any(md => md.BeforeMember != null && md.AfterMember is null && md.BeforeMember.IsBitField));
        var typeVM = new TypeLayoutItemDiffViewModel(type, testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.BeforeMember != null && mdvm.Member.AfterMember is null && mdvm.Member.BeforeMember.IsBitField);

        Assert.AreEqual("(bits 0-3)", MemberToBitfieldSuffixConverter.Instance.Convert(input, typeof(string), null, null));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => MemberToBitfieldSuffixConverter.Instance.ConvertBack("(bit 5)", typeof(TypeLayoutItemViewModel.MemberViewModel), null, null);
}
