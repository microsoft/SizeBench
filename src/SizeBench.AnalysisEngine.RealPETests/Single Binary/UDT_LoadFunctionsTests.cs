using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class UDT_LoadFunctionsTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;
    private string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory!, binary);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task UDTsContainCorrectFunctions()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var allTypes = await session.EnumerateAllUserDefinedTypes(this.CancellationToken);

        var base1UDT = allTypes.First(udt => udt.Name == "Base1");

        // ----------------------------------------------------------------------------------------------------------
        // Base1 assertions

        // The two functions we hand-authored, plus 3 constructors, plus 2 operator= overloads (those 5 are put in by the language by default)
        Assert.AreEqual(7, (await base1UDT.GetFunctionsAsync(this.CancellationToken)).Count);
        Assert.AreEqual(3, (await base1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("Base1::Base1(", StringComparison.Ordinal)).Count()); // 3 constructors
        Assert.AreEqual(2, (await base1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("operator=(", StringComparison.Ordinal)).Count()); // 2 overloads of operator=
        Assert.AreEqual(1, (await base1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("virtual void Base1::VirtualFunctionWithManyOverrides", StringComparison.Ordinal)).Count());
        Assert.AreEqual(1, (await base1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("virtual int Base1::VirtualFunctionWithNoOverrides", StringComparison.Ordinal)).Count());

        var manyOverridesFn = (await base1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual void Base1::VirtualFunctionWithManyOverrides()");

        Assert.IsTrue(manyOverridesFn.IsVirtual);
        Assert.IsTrue(manyOverridesFn.IsIntroVirtual);
        Assert.IsFalse(manyOverridesFn.IsPure);
        Assert.IsFalse(manyOverridesFn.IsStatic);
        Assert.IsFalse(manyOverridesFn.IsPGO);
        Assert.IsFalse(manyOverridesFn.IsOptimizedForSpeed);
        Assert.IsFalse(manyOverridesFn.IsSealed);
        Assert.IsFalse(manyOverridesFn.FunctionType!.IsConst);
        Assert.IsFalse(manyOverridesFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, manyOverridesFn.AccessModifier);
        Assert.IsInstanceOfType(manyOverridesFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", manyOverridesFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(manyOverridesFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)manyOverridesFn).Name, ((SimpleFunctionCodeSymbol)manyOverridesFn).CanonicalName);

        var noOverridesFn = (await base1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual int Base1::VirtualFunctionWithNoOverrides()");

        Assert.IsTrue(noOverridesFn.IsVirtual);
        Assert.IsTrue(noOverridesFn.IsIntroVirtual);
        Assert.IsFalse(noOverridesFn.IsPure);
        Assert.IsFalse(noOverridesFn.IsStatic);
        Assert.IsFalse(noOverridesFn.IsPGO);
        Assert.IsFalse(noOverridesFn.IsOptimizedForSpeed);
        Assert.IsFalse(noOverridesFn.IsSealed);
        Assert.IsFalse(noOverridesFn.FunctionType!.IsConst);
        Assert.IsFalse(noOverridesFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, noOverridesFn.AccessModifier);
        Assert.IsInstanceOfType(noOverridesFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", noOverridesFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(noOverridesFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)noOverridesFn).Name, ((SimpleFunctionCodeSymbol)noOverridesFn).CanonicalName);



        // ----------------------------------------------------------------------------------------------------------
        // Base1_Derived1 assertions
        var base1_derived1UDT = base1UDT.DerivedTypes!.First(dt => dt.Name == "Base1_Derived1");

        // The four functions we hand-authored, plus 3 constructors, plus 2 operator= overloads (those 5 are put in by the language by default)
        Assert.AreEqual(9, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Count);
        Assert.AreEqual(3, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("Base1_Derived1::Base1_Derived1(", StringComparison.Ordinal)).Count()); // 3 constructors
        Assert.AreEqual(2, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("operator=(", StringComparison.Ordinal)).Count()); // 2 overloads of operator=
        Assert.AreEqual(1, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "VirtualFunctionWithManyOverrides").Count());
        Assert.AreEqual(1, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "VirtualFunctionWithNoOverrides").Count());
        Assert.AreEqual(1, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "PureVirtualFunctionWithOneOverride").Count());
        Assert.AreEqual(1, (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "VirtualFunctionWithNoOverrides2").Count());

        manyOverridesFn = (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "void Base1_Derived1::VirtualFunctionWithManyOverrides() override");

        Assert.IsTrue(manyOverridesFn.IsVirtual);
        Assert.IsFalse(manyOverridesFn.IsIntroVirtual); // it was introduced by Base1
        Assert.IsFalse(manyOverridesFn.IsPure);
        Assert.IsFalse(manyOverridesFn.IsStatic);
        Assert.IsFalse(manyOverridesFn.IsPGO);
        Assert.IsFalse(manyOverridesFn.IsOptimizedForSpeed);
        Assert.IsFalse(manyOverridesFn.IsSealed);
        Assert.IsFalse(manyOverridesFn.FunctionType!.IsConst);
        Assert.IsFalse(manyOverridesFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, manyOverridesFn.AccessModifier);
        Assert.IsInstanceOfType(manyOverridesFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", manyOverridesFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(manyOverridesFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)manyOverridesFn).Name, ((SimpleFunctionCodeSymbol)manyOverridesFn).CanonicalName);

        var pureVirtualWithOneOverrideFn = (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual void Base1_Derived1::PureVirtualFunctionWithOneOverride()");

        Assert.IsTrue(pureVirtualWithOneOverrideFn.IsVirtual);
        Assert.IsTrue(pureVirtualWithOneOverrideFn.IsIntroVirtual);
        Assert.IsTrue(pureVirtualWithOneOverrideFn.IsPure);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsStatic);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsPGO);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsOptimizedForSpeed);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsSealed);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.FunctionType!.IsConst);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, pureVirtualWithOneOverrideFn.AccessModifier);
        Assert.IsInstanceOfType(pureVirtualWithOneOverrideFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", pureVirtualWithOneOverrideFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(pureVirtualWithOneOverrideFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)pureVirtualWithOneOverrideFn).Name, ((SimpleFunctionCodeSymbol)pureVirtualWithOneOverrideFn).CanonicalName);

        var noOverrides2Fn = (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual void Base1_Derived1::VirtualFunctionWithNoOverrides2()");

        Assert.IsTrue(noOverrides2Fn.IsVirtual);
        Assert.IsTrue(noOverrides2Fn.IsIntroVirtual);
        Assert.IsFalse(noOverrides2Fn.IsPure);
        Assert.IsFalse(noOverrides2Fn.IsStatic);
        Assert.IsFalse(noOverrides2Fn.IsPGO);
        Assert.IsFalse(noOverrides2Fn.IsOptimizedForSpeed);
        Assert.IsFalse(noOverrides2Fn.IsSealed);
        Assert.IsFalse(noOverrides2Fn.FunctionType!.IsConst);
        Assert.IsFalse(noOverrides2Fn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, noOverrides2Fn.AccessModifier);
        Assert.IsInstanceOfType(noOverrides2Fn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", noOverrides2Fn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(noOverrides2Fn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)noOverrides2Fn).Name, ((SimpleFunctionCodeSymbol)noOverrides2Fn).CanonicalName);

        var noOverridesIntFn = (await base1_derived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual int Base1_Derived1::VirtualFunctionWithNoOverrides(int)");

        Assert.IsTrue(noOverridesIntFn.IsVirtual);
        Assert.IsTrue(noOverridesIntFn.IsIntroVirtual);
        Assert.IsFalse(noOverridesIntFn.IsPure);
        Assert.IsFalse(noOverridesIntFn.IsStatic);
        Assert.IsFalse(noOverridesIntFn.IsPGO);
        Assert.IsFalse(noOverridesIntFn.IsOptimizedForSpeed);
        Assert.IsFalse(noOverridesIntFn.IsSealed);
        Assert.IsFalse(noOverridesIntFn.FunctionType!.IsConst);
        Assert.IsFalse(noOverridesIntFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, noOverridesIntFn.AccessModifier);
        Assert.IsInstanceOfType(noOverridesIntFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", noOverridesIntFn.FunctionType.ReturnValueType.Name);
        Assert.IsNotNull(noOverridesIntFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(1, noOverridesIntFn.FunctionType.ArgumentTypes.Count);
        Assert.IsInstanceOfType(noOverridesIntFn.FunctionType.ArgumentTypes[0], typeof(BasicTypeSymbol));
        Assert.AreEqual("int", noOverridesIntFn.FunctionType.ArgumentTypes[0].Name);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)noOverridesIntFn).Name, ((SimpleFunctionCodeSymbol)noOverridesIntFn).CanonicalName);



        // ----------------------------------------------------------------------------------------------------------
        // Base1_Derived2 assertions
        var base1_derived2UDT = base1UDT.DerivedTypes!.First(dt => dt.Name == "Base1_Derived2");

        // The function we hand-authored, plus 3 constructors, plus 2 operator= overloads (those 5 are put in by the language by default)
        Assert.AreEqual(6, (await base1_derived2UDT.GetFunctionsAsync(this.CancellationToken)).Count);
        Assert.AreEqual(3, (await base1_derived2UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("Base1_Derived2::Base1_Derived2(", StringComparison.Ordinal)).Count()); // 3 constructors
        Assert.AreEqual(2, (await base1_derived2UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("operator=(", StringComparison.Ordinal)).Count()); // 2 overloads of operator=
        Assert.AreEqual(1, (await base1_derived2UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "VirtualFunctionWithManyOverrides").Count());

        manyOverridesFn = (await base1_derived2UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "void Base1_Derived2::VirtualFunctionWithManyOverrides() override final");

        Assert.IsTrue(manyOverridesFn.IsVirtual);
        Assert.IsFalse(manyOverridesFn.IsIntroVirtual); // it was introduced by Base1
        Assert.IsFalse(manyOverridesFn.IsPure);
        Assert.IsFalse(manyOverridesFn.IsStatic);
        Assert.IsFalse(manyOverridesFn.IsPGO);
        Assert.IsFalse(manyOverridesFn.IsOptimizedForSpeed);
        Assert.IsTrue(manyOverridesFn.IsSealed);
        Assert.IsFalse(manyOverridesFn.FunctionType!.IsConst);
        Assert.IsFalse(manyOverridesFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, manyOverridesFn.AccessModifier);
        Assert.IsInstanceOfType(manyOverridesFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", manyOverridesFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(manyOverridesFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)manyOverridesFn).Name, ((SimpleFunctionCodeSymbol)manyOverridesFn).CanonicalName);



        // ----------------------------------------------------------------------------------------------------------
        // Base1_Derived1_MoreDerived1 assertions
        var base1_derived1_moreDerived1UDT = base1UDT.DerivedTypes!.First(dt => dt.Name == "Base1_Derived1_MoreDerived1");

        // The four functions we hand-authored, plus 3 constructors, plus 2 operator= overloads (those 5 are put in by the language by default)
        Assert.AreEqual(9, (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).Count);
        Assert.AreEqual(3, (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("Base1_Derived1_MoreDerived1(", StringComparison.Ordinal)).Count()); // 3 constructors
        Assert.AreEqual(2, (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("operator=(", StringComparison.Ordinal)).Count()); // 2 overloads of operator=
        Assert.AreEqual(1, (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "VirtualFunctionWithManyOverrides").Count());
        Assert.AreEqual(2, (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "VirtualFunctionWithNoOverrides").Count());
        Assert.AreEqual(1, (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FunctionName == "PureVirtualFunctionWithOneOverride").Count());

        manyOverridesFn = (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "void Base1_Derived1_MoreDerived1::VirtualFunctionWithManyOverrides() override final");

        Assert.IsTrue(manyOverridesFn.IsVirtual);
        Assert.IsFalse(manyOverridesFn.IsIntroVirtual); // it was introduced by Base1
        Assert.IsFalse(manyOverridesFn.IsPure);
        Assert.IsFalse(manyOverridesFn.IsStatic);
        Assert.IsFalse(manyOverridesFn.IsPGO);
        Assert.IsFalse(manyOverridesFn.IsOptimizedForSpeed);
        Assert.IsTrue(manyOverridesFn.IsSealed);
        Assert.IsFalse(manyOverridesFn.FunctionType!.IsConst);
        Assert.IsFalse(manyOverridesFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, manyOverridesFn.AccessModifier);
        Assert.IsInstanceOfType(manyOverridesFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", manyOverridesFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(manyOverridesFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)manyOverridesFn).Name, ((SimpleFunctionCodeSymbol)manyOverridesFn).CanonicalName);

        var noOverridesConstFn = (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual int Base1_Derived1_MoreDerived1::VirtualFunctionWithNoOverrides() const");

        Assert.IsTrue(noOverridesConstFn.IsVirtual);
        Assert.IsTrue(noOverridesConstFn.IsIntroVirtual);
        Assert.IsFalse(noOverridesConstFn.IsPure);
        Assert.IsFalse(noOverridesConstFn.IsStatic);
        Assert.IsFalse(noOverridesConstFn.IsPGO);
        Assert.IsFalse(noOverridesConstFn.IsOptimizedForSpeed);
        Assert.IsFalse(noOverridesConstFn.IsSealed);
        Assert.IsTrue(noOverridesConstFn.FunctionType!.IsConst);
        Assert.IsFalse(noOverridesConstFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, noOverridesConstFn.AccessModifier);
        Assert.IsInstanceOfType(noOverridesConstFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", noOverridesConstFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(noOverridesConstFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)noOverridesConstFn).Name, ((SimpleFunctionCodeSymbol)noOverridesConstFn).CanonicalName);

        var noOverridesFloatFn = (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "virtual int Base1_Derived1_MoreDerived1::VirtualFunctionWithNoOverrides(float)");

        Assert.IsTrue(noOverridesFloatFn.IsVirtual);
        Assert.IsTrue(noOverridesFloatFn.IsIntroVirtual);
        Assert.IsFalse(noOverridesFloatFn.IsPure);
        Assert.IsFalse(noOverridesFloatFn.IsStatic);
        Assert.IsFalse(noOverridesFloatFn.IsPGO);
        Assert.IsFalse(noOverridesFloatFn.IsOptimizedForSpeed);
        Assert.IsFalse(noOverridesFloatFn.IsSealed);
        Assert.IsFalse(noOverridesFloatFn.FunctionType!.IsConst);
        Assert.IsFalse(noOverridesFloatFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, noOverridesFloatFn.AccessModifier);
        Assert.IsInstanceOfType(noOverridesFloatFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", noOverridesFloatFn.FunctionType.ReturnValueType.Name);
        Assert.IsNotNull(noOverridesFloatFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(1, noOverridesFloatFn.FunctionType.ArgumentTypes.Count);
        Assert.IsInstanceOfType(noOverridesFloatFn.FunctionType.ArgumentTypes[0], typeof(BasicTypeSymbol));
        Assert.AreEqual("float", noOverridesFloatFn.FunctionType.ArgumentTypes[0].Name);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)noOverridesFloatFn).Name, ((SimpleFunctionCodeSymbol)noOverridesFloatFn).CanonicalName);

        pureVirtualWithOneOverrideFn = (await base1_derived1_moreDerived1UDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName.Contains("PureVirtualFunctionWithOneOverride()", StringComparison.Ordinal));

        Assert.IsTrue(pureVirtualWithOneOverrideFn.IsVirtual);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsIntroVirtual); // It was introduced by Base1_Derived1
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsPure); // this is the override so it's not pure
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsStatic);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsPGO);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsOptimizedForSpeed);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.IsSealed);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.FunctionType!.IsConst);
        Assert.IsFalse(pureVirtualWithOneOverrideFn.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Public, pureVirtualWithOneOverrideFn.AccessModifier);
        Assert.IsInstanceOfType(pureVirtualWithOneOverrideFn.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", pureVirtualWithOneOverrideFn.FunctionType.ReturnValueType.Name);
        Assert.IsNull(pureVirtualWithOneOverrideFn.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)pureVirtualWithOneOverrideFn).Name, ((SimpleFunctionCodeSymbol)pureVirtualWithOneOverrideFn).CanonicalName);



        // ----------------------------------------------------------------------------------------------------------
        // AccessModifiersTests assertions
        var accessModifiersTestsUDT = allTypes.First(udt => udt.Name == "AccessModifiersTests");

        Assert.AreEqual(4, (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).Count);
        Assert.AreEqual(1, (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("privateFunction", StringComparison.Ordinal)).Count());
        Assert.AreEqual(1, (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("privateStaticFunction", StringComparison.Ordinal)).Count());
        Assert.AreEqual(1, (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("protectedConstFunction", StringComparison.Ordinal)).Count());
        Assert.AreEqual(1, (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).Where(f => f.FullName.Contains("protectedStaticFunction", StringComparison.Ordinal)).Count());

        var privateFunction = (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "bool AccessModifiersTests::privateFunction()");

        Assert.IsFalse(privateFunction.IsVirtual);
        Assert.IsFalse(privateFunction.IsIntroVirtual);
        Assert.IsFalse(privateFunction.IsPure);
        Assert.IsFalse(privateFunction.IsStatic);
        Assert.IsFalse(privateFunction.IsPGO);
        Assert.IsFalse(privateFunction.IsOptimizedForSpeed);
        Assert.IsFalse(privateFunction.IsSealed);
        Assert.IsFalse(privateFunction.FunctionType!.IsConst);
        Assert.IsFalse(privateFunction.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Private, privateFunction.AccessModifier);
        Assert.IsInstanceOfType(privateFunction.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("bool", privateFunction.FunctionType.ReturnValueType.Name);
        Assert.IsNull(privateFunction.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)privateFunction).Name, ((SimpleFunctionCodeSymbol)privateFunction).CanonicalName);

        var privateStaticFunction = (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "static int AccessModifiersTests::privateStaticFunction()");

        Assert.IsFalse(privateStaticFunction.IsVirtual);
        Assert.IsFalse(privateStaticFunction.IsIntroVirtual);
        Assert.IsFalse(privateStaticFunction.IsPure);
        Assert.IsTrue(privateStaticFunction.IsStatic);
        Assert.IsFalse(privateStaticFunction.IsPGO);
        Assert.IsFalse(privateStaticFunction.IsOptimizedForSpeed);
        Assert.IsFalse(privateStaticFunction.IsSealed);
        Assert.IsFalse(privateStaticFunction.FunctionType!.IsConst);
        Assert.IsFalse(privateStaticFunction.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Private, privateStaticFunction.AccessModifier);
        Assert.IsInstanceOfType(privateStaticFunction.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", privateStaticFunction.FunctionType.ReturnValueType.Name);
        Assert.IsNull(privateStaticFunction.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)privateStaticFunction).Name, ((SimpleFunctionCodeSymbol)privateStaticFunction).CanonicalName);

        var protectedConstFunction = (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "int AccessModifiersTests::protectedConstFunction() const");

        Assert.IsFalse(protectedConstFunction.IsVirtual);
        Assert.IsFalse(protectedConstFunction.IsIntroVirtual);
        Assert.IsFalse(protectedConstFunction.IsPure);
        Assert.IsFalse(protectedConstFunction.IsStatic);
        Assert.IsFalse(protectedConstFunction.IsPGO);
        Assert.IsFalse(protectedConstFunction.IsOptimizedForSpeed);
        Assert.IsFalse(protectedConstFunction.IsSealed);
        Assert.IsTrue(protectedConstFunction.FunctionType!.IsConst);
        Assert.IsFalse(protectedConstFunction.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Protected, protectedConstFunction.AccessModifier);
        Assert.IsInstanceOfType(protectedConstFunction.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("int", protectedConstFunction.FunctionType.ReturnValueType.Name);
        Assert.IsNull(protectedConstFunction.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)protectedConstFunction).Name, ((SimpleFunctionCodeSymbol)protectedConstFunction).CanonicalName);

        var protectedStaticFunction = (await accessModifiersTestsUDT.GetFunctionsAsync(this.CancellationToken)).First(f => f.FullName == "static void AccessModifiersTests::protectedStaticFunction()");

        Assert.IsFalse(protectedStaticFunction.IsVirtual);
        Assert.IsFalse(protectedStaticFunction.IsIntroVirtual);
        Assert.IsFalse(protectedStaticFunction.IsPure);
        Assert.IsTrue(protectedStaticFunction.IsStatic);
        Assert.IsFalse(protectedStaticFunction.IsPGO);
        Assert.IsFalse(protectedStaticFunction.IsOptimizedForSpeed);
        Assert.IsFalse(protectedStaticFunction.IsSealed);
        Assert.IsFalse(protectedStaticFunction.FunctionType!.IsConst);
        Assert.IsFalse(protectedStaticFunction.FunctionType.IsVolatile);
        Assert.AreEqual(AccessModifier.Protected, protectedStaticFunction.AccessModifier);
        Assert.IsInstanceOfType(protectedStaticFunction.FunctionType.ReturnValueType, typeof(BasicTypeSymbol));
        Assert.AreEqual("void", protectedStaticFunction.FunctionType.ReturnValueType.Name);
        Assert.IsNull(protectedStaticFunction.FunctionType.ArgumentTypes);
        Assert.AreEqual(((SimpleFunctionCodeSymbol)protectedStaticFunction).Name, ((SimpleFunctionCodeSymbol)protectedStaticFunction).CanonicalName);
    }
}
