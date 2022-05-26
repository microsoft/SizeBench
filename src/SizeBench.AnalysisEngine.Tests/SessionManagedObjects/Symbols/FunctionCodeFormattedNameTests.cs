using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class FunctionCodeFormattedNameTests
{
    [TestMethod]
    public void SimplestPossibleFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        uint nextSymIndexId = 1;
        var function = new SimpleFunctionCodeSymbol(dataCache, "SomeFunctionName", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: null, argumentNames: null, parentType: null,
                                                    accessModifier: 0, isIntroVirtual: false, isPure: false, isStatic: false, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("SomeFunctionName", function.FunctionName);
        Assert.AreEqual("SomeFunctionName()", function.FullName);
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.All);
        Assert.AreEqual("SomeFunctionName", function.FormattedName.IncludeParentType);
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.UniqueSignature);
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("SomeFunctionName()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("SomeFunctionName", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void PureVirtualFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: voidType);
        var function = new SimpleFunctionCodeSymbol(dataCache, "APureVirtualFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: null,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: true, isPure: true, isStatic: false, isVirtual: true, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("APureVirtualFunction", function.FunctionName);
        Assert.AreEqual("virtual void APureVirtualFunction()", function.FullName);
        Assert.AreEqual("virtual void APureVirtualFunction()", function.FormattedName.All);
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.IncludeParentType);
        Assert.AreEqual("APureVirtualFunction()", function.FormattedName.UniqueSignature);
        Assert.AreEqual("APureVirtualFunction()", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("virtual void APureVirtualFunction()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("APureVirtualFunction()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("void APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("APureVirtualFunction()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("virtual APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("APureVirtualFunction()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("APureVirtualFunction()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("virtual APureVirtualFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void OverrideFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        uint nextSymIndexId = 1;
        var intType = new BasicTypeSymbol(dataCache, "int", 0, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: intType);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AnOverride", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: null,
                                                    accessModifier: AccessModifier.Private, isIntroVirtual: false, isPure: false, isStatic: false, isVirtual: true, isSealed: true, isPGO: true,
                                                    isOptimizedForSpeed: true);

        Assert.AreEqual("AnOverride", function.FunctionName);
        Assert.AreEqual("int AnOverride() override final", function.FullName);
        Assert.AreEqual("int AnOverride() override final", function.FormattedName.All);
        Assert.AreEqual("AnOverride", function.FormattedName.IncludeParentType);
        Assert.AreEqual("AnOverride()", function.FormattedName.UniqueSignature);
        Assert.AreEqual("AnOverride()", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("int AnOverride() override final", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("AnOverride", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("AnOverride()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AnOverride", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("AnOverride", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("int AnOverride", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("AnOverride final", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("AnOverride", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("AnOverride()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("AnOverride override", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("AnOverride", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("AnOverride()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AnOverride()", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("AnOverride override", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("AnOverride override final", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride | FunctionCodeNameFormatting.IncludeSealed));
    }

    [TestMethod]
    public void FreeFunctionInNamespaceGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { intType }, returnValueType: voidType);
        var function = new SimpleFunctionCodeSymbol(dataCache, "MyNamespace::AFreeFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: null,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: false, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("MyNamespace::AFreeFunction", function.FunctionName);
        Assert.AreEqual("void MyNamespace::AFreeFunction(int)", function.FullName);
        Assert.AreEqual("void MyNamespace::AFreeFunction(int)", function.FormattedName.All);
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.IncludeParentType);
        Assert.AreEqual("MyNamespace::AFreeFunction(int)", function.FormattedName.UniqueSignature);
        Assert.AreEqual("MyNamespace::AFreeFunction(int)", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("void MyNamespace::AFreeFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("MyNamespace::AFreeFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("void MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("MyNamespace::AFreeFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("MyNamespace::AFreeFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("MyNamespace::AFreeFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("MyNamespace::AFreeFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void MemberFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { intType }, returnValueType: constUDTRef);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AMemberFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: udt,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: true, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("AMemberFunction", function.FunctionName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int)", function.FullName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int)", function.FormattedName.All);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.IncludeParentType);
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int)", function.FormattedName.UniqueSignature);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction(int)", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("const ANamespace::SomeUDT& AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("static AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void FunctionWithArgumentTypesButNoNamesGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var intPtrType = new PointerTypeSymbol(dataCache, intType, "int*", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { intType, constUDTRef, intPtrType }, returnValueType: intType);
        var function = new SimpleFunctionCodeSymbol(dataCache, "MultipleArgs", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: null,
                                                    accessModifier: AccessModifier.Protected, isIntroVirtual: false, isPure: false, isStatic: false, isVirtual: false, isSealed: false, isPGO: true,
                                                    isOptimizedForSpeed: true);

        Assert.AreEqual("MultipleArgs", function.FunctionName);
        Assert.AreEqual("int MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FullName);
        Assert.AreEqual("int MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.All);
        Assert.AreEqual("MultipleArgs", function.FormattedName.IncludeParentType);
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.UniqueSignature);
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("int MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("int MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void FunctionWithNamedArgumentsGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var intPtrType = new PointerTypeSymbol(dataCache, intType, "int*", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { intType, constUDTRef, intPtrType }, returnValueType: intType);
        var argumentNames = new ParameterDataSymbol[]
        {
                new ParameterDataSymbol(dataCache, "specialIntIndex", nextSymIndexId++, intType),
                new ParameterDataSymbol(dataCache, "refToSuperImportantType", nextSymIndexId++, constUDTRef),
                new ParameterDataSymbol(dataCache, "ptr", nextSymIndexId++, intPtrType),
        };
        var function = new SimpleFunctionCodeSymbol(dataCache, "MultipleArgs", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: argumentNames, parentType: null,
                                                    accessModifier: AccessModifier.Protected, isIntroVirtual: false, isPure: false, isStatic: false, isVirtual: false, isSealed: false, isPGO: true,
                                                    isOptimizedForSpeed: true);

        Assert.AreEqual("MultipleArgs", function.FunctionName);
        Assert.AreEqual("int MultipleArgs(int specialIntIndex, const ANamespace::SomeUDT& refToSuperImportantType, int* ptr)", function.FullName);
        Assert.AreEqual("int MultipleArgs(int specialIntIndex, const ANamespace::SomeUDT& refToSuperImportantType, int* ptr)", function.FormattedName.All);
        Assert.AreEqual("MultipleArgs", function.FormattedName.IncludeParentType);
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.UniqueSignature);
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("int MultipleArgs(int specialIntIndex, const ANamespace::SomeUDT& refToSuperImportantType, int* ptr)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("int MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("MultipleArgs(int specialIntIndex, const ANamespace::SomeUDT& refToSuperImportantType, int* ptr)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("MultipleArgs(int, const ANamespace::SomeUDT&, int*)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("MultipleArgs", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void ConstFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: new TypeSymbol[] { intType }, returnValueType: constUDTRef);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AMemberFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: udt,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: true, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("AMemberFunction", function.FunctionName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const", function.FullName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const", function.FormattedName.All);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.IncludeParentType);
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) const", function.FormattedName.UniqueSignature);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction(int) const", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction const", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("const ANamespace::SomeUDT& AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("static AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) const", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction(int) const", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void VolatileFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: new TypeSymbol[] { intType }, returnValueType: constUDTRef);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AMemberFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: udt,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: true, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("AMemberFunction", function.FunctionName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) volatile", function.FullName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) volatile", function.FormattedName.All);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.IncludeParentType);
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) volatile", function.FormattedName.UniqueSignature);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction(int) volatile", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("const ANamespace::SomeUDT& AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("static AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction(int) volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }

    [TestMethod]
    public void ConstVolatileOverrideFunctionGeneratesCorrectFormattedNames()
    {
        using var dataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();
        uint nextSymIndexId = 1;
        var voidType = new BasicTypeSymbol(dataCache, "void", 0, nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", 4, nextSymIndexId++);
        var udt = new UserDefinedTypeSymbol(dataCache, diaAdapter, mockSession.Object, "ANamespace::SomeUDT", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var constUDT = new ModifiedTypeSymbol(dataCache, udt, $"const {udt.Name}", size: 10, nextSymIndexId++);
        var constUDTRef = new PointerTypeSymbol(dataCache, constUDT, $"{constUDT.Name}&", instanceSize: 10, nextSymIndexId++);
        var functionType = new FunctionTypeSymbol(dataCache, "", 0, nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: new TypeSymbol[] { intType }, returnValueType: constUDTRef);
        var function = new SimpleFunctionCodeSymbol(dataCache, "AMemberFunction", rva: 0, size: 0, symIndexId: nextSymIndexId++, functionType: functionType, argumentNames: null, parentType: udt,
                                                    accessModifier: AccessModifier.Public, isIntroVirtual: false, isPure: false, isStatic: true, isVirtual: false, isSealed: false, isPGO: false,
                                                    isOptimizedForSpeed: false);

        Assert.AreEqual("AMemberFunction", function.FunctionName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const volatile", function.FullName);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const volatile", function.FormattedName.All);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.IncludeParentType);
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) const volatile", function.FormattedName.UniqueSignature);
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction(int) const volatile", function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual("static const ANamespace::SomeUDT& ANamespace::SomeUDT::AMemberFunction(int) const volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.All));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction const volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType));
        Assert.AreEqual("const ANamespace::SomeUDT& AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeReturnType));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeSealed));
        Assert.AreEqual("static AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeStatic));
        Assert.AreEqual("static ANamespace::SomeUDT::AMemberFunction(int) const volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeVirtualOverride));
        Assert.AreEqual("AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.None));
        Assert.AreEqual("AMemberFunction(int)", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentNames | FunctionCodeNameFormatting.IncludeArgumentTypes));
        Assert.AreEqual("AMemberFunction(int) const volatile", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeArgumentTypes | FunctionCodeNameFormatting.IncludeCVQualifiers));
        Assert.AreEqual("ANamespace::SomeUDT::AMemberFunction", function.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeParentType | FunctionCodeNameFormatting.IncludeVirtualOverride));
    }
}
