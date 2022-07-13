using SizeBench.LocalBuild;
using SizeBench.PathLocators;

namespace SizeBench.GUI.Windows.Tests;

[TestClass]
public class OpenBinaryDiffWindowViewModelTests
{
    [TestMethod]
    public void OKButtonBeginsDisabled()
    {
        var vm = new OpenBinaryDiffWindowViewModel(new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }), new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }));
        Assert.IsFalse(vm.OKEnabled);
    }
}
