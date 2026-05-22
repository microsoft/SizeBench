using System.IO;
using SizeBench.TestInfrastructure;

namespace SizeBench.GUI.Tests;

[TestClass]
public sealed class DisassemblySettingsStoreTests
{
    [TestMethod]
    public void ZoomPercentPersistsAcrossStoreInstances()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var storagePath = Path.Combine(tempDirectory, "DisassemblySettings.json");
            using var logger = new TestNoOpApplicationLogger();
            var store = new DisassemblySettingsStore(logger, storagePath);

            Assert.AreEqual(DisassemblySettingsStore.DefaultZoomPercent, store.TemplateFoldabilityDisassemblyZoomPercent);

            store.TemplateFoldabilityDisassemblyZoomPercent = 140;

            var reloadedStore = new DisassemblySettingsStore(logger, storagePath);
            Assert.AreEqual(140, reloadedStore.TemplateFoldabilityDisassemblyZoomPercent);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
