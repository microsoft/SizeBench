using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public sealed class CompilandDiffAndCOFFGroupDiffToContributionSizeConverterTests : IDisposable
{
    private DiffTestDataGenerator _generator;

    [TestInitialize]
    public void TestInitialize() => this._generator = new DiffTestDataGenerator();
    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyTakesCompilandDiffAndCOFFGroupDiffValuesInThatOrder()
        => CompilandDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.DataXxCGDiff, this._generator.A1CompilandDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */);

    [TestMethod]
    public void ReturnsCorrectSizeWhenContributionExists()
    {
        Assert.AreEqual("-500 bytes", CompilandDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.A3CompilandDiff, this._generator.RDataFooCGDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.AreEqual("200 bytes", CompilandDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.A1CompilandDiff, this._generator.TextMnCGDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void ReturnsZeroWhenNoContributionExists()
        => Assert.AreEqual("0 bytes", CompilandDiffAndCOFFGroupDiffToContributionSizeConverter.Instance.Convert(new object[] { this._generator.C1CompilandDiff, this._generator.RsrcCGDiff }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));

    public void Dispose() => this._generator.Dispose();
}
