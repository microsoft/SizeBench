using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppWinRT.exe")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppWinRT.pdb")]
[TestClass]
public sealed class Session_EnumerateWastefulVirtualsTests
{
    public TestContext? TestContext { get; set; }

    private string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory!, binary);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task CppTestCasesBeforeWastefulVirtualsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var wasteful = await session.EnumerateWastefulVirtuals(CancellationToken.None);
        Assert.IsNotNull(wasteful);

        // We should have found wasteful virtuals in Base1, Base1_Derived1, BaseWastefulOnlyInBefore,
        // BaseWastefulOnlyInBefore_Derived1, DerivedTypeWithVfptr and std::exception
        Assert.AreEqual(6, wasteful.Count);
        var base1Wasteful = wasteful.Single(wvi => wvi.UserDefinedType.Name == "Base1");
        var base1_derived1Wasteful = wasteful.Single(wvi => wvi.UserDefinedType.Name == "Base1_Derived1");
        var baseWastefulOnlyInBeforeWasteful = wasteful.Single(wvi => wvi.UserDefinedType.Name == "BaseWastefulOnlyInBefore");
        var baseWastefulOnlyInBefore_Derived1Wasteful = wasteful.Single(wvi => wvi.UserDefinedType.Name == "BaseWastefulOnlyInBefore_Derived1");
        var DerivedTypeWithVfptrWasteful = wasteful.Single(wvi => wvi.UserDefinedType.Name == "DerivedTypeWithVfptr");
        var std_exceptionWasteful = wasteful.Single(wvi => wvi.UserDefinedType.Name == "std::exception");

        // Base1 should have one function considered wasteful: VirtualFunctionWithNoOverrides
        Assert.AreEqual(1, base1Wasteful.WastedOverridesNonPureWithNoOverrides.Count);
        Assert.AreEqual("VirtualFunctionWithNoOverrides", base1Wasteful.WastedOverridesNonPureWithNoOverrides.First().FunctionName);
        // Should be (8 bytes per word) * (4 classes in the hierarchy) = 32 bytes waste per slot
        Assert.AreEqual(32, base1Wasteful.WastePerSlot);
        // Should be 32 total bytes of waste, since there's only one wasted slot
        Assert.AreEqual<ulong>(32, base1Wasteful.WastedSize);

        // Base1_Derived1 should have three functions considered wasteful:
        // VirtualFunctionWithNoOverrides2
        // PureVirtualFunctionWithOneOverride
        // VirtualFunctionWithNoOverrides(int)
        Assert.AreEqual(2, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count);
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count(f => f.FunctionName == "VirtualFunctionWithNoOverrides2"));
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count(f => f.FunctionName == "VirtualFunctionWithNoOverrides" && f.FunctionType?.ArgumentTypes != null && f.FunctionType?.ArgumentTypes?.Count == 1));
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesPureWithExactlyOneOverride.Count);
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesPureWithExactlyOneOverride.Count(f => f.FunctionName == "PureVirtualFunctionWithOneOverride"));
        // Should be (8 bytes per word) * (2 classes in this sub-hierarchy) = 16 bytes waste per slot
        Assert.AreEqual(16, base1_derived1Wasteful.WastePerSlot);
        // Should be 48 bytes total of waste, since there's 3 wasted slots
        Assert.AreEqual<ulong>(48, base1_derived1Wasteful.WastedSize);
    }

    [TestMethod]
    public async Task CppWinRTTypesDetectedAsCOMTypesCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var wasteful = await session.EnumerateWastefulVirtuals(CancellationToken.None);
        Assert.IsNotNull(wasteful);

        // All the types that have "TestRuntimeClass" in them should be detected as COM types because the developer couldn't really influence those (at best, the C++/WinRT codebase/team could)
        var testRuntimeClassWVIs = wasteful.Where(wvi => wvi.UserDefinedType.Name.Contains("TestRuntimeClass", StringComparison.Ordinal)).ToList();
        foreach (var wvi in testRuntimeClassWVIs)
        {
            Assert.IsTrue(wvi.IsCOMType, $"{wvi.UserDefinedType.Name} not detected correctly as a COM type");
        }
    }
}
