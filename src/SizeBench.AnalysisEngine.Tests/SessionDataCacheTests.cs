using Dia2Lib;

namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public sealed class SessionDataCacheTests : IDisposable
{
    internal Mock<IDiaSession> MockDIASession = new Mock<IDiaSession>();
    internal SessionDataCache Cache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockDIASession = new Mock<IDiaSession>();
        this.Cache = new SessionDataCache();
    }

    [TestMethod]
    public void InitiallyEmpty()
    {
        Assert.IsNull(this.Cache.AllBinarySections);
        Assert.IsNull(this.Cache.AllCOFFGroups);
        Assert.IsNull(this.Cache.AllCompilands);
        Assert.IsNull(this.Cache.AllLibs);
        Assert.IsNull(this.Cache.AllSourceFiles);
    }

    public void Dispose() => this.Cache.Dispose();
}
