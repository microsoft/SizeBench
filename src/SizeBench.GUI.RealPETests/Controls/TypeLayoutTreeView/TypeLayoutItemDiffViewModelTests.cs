using System.Collections.Specialized;
using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.Logging;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public class TypeLayoutItemDiffViewModelTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BeforeBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string BeforePDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");
    private string AfterBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll");
    private string AfterPDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb");

    [TestMethod]
    public async Task InterspersedBitfieldsTest_Derived_WithvfptrTest()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        // This type is specifically tested since it has a vfptr introduced in a derived type, which 'slots in' above the base type in the type layout - an ususual
        // situation, and an edge case that SizeBenhc got wrong in the past, so specifically testing it.
        var layouts = await diffSession.LoadTypeLayoutDiffsByName("InterspersedBitfieldsTest_Derived_Withvfptr", CancellationToken.None);
        Assert.AreEqual(1, layouts.Count);

        var derivedPropertyChanges = 0;
        var basePropertyChanges = 0;
        var derivedVM = new TypeLayoutItemDiffViewModel(layouts[0], diffSession);
        derivedVM.PropertyChanged += (s, e) => derivedPropertyChanges++;
        Assert.IsTrue(ReferenceEquals(derivedVM.TypeLayoutItemDiff, layouts[0]));

        Assert.AreEqual(1, derivedVM.BaseTypes.Count);
        var baseVM = derivedVM.BaseTypes[0];
        baseVM.PropertyChanged += (s, e) => basePropertyChanges++;
        Assert.IsTrue(ReferenceEquals(baseVM.TypeLayoutItemDiff, layouts[0].BaseTypeDiffs[0]));

        Assert.AreEqual(false, derivedVM.Expanded);
        Assert.AreEqual(false, derivedVM.IsPlaceholderLoadingItem);
        Assert.AreEqual(6, derivedVM.Members.Count);
        Assert.AreEqual("vfptr", derivedVM.Members[0].Member.Name);
        Assert.AreEqual("anotherFlagBitfield", derivedVM.Members[1].Member.Name);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, derivedVM.Members[2].Member.Name);
        Assert.AreEqual("anotherFlag", derivedVM.Members[3].Member.Name);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, derivedVM.Members[4].Member.Name);
        Assert.AreEqual("finalInt", derivedVM.Members[5].Member.Name);

        Assert.AreEqual(0, derivedVM.FirstOffsetOfBaseTypeOrMember);
        var membersAndBaseTypesList = derivedVM.BaseTypesAndMembers.Cast<object>().ToList();
        Assert.AreEqual(6 /*members*/ + 1 /*baseTypes*/, membersAndBaseTypesList.Count);
        Assert.IsInstanceOfType(membersAndBaseTypesList[0], typeof(TypeLayoutItemDiffViewModel.MemberDiffViewModel));
        Assert.AreEqual("vfptr", (membersAndBaseTypesList[0] as TypeLayoutItemDiffViewModel.MemberDiffViewModel)!.Member.Name);
        Assert.IsInstanceOfType(membersAndBaseTypesList[1], typeof(TypeLayoutItemDiffViewModel));
        Assert.AreEqual("InterspersedBitfieldsTest", (membersAndBaseTypesList[1] as TypeLayoutItemDiffViewModel)!.TypeLayoutItemDiff!.UserDefinedType.Name);
        Assert.IsInstanceOfType(membersAndBaseTypesList[2], typeof(TypeLayoutItemDiffViewModel.MemberDiffViewModel));
        Assert.AreEqual("anotherFlagBitfield", (membersAndBaseTypesList[2] as TypeLayoutItemDiffViewModel.MemberDiffViewModel)!.Member.Name);

        Assert.AreEqual(false, baseVM.Expanded);
        Assert.AreEqual(false, baseVM.IsPlaceholderLoadingItem);
        Assert.AreEqual(9, baseVM.Members.Count);
        Assert.AreEqual("intBitfield1", baseVM.Members[0].Member.Name);
        Assert.AreEqual("intBitfield2", baseVM.Members[1].Member.Name);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, baseVM.Members[2].Member.Name);
        Assert.AreEqual("flag", baseVM.Members[3].Member.Name);
        Assert.AreEqual("flagBitfield1", baseVM.Members[4].Member.Name);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, baseVM.Members[5].Member.Name);
        Assert.AreEqual("x", baseVM.Members[6].Member.Name);
        Assert.AreEqual("xBitfield1", baseVM.Members[7].Member.Name);
        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, baseVM.Members[8].Member.Name);

        Assert.AreEqual(0, baseVM.FirstOffsetOfBaseTypeOrMember);
        membersAndBaseTypesList = baseVM.BaseTypesAndMembers.Cast<object>().ToList();
        Assert.AreEqual(9 /*members*/ + 0 /*baseTypes*/, membersAndBaseTypesList.Count);
        Assert.IsInstanceOfType(membersAndBaseTypesList[0], typeof(TypeLayoutItemDiffViewModel.MemberDiffViewModel));
        Assert.AreEqual("intBitfield1", (membersAndBaseTypesList[0] as TypeLayoutItemDiffViewModel.MemberDiffViewModel)!.Member.Name);
        Assert.IsInstanceOfType(membersAndBaseTypesList[1], typeof(TypeLayoutItemDiffViewModel.MemberDiffViewModel));
        Assert.AreEqual("intBitfield2", (membersAndBaseTypesList[1] as TypeLayoutItemDiffViewModel.MemberDiffViewModel)!.Member.Name);

        // Reading all the above should not have caused any change notifications
        Assert.AreEqual(0, derivedPropertyChanges);
        Assert.AreEqual(0, basePropertyChanges);

        // Should not bubble out to the 'derived' - just base should change
        baseVM.Expanded = true;
        Assert.AreEqual(0, derivedPropertyChanges);
        Assert.AreEqual(1, basePropertyChanges);

        derivedVM.Expanded = true;
        Assert.AreEqual(1, derivedPropertyChanges);
        Assert.AreEqual(1, basePropertyChanges);
    }

    [Timeout(30 * 1000)] // 30s
    [TestMethod]
    public async Task TypeLayoutDiff_MoreDerivedTest()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var layouts = await diffSession.LoadTypeLayoutDiffsByName("_XSTATE_CONFIGURATIONTest", CancellationToken.None);
        Assert.AreEqual(1, layouts.Count);

        var propertyChanges = 0;
        var vm = new TypeLayoutItemDiffViewModel(layouts[0], diffSession);
        vm.PropertyChanged += (s, e) => propertyChanges++;
        Assert.IsTrue(ReferenceEquals(vm.TypeLayoutItemDiff, layouts[0]));

        Assert.AreEqual(12, vm.Members.Count);

        // Let's test the union, it's interesting to see if it laid out correctly
        Assert.AreEqual("ControlFlags", vm.Members[3].Member.Name);
        Assert.AreEqual(20u, vm.Members[3].OffsetExcludingBitfield);
        Assert.AreEqual(20M, vm.Members[3].Member.AfterMember!.Offset);
        Assert.AreEqual(4, vm.Members[3].Member.AfterMember!.Size);
        Assert.AreEqual(0, vm.Members[3].ChildrenOfThisType.Count);

        Assert.AreEqual("OptimizedSave", vm.Members[4].Member.Name);
        Assert.AreEqual(20u, vm.Members[4].OffsetExcludingBitfield);
        Assert.AreEqual(20M, vm.Members[4].Member.AfterMember!.Offset);
        Assert.AreEqual(0.125M, vm.Members[4].Member.AfterMember!.Size);
        Assert.AreEqual(0, vm.Members[4].ChildrenOfThisType.Count);

        Assert.AreEqual("CompactionEnabled", vm.Members[5].Member.Name);
        Assert.AreEqual(20u, vm.Members[5].OffsetExcludingBitfield);
        Assert.AreEqual(20.125M, vm.Members[5].Member.AfterMember!.Offset);
        Assert.AreEqual(0.125M, vm.Members[5].Member.AfterMember!.Size);
        Assert.AreEqual(0, vm.Members[5].ChildrenOfThisType.Count);

        // And the member right after this should be the next one in the struct, NOT an alignment member, since the ControlFlags in the union eats up all the 'alignment'
        Assert.AreEqual("Features", vm.Members[6].Member.Name);

        // And now we'll expand a type that has children, at last
        Assert.AreEqual(1, vm.Members[6].ChildrenOfThisType.Count);
        Assert.IsTrue(ReferenceEquals(TypeLayoutItemDiffViewModel.PlaceholderLoadingItem, vm.Members[6].ChildrenOfThisType[0]));

        Assert.AreEqual(0, propertyChanges);

        // Kick off loading the _XSTATE_FEATURETest type as a child type of the "Features" member
        var inccChanges = 0;
        var tcsINCC = new TaskCompletionSource<int>();
        vm.Members[6].ChildrenOfThisType.CollectionChanged += (s, e) =>
        {
            inccChanges++;
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                tcsINCC.SetResult(inccChanges);
            }
        };
        var member6PropertyChanges = 0;
        vm.Members[6].PropertyChanged += (s, e) => member6PropertyChanges++;
        vm.Members[6].Expanded = true;
        Assert.AreEqual(0, propertyChanges);
        Assert.AreEqual(1, member6PropertyChanges);

        // Now it's not the placeholder loading item, it's the real deal
        _ = await tcsINCC.Task;
        Assert.AreEqual(2, inccChanges); // Sholud see 2 changes, a clear, then an add
        Assert.IsFalse(ReferenceEquals(TypeLayoutItemDiffViewModel.PlaceholderLoadingItem, vm.Members[6].ChildrenOfThisType[0]));
        Assert.AreEqual(1, vm.Members[6].ChildrenOfThisType.Count);
        Assert.AreEqual("_XSTATE_FEATURETest", vm.Members[6].ChildrenOfThisType[0].TypeLayoutItemDiff!.UserDefinedType.Name);
    }
}
