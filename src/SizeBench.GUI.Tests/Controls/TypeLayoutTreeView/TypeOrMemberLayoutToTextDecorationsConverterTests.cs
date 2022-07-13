using System.Windows;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class TypeOrMemberLayoutToTextDecorationsConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotATypeOrMemberDiffViewModel()
    {
        Assert.ThrowsException<ArgumentException>(() => TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(3, typeof(TextDecorationCollection), null, null));
        Assert.ThrowsException<ArgumentException>(() => TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert("test", typeof(TextDecorationCollection), null, null));
        Assert.ThrowsException<ArgumentException>(() => TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(null, typeof(TextDecorationCollection), null, null));
    }

    [TestMethod]
    public void ConvertThrowsIfTargetTypeNotTextDecorationCollection()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs[0], testDataGenerator.MockDiffSession.Object);

        Assert.ThrowsException<ArgumentException>(() => TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(input, typeof(bool), null, null));
        Assert.ThrowsException<ArgumentException>(() => TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(input, typeof(int), null, null));
    }

    [TestMethod]
    public void TypeGoneInAfterIsStrikethrough()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.AfterTypeLayout is null), testDataGenerator.MockDiffSession.Object);

        Assert.AreEqual(TextDecorations.Strikethrough, TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(input, typeof(TextDecorationCollection), null, null));
    }

    [TestMethod]
    public void TypePresentInBothIsNotDecorated()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout != null), testDataGenerator.MockDiffSession.Object);

        Assert.IsNull(TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(input, typeof(TextDecorationCollection), null, null));
    }

    [TestMethod]
    public void MemberGoneInAfterIsStrikethrough()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.AfterTypeLayout is null), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First();

        Assert.AreEqual(TextDecorations.Strikethrough, TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(input, typeof(TextDecorationCollection), null, null));
    }

    [TestMethod]
    public void MemberPresentInBothIsNotDecorated()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout != null && tlid.MemberDiffs.Any(md => md.BeforeMember != null && md.AfterMember != null)), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.BeforeMember != null && mdvm.Member.AfterMember != null);

        Assert.IsNull(TypeOrMemberLayoutToTextDecorationsConverter.Instance.Convert(input, typeof(TextDecorationCollection), null, null));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => TypeOrMemberLayoutToTextDecorationsConverter.Instance.ConvertBack(TextDecorations.Strikethrough, typeof(TypeLayoutItemDiffViewModel.MemberDiffViewModel), null, null);
}
