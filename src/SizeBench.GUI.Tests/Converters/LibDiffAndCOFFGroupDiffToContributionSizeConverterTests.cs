using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public sealed class LibDiffAndCOFFGroupDiffToContributionSizeConverterTests : IDisposable
{
    private DiffTestDataGenerator _generator;

    [TestInitialize]
    public void TestInitialize() => this._generator = new DiffTestDataGenerator();

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyTakesLibDiffAndCOFFGroupDiffValuesInThatOrder()
        => LibDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.DataXxCGDiff, this._generator.ALibDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */);

    [TestMethod]
    public void ReturnsCorrectSizeWhenContributionExists()
    {
        Assert.AreEqual("-25 bytes", LibDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.ALibDiff, this._generator.DataZzCGDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.AreEqual("200 bytes", LibDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.DLibDiff, this._generator.RsrcCGDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void ReturnsZeroWhenNoContributionExists()
        => Assert.AreEqual("0 bytes", LibDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.CLibDiff, this._generator.RDataAftCGDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));

    public void Dispose() => this._generator.Dispose();
}
