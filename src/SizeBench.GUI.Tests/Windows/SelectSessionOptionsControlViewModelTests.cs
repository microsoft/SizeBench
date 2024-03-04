using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Windows.Tests;

[TestClass]
public sealed class SelectSessionOptionsControlViewModelTests
{
    [TestMethod]
    public void ChangingIndividualSymbolTypesUpdatesSymbolSourcesSupported()
    {
        var propertiesChanged = new List<string>();
        var vm = new SelectSessionOptionsControlViewModel();
        vm.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName!);

        // By default we support all symbol types and should not have notified the UI yet (it's the initial state)
        Assert.AreEqual(SymbolSourcesSupported.All, vm.SymbolSourcesSupported);
        Assert.AreEqual(new SessionOptions(), vm.SessionOptions);
        Assert.IsTrue(vm.CodeSymbolsSupported);
        Assert.IsTrue(vm.DataSymbolsSupported);
        Assert.IsTrue(vm.PDATASymbolsSupported);
        Assert.IsTrue(vm.XDATASymbolsSupported);
        Assert.IsTrue(vm.RSRCSymbolsSupported);
        Assert.IsTrue(vm.OtherPESymbolsSupported);
        Assert.AreEqual(0, propertiesChanged.Count);

        // Toggle just one setting off
        vm.XDATASymbolsSupported = false;
        Assert.AreEqual(SymbolSourcesSupported.Code | 
                        SymbolSourcesSupported.DataSymbols |
                        SymbolSourcesSupported.PDATA |
                        SymbolSourcesSupported.RSRC |
                        SymbolSourcesSupported.OtherPESymbols,
                        vm.SymbolSourcesSupported);
        Assert.AreEqual(new SessionOptions() { SymbolSourcesSupported = vm.SymbolSourcesSupported }, vm.SessionOptions);
        Assert.IsTrue(vm.CodeSymbolsSupported);
        Assert.IsTrue(vm.DataSymbolsSupported);
        Assert.IsTrue(vm.PDATASymbolsSupported);
        Assert.IsFalse(vm.XDATASymbolsSupported);
        Assert.IsTrue(vm.RSRCSymbolsSupported);
        Assert.IsTrue(vm.OtherPESymbolsSupported);
        // Calculating all the right properties to refresh is tedious so we just spam empty string to force all properties to be re-evaluated, it's not
        // perf-sensitive and this UI is short-lived.
        Assert.AreEqual(1, propertiesChanged.Count);
        CollectionAssert.AreEquivalent(new[] { "" }, propertiesChanged);

        // Another setting off
        vm.RSRCSymbolsSupported = false;
        Assert.AreEqual(SymbolSourcesSupported.Code |
                        SymbolSourcesSupported.DataSymbols |
                        SymbolSourcesSupported.PDATA |
                        SymbolSourcesSupported.OtherPESymbols,
                        vm.SymbolSourcesSupported);
        Assert.AreEqual(new SessionOptions() { SymbolSourcesSupported = vm.SymbolSourcesSupported }, vm.SessionOptions);
        Assert.IsTrue(vm.CodeSymbolsSupported);
        Assert.IsTrue(vm.DataSymbolsSupported);
        Assert.IsTrue(vm.PDATASymbolsSupported);
        Assert.IsFalse(vm.XDATASymbolsSupported);
        Assert.IsFalse(vm.RSRCSymbolsSupported);
        Assert.IsTrue(vm.OtherPESymbolsSupported);
        Assert.AreEqual(2, propertiesChanged.Count);
        CollectionAssert.AreEquivalent(new[] { "", "" }, propertiesChanged);

        // Turn one back on
        vm.XDATASymbolsSupported = true;
        Assert.AreEqual(SymbolSourcesSupported.Code |
                        SymbolSourcesSupported.DataSymbols |
                        SymbolSourcesSupported.PDATA |
                        SymbolSourcesSupported.XDATA |
                        SymbolSourcesSupported.OtherPESymbols,
                        vm.SymbolSourcesSupported);
        Assert.AreEqual(new SessionOptions() { SymbolSourcesSupported = vm.SymbolSourcesSupported }, vm.SessionOptions);
        Assert.IsTrue(vm.CodeSymbolsSupported);
        Assert.IsTrue(vm.DataSymbolsSupported);
        Assert.IsTrue(vm.PDATASymbolsSupported);
        Assert.IsTrue(vm.XDATASymbolsSupported);
        Assert.IsFalse(vm.RSRCSymbolsSupported);
        Assert.IsTrue(vm.OtherPESymbolsSupported);
        Assert.AreEqual(3, propertiesChanged.Count);
        CollectionAssert.AreEquivalent(new[] { "", "", "" }, propertiesChanged);

        // And turning the last one on brings us back to the "All" state
        vm.RSRCSymbolsSupported = true;
        Assert.AreEqual(SymbolSourcesSupported.All, vm.SymbolSourcesSupported);
        Assert.AreEqual(new SessionOptions(), vm.SessionOptions);
        Assert.IsTrue(vm.CodeSymbolsSupported);
        Assert.IsTrue(vm.DataSymbolsSupported);
        Assert.IsTrue(vm.PDATASymbolsSupported);
        Assert.IsTrue(vm.XDATASymbolsSupported);
        Assert.IsTrue(vm.RSRCSymbolsSupported);
        Assert.IsTrue(vm.OtherPESymbolsSupported);
        Assert.AreEqual(4, propertiesChanged.Count);
        CollectionAssert.AreEquivalent(new[] { "", "", "", "" }, propertiesChanged);
    }
}
