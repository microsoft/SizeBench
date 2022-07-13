using System.Windows;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class IsAlignmentMemberToFontWeightConverterTests
{
    [TestMethod]
    public void ConvertThrowsIfValueNotABool()
    {
        Assert.ThrowsException<ArgumentException>(() => IsAlignmentMemberToFontWeightConverter.Instance.Convert(3, typeof(FontWeight), null, null));
        Assert.ThrowsException<ArgumentException>(() => IsAlignmentMemberToFontWeightConverter.Instance.Convert("test", typeof(FontWeight), null, null));
        Assert.ThrowsException<ArgumentException>(() => IsAlignmentMemberToFontWeightConverter.Instance.Convert(FontWeights.Bold, typeof(FontWeight), null, null));
    }

    [TestMethod]
    public void ConvertThrowsIfTargetTypeNotFontWeight()
    {
        Assert.ThrowsException<ArgumentException>(() => IsAlignmentMemberToFontWeightConverter.Instance.Convert(true, typeof(bool), null, null));
        Assert.ThrowsException<ArgumentException>(() => IsAlignmentMemberToFontWeightConverter.Instance.Convert(true, typeof(string), null, null));
        Assert.ThrowsException<ArgumentException>(() => IsAlignmentMemberToFontWeightConverter.Instance.Convert(true, typeof(int), null, null));
    }

    [TestMethod]
    public void NotAlignmentMemberIsNormalFontWeight()
    {
        Assert.AreEqual(FontWeights.Normal, IsAlignmentMemberToFontWeightConverter.Instance.Convert(false, typeof(FontWeight), null, null));
    }

    [TestMethod]
    public void AlignmentMemberIsBold()
    {
        Assert.AreEqual(FontWeights.Bold, IsAlignmentMemberToFontWeightConverter.Instance.Convert(true, typeof(FontWeight), null, null));
    }

    [ExpectedException(typeof(NotImplementedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertBackIsNotImplemented()
    {
        IsAlignmentMemberToFontWeightConverter.Instance.ConvertBack(FontWeights.Bold, typeof(bool), null, null);
    }
}
