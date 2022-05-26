using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateWastefulVirtualItemDiffsTests
{
    public TestContext? TestContext { get; set; }

    private string BeforeBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string BeforePDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    private string AfterBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll");

    private string AfterPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb");

    [TestMethod]
    public async Task DiffWithSelfHasZeroSizeDiff()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.BeforeBinaryPath, this.BeforePDBPath,
                                                               logger);
        var wviDiffs = await diffSession.EnumerateWastefulVirtualItemDiffs(CancellationToken.None);
        Assert.IsNotNull(wviDiffs);

        // No entries returned, since every type is 'trivially diffed' to a zero size (no hierarchy changes, no override changes, no size changes)
        Assert.AreEqual(0, wviDiffs.Count);
    }

    [TestMethod]
    public async Task DiffsFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var wviDiffs = await diffSession.EnumerateWastefulVirtualItemDiffs(CancellationToken.None);
        Assert.IsNotNull(wviDiffs);

        Assert.AreEqual(8, wviDiffs.Count);

        // Test case for: removing a virtual, which saves on the whole hierarchy of types
        // Base1_Derived1::PureVirtualFunctionWithOneOverride
        var base1_derived1Diff = wviDiffs.Single(wviDiff => wviDiff.TypeName == "Base1_Derived1");
        Assert.AreEqual(-16 + 16, base1_derived1Diff.WastedSizeDiff);
        Assert.AreEqual(48u, base1_derived1Diff.WastedSizeRemaining);
        Assert.AreEqual(1, base1_derived1Diff.TypeHierarchyChanges.Count);
        Assert.AreEqual("Base1_Derived1_MoreDerived2", base1_derived1Diff.TypeHierarchyChanges[0].Type.Name);
        Assert.AreEqual(16, base1_derived1Diff.TypeHierarchyChanges[0].WasteChange);
        Assert.AreEqual(1, base1_derived1Diff.WastedOverrideChanges.Count);
        Assert.AreEqual("void Base1_Derived1_MoreDerived1::PureVirtualFunctionWithOneOverride() override", base1_derived1Diff.WastedOverrideChanges[0].Function.FullName);
        Assert.AreEqual("void Base1_Derived1_MoreDerived1::PureVirtualFunctionWithOneOverride() override", base1_derived1Diff.WastedOverrideChanges[0].Function.FullName);
        Assert.AreEqual(-16, base1_derived1Diff.WastedOverrideChanges[0].WasteChange);

        var base1Diff = wviDiffs.Single(wviDiff => wviDiff.TypeName == "Base1");
        Assert.AreEqual(32 - 8 + 8, base1Diff.WastedSizeDiff);
        Assert.AreEqual(64u, base1Diff.WastedSizeRemaining);

        // Test case for: adding a virtual, which costs on the whole hierarchy
        // Base1::VirtualFunctionWithNoOverridesOnlyInAfter
        Assert.AreEqual(1, base1Diff.WastedOverrideChanges.Count);
        Assert.AreEqual("virtual int Base1::VirtualFunctionWithNoOverridesOnlyInAfter()", base1Diff.WastedOverrideChanges[0].Function.FullName);
        Assert.AreEqual(32, base1Diff.WastedOverrideChanges[0].WasteChange);

        // Test case for: removing a type in the hierarchy, which saves on all the wasteful virtual slots on that type
        // Base1_Derived2
        Assert.AreEqual(2, base1Diff.TypeHierarchyChanges.Count);
        var base1_Derived2Change = base1Diff.TypeHierarchyChanges.Single(change => change.Type.Name == "Base1_Derived2");
        Assert.AreEqual(-8, base1_Derived2Change.WasteChange); // removed the one wasteful virtual from 'before'

        // Test case for: adding a type in the hierarchy, which compounds all wasteful virtuals
        // Base1_Derived1_MoreDerived2
        var base1_Derived1_MoreDerived2Change = base1Diff.TypeHierarchyChanges.Single(change => change.Type.Name == "Base1_Derived1_MoreDerived2");
        Assert.AreEqual(8, base1_Derived1_MoreDerived2Change.WasteChange); // added the one wasteful virtual from 'before' (the new virtual's cost is accounted for above, by the WastedOverrideChange)

        // Test case for: an 'all issues on this type fixed' message that's nice and simple like SizeBench v1 had?
        // BaseWastefulOnlyInBefore and BaseWastefulOnlyInBefore_Derived1
        var baseWastefulOnlyInBeforeDiff = wviDiffs.Single(wviDiff => wviDiff.TypeName == "BaseWastefulOnlyInBefore");
        Assert.AreEqual(-24, baseWastefulOnlyInBeforeDiff.WastedSizeDiff);
        Assert.AreEqual(0u, baseWastefulOnlyInBeforeDiff.WastedSizeRemaining);
        Assert.AreEqual(0, baseWastefulOnlyInBeforeDiff.TypeHierarchyChanges.Count); // No change in the number of types, functions account for all of it
        Assert.AreEqual(1, baseWastefulOnlyInBeforeDiff.WastedOverrideChanges.Count);
        Assert.AreEqual("virtual int BaseWastefulOnlyInBefore::VirtualFunctionWithNoOverrides()", baseWastefulOnlyInBeforeDiff.WastedOverrideChanges[0].Function.FullName);
        Assert.AreEqual(-24, baseWastefulOnlyInBeforeDiff.WastedOverrideChanges[0].WasteChange);

        var baseWastefulOnlyInBefore_Derived1Diff = wviDiffs.Single(wviDiff => wviDiff.TypeName == "BaseWastefulOnlyInBefore_Derived1");
        Assert.AreEqual(0 - 16 - 16 - 16, baseWastefulOnlyInBefore_Derived1Diff.WastedSizeDiff);
        Assert.AreEqual(0u, baseWastefulOnlyInBefore_Derived1Diff.WastedSizeRemaining);
        Assert.AreEqual(0, baseWastefulOnlyInBefore_Derived1Diff.TypeHierarchyChanges.Count); // No change in the number of types, functions account for all of it
        Assert.AreEqual(3, baseWastefulOnlyInBefore_Derived1Diff.WastedOverrideChanges.Count);
        Assert.AreEqual(-16, baseWastefulOnlyInBefore_Derived1Diff.WastedOverrideChanges.Single(change => change.Function.FullName == "void BaseWastefulOnlyInBefore_Derived1_MoreDerived1::PureVirtualFunctionWithOneOverride() override").WasteChange);
        Assert.AreEqual(-16, baseWastefulOnlyInBefore_Derived1Diff.WastedOverrideChanges.Single(change => change.Function.FullName == "virtual void BaseWastefulOnlyInBefore_Derived1::VirtualFunctionWithNoOverrides2()").WasteChange);
        Assert.AreEqual(-16, baseWastefulOnlyInBefore_Derived1Diff.WastedOverrideChanges.Single(change => change.Function.FullName == "virtual int BaseWastefulOnlyInBefore_Derived1::VirtualFunctionWithNoOverrides(int)").WasteChange);

        // Test case for: a 'this waste is all brand new' message that's simple like SizeBench v1 had?
        // BaseWastefulOnlyInAfter and BaseWastefulOnlyInAfter_Derived1
        var baseWastefulOnlyInAfterDiff = wviDiffs.Single(wviDiff => wviDiff.TypeName == "BaseWastefulOnlyInAfter");
        Assert.AreEqual(8 + 8 + 8, baseWastefulOnlyInAfterDiff.WastedSizeDiff);
        Assert.AreEqual(8u + 8u + 8u, baseWastefulOnlyInAfterDiff.WastedSizeRemaining);
        Assert.AreEqual(0, baseWastefulOnlyInAfterDiff.TypeHierarchyChanges.Count); // No change in the number of types, functions account for all of it
        Assert.AreEqual("virtual int BaseWastefulOnlyInAfter::VirtualFunctionWithNoOverrides()", baseWastefulOnlyInAfterDiff.WastedOverrideChanges[0].Function.FullName);
        Assert.AreEqual(24, baseWastefulOnlyInAfterDiff.WastedOverrideChanges[0].WasteChange);


        var baseWastefulOnlyInAfter_Derived1Diff = wviDiffs.Single(wviDiff => wviDiff.TypeName == "BaseWastefulOnlyInAfter_Derived1");
        Assert.AreEqual(16 + 16 + 16, baseWastefulOnlyInAfter_Derived1Diff.WastedSizeDiff);
        Assert.AreEqual(16u + 16u + 16u, baseWastefulOnlyInAfter_Derived1Diff.WastedSizeRemaining);
        Assert.AreEqual(0, baseWastefulOnlyInAfter_Derived1Diff.TypeHierarchyChanges.Count); // No change in the number of types, functions account for all of it
        Assert.AreEqual(3, baseWastefulOnlyInAfter_Derived1Diff.WastedOverrideChanges.Count);
        Assert.AreEqual(16, baseWastefulOnlyInAfter_Derived1Diff.WastedOverrideChanges.Single(change => change.Function.FullName == "void BaseWastefulOnlyInAfter_Derived1_MoreDerived1::PureVirtualFunctionWithOneOverride() override").WasteChange);
        Assert.AreEqual(16, baseWastefulOnlyInAfter_Derived1Diff.WastedOverrideChanges.Single(change => change.Function.FullName == "virtual void BaseWastefulOnlyInAfter_Derived1::VirtualFunctionWithNoOverrides2()").WasteChange);
        Assert.AreEqual(16, baseWastefulOnlyInAfter_Derived1Diff.WastedOverrideChanges.Single(change => change.Function.FullName == "virtual int BaseWastefulOnlyInAfter_Derived1::VirtualFunctionWithNoOverrides(int)").WasteChange);
    }
}
