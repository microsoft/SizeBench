using System.IO;
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
        Assert.IsEmpty(propertiesChanged);
    }

    [TestMethod]
    public void OKButtonEnabledWhenBinaryExistsAndSymbolServerConfigured()
    {
        // Pick an existing binary that we know does not have a matching PDB next to it on disk.
        // notepad.exe is always present in System32 on test machines and won't have a sibling .pdb.
        var existingBinaryPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        if (!File.Exists(existingBinaryPath))
        {
            Assert.Inconclusive($"Cannot run test - expected sentinel binary at {existingBinaryPath}");
        }

        var sso = new SelectSessionOptionsControlViewModel();
        var vm = new OpenSingleBinaryWindowViewModel(new SelectSingleBinaryAndPDBControlViewModel(new IBinaryLocator[] { new LocalBuildPathLocator() }),
                                                     sso);

        // Just the binary, no PDB, no symbol server -> still disabled
        vm.SelectSingleBinaryAndPDBControlViewModel.BinaryPath = existingBinaryPath;
        // Defensive: blow away any inferred PDB so the symbol-server branch is the only way to enable OK
        vm.SelectSingleBinaryAndPDBControlViewModel.PDBPath = String.Empty;
        Assert.IsFalse(vm.OKEnabled);

        // Enabling the symbol server checkbox without any paths -> still disabled
        sso.UseSymbolServer = true;
        sso.SymbolServerPathsText = String.Empty;
        Assert.IsFalse(vm.OKEnabled);

        // Once a path is provided, OK becomes enabled even without a local PDB
        sso.SymbolServerPathsText = "srv*https://msdl.microsoft.com/download/symbols";
        Assert.IsTrue(vm.OKEnabled);

        // Disabling the symbol server toggle should drop the OK enablement again
        sso.UseSymbolServer = false;
        Assert.IsFalse(vm.OKEnabled);
    }
}
