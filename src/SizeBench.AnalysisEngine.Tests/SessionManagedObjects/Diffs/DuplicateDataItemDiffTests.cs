using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Diffs.Tests;

[TestClass]
public sealed class DuplicateDataItemDiffTests : IDisposable
{
    private readonly DiffTestDataGenerator _testGenerator = new DiffTestDataGenerator();

    [TestMethod]
    public void BeforeAndAfterNullThrows() => Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DuplicateDataItemDiff(null, null, new DiffSessionDataCache()));

    [TestMethod]
    public void BasicPropertiesCalculatedCorrectly()
    {
        var allsymbolDiffsInCompilandDiff = this._testGenerator.GenerateSymbolDiffsInCompilandList(this._testGenerator.A1CompilandDiff, typeof(StaticDataSymbol));

        var beforeDDI = new DuplicateDataItem((StaticDataSymbol)allsymbolDiffsInCompilandDiff[0].BeforeSymbol!, firstCompilandFoundIn: this._testGenerator.A1CompilandDiff.BeforeCompiland!);
        beforeDDI.AddReferencedCompilandIfNecessary(this._testGenerator.A2CompilandDiff.BeforeCompiland!, rvaInThatCompiland: 0);
        beforeDDI.AddReferencedCompilandIfNecessary(this._testGenerator.A3CompilandDiff.BeforeCompiland!, rvaInThatCompiland: 0);

        var afterDDI = new DuplicateDataItem((StaticDataSymbol)allsymbolDiffsInCompilandDiff[0].AfterSymbol!, firstCompilandFoundIn: this._testGenerator.A1CompilandDiff.AfterCompiland!);
        afterDDI.AddReferencedCompilandIfNecessary(this._testGenerator.A3CompilandDiff.AfterCompiland!, rvaInThatCompiland: 0);

        var ddiDiff = new DuplicateDataItemDiff(beforeDDI, afterDDI, this._testGenerator.DiffDataCache);

        var symbolDiff = allsymbolDiffsInCompilandDiff[0];

        Assert.IsTrue(ReferenceEquals(beforeDDI, ddiDiff.BeforeDuplicate));
        Assert.IsTrue(ReferenceEquals(afterDDI, ddiDiff.AfterDuplicate));
        Assert.AreEqual(symbolDiff.BeforeSymbol!.Size, ddiDiff.BeforeDuplicate!.Symbol.Size);
        Assert.AreEqual(symbolDiff.AfterSymbol!.Size, ddiDiff.AfterDuplicate!.Symbol.Size);

        // Verify before/after duplicates are what they should be
        Assert.AreEqual(symbolDiff.BeforeSymbol.Size * 3, ddiDiff.BeforeDuplicate.TotalSize);
        Assert.AreEqual(symbolDiff.AfterSymbol.Size * 2, ddiDiff.AfterDuplicate.TotalSize);
        Assert.AreEqual(symbolDiff.BeforeSymbol.Size * 2, ddiDiff.BeforeDuplicate.WastedSize);
        Assert.AreEqual(symbolDiff.AfterSymbol.Size * 1, ddiDiff.AfterDuplicate.WastedSize);

        // Verify the actual calculations of the duplicate item diff
        Assert.AreEqual(afterDDI.WastedSize - (long)beforeDDI.WastedSize, ddiDiff.WastedSizeDiff);
        Assert.AreEqual(afterDDI.TotalSize - (long)beforeDDI.TotalSize, ddiDiff.SizeDiff);
        Assert.AreEqual(afterDDI.WastedSize, ddiDiff.WastedSizeRemaining);
        Assert.AreEqual(symbolDiff.Name, ddiDiff.SymbolDiff.Name);
    }

    [TestMethod]
    public void BeforeDDINullWorks()
    {
        var allsymbolDiffsInCompilandDiff = this._testGenerator.GenerateSymbolDiffsInCompilandList(this._testGenerator.A1CompilandDiff, typeof(StaticDataSymbol));

        var symbolDiff = allsymbolDiffsInCompilandDiff.First(symDiff => symDiff.BeforeSymbol is null);

        var afterDDI = new DuplicateDataItem((StaticDataSymbol)(symbolDiff.AfterSymbol!), firstCompilandFoundIn: this._testGenerator.A1CompilandDiff.AfterCompiland!);
        afterDDI.AddReferencedCompilandIfNecessary(this._testGenerator.A3CompilandDiff.AfterCompiland!, rvaInThatCompiland: 0);

        var ddiDiff = new DuplicateDataItemDiff(null, afterDDI, this._testGenerator.DiffDataCache);

        Assert.IsNull(ddiDiff.BeforeDuplicate);
        Assert.IsTrue(ReferenceEquals(afterDDI, ddiDiff.AfterDuplicate));
        Assert.AreEqual(symbolDiff.AfterSymbol!.Size, ddiDiff.AfterDuplicate!.Symbol.Size);

        // Verify before/after duplicates are what they should be
        Assert.AreEqual(symbolDiff.AfterSymbol.Size * 2, ddiDiff.AfterDuplicate.TotalSize);
        Assert.AreEqual(symbolDiff.AfterSymbol.Size * 1, ddiDiff.AfterDuplicate.WastedSize);

        // Verify the actual calculations of the duplicate item diff
        Assert.AreEqual(afterDDI.WastedSize, ddiDiff.WastedSizeDiff);
        Assert.AreEqual((long)afterDDI.TotalSize, ddiDiff.SizeDiff);
        Assert.AreEqual(afterDDI.WastedSize, ddiDiff.WastedSizeRemaining);
        Assert.AreEqual(symbolDiff.Name, ddiDiff.SymbolDiff.Name);
    }

    [TestMethod]
    public void AfterDDINullWorks()
    {
        var allsymbolDiffsInCompilandDiff = this._testGenerator.GenerateSymbolDiffsInCompilandList(this._testGenerator.A1CompilandDiff, typeof(StaticDataSymbol));

        var symbolDiff = allsymbolDiffsInCompilandDiff.First(symDiff => symDiff.AfterSymbol is null);

        var beforeDDI = new DuplicateDataItem((StaticDataSymbol)(symbolDiff.BeforeSymbol!), firstCompilandFoundIn: this._testGenerator.A1CompilandDiff.BeforeCompiland!);
        beforeDDI.AddReferencedCompilandIfNecessary(this._testGenerator.A2CompilandDiff.BeforeCompiland!, rvaInThatCompiland: 0);
        beforeDDI.AddReferencedCompilandIfNecessary(this._testGenerator.A3CompilandDiff.BeforeCompiland!, rvaInThatCompiland: 0);

        var ddiDiff = new DuplicateDataItemDiff(beforeDDI, null, this._testGenerator.DiffDataCache);

        Assert.IsTrue(ReferenceEquals(beforeDDI, ddiDiff.BeforeDuplicate));
        Assert.IsNull(ddiDiff.AfterDuplicate);
        Assert.AreEqual(symbolDiff.BeforeSymbol!.Size, ddiDiff.BeforeDuplicate!.Symbol.Size);

        // Verify before/after duplicates are what they should be
        Assert.AreEqual(symbolDiff.BeforeSymbol.Size * 3, ddiDiff.BeforeDuplicate.TotalSize);
        Assert.AreEqual(symbolDiff.BeforeSymbol.Size * 2, ddiDiff.BeforeDuplicate.WastedSize);

        // Verify the actual calculations of the duplicate item diff
        Assert.AreEqual(0 - (long)beforeDDI.WastedSize, ddiDiff.WastedSizeDiff);
        Assert.AreEqual(0 - (long)beforeDDI.TotalSize, ddiDiff.SizeDiff);
        Assert.AreEqual((ulong)0, ddiDiff.WastedSizeRemaining);
        Assert.AreEqual(symbolDiff.Name, ddiDiff.SymbolDiff.Name);
    }

    public void Dispose() => this._testGenerator.Dispose();
}
