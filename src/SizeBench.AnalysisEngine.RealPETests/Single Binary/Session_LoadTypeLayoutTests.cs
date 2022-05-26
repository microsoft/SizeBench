using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_LoadTypeLayoutTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory, binary);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task CppTestCasesBeforeTypeLayoutsCanBeLoaded()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var typeLayouts = await session.LoadAllTypeLayouts(CancellationToken.None);
        Assert.IsNotNull(typeLayouts);

        var alignasUnspecifiedTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "AlignasUnspecifiedType");
        Assert.AreEqual(8u, alignasUnspecifiedTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(3, alignasUnspecifiedTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(alignasUnspecifiedTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(3, alignasUnspecifiedTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(3, alignasUnspecifiedTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("x", alignasUnspecifiedTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(alignasUnspecifiedTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", alignasUnspecifiedTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(4, alignasUnspecifiedTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(alignasUnspecifiedTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(alignasUnspecifiedTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0, alignasUnspecifiedTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("y", alignasUnspecifiedTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(alignasUnspecifiedTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("char", alignasUnspecifiedTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(1, alignasUnspecifiedTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(alignasUnspecifiedTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsFalse(alignasUnspecifiedTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(4, alignasUnspecifiedTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, alignasUnspecifiedTypeLayout.MemberLayouts![2].Name);
        Assert.IsNull(alignasUnspecifiedTypeLayout.MemberLayouts![2].Type);
        Assert.AreEqual(3, alignasUnspecifiedTypeLayout.MemberLayouts![2].Size);
        Assert.IsTrue(alignasUnspecifiedTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsFalse(alignasUnspecifiedTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(0u, alignasUnspecifiedTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(5, alignasUnspecifiedTypeLayout.MemberLayouts![2].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var base1_Derived1_MoreDerived1Layout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Base1_Derived1_MoreDerived1");
        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.UserDefinedType.InstanceSize);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.MemberLayouts!.Count);
        Assert.AreEqual(1, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts!.Count);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.AlignmentWasteExclusive);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, base1_Derived1_MoreDerived1Layout.UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].UserDefinedType.InstanceSize);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].MemberLayouts!.Count);
        Assert.AreEqual(1, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts!.Count);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].AlignmentWasteExclusive);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].UserDefinedType.InstanceSize);
        Assert.AreEqual(1, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts!.Count);
        Assert.IsNull(base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].BaseTypeLayouts);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].AlignmentWasteExclusive);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("vfptr", base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].Name);
        Assert.IsNull(base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].Type);
        Assert.AreEqual(8, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].Size);
        Assert.IsFalse(base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0, base1_Derived1_MoreDerived1Layout.BaseTypeLayouts![0].BaseTypeLayouts![0].MemberLayouts![0].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // TODO (Product Backlog Item 1500): support alignas(X) correctly in Class Layout View
        /*
        var alignas8TypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Alignas8Type");
        var alignas1TypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Alignas1Type");

        // Alignas8Type_Derived seems to be wrong - or my understanding of alignas(8) is wrong.  The total size of this type is only 8?
        // GCC and Clang report sizeof(Alignas8Type_Derived)==16, but MSVC says it's 8...MSVC seems wrong?  Thread started with the compiler
        // team to investigate.  In the mean time, disabling this test.
        var alignas8Type_DerivedLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Alignas8Type_Derived"); ;
        var alignas4TypeWithBitfieldsLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Alignas4TypeWithBitfields"); ;
        var alignas2TypeWithBitfieldsLaout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Alignas2TypeWithBitfields"); ;

        // This one is also wrong, but again this may be my misunderstanding of alignas(2).  It starts with an alignment member so far
        // which should never be possible as the tail slop from the base type should always account for this...
        var alignas2TypeWithBitfields_DerivedLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "Alignas2TypeWithBitfields_Derived"); ;
        */



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var arraysTestTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "ArraysTest");
        Assert.AreEqual(208u, arraysTestTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(2, arraysTestTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(arraysTestTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(0, arraysTestTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0, arraysTestTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, arraysTestTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, arraysTestTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("float[4]", arraysTestTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.IsInstanceOfType(arraysTestTypeLayout.MemberLayouts![0].Type, typeof(ArrayTypeSymbol));
        Assert.AreEqual(4u, (arraysTestTypeLayout.MemberLayouts![0].Type as ArrayTypeSymbol)!.ElementCount);
        Assert.IsInstanceOfType((arraysTestTypeLayout.MemberLayouts![0].Type as ArrayTypeSymbol)!.ElementType, typeof(BasicTypeSymbol));
        Assert.AreEqual("float", (arraysTestTypeLayout.MemberLayouts![0].Type as ArrayTypeSymbol)!.ElementType.Name);
        Assert.AreEqual("testOneDimensionalArray", arraysTestTypeLayout.MemberLayouts![0].Name);
        Assert.AreEqual(16, arraysTestTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(arraysTestTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(arraysTestTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, arraysTestTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, arraysTestTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0, arraysTestTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("float[3][2][8]", arraysTestTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.IsInstanceOfType(arraysTestTypeLayout.MemberLayouts![1].Type, typeof(ArrayTypeSymbol));
        Assert.AreEqual(3u, (arraysTestTypeLayout.MemberLayouts![1].Type as ArrayTypeSymbol)!.ElementCount);
        Assert.IsInstanceOfType((arraysTestTypeLayout.MemberLayouts![1].Type as ArrayTypeSymbol)!.ElementType, typeof(ArrayTypeSymbol));
        Assert.AreEqual("float[2][8]", (arraysTestTypeLayout.MemberLayouts![1].Type as ArrayTypeSymbol)!.ElementType.Name);
        Assert.AreEqual("testMultiDimensionalArray", arraysTestTypeLayout.MemberLayouts![1].Name);
        Assert.AreEqual(192, arraysTestTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(arraysTestTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsFalse(arraysTestTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, arraysTestTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(0u, arraysTestTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(16, arraysTestTypeLayout.MemberLayouts![1].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var interspersedBitfieldsTestTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "InterspersedBitfieldsTest");
        Assert.AreEqual(16u, interspersedBitfieldsTestTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(9, interspersedBitfieldsTestTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(interspersedBitfieldsTestTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(9.75m, interspersedBitfieldsTestTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(9.75m, interspersedBitfieldsTestTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("intBitfield1", interspersedBitfieldsTestTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTestTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTestTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(0.125m, interspersedBitfieldsTestTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(1u, interspersedBitfieldsTestTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0, interspersedBitfieldsTestTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("intBitfield2", interspersedBitfieldsTestTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTestTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTestTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(0.250m, interspersedBitfieldsTestTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(1u, interspersedBitfieldsTestTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(2u, interspersedBitfieldsTestTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(0.125m, interspersedBitfieldsTestTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTestTypeLayout.MemberLayouts![2].Name);
        Assert.IsNull(interspersedBitfieldsTestTypeLayout.MemberLayouts![2].Type);
        Assert.AreEqual(3.625m, interspersedBitfieldsTestTypeLayout.MemberLayouts![2].Size);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(3u, interspersedBitfieldsTestTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(29u, interspersedBitfieldsTestTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(0.375m, interspersedBitfieldsTestTypeLayout.MemberLayouts![2].Offset);

        Assert.AreEqual("flag", interspersedBitfieldsTestTypeLayout.MemberLayouts![3].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTestTypeLayout.MemberLayouts![3].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTestTypeLayout.MemberLayouts![3].Type!.Name);
        Assert.AreEqual(1.0m, interspersedBitfieldsTestTypeLayout.MemberLayouts![3].Size);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![3].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![3].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![3].NumberOfBits);
        Assert.AreEqual(4.0m, interspersedBitfieldsTestTypeLayout.MemberLayouts![3].Offset);

        Assert.AreEqual("flagBitfield1", interspersedBitfieldsTestTypeLayout.MemberLayouts![4].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTestTypeLayout.MemberLayouts![4].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTestTypeLayout.MemberLayouts![4].Type!.Name);
        Assert.AreEqual(0.125m, interspersedBitfieldsTestTypeLayout.MemberLayouts![4].Size);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![4].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![4].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![4].BitStartPosition);
        Assert.AreEqual(1u, interspersedBitfieldsTestTypeLayout.MemberLayouts![4].NumberOfBits);
        Assert.AreEqual(5.0m, interspersedBitfieldsTestTypeLayout.MemberLayouts![4].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTestTypeLayout.MemberLayouts![5].Name);
        Assert.IsNull(interspersedBitfieldsTestTypeLayout.MemberLayouts![5].Type);
        Assert.AreEqual(2.875m, interspersedBitfieldsTestTypeLayout.MemberLayouts![5].Size);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![5].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![5].IsBitField);
        Assert.AreEqual(1u, interspersedBitfieldsTestTypeLayout.MemberLayouts![5].BitStartPosition);
        Assert.AreEqual(23u, interspersedBitfieldsTestTypeLayout.MemberLayouts![5].NumberOfBits);
        Assert.AreEqual(5.125m, interspersedBitfieldsTestTypeLayout.MemberLayouts![5].Offset);

        Assert.AreEqual("x", interspersedBitfieldsTestTypeLayout.MemberLayouts![6].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTestTypeLayout.MemberLayouts![6].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTestTypeLayout.MemberLayouts![6].Type!.Name);
        Assert.AreEqual(4.0m, interspersedBitfieldsTestTypeLayout.MemberLayouts![6].Size);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![6].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![6].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![6].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![6].NumberOfBits);
        Assert.AreEqual(8m, interspersedBitfieldsTestTypeLayout.MemberLayouts![6].Offset);

        Assert.AreEqual("xBitfield1", interspersedBitfieldsTestTypeLayout.MemberLayouts![7].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTestTypeLayout.MemberLayouts![7].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTestTypeLayout.MemberLayouts![7].Type!.Name);
        Assert.AreEqual(0.750m, interspersedBitfieldsTestTypeLayout.MemberLayouts![7].Size);
        Assert.IsFalse(interspersedBitfieldsTestTypeLayout.MemberLayouts![7].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![7].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTestTypeLayout.MemberLayouts![7].BitStartPosition);
        Assert.AreEqual(6u, interspersedBitfieldsTestTypeLayout.MemberLayouts![7].NumberOfBits);
        Assert.AreEqual(12.0m, interspersedBitfieldsTestTypeLayout.MemberLayouts![7].Offset);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, interspersedBitfieldsTestTypeLayout.MemberLayouts![8].Name);
        Assert.IsNull(interspersedBitfieldsTestTypeLayout.MemberLayouts![8].Type);
        Assert.AreEqual(3.250m, interspersedBitfieldsTestTypeLayout.MemberLayouts![8].Size);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![8].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTestTypeLayout.MemberLayouts![8].IsBitField);
        Assert.AreEqual(6u, interspersedBitfieldsTestTypeLayout.MemberLayouts![8].BitStartPosition);
        Assert.AreEqual(26u, interspersedBitfieldsTestTypeLayout.MemberLayouts![8].NumberOfBits);
        Assert.AreEqual(12.750m, interspersedBitfieldsTestTypeLayout.MemberLayouts![8].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var interspersedBitfieldsTest_DerivedTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "InterspersedBitfieldsTest_Derived");
        Assert.AreEqual(20u, interspersedBitfieldsTest_DerivedTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(3, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts!.Count);
        Assert.AreEqual(1, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts!.Count);
        Assert.AreEqual(1.625m, interspersedBitfieldsTest_DerivedTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(9.75m + 1.625m, interspersedBitfieldsTest_DerivedTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual(16u, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].UserDefinedType.InstanceSize);
        Assert.AreEqual(9, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].MemberLayouts!.Count);
        Assert.IsNull(interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].BaseTypeLayouts);
        Assert.AreEqual(9.75m, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].AlignmentWasteExclusive);
        Assert.AreEqual(9.75m, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.BaseTypeLayouts![0].UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("flagBitfield2", interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(0.375m, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(3u, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(16.0m, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].Name);
        Assert.IsNull(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].Type);
        Assert.AreEqual(1.625m, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].Size);
        Assert.IsTrue(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(3u, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(13u, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(16.375m, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual("shortMember", interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("short", interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].Type!.Name);
        Assert.AreEqual(2.0m, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].Size);
        Assert.IsFalse(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(18.0m, interspersedBitfieldsTest_DerivedTypeLayout.MemberLayouts![2].Offset);




        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var interspersedBitfieldsTest_Derived_WithvfptrTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "InterspersedBitfieldsTest_Derived_Withvfptr");
        Assert.AreEqual(32u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(6, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts!.Count);
        Assert.AreEqual(1, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts!.Count);
        Assert.AreEqual(2.375m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(9.75m + 2.375m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(8u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual(16u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].UserDefinedType.InstanceSize);
        Assert.AreEqual(9, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts!.Count);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].BaseTypeLayouts);
        Assert.AreEqual(9.75m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].AlignmentWasteExclusive);
        Assert.AreEqual(9.75m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("vfptr", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].Name);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].Type);
        Assert.AreEqual(8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("anotherFlagBitfield", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(0.625m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(5u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(24.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].Name);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].Type);
        Assert.AreEqual(0.375m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].Size);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(5u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(3u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(24.625m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![2].Offset);

        Assert.AreEqual("anotherFlag", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].Type!.Name);
        Assert.AreEqual(1.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].NumberOfBits);
        Assert.AreEqual(25.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![3].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].Name);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].Type);
        Assert.AreEqual(2.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].Size);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].NumberOfBits);
        Assert.AreEqual(26.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![4].Offset);

        Assert.AreEqual("finalInt", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].Type!.Name);
        Assert.AreEqual(4.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].NumberOfBits);
        Assert.AreEqual(28.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.MemberLayouts![5].Offset);

        // Now look at all the fields in the base class - they should all have "+8.0m" on their offset since the vfptr from the derived type "scooted them down"
        Assert.AreEqual("intBitfield1", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].Type!.Name);
        Assert.AreEqual(0.125m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(1u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![0].Offset);

        Assert.AreEqual("intBitfield2", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].Type!.Name);
        Assert.AreEqual(0.250m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].IsBitField);
        Assert.AreEqual(1u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(2u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(0.125m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![1].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].Name);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].Type);
        Assert.AreEqual(3.625m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].Size);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].IsBitField);
        Assert.AreEqual(3u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(29u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(0.375m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![2].Offset);

        Assert.AreEqual("flag", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].Type!.Name);
        Assert.AreEqual(1.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].NumberOfBits);
        Assert.AreEqual(4.0m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![3].Offset);

        Assert.AreEqual("flagBitfield1", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].Type!.Name);
        Assert.AreEqual(0.125m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].BitStartPosition);
        Assert.AreEqual(1u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].NumberOfBits);
        Assert.AreEqual(5.0m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![4].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].Name);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].Type);
        Assert.AreEqual(2.875m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].Size);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].IsBitField);
        Assert.AreEqual(1u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].BitStartPosition);
        Assert.AreEqual(23u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].NumberOfBits);
        Assert.AreEqual(5.125m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![5].Offset);

        Assert.AreEqual("x", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].Type!.Name);
        Assert.AreEqual(4.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].IsAlignmentMember);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].BitStartPosition);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].NumberOfBits);
        Assert.AreEqual(8m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![6].Offset);

        Assert.AreEqual("xBitfield1", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].Name);
        Assert.IsInstanceOfType(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].Type!.Name);
        Assert.AreEqual(0.750m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].Size);
        Assert.IsFalse(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].IsBitField);
        Assert.AreEqual(0u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].BitStartPosition);
        Assert.AreEqual(6u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].NumberOfBits);
        Assert.AreEqual(12.0m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![7].Offset);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].Name);
        Assert.IsNull(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].Type);
        Assert.AreEqual(3.250m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].Size);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].IsAlignmentMember);
        Assert.IsTrue(interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].IsBitField);
        Assert.AreEqual(6u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].BitStartPosition);
        Assert.AreEqual(26u, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].NumberOfBits);
        Assert.AreEqual(12.750m + 8.0m, interspersedBitfieldsTest_Derived_WithvfptrTypeLayout.BaseTypeLayouts![0].MemberLayouts![8].Offset);






        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var tightlyPackedBitfieldsTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "TightlyPackedBitfields");
        Assert.AreEqual(4u, tightlyPackedBitfieldsTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(4, tightlyPackedBitfieldsTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(tightlyPackedBitfieldsTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(0.0m, tightlyPackedBitfieldsTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0.0m, tightlyPackedBitfieldsTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, tightlyPackedBitfieldsTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, tightlyPackedBitfieldsTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("bit0", tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(0.125m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsTrue(tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(1u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0.0m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("bits1_to_10", tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(1.250m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsTrue(tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(1u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(10u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(0.125m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual("bits11_to_30", tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].Name);
        Assert.IsInstanceOfType(tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].Type!.Name);
        Assert.AreEqual(2.500m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].Size);
        Assert.IsFalse(tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsTrue(tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(11u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(20u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(1.375m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![2].Offset);

        Assert.AreEqual("bit31", tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].Name);
        Assert.IsInstanceOfType(tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].Type!.Name);
        Assert.AreEqual(0.125m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].Size);
        Assert.IsFalse(tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].IsAlignmentMember);
        Assert.IsTrue(tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].IsBitField);
        Assert.AreEqual(31u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(1u, tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].NumberOfBits);
        Assert.AreEqual(3.875m, tightlyPackedBitfieldsTypeLayout.MemberLayouts![3].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var simpleUnionTestTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "SimpleUnionTest");
        Assert.AreEqual(4u, simpleUnionTestTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(2, simpleUnionTestTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(simpleUnionTestTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(0.0m, simpleUnionTestTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0.0m, simpleUnionTestTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, simpleUnionTestTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, simpleUnionTestTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("x", simpleUnionTestTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(simpleUnionTestTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", simpleUnionTestTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(4.0m, simpleUnionTestTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(simpleUnionTestTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(simpleUnionTestTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, simpleUnionTestTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, simpleUnionTestTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0.0m, simpleUnionTestTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("y", simpleUnionTestTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(simpleUnionTestTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("short", simpleUnionTestTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(2.0m, simpleUnionTestTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(simpleUnionTestTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsFalse(simpleUnionTestTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, simpleUnionTestTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(0u, simpleUnionTestTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(0.0m, simpleUnionTestTypeLayout.MemberLayouts![1].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var simpleUnionTest_DerivedTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "SimpleUnionTest_Derived");
        Assert.AreEqual(8u, simpleUnionTest_DerivedTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(1, simpleUnionTest_DerivedTypeLayout.MemberLayouts!.Count);
        Assert.AreEqual(1, simpleUnionTest_DerivedTypeLayout.BaseTypeLayouts!.Count);
        Assert.AreEqual("SimpleUnionTest", simpleUnionTest_DerivedTypeLayout.BaseTypeLayouts![0].UserDefinedType.Name);
        Assert.AreEqual(0.0m, simpleUnionTest_DerivedTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0.0m, simpleUnionTest_DerivedTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, simpleUnionTest_DerivedTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, simpleUnionTest_DerivedTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("z", simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(4.0m, simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(4.0m, simpleUnionTest_DerivedTypeLayout.MemberLayouts![0].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "_UMS_SYSTEM_THREAD_INFORMATIONTest");
        Assert.AreEqual(8u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(4, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(0.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("UmsVersion", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(4.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("IsUmsSchedulerThread", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(0.125m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsTrue(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(1u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(4.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual("ThreadUmsFlags", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].Name);
        Assert.IsInstanceOfType(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].Type!.Name);
        Assert.AreEqual(4.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].Size);
        Assert.IsFalse(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsFalse(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(0u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(4.0m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![2].Offset);

        Assert.AreEqual("IsUmsWorkerThread", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].Name);
        Assert.IsInstanceOfType(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].Type!.Name);
        Assert.AreEqual(0.125m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].Size);
        Assert.IsFalse(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].IsAlignmentMember);
        Assert.IsTrue(_UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].IsBitField);
        Assert.AreEqual(1u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(1u, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].NumberOfBits);
        Assert.AreEqual(4.125m, _UMS_SYSTEM_THREAD_INFORMATIONTestTypeLayout.MemberLayouts![3].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var _XSTATE_CONFIGURATIONTestTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "_XSTATE_CONFIGURATIONTest");
        Assert.AreEqual(816u, _XSTATE_CONFIGURATIONTestTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(12, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(_XSTATE_CONFIGURATIONTestTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(4.0m, _XSTATE_CONFIGURATIONTestTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(4.0m, _XSTATE_CONFIGURATIONTestTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("EnabledFeatures", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned int64", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(8.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("EnabledVolatileFeatures", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned int64", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(8.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(8.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual("Size", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].Type!.Name);
        Assert.AreEqual(4.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(16.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![2].Offset);

        Assert.AreEqual("ControlFlags", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].Type!.Name);
        Assert.AreEqual(4.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].NumberOfBits);
        Assert.AreEqual(20.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![3].Offset);

        Assert.AreEqual("OptimizedSave", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].Type!.Name);
        Assert.AreEqual(0.125m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].IsAlignmentMember);
        Assert.IsTrue(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].BitStartPosition);
        Assert.AreEqual(1u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].NumberOfBits);
        Assert.AreEqual(20.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![4].Offset);

        Assert.AreEqual("CompactionEnabled", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].Type!.Name);
        Assert.AreEqual(0.125m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].IsAlignmentMember);
        Assert.IsTrue(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].IsBitField);
        Assert.AreEqual(1u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].BitStartPosition);
        Assert.AreEqual(1u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].NumberOfBits);
        Assert.AreEqual(20.125m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![5].Offset);

        Assert.AreEqual("Features", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].Type, typeof(ArrayTypeSymbol));
        Assert.AreEqual("_XSTATE_FEATURETest[64]", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].Type!.Name);
        Assert.AreEqual(512.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].NumberOfBits);
        Assert.AreEqual(24.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![6].Offset);

        Assert.AreEqual("EnabledSupervisorFeatures", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned int64", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].Type!.Name);
        Assert.AreEqual(8.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].NumberOfBits);
        Assert.AreEqual(536.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![7].Offset);

        Assert.AreEqual("AlignedFeatures", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned int64", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].Type!.Name);
        Assert.AreEqual(8.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].NumberOfBits);
        Assert.AreEqual(544.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![8].Offset);

        Assert.AreEqual("AllFeatureSize", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("unsigned long", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].Type!.Name);
        Assert.AreEqual(4.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].NumberOfBits);
        Assert.AreEqual(552.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![9].Offset);

        Assert.AreEqual("AllFeatures", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].Name);
        Assert.IsInstanceOfType(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].Type, typeof(ArrayTypeSymbol));
        Assert.AreEqual("unsigned long[64]", _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].Type!.Name);
        Assert.AreEqual(256.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].Size);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].NumberOfBits);
        Assert.AreEqual(556.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![10].Offset);

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].Name);
        Assert.IsNull(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].Type);
        Assert.AreEqual(4.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].Size);
        Assert.IsTrue(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].IsAlignmentMember);
        Assert.IsFalse(_XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].IsBitField);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].BitStartPosition);
        Assert.AreEqual(0u, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].NumberOfBits);
        Assert.AreEqual(812.0m, _XSTATE_CONFIGURATIONTestTypeLayout.MemberLayouts![11].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var trailingUnionWithBitfieldBaseTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "TrailingUnionWithBitfieldBase");
        Assert.AreEqual(8u, trailingUnionWithBitfieldBaseTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(3, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts!.Count);
        Assert.IsNull(trailingUnionWithBitfieldBaseTypeLayout.BaseTypeLayouts);
        Assert.AreEqual(0.0m, trailingUnionWithBitfieldBaseTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0.0m, trailingUnionWithBitfieldBaseTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("x", trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(4.0m, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(0.0m, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("y", trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].Name);
        Assert.IsInstanceOfType(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].Type!.Name);
        Assert.AreEqual(4.0m, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].Size);
        Assert.IsFalse(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.IsFalse(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(4.0m, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![1].Offset);

        Assert.AreEqual("yBitfield", trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].Name);
        Assert.IsInstanceOfType(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].Type!.Name);
        Assert.AreEqual(0.125m, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].Size);
        Assert.IsFalse(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].IsAlignmentMember);
        Assert.IsTrue(trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].IsBitField);
        Assert.AreEqual(0u, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(1u, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].NumberOfBits);
        Assert.AreEqual(4.0m, trailingUnionWithBitfieldBaseTypeLayout.MemberLayouts![2].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var trailingUnionwithBitfieldBase_DerivedTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "TrailingUnionWithBitfieldBase_Derived");
        Assert.AreEqual(12u, trailingUnionwithBitfieldBase_DerivedTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(1, trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts!.Count);
        Assert.AreEqual(1, trailingUnionwithBitfieldBase_DerivedTypeLayout.BaseTypeLayouts!.Count);
        Assert.AreEqual("TrailingUnionWithBitfieldBase", trailingUnionwithBitfieldBase_DerivedTypeLayout.BaseTypeLayouts![0].UserDefinedType.Name);
        Assert.AreEqual(0.0m, trailingUnionwithBitfieldBase_DerivedTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0.0m, trailingUnionwithBitfieldBase_DerivedTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, trailingUnionwithBitfieldBase_DerivedTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(0u, trailingUnionwithBitfieldBase_DerivedTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        Assert.AreEqual("z", trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].Name);
        Assert.IsInstanceOfType(trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].Type, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].Type!.Name);
        Assert.AreEqual(4.0m, trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].Size);
        Assert.IsFalse(trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.IsFalse(trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(0u, trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(8.0m, trailingUnionwithBitfieldBase_DerivedTypeLayout.MemberLayouts![0].Offset);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // This type is a regression test that was a bug in the past - if the derived type has no data members, but is the introduction of the vfptr, then we need to calculate
        // the max offset + size correctly to ensure all space is used up (to avoid throwing a sanity exception).  This type does that so it'll hit that edge case.
        // Type stolen in principle from the Windows.UI.Xaml codebase, stripped down to a minimal repro.
        var xstackOfIntsTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "xstack<int>");
        Assert.AreEqual(40u, xstackOfIntsTypeLayout.UserDefinedType.InstanceSize);



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // This type is another regression test for a bug that previously existed - if a type had a member of type "nullptr_t" this can create a PointerTypeSymbol that requires a
        // ModifiedTypeSymbol to get "void**" (ModifiedType for "*" with underlying PointerType for "void*").  Example trimmed down from Windows.UI.Xaml codebase where the bug was
        // discovered.
        var ValueTypeInfoOfvalueAnyTypeLayout = typeLayouts.Single(tli => tli.UserDefinedType.Name == "ValueTypeInfo<0>");
        Assert.AreEqual(1u, ValueTypeInfoOfvalueAnyTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(2, ValueTypeInfoOfvalueAnyTypeLayout.UserDefinedType.DataMembers.Length);
        Assert.IsTrue(ValueTypeInfoOfvalueAnyTypeLayout.UserDefinedType.DataMembers.All(ds => ds.IsStaticMember));
        var EmptyDataMember = ValueTypeInfoOfvalueAnyTypeLayout.UserDefinedType.DataMembers.Single(ds => ds.Name == "Empty");
        Assert.AreEqual("void** const", EmptyDataMember.Type!.Name);
        Assert.IsInstanceOfType(EmptyDataMember.Type, typeof(ModifiedTypeSymbol));
        Assert.IsInstanceOfType((EmptyDataMember.Type as ModifiedTypeSymbol)!.UnmodifiedTypeSymbol, typeof(PointerTypeSymbol));



        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Yet another regression test for a bug that previously existed - if a type had an alignment member as its first member, this would crash.
        // Example trimmed down from an xbox game codebase where the bug was discovered.
        var TypeWithPaddingAsFirstMemberLayout = typeLayouts.Single(tli => tli.UserDefinedType.Name == "TypeWithPaddingAsFirstMember");
        Assert.AreEqual(4u, TypeWithPaddingAsFirstMemberLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(9, TypeWithPaddingAsFirstMemberLayout.MemberLayouts!.Count);
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].Name);
        Assert.AreEqual(0.125m, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].Size);
        Assert.AreEqual(true, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(1u, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].NumberOfBits);
        Assert.AreEqual(true, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].IsAlignmentMember);
        Assert.AreEqual(false, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].IsTailSlopAlignmentMember);
        Assert.AreEqual(0, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![0].Offset);

        Assert.AreEqual("A3BitBool", TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].Name);
        Assert.AreEqual(0.375m, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].Size);
        Assert.AreEqual(true, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].IsBitField);
        Assert.AreEqual(1u, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(3u, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].NumberOfBits);
        Assert.AreEqual(false, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].IsAlignmentMember);
        Assert.AreEqual(false, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].IsTailSlopAlignmentMember);
        Assert.AreEqual(0.125m, TypeWithPaddingAsFirstMemberLayout.MemberLayouts![1].Offset);

        // Another regression test, for a type hierarchy where the "middle" type is where the vfptr
        // gets introduced.
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var MoreDerivedTypeLayout = typeLayouts.First(tli => tli.UserDefinedType.Name == "MoreDerivedType");
        Assert.AreEqual(32u, MoreDerivedTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(1, MoreDerivedTypeLayout.MemberLayouts!.Count);
        Assert.AreEqual(1, MoreDerivedTypeLayout.BaseTypeLayouts!.Count);
        Assert.AreEqual(0m, MoreDerivedTypeLayout.AlignmentWasteExclusive);
        Assert.AreEqual(0m, MoreDerivedTypeLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, MoreDerivedTypeLayout.UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, MoreDerivedTypeLayout.UsedForVFPtrsIncludingBaseTypes);

        // Another regression test, for a type hierarchy where the base type has a union containing bitfields and non-bitfields.
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        var ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout = typeLayouts.Single(tli => tli.UserDefinedType.Name == "ComplicatedTypeLayoutWithBitfieldsDerived");
        Assert.AreEqual(32u, ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout.UserDefinedType.InstanceSize);
        Assert.AreEqual(8, ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout.MemberLayouts!.Count);
        // The base type should have no padding, it's a union of a 4-byte value and a bunch of bitfields that don't add up to 4 bytes, but that's still no-padding from our perspective.
        Assert.AreEqual(0, ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout.BaseTypeLayouts![0].AlignmentWasteExclusive);
        // The first member in the derived type is padding - 4 bytes, between the 4-byte base type and the 8-byte-aligned pointer as the first declared member.
        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout.MemberLayouts![0].Name);
        Assert.AreEqual(4, ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout.MemberLayouts![0].Size);
        Assert.AreEqual(4, ComplicatedTypeLayoutWithBitfieldsDerivedTypeLayout.MemberLayouts![0].Offset);
    }
}
