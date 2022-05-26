using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public sealed class DiffSession_LoadTypeLayoutDiffsTests
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
        var tliDiffs = await diffSession.LoadAllTypeLayoutDiffs(CancellationToken.None);
        Assert.IsNotNull(tliDiffs);

        foreach (var tliDiff in tliDiffs)
        {
            Assert.AreEqual(0, tliDiff.InstanceSizeDiff);
            Assert.AreEqual(0, tliDiff.AlignmentWasteExclusiveDiff);
            Assert.AreEqual(0, tliDiff.AlignmentWasteIncludingBaseTypesDiff);
            if (tliDiff.BaseTypeDiffs != null)
            {
                foreach (var baseTypeDiff in tliDiff.BaseTypeDiffs)
                {
                    Assert.AreEqual(0, baseTypeDiff.InstanceSizeDiff);
                }
            }
        }
    }

    [TestMethod]
    public async Task LoadingSingleTypeLayoutDiffWorks()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var tliDiffs = await diffSession.LoadAllTypeLayoutDiffs(CancellationToken.None);
        Assert.IsNotNull(tliDiffs);

        var nullBefore = tliDiffs.First(tliDiff => tliDiff.UserDefinedTypeDiff.BeforeSymbol is null);
        var nullAfter = tliDiffs.First(tliDiff => tliDiff.UserDefinedTypeDiff.AfterSymbol is null);

        var tliNullBeforeType = await diffSession.LoadTypeLayoutDiff(nullBefore.UserDefinedTypeDiff, CancellationToken.None);
        Assert.IsNotNull(tliNullBeforeType);

        var tliNullAfterType = await diffSession.LoadTypeLayoutDiff(nullAfter.UserDefinedTypeDiff, CancellationToken.None);
        Assert.IsNotNull(tliNullAfterType);
    }

    [TestMethod]
    public async Task DiffsFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var tliDiffs = await diffSession.LoadTypeLayoutDiffsByName("TypeLayoutDiff_*", CancellationToken.None);

        Assert.AreEqual(7, tliDiffs.Count);

        // This tests some basic stuff, like changing the type of a member, chewing into padding, etc.
        var tliTypeLayoutDiff_Basics = tliDiffs.Single(tli => tli.UserDefinedType.Name == "TypeLayoutDiff_Basics");

        Assert.AreEqual(-4, tliTypeLayoutDiff_Basics.InstanceSizeDiff);
        Assert.AreEqual(0, tliTypeLayoutDiff_Basics.AlignmentWasteExclusiveDiff);
        Assert.AreEqual(0, tliTypeLayoutDiff_Basics.AlignmentWasteIncludingBaseTypesDiff);

        Assert.AreEqual(0, tliTypeLayoutDiff_Basics.BaseTypeDiffs.Count);
        Assert.AreEqual(7, tliTypeLayoutDiff_Basics.MemberDiffs.Count);

        // int -> uint16_t, means 4->2 bytes
        Assert.AreEqual("x", tliTypeLayoutDiff_Basics.MemberDiffs![0].Name);
        Assert.AreEqual(-2, tliTypeLayoutDiff_Basics.MemberDiffs![0].SizeDiff);
        Assert.AreEqual("y", tliTypeLayoutDiff_Basics.MemberDiffs![1].Name);
        Assert.AreEqual(0, tliTypeLayoutDiff_Basics.MemberDiffs![1].SizeDiff);
        // 3 bytes of padding -> 1 byte
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, tliTypeLayoutDiff_Basics.MemberDiffs![2].Name);
        Assert.AreEqual(-2, tliTypeLayoutDiff_Basics.MemberDiffs![2].SizeDiff);
        Assert.AreEqual(1, tliTypeLayoutDiff_Basics.MemberDiffs![2].AfterMember!.Size);
        // int -> uint16_t again, 4->2 bytes
        Assert.AreEqual("z", tliTypeLayoutDiff_Basics.MemberDiffs![3].Name);
        Assert.AreEqual(-2, tliTypeLayoutDiff_Basics.MemberDiffs![3].SizeDiff);
        // Member removed entirely
        Assert.AreEqual("onlyInBefore", tliTypeLayoutDiff_Basics.MemberDiffs![4].Name);
        Assert.AreEqual(-4, tliTypeLayoutDiff_Basics.MemberDiffs![4].SizeDiff);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics.MemberDiffs![4].BeforeMember);
        Assert.IsNull(tliTypeLayoutDiff_Basics.MemberDiffs![4].AfterMember);
        // New alignment between z and "beforeIn*" of 2 bytes, since uint16_t needs 2 bytes padding before int
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, tliTypeLayoutDiff_Basics.MemberDiffs![5].Name);
        Assert.IsNull(tliTypeLayoutDiff_Basics.MemberDiffs![5].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics.MemberDiffs![5].AfterMember);
        Assert.AreEqual(2, tliTypeLayoutDiff_Basics.MemberDiffs![5].SizeDiff);
        Assert.AreEqual(2, tliTypeLayoutDiff_Basics.MemberDiffs![5].AfterMember!.Size);
        // Member added
        Assert.AreEqual("onlyInAfter", tliTypeLayoutDiff_Basics.MemberDiffs![6].Name);
        Assert.AreEqual(4, tliTypeLayoutDiff_Basics.MemberDiffs![6].SizeDiff);
        Assert.IsNull(tliTypeLayoutDiff_Basics.MemberDiffs![6].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics.MemberDiffs![6].AfterMember);


        // This tests removing a base type from the hierarchy in the middle (MoreDerived : Derived :Base -> MoreDerived : Base)
        var tliTypeLayoutDiff_MoreDerived = tliDiffs.Single(tli => tli.UserDefinedType.Name == "TypeLayoutDiff_MoreDerived");

        Assert.AreEqual(-4, tliTypeLayoutDiff_MoreDerived.InstanceSizeDiff);
        Assert.AreEqual(-2, tliTypeLayoutDiff_MoreDerived.AlignmentWasteExclusiveDiff);
        Assert.AreEqual(-3, tliTypeLayoutDiff_MoreDerived.AlignmentWasteIncludingBaseTypesDiff);

        Assert.AreEqual(2, tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs.Count);

        Assert.AreEqual("TypeLayoutDiff_Derived", tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[0].UserDefinedType.Name);
        Assert.IsNotNull(tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[0].BeforeTypeLayout);
        Assert.IsNull(tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[0].AfterTypeLayout);
        Assert.AreEqual(-4, tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[0].InstanceSizeDiff);

        Assert.AreEqual("TypeLayoutDiff_Base", tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[1].UserDefinedType.Name);
        Assert.IsNull(tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[1].BeforeTypeLayout);
        Assert.IsNotNull(tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[1].AfterTypeLayout);
        Assert.AreEqual(2, tliTypeLayoutDiff_MoreDerived.BaseTypeDiffs[1].InstanceSizeDiff);

        Assert.AreEqual(3, tliTypeLayoutDiff_MoreDerived.MemberDiffs.Count);

        Assert.AreEqual(0, tliTypeLayoutDiff_MoreDerived.MemberDiffs![0].SizeDiff);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, tliTypeLayoutDiff_MoreDerived.MemberDiffs![1].Name);
        Assert.AreEqual(-2, tliTypeLayoutDiff_MoreDerived.MemberDiffs![1].SizeDiff);

        Assert.AreEqual(0, tliTypeLayoutDiff_MoreDerived.MemberDiffs![2].SizeDiff);


        // This tests adding a base class into the middle of the hierarchy, and causing tail slop alignment to increase in size
        var tliTypeLayoutDiff_MoreDerived2 = tliDiffs.Single(tli => tli.UserDefinedType.Name == "TypeLayoutDiff_MoreDerived2");
        Assert.AreEqual(8, tliTypeLayoutDiff_MoreDerived2.InstanceSizeDiff);
        Assert.AreEqual(2, tliTypeLayoutDiff_MoreDerived2.AlignmentWasteExclusiveDiff);
        Assert.AreEqual(3, tliTypeLayoutDiff_MoreDerived2.AlignmentWasteIncludingBaseTypesDiff);

        Assert.AreEqual(2, tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs.Count);

        Assert.AreEqual("TypeLayoutDiff_Base", tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[0].UserDefinedType.Name);
        Assert.IsNotNull(tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[0].BeforeTypeLayout);
        Assert.IsNull(tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[0].AfterTypeLayout);
        Assert.AreEqual(-2, tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[0].InstanceSizeDiff);

        Assert.AreEqual("TypeLayoutDiff_Derived", tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[1].UserDefinedType.Name);
        Assert.IsNull(tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[1].BeforeTypeLayout);
        Assert.IsNotNull(tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[1].AfterTypeLayout);
        Assert.AreEqual(4, tliTypeLayoutDiff_MoreDerived2.BaseTypeDiffs[1].InstanceSizeDiff);

        Assert.AreEqual(3, tliTypeLayoutDiff_MoreDerived2.MemberDiffs.Count);

        Assert.AreEqual(0, tliTypeLayoutDiff_MoreDerived2.MemberDiffs![0].SizeDiff);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, tliTypeLayoutDiff_MoreDerived2.MemberDiffs![1].Name);
        Assert.AreEqual(2, tliTypeLayoutDiff_MoreDerived2.MemberDiffs![1].SizeDiff);

        Assert.AreEqual("moreDerived2Member", tliTypeLayoutDiff_MoreDerived2.MemberDiffs![2].Name);
        Assert.AreEqual(4, tliTypeLayoutDiff_MoreDerived2.MemberDiffs![2].SizeDiff);


        // This tests a base class that has members added/removed, and causing tail slop alignment to exist when it didn't before
        var tliTypeLayoutDiff_Basics_Derived = tliDiffs.Single(tli => tli.UserDefinedType.Name == "TypeLayoutDiff_Basics_Derived");
        Assert.AreEqual(0, tliTypeLayoutDiff_Basics_Derived.InstanceSizeDiff);
        Assert.AreEqual(3, tliTypeLayoutDiff_Basics_Derived.AlignmentWasteExclusiveDiff);
        Assert.AreEqual(3, tliTypeLayoutDiff_Basics_Derived.AlignmentWasteIncludingBaseTypesDiff);

        Assert.AreEqual(1, tliTypeLayoutDiff_Basics_Derived.BaseTypeDiffs.Count);

        Assert.AreEqual("TypeLayoutDiff_Basics", tliTypeLayoutDiff_Basics_Derived.BaseTypeDiffs[0].UserDefinedType.Name);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics_Derived.BaseTypeDiffs[0].BeforeTypeLayout);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics_Derived.BaseTypeDiffs[0].AfterTypeLayout);
        Assert.AreEqual(-4, tliTypeLayoutDiff_Basics_Derived.BaseTypeDiffs[0].InstanceSizeDiff);

        Assert.AreEqual(2, tliTypeLayoutDiff_Basics_Derived.MemberDiffs.Count);

        Assert.AreEqual("basicsDerivedMember", tliTypeLayoutDiff_Basics_Derived.MemberDiffs![0].Name);
        Assert.IsNull(tliTypeLayoutDiff_Basics_Derived.MemberDiffs![0].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics_Derived.MemberDiffs![0].AfterMember);
        Assert.AreEqual(1, tliTypeLayoutDiff_Basics_Derived.MemberDiffs![0].SizeDiff);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, tliTypeLayoutDiff_Basics_Derived.MemberDiffs![1].Name);
        Assert.IsNull(tliTypeLayoutDiff_Basics_Derived.MemberDiffs![1].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_Basics_Derived.MemberDiffs![1].AfterMember);
        Assert.AreEqual(3, tliTypeLayoutDiff_Basics_Derived.MemberDiffs![1].SizeDiff);


        // This tests tail slop alignment padding shrinking, member rearranging (the toughest case for tail slop "matching") and alignment padding shrinking
        var tliTypeLayoutDiff_WantsToBeTightlyPacked = tliDiffs.Single(tli => tli.UserDefinedType.Name == "TypeLayoutDiff_WantsToBeTightlyPacked");
        Assert.AreEqual(-4, tliTypeLayoutDiff_WantsToBeTightlyPacked.InstanceSizeDiff);
        Assert.AreEqual(-4, tliTypeLayoutDiff_WantsToBeTightlyPacked.AlignmentWasteExclusiveDiff);
        Assert.AreEqual(-4, tliTypeLayoutDiff_WantsToBeTightlyPacked.AlignmentWasteIncludingBaseTypesDiff);

        Assert.AreEqual(6, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs.Count);

        Assert.AreEqual("myInt", tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![0].Name);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![0].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![0].AfterMember);
        Assert.AreEqual(0, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![0].SizeDiff);

        Assert.AreEqual("myBool", tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![1].Name);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![1].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![1].AfterMember);
        Assert.AreEqual(0, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![1].SizeDiff);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![2].Name);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![2].BeforeMember);
        Assert.IsNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![2].AfterMember);
        Assert.AreEqual(-3, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![2].SizeDiff);

        Assert.AreEqual("myInt2", tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![3].Name);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![3].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![3].AfterMember);
        Assert.AreEqual(0, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![3].SizeDiff);

        Assert.AreEqual("myBool2", tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![4].Name);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![4].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![4].AfterMember);
        Assert.AreEqual(0, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![4].SizeDiff);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![5].Name);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![5].BeforeMember);
        Assert.IsNotNull(tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![5].AfterMember);
        Assert.AreEqual(-1, tliTypeLayoutDiff_WantsToBeTightlyPacked.MemberDiffs![5].SizeDiff);
    }
}
