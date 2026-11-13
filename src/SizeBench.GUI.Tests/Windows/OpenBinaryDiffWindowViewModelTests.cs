using System.IO;
using SizeBench.LocalBuild;
using SizeBench.PathLocators;

namespace SizeBench.GUI.Windows.Tests;

[TestClass]
public class OpenBinaryDiffWindowViewModelTests
{
    [TestMethod]
    public void OKButtonBeginsDisabled()
    {
        var vm = CreateViewModel();
        Assert.IsFalse(vm.OKEnabled);
    }

    [TestMethod]
    public void BothBinariesRequireLocalPdbsOrConfiguredSymbolServer()
    {
        var existingPath = typeof(OpenBinaryDiffWindowViewModelTests).Assembly.Location;
        var vm = CreateViewModel();
        vm.Before.BinaryPath = existingPath;
        vm.After.BinaryPath = existingPath;
        vm.Before.PDBPath = String.Empty;
        vm.After.PDBPath = String.Empty;
        Assert.IsFalse(vm.OKEnabled);

        vm.SelectSessionOptionsControlViewModel.UseSymbolServer = true;
        Assert.IsFalse(vm.OKEnabled);

        vm.SelectSessionOptionsControlViewModel.SymbolServerPathsText = "srv*C:\\Symbols*https://msdl.microsoft.com/download/symbols";
        Assert.IsTrue(vm.OKEnabled);

        vm.Before.BinaryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.IsFalse(vm.OKEnabled);
        vm.Before.BinaryPath = existingPath;
        Assert.IsTrue(vm.OKEnabled);

        vm.After.BinaryPath = String.Empty;
        Assert.IsFalse(vm.OKEnabled);
        vm.After.BinaryPath = existingPath;
        Assert.IsTrue(vm.OKEnabled);

        vm.Before.PDBPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.IsFalse(vm.OKEnabled);
        vm.Before.PDBPath = String.Empty;
        Assert.IsTrue(vm.OKEnabled);

        vm.SelectSessionOptionsControlViewModel.SymbolServerPathsText = " \r\n ";
        Assert.IsFalse(vm.OKEnabled);
    }

    [TestMethod]
    public void LocalPdbsRemainUsableWhenSymbolServerIsDisabled()
    {
        var existingPath = typeof(OpenBinaryDiffWindowViewModelTests).Assembly.Location;
        var vm = CreateViewModel();
        vm.Before.BinaryPath = existingPath;
        vm.After.BinaryPath = existingPath;
        vm.Before.PDBPath = existingPath;
        vm.After.PDBPath = String.Empty;
        Assert.IsFalse(vm.OKEnabled);

        vm.SelectSessionOptionsControlViewModel.SymbolServerPathsText = "C:\\Symbols";
        vm.SelectSessionOptionsControlViewModel.UseSymbolServer = true;
        Assert.IsTrue(vm.OKEnabled);

        vm.SelectSessionOptionsControlViewModel.UseSymbolServer = false;
        Assert.IsFalse(vm.OKEnabled);

        vm.After.PDBPath = existingPath;
        Assert.IsTrue(vm.OKEnabled);
    }

    private static OpenBinaryDiffWindowViewModel CreateViewModel()
        => new OpenBinaryDiffWindowViewModel(new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }),
                                             new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }),
                                             new SelectSessionOptionsControlViewModel());
}
