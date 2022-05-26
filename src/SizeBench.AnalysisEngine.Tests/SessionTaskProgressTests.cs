namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public class SessionTaskProgressTests
{
    [TestMethod]
    public void NullItemTotalMeansIndeterminate()
    {
        var progress = new SessionTaskProgress("Test message", 3, null);
        Assert.AreEqual("Test message", progress.Message);
        Assert.AreEqual(3u, progress.ItemsComplete);
        Assert.AreEqual(0u, progress.ItemsTotal);
        Assert.IsTrue(progress.IsProgressIndeterminate);
    }

    [TestMethod]
    public void ItemTotalPresentMeansNotIndeterminate()
    {
        var progress = new SessionTaskProgress("Test message", 3, 10);
        Assert.AreEqual("Test message", progress.Message);
        Assert.AreEqual(3u, progress.ItemsComplete);
        Assert.AreEqual(10u, progress.ItemsTotal);
        Assert.IsFalse(progress.IsProgressIndeterminate);
    }
}
