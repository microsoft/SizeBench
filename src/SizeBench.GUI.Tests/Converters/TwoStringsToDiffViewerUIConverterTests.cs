using System.Windows;
using DiffPlex.Wpf.Controls;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[STATestClass]
public sealed class TwoStringsToDiffViewerUIConverterTests
{
    [TestMethod]
    public void TargetTypeMustBeObject()
        => Assert.ThrowsException<ArgumentException>(() => TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { 5 }, typeof(int), null /* ConverterParameter */, null /* CultureInfo */));

    [TestMethod]
    public void MoreThanThreeInputsNotAccepted()
    {
        Assert.ThrowsException<ArgumentException>(() => TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", "disasm 2", "disasm 3", "disasm 4" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.ThrowsException<ArgumentException>(() => TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", "disasm 2", "disasm 3", "disasm 4", "disasm 5" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void InsufficientInputsReturnsNull()
    {
        Assert.IsNull(TwoStringsToDiffViewerUIConverter.Instance.Convert(null, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.IsNull(TwoStringsToDiffViewerUIConverter.Instance.Convert(Array.Empty<object>(), typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.IsNull(TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.IsNull(TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { null, "disasm 2" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.IsNull(TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", null }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void IfEitherInputIsNotAStringThrow()
    {
        Assert.ThrowsException<ArgumentException>(() => TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", 23 }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.ThrowsException<ArgumentException>(() => TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { 0.8f, "disasm 2" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void ThirdArgumentMustBeZoomPercentInt()
        => Assert.ThrowsException<ArgumentException>(() => TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", "disasm 2", "100" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */));

    [TestMethod]
    public void IfBothInputsAreStringsReturnsSomething()
    {
        // Validating that this is actually a diff viewer, that it is useful (good colors, etc.) is really hard and doesn't seem worth it, so we'll just check
        // that it's a FrameworkElement for now and assume what we got back is good.
        Assert.IsInstanceOfType(TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", "disasm 2" }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */),
                                typeof(FrameworkElement));
    }

    [TestMethod]
    public void ConvertBackThrows()
        => Assert.ThrowsException<NotImplementedException>(() => TwoStringsToDiffViewerUIConverter.Instance.ConvertBack(new FrameworkElement(), new Type[] { typeof(string), typeof(string) }, null /* ConverterParameter */, null /* CultureInfo */));

    [TestMethod]
    public void ThirdArgumentControlsFontSize()
    {
        var diffViewer = (DiffViewer)TwoStringsToDiffViewerUIConverter.Instance.Convert(new object[] { "disasm 1", "disasm 2", 120 }, typeof(object), null /* ConverterParameter */, null /* CultureInfo */);
        Assert.AreEqual(19.2, diffViewer.FontSize, 0.001);
    }
}
