using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateWastefulVirtualsSessionTaskTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();
    private UserDefinedTypeSymbol? Base1UDT;
    private UserDefinedTypeSymbol? Base1_Derived1UDT;
    private UserDefinedTypeSymbol? Base1_Derived2UDT;
    private UserDefinedTypeSymbol? Base1_Derived1_Derived1UDT;

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockSession.SetupGet(s => s.BytesPerWord).Returns(8);

        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);

        /* this data simulates basically this:
         * 
         * class Base1 {
         *      void NonVirtual();
         *      virtual void VirtualFunctionWithOverrides();
         *      virtual void VirtualFunctionWithNoOverrides();
         * }
         * 
         * class Base1_Derived1 : public Base1 {
         *      void VirtualFunctionWithOverrides() override;
         *      virtual void PureVirtualFunctionWithOneOverride() = 0;
         *      virtual void VirtualFunctionWithNoOverrides2();
         *      virtual void VirtualFunctionWithNoOverrides() const; // Note this is const, so it's NOT an override of the one in Base1!
         *      virtual void VirtualFunctionWithNoOverrides(int x); // Note this has an argument so it's NOT an override of the one in Base1!
         * }
         * 
         * class Base1_Derived1_Derived1 : public Base1_Derived1 {
         *      void PureVirtualFunctionWithOneOverride() override;
         *      virtual void VirtualFunctionWithNoOverrides(float y); // Note this has an argument of a different type than the one in Base1_Derived1 so it's not an override!
         * }
         * 
         * class Base1_Derived2 : public Base1 {
         *      void NonVirtual2();
         *      void VirtualFunctionWithOverrides() override;
         * }
         */

        var voidType = new BasicTypeSymbol(this.DataCache, "void", 0, symIndexId: 5000);

        this.Base1UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "Base1", instanceSize: 100, symIndexId: 0, udtKind: UserDefinedTypeKind.UdtClass);
        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Base1UDT.SymIndexId, this.Base1UDT);
        this.Base1_Derived1UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "Base1_Derived1", instanceSize: 150, symIndexId: 2, udtKind: UserDefinedTypeKind.UdtClass);
        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Base1_Derived1UDT.SymIndexId, this.Base1_Derived1UDT);
        this.TestDIAAdapter.BaseTypeIDsToFindByUDT.Add(this.Base1_Derived1UDT, [(this.Base1UDT.SymIndexId, 0)]);
        this.Base1_Derived2UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "Base1_Derived2", instanceSize: 120, symIndexId: 3, udtKind: UserDefinedTypeKind.UdtClass);
        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Base1_Derived2UDT.SymIndexId, this.Base1_Derived2UDT);
        this.TestDIAAdapter.BaseTypeIDsToFindByUDT.Add(this.Base1_Derived2UDT, [(this.Base1UDT.SymIndexId, 0)]);
        this.Base1_Derived1_Derived1UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "Base1_Derived1_Derived1", instanceSize: 120, symIndexId: 4, udtKind: UserDefinedTypeKind.UdtClass);
        this.TestDIAAdapter.TypeSymbolsToFindBySymIndexId.Add(this.Base1_Derived1_Derived1UDT.SymIndexId, this.Base1_Derived1_Derived1UDT);
        this.TestDIAAdapter.BaseTypeIDsToFindByUDT.Add(this.Base1_Derived1_Derived1UDT, [(this.Base1_Derived1UDT.SymIndexId, 0)]);

        var base1_nonVirtual = new SimpleFunctionCodeSymbol(this.DataCache, "NonVirtual", rva: 1000, size: 100, symIndexId: 5, parentType: this.Base1UDT);
        var base1_virtualFunctionWithOverrides = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithOverrides", rva: 1100, size: 100, symIndexId: 6, isVirtual: true, isIntroVirtual: true, parentType: this.Base1UDT);
        var base1_virtualFunctionWithNoOverrides = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithNoOverrides", rva: 1200, size: 100, symIndexId: 7, isVirtual: true, isIntroVirtual: true, parentType: this.Base1UDT);
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(this.Base1UDT.SymIndexId, new List<IFunctionCodeSymbol>() { base1_nonVirtual, base1_virtualFunctionWithOverrides, base1_virtualFunctionWithNoOverrides });

        var base1_derived1VirtualFunctionWithOverrides = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithOverrides", rva: 1300, size: 100, symIndexId: 8, isVirtual: true, parentType: this.Base1_Derived1UDT);
        var base1_derived1PureVirtualFunctionWithOneOverride = new SimpleFunctionCodeSymbol(this.DataCache, "PureVirtualFunctionWithOneOverride", rva: 1400, size: 100, symIndexId: 9, isVirtual: true, isIntroVirtual: true, isPure: true, parentType: this.Base1_Derived1UDT);
        var base1_derived1VirtualFunctionWithNoOverrides2 = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithNoOverrides2", rva: 1500, size: 100, symIndexId: 10, isVirtual: true, isIntroVirtual: true, parentType: this.Base1_Derived1UDT);
        var base1_derived1VirtualFunctionWithNoOverridesType = new FunctionTypeSymbol(this.DataCache, "void VirtualFunctionWithNoOverrides() const", size: 0, symIndexId: 11, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidType);
        var base1_derived1VirtualFunctionWithNoOverrides = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithNoOverrides", rva: 1600, size: 100, symIndexId: 12, isVirtual: true, isIntroVirtual: true, functionType: base1_derived1VirtualFunctionWithNoOverridesType, parentType: this.Base1_Derived1UDT);
        var intArgType = new BasicTypeSymbol(this.DataCache, "int", size: 4, symIndexId: 13);
        var floatArgType = new BasicTypeSymbol(this.DataCache, "float", size: 4, symIndexId: 14);
        var base1_derived1VirtualFunctionWithNoOverrides_withArgType = new FunctionTypeSymbol(this.DataCache, "void VirtualFunctionWithNoOverrides(int)", size: 0, symIndexId: 15, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[1] { intArgType }, returnValueType: voidType);
        var base1_derived1VirtualFunctionWithNoOverrides_withArg = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithNoOverrides", rva: 1700, size: 100, symIndexId: 16, isVirtual: true, isIntroVirtual: true, functionType: base1_derived1VirtualFunctionWithNoOverrides_withArgType, parentType: this.Base1_Derived1UDT);
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(this.Base1_Derived1UDT.SymIndexId, new List<IFunctionCodeSymbol>()
            {
                base1_derived1VirtualFunctionWithOverrides,
                base1_derived1PureVirtualFunctionWithOneOverride,
                base1_derived1VirtualFunctionWithNoOverrides2,
                base1_derived1VirtualFunctionWithNoOverrides,
                base1_derived1VirtualFunctionWithNoOverrides_withArg
            });

        var base1_derived1_derived1PureVirtualFunctionWithOneOverride = new SimpleFunctionCodeSymbol(this.DataCache, "PureVirtualFunctionWithOneOverride", rva: 1800, size: 100, symIndexId: 17, isVirtual: true, parentType: this.Base1_Derived1_Derived1UDT);
        var base1_derived1_derived1VirtualFunctionWithNoOverridesType = new FunctionTypeSymbol(this.DataCache, "void VirtualFunctionWithNoOverrides(float)", size: 0, symIndexId: 18, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[1] { floatArgType }, returnValueType: voidType);
        var base1_derived1_derived1VirtualFunctionWithNoOverrides = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithNoOverrides", rva: 1900, size: 100, symIndexId: 19, isVirtual: true, isIntroVirtual: true, functionType: base1_derived1_derived1VirtualFunctionWithNoOverridesType, parentType: this.Base1_Derived1_Derived1UDT);
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(this.Base1_Derived1_Derived1UDT.SymIndexId, new List<IFunctionCodeSymbol>()
            {
                base1_derived1_derived1PureVirtualFunctionWithOneOverride,
                base1_derived1_derived1VirtualFunctionWithNoOverrides
            });

        var base1_derived2nonVirtual = new SimpleFunctionCodeSymbol(this.DataCache, "NonVirtual2", rva: 2000, size: 100, symIndexId: 20, isVirtual: false, parentType: this.Base1_Derived2UDT);
        var base1_derived2virtualFunctionWithOverrides = new SimpleFunctionCodeSymbol(this.DataCache, "VirtualFunctionWithOverrides", rva: 2100, size: 100, symIndexId: 21, isVirtual: true, parentType: this.Base1_Derived2UDT);
        this.TestDIAAdapter.FunctionsToFindBySymIndexId.Add(this.Base1_Derived2UDT.SymIndexId, new List<IFunctionCodeSymbol>() { base1_derived2nonVirtual, base1_derived2virtualFunctionWithOverrides });
    }

    [TestMethod]
    public void CacheIsReusedAfterOneRun()
    {
        this.TestDIAAdapter.UserDefinedTypesToFind = new List<UserDefinedTypeSymbol>()
            {
                this.Base1UDT!, this.Base1_Derived1UDT!, this.Base1_Derived1_Derived1UDT!, this.Base1_Derived2UDT!
            };

        var task = new EnumerateWastefulVirtualsSessionTask(this.SessionTaskParameters!, CancellationToken.None, progressReporter: null);

        Assert.IsNull(this.DataCache.AllWastefulVirtualItems);

        using var logger = new NoOpLogger();
        var wasteful = task.Execute(logger);

        Assert.IsNotNull(this.DataCache.AllWastefulVirtualItems);

        var wasteful2 = new EnumerateWastefulVirtualsSessionTask(this.SessionTaskParameters!,
                                                                                       CancellationToken.None,
                                                                                       null /*progressReporter*/).Execute(logger);

        Assert.IsTrue(ReferenceEquals(wasteful, wasteful2));
        Assert.IsTrue(ReferenceEquals(wasteful2, this.DataCache.AllWastefulVirtualItems));
    }

    [TestMethod]
    public void WastefulVirtualsCorrectlyDetected()
    {
        this.TestDIAAdapter.UserDefinedTypesToFind = new List<UserDefinedTypeSymbol>()
            {
                this.Base1UDT!, this.Base1_Derived1UDT!, this.Base1_Derived1_Derived1UDT!, this.Base1_Derived2UDT!
            };

        var task = new EnumerateWastefulVirtualsSessionTask(this.SessionTaskParameters!, CancellationToken.None, progressReporter: null);
        using var logger = new NoOpLogger();
        var wasteful = task.Execute(logger);

        // We should have found wasteful virtuals in Base1 and Base1_Derived1
        Assert.AreEqual(2, wasteful.Count);
        var base1Wasteful = wasteful.First(wvi => wvi.UserDefinedType.Name == "Base1");
        var base1_derived1Wasteful = wasteful.First(wvi => wvi.UserDefinedType.Name == "Base1_Derived1");

        // Base1 should have one function considered wasteful: VirtualFunctionWithNoOverrides
        Assert.AreEqual(1, base1Wasteful.WastedOverridesNonPureWithNoOverrides.Count);
        Assert.AreEqual(0, base1Wasteful.WastedOverridesPureWithExactlyOneOverride.Count);
        Assert.AreEqual("VirtualFunctionWithNoOverrides", base1Wasteful.WastedOverridesNonPureWithNoOverrides.First().FunctionName);
        // Should be (8 bytes per word) * (4 classes in the hierarchy) = 32 bytes waste per slot
        Assert.AreEqual(32, base1Wasteful.WastePerSlot);
        // Should be 32 total bytes of waste, since there's only one wasted slot
        Assert.AreEqual<ulong>(32, base1Wasteful.WastedSize);

        // Base1_Derived1 should have two functions considered wasteful: VirtualFunctionWithNoOverrides2 and PureVirtualFunctionWithOneOverride
        Assert.AreEqual(3, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count);
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count(f => f.FunctionName == "VirtualFunctionWithNoOverrides2"));
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count(f => f.FunctionName == "VirtualFunctionWithNoOverrides" && f.FunctionType?.IsConst == true));
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesNonPureWithNoOverrides.Count(f => f.FunctionName == "VirtualFunctionWithNoOverrides" && f.FunctionType?.ArgumentTypes != null && f.FunctionType?.ArgumentTypes.Count == 1));
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesPureWithExactlyOneOverride.Count);
        Assert.AreEqual(1, base1_derived1Wasteful.WastedOverridesPureWithExactlyOneOverride.Count(f => f.FunctionName == "PureVirtualFunctionWithOneOverride"));
        // Should be (8 bytes per word) * (2 classes in this sub-hierarchy) = 16 bytes waste per slot
        Assert.AreEqual(16, base1_derived1Wasteful.WastePerSlot);
        // Should be 32 bytes total of waste, since there's 3 wasted slots
        Assert.AreEqual<ulong>(64, base1_derived1Wasteful.WastedSize);
    }

    public void Dispose() => this.DataCache.Dispose();
}
