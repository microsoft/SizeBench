using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class ComplexFunctionSymbolTests : IDisposable
{
    private SessionDataCache SessionDataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize() => this.SessionDataCache = new SessionDataCache()
    {
        AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
    };

    [TestMethod]
    public void FunctionSymbolPassesThrough()
    {
        var primaryBlock = new PrimaryCodeBlockSymbol(this.SessionDataCache, rva: 10u, size: 50u, symIndexId: 0);
        var separatedBlocks = new List<SeparatedCodeBlockSymbol>();
        var function = new ComplexFunctionCodeSymbol(this.SessionDataCache, "FunctionName1", primaryBlock, separatedBlocks,
                                                accessModifier: AccessModifier.Private, isVirtual: true,
                                                isPGO: true);
        Assert.AreEqual("FunctionName1", function.FunctionName);
        Assert.AreEqual(10u, function.PrimaryBlock.RVA);
        Assert.AreEqual(50u, function.PrimaryBlock.Size);
        Assert.AreEqual(AccessModifier.Private, function.AccessModifier);
        Assert.IsFalse(function.IsIntroVirtual);
        Assert.IsFalse(function.IsPure);
        Assert.IsFalse(function.IsStatic);
        Assert.IsTrue(function.IsVirtual);
        Assert.IsTrue(function.IsPGO);
        Assert.IsFalse(function.IsOptimizedForSpeed);
    }

    [TestMethod]
    public void FunctionFullNameIsBuiltCorrectly()
    {
        uint nextSymIndexId = 0;
        var diaAdapter = new TestDIAAdapter();
        var mockSession = new Mock<ISession>();

        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);

        // Start by trying this out on intro virtuals
        var function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isIntroVirtual: true);

        Assert.AreEqual("virtual FunctionName1()", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isIntroVirtual: true,
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("virtual void FunctionName1() const", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isIntroVirtual: true,
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("virtual void FunctionName1() volatile", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isIntroVirtual: true,
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("virtual void FunctionName1() const volatile", function.FullName);

        var args = new TypeSymbol[]
        {
                new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++),
                new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "MyCustomType", 24, nextSymIndexId++, UserDefinedTypeKind.UdtClass, baseTypeIDs: null)
        };
        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isIntroVirtual: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType),
                                       argumentNames: new ParameterDataSymbol[] { new ParameterDataSymbol(this.SessionDataCache, "wow", nextSymIndexId++, args[0]) });
        Assert.AreEqual("virtual void FunctionName1(int wow, MyCustomType)", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isVirtual: false, isStatic: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType));
        Assert.AreEqual("static void FunctionName1(int, MyCustomType)", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isIntroVirtual: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType),
                                       argumentNames: new ParameterDataSymbol[] { new ParameterDataSymbol(this.SessionDataCache, "wow", nextSymIndexId++, args[0]), new ParameterDataSymbol(this.SessionDataCache, "__anotherArg", nextSymIndexId++, args[1]) });
        Assert.AreEqual("virtual void FunctionName1(int wow, MyCustomType __anotherArg) const", function.FullName);

        // Now try this out on overrides
        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1");

        Assert.AreEqual("FunctionName1() override", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1",
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1() const override", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1",
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1() volatile override", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1",
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1() const volatile override", function.FullName);

        args = new TypeSymbol[]
        {
                new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++),
                new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "MyCustomType", 24, nextSymIndexId++, UserDefinedTypeKind.UdtClass, baseTypeIDs: null)
        };
        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1",
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1(int, MyCustomType) override", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1",
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1(int, MyCustomType) const override", function.FullName);

        // Now try some "final"/"sealed" things
        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isSealed: true);

        Assert.AreEqual("FunctionName1() override final", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isSealed: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1() const override final", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isSealed: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1() volatile override final", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isSealed: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: null, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1() const volatile override final", function.FullName);

        args = new TypeSymbol[]
        {
                new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++),
                new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter, mockSession.Object, "MyCustomType", 24, nextSymIndexId++, UserDefinedTypeKind.UdtClass, baseTypeIDs: null)
        };
        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isSealed: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1(int, MyCustomType) override final", function.FullName);

        function = BuildComplexFunction(ref nextSymIndexId, "FunctionName1", isSealed: true,
                                       functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType));
        Assert.AreEqual("void FunctionName1(int, MyCustomType) const override final", function.FullName);
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenStaticDiffers()
    {
        // These two differ:
        // void CFoo::ABC();
        // static void CFoo::ABC();

        uint nextSymIndexId = 0;
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", isVirtual: false);
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", isVirtual: false, isStatic: true);

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenNameDiffers()
    {
        // These two differ:
        // void CFoo::ABC();
        // void CFoo::ABC2();

        uint nextSymIndexId = 0;
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC");
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC2");

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenOtherSymbolIsASimpleFunction()
    {
        // If we have a complex function and a simple function, but they are the same in terms of name/params/etc. then they can be the same

        uint nextSymIndexId = 0;
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC");
        var simpleFunction = new SimpleFunctionCodeSymbol(this.SessionDataCache, "CFoo::ABC", rva: 20u, size: 100u, symIndexId: nextSymIndexId++);

        Assert.IsTrue(func1.IsVeryLikelyTheSameAs(simpleFunction));
        Assert.IsTrue(simpleFunction.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenFunctionTypeConstDiffers()
    {
        // These two differ:
        // void CFoo::ABC();
        // void CFoo::ABC() const;

        uint nextSymIndexId = 0;
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: voidType));
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidType));

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenFunctionTypeVolatileDiffers()
    {
        // These two differ:
        // void CFoo::ABC();
        // void CFoo::ABC() volatile;

        uint nextSymIndexId = 0;
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);

        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: voidType));
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: null, returnValueType: voidType));

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenFunctionTypeVolatileDiffers2()
    {
        // These two differ:
        // void CFoo::ABC() const;
        // void CFoo::ABC() const volatile;

        uint nextSymIndexId = 0;
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidType));
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: true, argumentTypes: null, returnValueType: voidType));

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenNumberOfArgumentsDiffers()
    {
        // These two differ:
        // void CFoo::ABC(int);
        // void CFoo::ABC(int,int);

        uint nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var args = new TypeSymbol[]
        {
                intBasicType
        };
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType));

        args = new TypeSymbol[]
        {
                intBasicType,
                intBasicType
        };
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: args, returnValueType: voidType));

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenArgumentsAreSameTypesButDifferentOrder()
    {
        // These two differ:
        // void CFoo::ABC(int,float);
        // void CFoo::ABC(float,int);

        uint nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++);
        var floatBasicType = new BasicTypeSymbol(this.SessionDataCache, "float", 4, nextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var args = new TypeSymbol[]
        {
                intBasicType,
                floatBasicType
        };
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType));

        args = new TypeSymbol[]
        {
                floatBasicType,
                intBasicType
        };
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: args, returnValueType: voidType));

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsCanBeVeryLikelyTheSameAcrossSessions()
    {
        // These two are the same, when each exists in a different session data cache and as a separate set of symbols (like a diff would be in a DiffSession):
        // void CFoo::ABC(int,float) const;
        // void CFoo::ABC(int,float) const;

        uint nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++);
        var floatBasicType = new BasicTypeSymbol(this.SessionDataCache, "float", 4, nextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var args = new TypeSymbol[]
        {
                intBasicType,
                floatBasicType
        };
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", sessionDataCache: this.SessionDataCache,
                                         functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType));

        using var dataCache2 = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        nextSymIndexId = 0;
        intBasicType = new BasicTypeSymbol(dataCache2, "int", 4, nextSymIndexId++);
        floatBasicType = new BasicTypeSymbol(dataCache2, "float", 4, nextSymIndexId++);
        args = new TypeSymbol[]
        {
                intBasicType,
                floatBasicType
        };
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", sessionDataCache: dataCache2,
                                         functionType: new FunctionTypeSymbol(dataCache2, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType));

        Assert.IsTrue(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsTrue(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsCanBeVeryLikelyTheSameAcrossSessionsIfOneIsSimpleAndOneIsComplex()
    {
        // These two are the same, when each exists in a different session data cache and as a separate set of symbols (like a diff would be in a DiffSession):
        // void CFoo::ABC(int,float) const;
        // void CFoo::ABC(int,float) const;

        uint nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++);
        var floatBasicType = new BasicTypeSymbol(this.SessionDataCache, "float", 4, nextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var args = new TypeSymbol[]
        {
                intBasicType,
                floatBasicType
        };
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", sessionDataCache: this.SessionDataCache,
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType));

        using var dataCache2 = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        nextSymIndexId = 0;
        intBasicType = new BasicTypeSymbol(dataCache2, "int", 4, nextSymIndexId++);
        floatBasicType = new BasicTypeSymbol(dataCache2, "float", 4, nextSymIndexId++);
        args = new TypeSymbol[]
        {
                intBasicType,
                floatBasicType
        };
        var func2 = new SimpleFunctionCodeSymbol(dataCache2, "CFoo::ABC", rva: 10u, size: 50u, symIndexId: nextSymIndexId++,
                                                 functionType: new FunctionTypeSymbol(dataCache2, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: args, returnValueType: voidType));

        Assert.IsTrue(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsTrue(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreNotVeryLikelyTheSameAsWhenReturnTypesDiffer()
    {
        // Overloading on return type isn't possible in C/C++, but this comparison could be done across sessions in a diff
        // So these two should differ:
        // void CFoo::ABC() const;
        // int CFoo::ABC() const;

        uint nextSymIndexId = 0;
        var voidBasicType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", sessionDataCache: this.SessionDataCache,
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: voidBasicType));

        using var dataCache2 = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(dataCache2, "int", 4, nextSymIndexId++);
        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC", sessionDataCache: dataCache2,
                                         functionType: new FunctionTypeSymbol(dataCache2, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: null, returnValueType: intBasicType));

        Assert.IsFalse(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsFalse(func2.IsVeryLikelyTheSameAs(func1));
    }

    [TestMethod]
    public void FunctionsAreVeryLikelyTheSameIfSignatureIsSameButArgumentNamesDiffer()
    {
        // These two are very likely the same (only the argument names changed):
        // void CFoo::ABC(int wow,float moreArgs);
        // void CFoo::ABC(int renamed,float moreArgs);

        uint nextSymIndexId = 0;
        var intBasicType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, nextSymIndexId++);
        var floatBasicType = new BasicTypeSymbol(this.SessionDataCache, "float", 4, nextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.SessionDataCache, "void", 0, nextSymIndexId++);
        var args = new TypeSymbol[]
        {
                intBasicType,
                floatBasicType
        };
        var argumentNames = new ParameterDataSymbol[]
        {
                new ParameterDataSymbol(this.SessionDataCache, "wow", nextSymIndexId++, args[0]),
                new ParameterDataSymbol(this.SessionDataCache, "moreArgs", nextSymIndexId++, args[1]),
        };
        var func1 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType),
                                        argumentNames: argumentNames);

        argumentNames = new ParameterDataSymbol[]
        {
                new ParameterDataSymbol(this.SessionDataCache, "renamed", nextSymIndexId++, args[0]),
                new ParameterDataSymbol(this.SessionDataCache, "moreArgs", nextSymIndexId++, args[1]),
        };

        var func2 = BuildComplexFunction(ref nextSymIndexId, "CFoo::ABC",
                                        functionType: new FunctionTypeSymbol(this.SessionDataCache, String.Empty, 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: args, returnValueType: voidType),
                                        argumentNames: argumentNames);

        Assert.IsTrue(func1.IsVeryLikelyTheSameAs(func2));
        Assert.IsTrue(func2.IsVeryLikelyTheSameAs(func1));
    }

    private ComplexFunctionCodeSymbol BuildComplexFunction(ref uint nextSymIndexId, string name, bool isVirtual = true, bool isIntroVirtual = false, bool isSealed = false,
                                                           bool isStatic = false,
                                                           SessionDataCache? sessionDataCache = null,
                                                           FunctionTypeSymbol? functionType = null,
                                                           ParameterDataSymbol[]? argumentNames = null)
    {
        var primaryBlock = new PrimaryCodeBlockSymbol(sessionDataCache ?? this.SessionDataCache, rva: 10u, size: 50u, symIndexId: nextSymIndexId++);
        var separatedBlocks = new List<SeparatedCodeBlockSymbol>()
            {
                new SeparatedCodeBlockSymbol(sessionDataCache ?? this.SessionDataCache, rva: 100u, size: 20u, symIndexId: nextSymIndexId++, parentFunctionSymIndexId: primaryBlock.SymIndexId),
                new SeparatedCodeBlockSymbol(sessionDataCache ?? this.SessionDataCache, rva: 200u, size: 20u, symIndexId: nextSymIndexId++, parentFunctionSymIndexId: primaryBlock.SymIndexId),
            };
        return new ComplexFunctionCodeSymbol(sessionDataCache ?? this.SessionDataCache,
                                             name,
                                             primaryBlock,
                                             separatedBlocks,
                                             functionType: functionType,
                                             argumentNames: argumentNames,
                                             accessModifier: AccessModifier.Private,
                                             isVirtual: isVirtual,
                                             isIntroVirtual: isIntroVirtual,
                                             isSealed: isSealed,
                                             isStatic: isStatic,
                                             isPGO: true);
    }

    public void Dispose() => this.SessionDataCache.Dispose();
}
