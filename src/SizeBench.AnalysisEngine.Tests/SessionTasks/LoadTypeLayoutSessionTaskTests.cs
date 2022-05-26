using SizeBench.AnalysisEngine.SessionTasks;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Tests.SessionTasks;

[TestClass]
public sealed class LoadTypeLayoutSessionTaskTests : IDisposable
{
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();
    private Mock<ISession> MockSession = new Mock<ISession>();
    private uint NextSymIndexId;

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.MockSession = new Mock<ISession>();
        this.MockSession.SetupGet(s => s.BytesPerWord).Returns(8); // we'll simulate a 64-bit binary

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);
    }

    [TestMethod]
    public void LoadingLayoutOfTypesWhichHaveNoLayoutThrows()
    {
        uint nextSymIndexId = 0;
        var basicType = new BasicTypeSymbol(this.DataCache, "int", 4, nextSymIndexId++);
        var enumType = new EnumTypeSymbol(this.DataCache, "enum Foo", 2, nextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.DataCache, "void", 0, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(this.DataCache, "int (*function)(float)", 100, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: voidType);
        var arrayOfBasicTypes = new ArrayTypeSymbol(this.DataCache, "int[5]", 20, nextSymIndexId++, basicType, elementCount: 5);
        var modifiedInt = new ModifiedTypeSymbol(this.DataCache, basicType, "const int", 4, nextSymIndexId++);
        var pointerToInt = new PointerTypeSymbol(this.DataCache, basicType, "int*", 4, nextSymIndexId++);

        Assert.ThrowsException<ArgumentException>(() => new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, basicType, 0, null, CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, enumType, 0, null, CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, functionType, 0, null, CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, arrayOfBasicTypes, 0, null, CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, modifiedInt, 0, null, CancellationToken.None));
        Assert.ThrowsException<ArgumentException>(() => new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, pointerToInt, 0, null, CancellationToken.None));
    }

    [TestMethod]
    public void ChasingThroughToUDTWorksThroughAllLayers()
    {
        // This will simulate having a "const MyCustomType*[]" - an array of pointers to const MyCustomTypes, which hits all the different kinds
        // of symbols we want to 'chase through' to find the real UDT whose type layout we should load
        uint nextSymIndexId = 0;
        var udt = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "MyCustomType", 20, nextSymIndexId++, UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(this.DataCache, udt, "const MyCustomType", 20, nextSymIndexId++);
        var pointerToConstUDT = new PointerTypeSymbol(this.DataCache, constUDT, "const MyCustomType*", 8, nextSymIndexId++);
        var arrayOfPointerToConstUDT = new ArrayTypeSymbol(this.DataCache, "const MyCustomType*[3]", 24, nextSymIndexId++, pointerToConstUDT, 3);

        var task = new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, arrayOfPointerToConstUDT, 0, null, CancellationToken.None);
        Assert.IsTrue(ReferenceEquals(udt, task.TypeSymbol));
    }

    [TestMethod]
    public void LoadingByTypeSymbolWorks()
    {
        /*All this tedious creation of objects is simulating this code:
         * 
         * class TestBase {
         * public:
         *      virtual void VirtualFunction(); // forces TestBase to have a vfptr
         *      int intDataMember;
         *      int intBitfield : 1;
         * }
         * 
         * class TestType {
         * public:
         *      virtual void AnotherVirtualFunction(); // should share the vfptr with the base
         *      bool bitField1 : 1
         *      bool bitField2 : 6
         *      bool flag;
         * }
         * 
         * Which yields this layout:
         * TestType
         *      TestBase
         * 0        vfptr
         * 8        intDataMember
         * 12       intBitfield : 1
         * 12       <tail slop padding 3.875 bytes>
         * 16   bool bitField1 : 1
         * 16   bool bitfield2 : 6
         * 16   <alignment padding 0.125 bytes>
         * 17   bool flag;
         * 18   <tail slop padding 6 bytes>
         */
        var voidType = new BasicTypeSymbol(this.DataCache, "void", 0, this.NextSymIndexId++);
        var baseUdt = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "TestBase", 16, this.NextSymIndexId++, UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var baseFunctions = new List<IFunctionCodeSymbol>()
            {
                new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunction", 0, 10, this.NextSymIndexId++,
                                         isVirtual: true, isIntroVirtual: true, accessModifier: AccessModifier.Public,
                                         functionType: new FunctionTypeSymbol(this.DataCache, "void (*function)()", 0, this.NextSymIndexId++, false, false, null, voidType))
            };
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(baseUdt.SymIndexId, baseFunctions);
        this.TestDIAAdapter.CountOfVTablesToFind.Add(baseUdt.SymIndexId, 1);
        var intType = new BasicTypeSymbol(this.DataCache, "int", 4, this.NextSymIndexId++);
        var baseDataMembers = new List<MemberDataSymbol>()
            {
                new MemberDataSymbol(this.DataCache, "intDataMember", size: 4, this.NextSymIndexId++, isStaticMember: false, false, 0, 8, intType),
                new MemberDataSymbol(this.DataCache, "intBitfield", size: 1, this.NextSymIndexId++, isStaticMember: false, true, 0, 12, intType)
            };
        this.TestDIAAdapter.MemberDataSymbolsToFindByUDT.Add(baseUdt, baseDataMembers);


        var derivedUdt = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "TestType", 24, this.NextSymIndexId++, UserDefinedTypeKind.UdtClass, new Dictionary<uint, uint>() { { baseUdt.SymIndexId, 0 } });
        var derivedFunctions = new List<IFunctionCodeSymbol>()
            {
                new SimpleFunctionCodeSymbol(this.DataCache, "AnotherVirtualFunction", 0, 30, this.NextSymIndexId++)
            };
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(derivedUdt.SymIndexId, derivedFunctions);
        this.TestDIAAdapter.CountOfVTablesToFind.Add(derivedUdt.SymIndexId, 1);
        var boolType = new BasicTypeSymbol(this.DataCache, "bool", 1, this.NextSymIndexId++);
        var derivedDataMembers = new List<MemberDataSymbol>()
            {
                new MemberDataSymbol(this.DataCache, "bitField1", size: 1, this.NextSymIndexId++, isStaticMember: false, isBitField: true, 0, 16, boolType),
                new MemberDataSymbol(this.DataCache, "bitField2", size: 6, this.NextSymIndexId++, isStaticMember: false, isBitField: true, 1, 16, boolType),
                new MemberDataSymbol(this.DataCache, "flag", size: 1, this.NextSymIndexId++, isStaticMember: false, isBitField: false, 0, 17, boolType)
            };
        this.TestDIAAdapter.MemberDataSymbolsToFindByUDT.Add(derivedUdt, derivedDataMembers);

        var task = new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, derivedUdt, 0, null, CancellationToken.None);
        using var logger = new NoOpLogger();
        var typeLayouts = task.Execute(logger);

        Assert.AreEqual(1, typeLayouts.Count);
        Assert.AreEqual(1, typeLayouts[0].BaseTypeLayouts!.Count);

        // Assertions about the base type layout
        Assert.IsTrue(ReferenceEquals(baseUdt, typeLayouts[0].BaseTypeLayouts![0].UserDefinedType));
        Assert.AreEqual(3.875m, typeLayouts[0].BaseTypeLayouts![0].AlignmentWasteExclusive);
        Assert.AreEqual(3.875m, typeLayouts[0].BaseTypeLayouts![0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(8u, typeLayouts[0].BaseTypeLayouts![0].UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, typeLayouts[0].BaseTypeLayouts![0].UsedForVFPtrsIncludingBaseTypes);
        Assert.AreEqual(4, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts!.Count);

        Assert.AreEqual("vfptr", typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![0].Name);
        Assert.AreEqual(8m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![0].Size);
        Assert.AreEqual(0m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![0].Offset);

        Assert.AreEqual("intDataMember", typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![1].Name);
        Assert.AreEqual(4m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![1].Size);
        Assert.AreEqual(8m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![1].Offset);
        Assert.IsFalse(typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![1].IsBitField);
        Assert.IsTrue(ReferenceEquals(intType, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![1].Type));

        Assert.AreEqual("intBitfield", typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].Name);
        Assert.AreEqual(0.125m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].Size);
        Assert.AreEqual(12m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].Offset);
        Assert.IsTrue(typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].IsBitField);
        Assert.AreEqual(0u, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(1u, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].NumberOfBits);
        Assert.IsTrue(ReferenceEquals(intType, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![2].Type));

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].Name);
        Assert.AreEqual(3.875m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].Size);
        Assert.AreEqual(12.125m, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].Offset);
        Assert.IsTrue(typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].IsBitField);
        Assert.AreEqual(1u, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].BitStartPosition);
        Assert.AreEqual(31u, typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].NumberOfBits);
        Assert.IsTrue(typeLayouts[0].BaseTypeLayouts![0].MemberLayouts![3].IsAlignmentMember);


        // Assertions about the derived type layout
        Assert.IsTrue(ReferenceEquals(derivedUdt, typeLayouts[0].UserDefinedType));
        Assert.AreEqual(0u, typeLayouts[0].UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, typeLayouts[0].UsedForVFPtrsIncludingBaseTypes);
        Assert.AreEqual(6.125m, typeLayouts[0].AlignmentWasteExclusive);
        Assert.AreEqual(10.0m, typeLayouts[0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(5, typeLayouts[0].MemberLayouts!.Count);

        Assert.AreEqual("bitField1", typeLayouts[0].MemberLayouts![0].Name);
        Assert.AreEqual(0.125m, typeLayouts[0].MemberLayouts![0].Size);
        Assert.AreEqual(16m, typeLayouts[0].MemberLayouts![0].Offset);
        Assert.IsTrue(typeLayouts[0].MemberLayouts![0].IsBitField);
        Assert.AreEqual(0u, typeLayouts[0].MemberLayouts![0].BitStartPosition);
        Assert.AreEqual(1u, typeLayouts[0].MemberLayouts![0].NumberOfBits);
        Assert.IsTrue(ReferenceEquals(boolType, typeLayouts[0].MemberLayouts![0].Type));

        Assert.AreEqual("bitField2", typeLayouts[0].MemberLayouts![1].Name);
        Assert.AreEqual(0.75m, typeLayouts[0].MemberLayouts![1].Size);
        Assert.AreEqual(16.125m, typeLayouts[0].MemberLayouts![1].Offset);
        Assert.IsTrue(typeLayouts[0].MemberLayouts![1].IsBitField);
        Assert.AreEqual(1u, typeLayouts[0].MemberLayouts![1].BitStartPosition);
        Assert.AreEqual(6u, typeLayouts[0].MemberLayouts![1].NumberOfBits);
        Assert.IsTrue(ReferenceEquals(boolType, typeLayouts[0].MemberLayouts![1].Type));

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, typeLayouts[0].MemberLayouts![2].Name);
        Assert.AreEqual(0.125m, typeLayouts[0].MemberLayouts![2].Size);
        Assert.AreEqual(16.875m, typeLayouts[0].MemberLayouts![2].Offset);
        Assert.IsTrue(typeLayouts[0].MemberLayouts![2].IsBitField);
        Assert.AreEqual(7u, typeLayouts[0].MemberLayouts![2].BitStartPosition);
        Assert.AreEqual(1u, typeLayouts[0].MemberLayouts![2].NumberOfBits);
        Assert.IsTrue(typeLayouts[0].MemberLayouts![2].IsAlignmentMember);

        Assert.AreEqual("flag", typeLayouts[0].MemberLayouts![3].Name);
        Assert.AreEqual(1m, typeLayouts[0].MemberLayouts![3].Size);
        Assert.AreEqual(17m, typeLayouts[0].MemberLayouts![3].Offset);
        Assert.IsFalse(typeLayouts[0].MemberLayouts![3].IsBitField);
        Assert.AreEqual(0u, typeLayouts[0].MemberLayouts![3].BitStartPosition);
        Assert.IsFalse(typeLayouts[0].MemberLayouts![3].IsAlignmentMember);
        Assert.IsTrue(ReferenceEquals(boolType, typeLayouts[0].MemberLayouts![3].Type));

        Assert.AreEqual(TypeLayoutItemMember.TailSlopAlignmentName, typeLayouts[0].MemberLayouts![4].Name);
        Assert.AreEqual(6m, typeLayouts[0].MemberLayouts![4].Size);
        Assert.AreEqual(18m, typeLayouts[0].MemberLayouts![4].Offset);
        Assert.IsFalse(typeLayouts[0].MemberLayouts![4].IsBitField);
        Assert.AreEqual(0u, typeLayouts[0].MemberLayouts![4].BitStartPosition);
        Assert.IsTrue(typeLayouts[0].MemberLayouts![4].IsAlignmentMember);
    }

    [TestMethod]
    public void VfptrFollowedBy16ByteAlignedTypeCalculatesCorrectAlignmentPadding()
    {
        // There used to be a bug where a vfptr followed immediately by padding (because the first data member required >8 byte alignment), would cause that bit of alignment
        // padding to be at the wrong offset and size.

        var voidType = new BasicTypeSymbol(this.DataCache, "void", 0, this.NextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "TestBase", 32, this.NextSymIndexId++, UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var functions = new List<IFunctionCodeSymbol>()
            {
                new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunction", 0, 10, this.NextSymIndexId++,
                                         isVirtual: true, isIntroVirtual: true, accessModifier: AccessModifier.Public,
                                         functionType: new FunctionTypeSymbol(this.DataCache, "void (*function)()", 0, this.NextSymIndexId++, false, false, null, voidType))
            };
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(udt.SymIndexId, functions);
        this.TestDIAAdapter.CountOfVTablesToFind.Add(udt.SymIndexId, 1);
        var m128Type = new BasicTypeSymbol(this.DataCache, "__m128", 16, this.NextSymIndexId++);
        var dataMembers = new List<MemberDataSymbol>()
            {
                new MemberDataSymbol(this.DataCache, "sixteenByteAlignedDataMember", size: 16, this.NextSymIndexId++, isStaticMember: false, isBitField: false, 0, 16, m128Type),
            };
        this.TestDIAAdapter.MemberDataSymbolsToFindByUDT.Add(udt, dataMembers);


        var task = new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, udt, 0, null, CancellationToken.None);
        using var logger = new NoOpLogger();
        var typeLayouts = task.Execute(logger);

        Assert.AreEqual(1, typeLayouts.Count);
        Assert.IsNull(typeLayouts[0].BaseTypeLayouts);

        // Assertions about the base type layout
        Assert.IsTrue(ReferenceEquals(udt, typeLayouts[0].UserDefinedType));
        Assert.AreEqual(8m, typeLayouts[0].AlignmentWasteExclusive);
        Assert.AreEqual(8m, typeLayouts[0].AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(8u, typeLayouts[0].UsedForVFPtrsExclusive);
        Assert.AreEqual(8u, typeLayouts[0].UsedForVFPtrsIncludingBaseTypes);
        Assert.AreEqual(3, typeLayouts[0].MemberLayouts!.Count);

        Assert.AreEqual("vfptr", typeLayouts[0].MemberLayouts![0].Name);
        Assert.AreEqual(8m, typeLayouts[0].MemberLayouts![0].Size);
        Assert.AreEqual(0m, typeLayouts[0].MemberLayouts![0].Offset);

        Assert.AreEqual(TypeLayoutItemMember.AlignmentPaddingName, typeLayouts[0].MemberLayouts![1].Name);
        Assert.IsTrue(typeLayouts[0].MemberLayouts![1].IsAlignmentMember);
        Assert.IsFalse(typeLayouts[0].MemberLayouts![1].IsTailSlopAlignmentMember);
        Assert.AreEqual(8m, typeLayouts[0].MemberLayouts![1].Size);
        Assert.AreEqual(8m, typeLayouts[0].MemberLayouts![1].Offset);
        Assert.IsFalse(typeLayouts[0].MemberLayouts![1].IsBitField);

        Assert.AreEqual("sixteenByteAlignedDataMember", typeLayouts[0].MemberLayouts![2].Name);
        Assert.AreEqual(16m, typeLayouts[0].MemberLayouts![2].Size);
        Assert.AreEqual(16m, typeLayouts[0].MemberLayouts![2].Offset);
        Assert.IsFalse(typeLayouts[0].MemberLayouts![2].IsBitField);
        Assert.IsTrue(ReferenceEquals(m128Type, typeLayouts[0].MemberLayouts![2].Type));
    }

    private IEnumerable<UserDefinedTypeSymbol> GenerateListOfUDTs(int count, string namePrefix)
    {
        for (uint i = 0; i < count; i++)
        {
            var udt = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, namePrefix + i, 10, symIndexId: this.NextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
            yield return udt;
        }
    }

    [TestMethod]
    public void NullInputsLoadsAllTypeLayouts()
    {
        this.TestDIAAdapter.UserDefinedTypesToFind = GenerateListOfUDTs(3, "TestType").Union(GenerateListOfUDTs(3, "ADifferentName"));

        var task = new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, null, null, 0, null, CancellationToken.None);
        using var logger = new NoOpLogger();
        var typeLayouts = task.Execute(logger);

        Assert.AreEqual(6, typeLayouts.Count);
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "TestType0"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "TestType1"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "TestType2"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "ADifferentName0"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "ADifferentName1"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "ADifferentName2"));
    }

    [TestMethod]
    public void LoadingByNameCanLoadAListOfLayoutsFromWildcards()
    {
        this.TestDIAAdapter.UserDefinedTypesToFindByName.Add("TestType*", GenerateListOfUDTs(3, "TestType"));
        this.TestDIAAdapter.UserDefinedTypesToFindByName.Add("ADifferentName*", GenerateListOfUDTs(3, "ADifferentName"));

        var task = new LoadTypeLayoutSessionTask(this.SessionTaskParameters!, "TestType*", null, 0, null, CancellationToken.None);
        using var logger = new NoOpLogger();
        var typeLayouts = task.Execute(logger);

        Assert.AreEqual(3, typeLayouts.Count);
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "TestType0"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "TestType1"));
        Assert.IsTrue(typeLayouts.Any(udt => udt.UserDefinedType.Name == "TestType2"));
    }

    public void Dispose() => this.DataCache.Dispose();
}
