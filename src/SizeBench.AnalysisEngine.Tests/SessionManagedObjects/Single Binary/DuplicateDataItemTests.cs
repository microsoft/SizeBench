using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public sealed class DuplicateDataItemTests : IDisposable
{
    SessionDataCache DataCache = new SessionDataCache();
    StaticDataSymbol? DataSymbol;
    Compiland? Compiland1;
    Compiland? Compiland2;
    Compiland? Compiland3;

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataCache = new SessionDataCache
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.DataSymbol = new StaticDataSymbol(this.DataCache, "testDataSymbolName", rva: 0, size: 16,
                                               isVirtualSize: false, symIndexId: 0, dataKind: DataKind.DataIsFileStatic,
                                               type: null, referencedIn: null, functionParent: null);
        var Lib = new Library("test lib name");
        this.Compiland1 = new Compiland(this.DataCache, @"c:\foo\1.obj", Lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 1);
        this.Compiland2 = new Compiland(this.DataCache, @"c:\foo\2.obj", Lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 2);
        this.Compiland3 = new Compiland(this.DataCache, @"c:\foo\3.obj", Lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 3);
    }

    [TestMethod]
    public void InitiallyConstructedItemHasZeroWastedSize()
    {
        var item = new DuplicateDataItem(this.DataSymbol!, this.Compiland1!);
        Assert.AreEqual<ulong>(0, item.WastedSize);
        Assert.AreEqual(16u, item.Symbol.Size);
    }

    [TestMethod]
    public void AddingMoreReferencesIncreasesWastedSize()
    {
        var item = new DuplicateDataItem(this.DataSymbol!, this.Compiland1!);

        item.AddReferencedCompilandIfNecessary(this.Compiland2!, 16);
        Assert.AreEqual<ulong>(16, item.WastedSize);
        Assert.AreEqual(16u, item.Symbol.Size);

        item.AddReferencedCompilandIfNecessary(this.Compiland3!, 32);
        Assert.AreEqual<ulong>(32, item.WastedSize);
        Assert.AreEqual(16u, item.Symbol.Size);
    }

    [TestMethod]
    public void AddingTheSameCompilandTwiceIsANoOp()
    {
        var item = new DuplicateDataItem(this.DataSymbol!, this.Compiland1!);
        item.AddReferencedCompilandIfNecessary(this.Compiland1!, 16);
        Assert.AreEqual<ulong>(0, item.WastedSize);
        Assert.AreEqual(16u, item.Symbol.Size);

        item.AddReferencedCompilandIfNecessary(this.Compiland2!, 32);
        Assert.AreEqual<ulong>(16, item.WastedSize);
        Assert.AreEqual(16u, item.Symbol.Size);

        item.AddReferencedCompilandIfNecessary(this.Compiland2!, 48);
        Assert.AreEqual<ulong>(16, item.WastedSize);
        Assert.AreEqual(16u, item.Symbol.Size);
    }

    public void Dispose() => this.DataCache.Dispose();
}
