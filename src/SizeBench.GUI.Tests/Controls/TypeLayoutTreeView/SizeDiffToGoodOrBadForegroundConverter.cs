using System.Windows.Media;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class SizeDiffToGoodOrBadForegroundConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotATypeOrMemberDiffViewModel()
    {
        Assert.ThrowsException<ArgumentException>(() => SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(3, typeof(Brush), null, null));
        Assert.ThrowsException<ArgumentException>(() => SizeDiffToGoodOrBadForegroundConverter.Instance.Convert("test", typeof(Brush), null, null));
        Assert.ThrowsException<ArgumentException>(() => SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(null, typeof(Brush), null, null));
    }

    [TestMethod]
    public void ConvertThrowsIfTargetTypeNotBrush()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs[0], testDataGenerator.MockDiffSession.Object);

        Assert.ThrowsException<ArgumentException>(() => SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(object), null, null));
        Assert.ThrowsException<ArgumentException>(() => SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(int), null, null));
    }

    [TestMethod]
    public void TypeGoneInAfterIsGood()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.AfterTypeLayout is null), testDataGenerator.MockDiffSession.Object);

        // Comparing two SolidColorBrushes doesn't work, since they don't implement equality properly, so let's just look at the color
        Assert.AreEqual(new SolidColorBrush(Colors.Green).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void TypeNewInAfterIsBad()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.BeforeTypeLayout is null), testDataGenerator.MockDiffSession.Object);

        // Comparing two SolidColorBrushes doesn't work, since they don't implement equality properly, so let's just look at the color
        Assert.AreEqual(new SolidColorBrush(Colors.Red).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void TypeZeroSizeDiffIsNeutral()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.InstanceSizeDiff == 0), testDataGenerator.MockDiffSession.Object);

        Assert.AreEqual(new SolidColorBrush(Colors.Black).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void TypePositiveSizeDiffIsBad()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.InstanceSizeDiff > 0), testDataGenerator.MockDiffSession.Object);

        Assert.AreEqual(new SolidColorBrush(Colors.Red).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void TypeNegativeSizeDiffIsGood()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var input = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.InstanceSizeDiff < 0), testDataGenerator.MockDiffSession.Object);

        Assert.AreEqual(new SolidColorBrush(Colors.Green).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void MemberGoneInAfterIsGood()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.AfterTypeLayout is null), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First();

        Assert.AreEqual(new SolidColorBrush(Colors.Green).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void MemberNewInAfterIsBad()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.BeforeTypeLayout is null), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First();

        Assert.AreEqual(new SolidColorBrush(Colors.Red).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void MemberZeroSizeDiffIsNeutral()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.MemberDiffs.Any(md => md.SizeDiff == 0)), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.SizeDiff == 0);

        Assert.AreEqual(new SolidColorBrush(Colors.Black).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void MemberPositiveSizeDiffIsBad()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.MemberDiffs.Any(md => md.SizeDiff > 0)), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.SizeDiff > 0);

        Assert.AreEqual(new SolidColorBrush(Colors.Red).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [TestMethod]
    public void MemberNegativeSizeDiffIsGood()
    {
        using var testDataGenerator = new DiffTestDataGenerator();
        var tliDiffs = testDataGenerator.GenerateTypeLayoutItemDiffs(out var beforeTLIList, out var afterTLIList);
        var typeVM = new TypeLayoutItemDiffViewModel(tliDiffs.First(tlid => tlid.MemberDiffs.Any(md => md.SizeDiff < 0)), testDataGenerator.MockDiffSession.Object);
        var input = typeVM.Members.First(mdvm => mdvm.Member.SizeDiff < 0);

        Assert.AreEqual(new SolidColorBrush(Colors.Green).Color, (SizeDiffToGoodOrBadForegroundConverter.Instance.Convert(input, typeof(Brush), null, null) as SolidColorBrush).Color);
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
        => SizeDiffToGoodOrBadForegroundConverter.Instance.ConvertBack(new SolidColorBrush(Colors.Red), typeof(TypeLayoutItemDiffViewModel.MemberDiffViewModel), null, null);
}
