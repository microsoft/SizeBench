using SizeBench.LocalBuild;
using SizeBench.PathLocators;

namespace SizeBench.GUI.Windows.Tests;

[TestClass]
public class OpenSingleBinaryWindowViewModelTests
{
    [TestMethod]
    public void OKButtonBeginsDisabled()
    {
        var vm = new OpenSingleBinaryWindowViewModel(new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }),
                                                     new SelectSessionOptionsControlViewModel());
        Assert.IsFalse(vm.OKEnabled);
    }

    [TestMethod]
    public void OKButtonRemainsDisabledWithJustBinaryPathSet()
    {
        var propertiesChanged = new List<string>();
        var vm = new OpenSingleBinaryWindowViewModel(new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }),
                                                     new SelectSessionOptionsControlViewModel());
        vm.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName!);

        vm.SelectSingleBinaryAndPDBControlViewModel.BinaryPath = "Foo.dll";

        Assert.IsFalse(vm.OKEnabled);
        Assert.AreEqual(0, propertiesChanged.Count);
    }
}
