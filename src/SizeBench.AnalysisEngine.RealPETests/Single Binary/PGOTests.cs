using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\External\x64\Microsoft.UI.Xaml.dll")]
[DeploymentItem(@"Test PEs\External\x64\Microsoft.UI.Xaml.pdb")]
[TestCategory(CommonTestCategories.SlowTests)]
[TestClass]
public sealed class PGOTests
{
    public TestContext? TestContext { get; set; }

    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    // PGO'd binaries are complicated and slow to open, so we don't want to re-open this for each test method.
    private static Session? MUXSession;
    private static NoOpLogger? SessionLogger;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);

        SessionLogger = new NoOpLogger();
        MUXSession = await Session.Create(Path.Combine(testContext.DeploymentDirectory!, "Microsoft.UI.Xaml.dll"),
                                          Path.Combine(testContext.DeploymentDirectory!, "Microsoft.UI.Xaml.pdb"),
                                          SessionLogger);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (MUXSession != null)
        {
            await MUXSession.DisposeAsync();
            SessionLogger?.Dispose();
        }
    }

    [TestMethod]
    public async Task ColdFunctionCanBeParsedIncludingFrameHandler4XDATA()
    {
        // Try to get a function that is cold ($zz) and verify stuff about it.
        // This function was specifically chosen because it also has __CxxFrameHandler4 metadata so we can validate PGO'd FH4 metadata.

        var TeachingTip_UpdateTail_Function = await MUXSession!.LoadSymbolByRVA(0x33E9E8) as SimpleFunctionCodeSymbol;
        Assert.AreEqual(AccessModifier.Private, TeachingTip_UpdateTail_Function!.AccessModifier);
        Assert.IsNull(TeachingTip_UpdateTail_Function.ArgumentNames);
        Assert.AreEqual(1, TeachingTip_UpdateTail_Function.Blocks.Count);
        Assert.IsTrue(TeachingTip_UpdateTail_Function.CanBeFolded);
        Assert.AreEqual(TeachingTip_UpdateTail_Function.Name, TeachingTip_UpdateTail_Function.CanonicalName);
        Assert.IsNotNull(TeachingTip_UpdateTail_Function.FunctionType);
        Assert.IsNull(TeachingTip_UpdateTail_Function.FunctionType.ArgumentTypes);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.FunctionType.CanLoadLayout);
        Assert.AreEqual(0u, TeachingTip_UpdateTail_Function.FunctionType.InstanceSize);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.FunctionType.IsConst);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.FunctionType.IsVolatile);
        Assert.AreEqual("bool", TeachingTip_UpdateTail_Function.FunctionType.ReturnValueType.Name);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsCOMDATFolded);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsIntroVirtual);
        Assert.IsTrue(TeachingTip_UpdateTail_Function.IsMemberFunction);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsOptimizedForSpeed);
        Assert.IsTrue(TeachingTip_UpdateTail_Function.IsPGO);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsPure);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsSealed);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsStatic);
        Assert.IsFalse(TeachingTip_UpdateTail_Function.IsVirtual);
        Assert.AreEqual("TeachingTip::UpdateTail()", TeachingTip_UpdateTail_Function.Name);
        Assert.AreEqual(TeachingTip_UpdateTail_Function, TeachingTip_UpdateTail_Function.ParentFunction);
        Assert.IsNotNull(TeachingTip_UpdateTail_Function.ParentType);
        Assert.AreEqual("TeachingTip", TeachingTip_UpdateTail_Function.ParentType.Name);
        Assert.AreEqual(TeachingTip_UpdateTail_Function, TeachingTip_UpdateTail_Function.PrimaryBlock);
        Assert.AreEqual(0x33E9E8u, TeachingTip_UpdateTail_Function.RVA);
        Assert.AreEqual(0x33FFF3u, TeachingTip_UpdateTail_Function.RVAEnd);
        Assert.AreEqual(5644u, TeachingTip_UpdateTail_Function.Size);
        Assert.AreEqual(SymbolComparisonClass.PrimaryCodeBlock, TeachingTip_UpdateTail_Function.SymbolComparisonClass);
        Assert.AreEqual(5644u, TeachingTip_UpdateTail_Function.VirtualSize);

        var cppxdata = await MUXSession.LoadSymbolByRVA(0x56A3B0) as CppXdataSymbol;
        Assert.AreEqual(9u, cppxdata!.Size);
        Assert.AreEqual(TeachingTip_UpdateTail_Function.RVA, cppxdata.TargetStartRVA);
        Assert.AreEqual($"[cppxdata] {TeachingTip_UpdateTail_Function.Name}", cppxdata.Name);

        var unwind = await MUXSession.LoadSymbolByRVA(0x56A380) as UnwindInfoSymbol;
        Assert.AreEqual(48u, unwind!.Size);
        Assert.AreEqual(TeachingTip_UpdateTail_Function.RVA, unwind.TargetStartRVA);
        Assert.AreEqual($"[unwind] {TeachingTip_UpdateTail_Function.Name}", unwind.Name);

        var stateUnwindMap = await MUXSession.LoadSymbolByRVA(0x56A3B9) as StateUnwindMapSymbol;
        Assert.AreEqual(37u, stateUnwindMap!.Size);
        Assert.AreEqual(TeachingTip_UpdateTail_Function.RVA, stateUnwindMap.TargetStartRVA);
        Assert.AreEqual($"[stateUnwindMap] {TeachingTip_UpdateTail_Function.Name}", stateUnwindMap.Name);

        var ip2State = await MUXSession.LoadSymbolByRVA(0x56A3DE) as IpToStateMapSymbol;
        Assert.AreEqual(144u, ip2State!.Size);
        Assert.AreEqual(TeachingTip_UpdateTail_Function.RVA, ip2State.TargetStartRVA);
        Assert.AreEqual($"[ip2state] {TeachingTip_UpdateTail_Function.Name}", ip2State.Name);
    }

    [TestMethod]
    public async Task ColdFunctionsThatAreCOMDATFoldedCanBeParsed()
    {
        // Try to get a function that is cold ($zz) that is COMDAT folded with other cold functions

        var PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function = await MUXSession!.LoadSymbolByRVA(0x3C63E0) as SimpleFunctionCodeSymbol;
        Assert.AreEqual(AccessModifier.Public, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function!.AccessModifier);
        Assert.IsNotNull(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.ArgumentNames);
        Assert.AreEqual(2, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.ArgumentNames.Count);
        Assert.AreEqual(1, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.Blocks.Count);
        Assert.IsTrue(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.CanBeFolded);
        Assert.AreEqual(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.Name, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.CanonicalName);
        Assert.IsNotNull(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType);
        Assert.IsNotNull(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.ArgumentTypes);
        Assert.AreEqual(2, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.ArgumentTypes.Count);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.CanLoadLayout);
        Assert.AreEqual(0u, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.InstanceSize);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.IsConst);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.IsVolatile);
        Assert.AreEqual("void", PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.FunctionType.ReturnValueType.Name);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsCOMDATFolded);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsIntroVirtual);
        Assert.IsTrue(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsMemberFunction);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsOptimizedForSpeed);
        Assert.IsTrue(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsPGO);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsPure);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsSealed);
        Assert.IsTrue(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsStatic);
        Assert.IsFalse(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.IsVirtual);
        Assert.AreEqual("PipsPagerProperties::OnMaxVisiblePipsPropertyChanged(const winrt::Windows::UI::Xaml::DependencyObject&, const winrt::Windows::UI::Xaml::DependencyPropertyChangedEventArgs&)", PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.Name);
        Assert.AreEqual(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.ParentFunction);
        Assert.IsNotNull(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.ParentType);
        Assert.AreEqual("PipsPagerProperties", PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.ParentType.Name);
        Assert.AreEqual(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.PrimaryBlock);
        Assert.AreEqual(0x3C63E0u, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.RVA);
        Assert.AreEqual(0x3C642Fu, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.RVAEnd);
        Assert.AreEqual(80u, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.Size);
        Assert.AreEqual(SymbolComparisonClass.PrimaryCodeBlock, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.SymbolComparisonClass);
        Assert.AreEqual(80u, PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.VirtualSize);

        var PipsPagerProperties_FoldedColdFunctions = await MUXSession.EnumerateAllSymbolsFoldedAtRVA(0x3C63E0, this.CancellationToken);
        Assert.AreEqual(11, PipsPagerProperties_FoldedColdFunctions.Count);
        var PipsPagerProperties_FoldedColdFunctions_Attributed_To = PipsPagerProperties_FoldedColdFunctions.Single(sym => !sym.IsCOMDATFolded);
        Assert.AreEqual(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function, PipsPagerProperties_FoldedColdFunctions_Attributed_To);
        var PipsPagerProperties_OnNextButtonStylePropertyChanged_Function = PipsPagerProperties_FoldedColdFunctions.OfType<SimpleFunctionCodeSymbol>().Single(sym => sym.Name == "PipsPagerProperties::OnNextButtonStylePropertyChanged(const winrt::Windows::UI::Xaml::DependencyObject&, const winrt::Windows::UI::Xaml::DependencyPropertyChangedEventArgs&)");
        Assert.AreEqual(AccessModifier.Public, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function!.AccessModifier);
        Assert.IsNotNull(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.ArgumentNames);
        Assert.AreEqual(2, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.ArgumentNames.Count);
        Assert.AreEqual(1, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.Blocks.Count);
        Assert.IsTrue(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.CanBeFolded);
        Assert.AreEqual(PipsPagerProperties_OnMaxVisiblePipsPropertyChanged_Function.Name, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.CanonicalName); // Note the canonical name is for the 'primary' function above
        Assert.IsNotNull(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType);
        Assert.IsNotNull(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.ArgumentTypes);
        Assert.AreEqual(2, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.ArgumentTypes.Count);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.CanLoadLayout);
        Assert.AreEqual(0u, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.InstanceSize);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.IsConst);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.IsVolatile);
        Assert.AreEqual("void", PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.FunctionType.ReturnValueType.Name);
        Assert.IsTrue(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsCOMDATFolded);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsIntroVirtual);
        Assert.IsTrue(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsMemberFunction);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsOptimizedForSpeed);
        Assert.IsTrue(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsPGO);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsPure);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsSealed);
        Assert.IsTrue(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsStatic);
        Assert.IsFalse(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.IsVirtual);
        Assert.AreEqual("PipsPagerProperties::OnNextButtonStylePropertyChanged(const winrt::Windows::UI::Xaml::DependencyObject&, const winrt::Windows::UI::Xaml::DependencyPropertyChangedEventArgs&)", PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.Name);
        Assert.AreEqual(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.ParentFunction);
        Assert.IsNotNull(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.ParentType);
        Assert.AreEqual("PipsPagerProperties", PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.ParentType.Name);
        Assert.AreEqual(PipsPagerProperties_OnNextButtonStylePropertyChanged_Function, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.PrimaryBlock);
        Assert.AreEqual(0x3C63E0u, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.RVA);
        Assert.AreEqual(0x3C63E0u, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.RVAEnd);
        Assert.AreEqual(0u, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.Size);
        Assert.AreEqual(SymbolComparisonClass.PrimaryCodeBlock, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.SymbolComparisonClass);
        Assert.AreEqual(0u, PipsPagerProperties_OnNextButtonStylePropertyChanged_Function.VirtualSize);
    }

    [TestMethod]
    public async Task HotFunctionCanBeParsed()
    {
        // Try to get a function that is hot ($lp) and verify stuff about it

        var XamlMetadataProviderGenerated_RegisterTypes_Function = await MUXSession!.LoadSymbolByRVA(0x51590) as SimpleFunctionCodeSymbol;
        Assert.AreEqual(AccessModifier.Public, XamlMetadataProviderGenerated_RegisterTypes_Function!.AccessModifier);
        Assert.IsNull(XamlMetadataProviderGenerated_RegisterTypes_Function.ArgumentNames);
        Assert.AreEqual(1, XamlMetadataProviderGenerated_RegisterTypes_Function.Blocks.Count);
        Assert.IsTrue(XamlMetadataProviderGenerated_RegisterTypes_Function.CanBeFolded);
        Assert.AreEqual(XamlMetadataProviderGenerated_RegisterTypes_Function.Name, XamlMetadataProviderGenerated_RegisterTypes_Function.CanonicalName);
        Assert.IsNotNull(XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType);
        Assert.IsNull(XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType.ArgumentTypes);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType.CanLoadLayout);
        Assert.AreEqual(0u, XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType.InstanceSize);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType.IsConst);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType.IsVolatile);
        Assert.AreEqual("void", XamlMetadataProviderGenerated_RegisterTypes_Function.FunctionType.ReturnValueType.Name);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.IsCOMDATFolded);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.IsIntroVirtual);
        Assert.IsTrue(XamlMetadataProviderGenerated_RegisterTypes_Function.IsMemberFunction);
        Assert.IsTrue(XamlMetadataProviderGenerated_RegisterTypes_Function.IsOptimizedForSpeed);
        Assert.IsTrue(XamlMetadataProviderGenerated_RegisterTypes_Function.IsPGO);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.IsPure);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.IsSealed);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.IsStatic);
        Assert.IsFalse(XamlMetadataProviderGenerated_RegisterTypes_Function.IsVirtual);
        Assert.AreEqual("XamlMetadataProviderGenerated::RegisterTypes()", XamlMetadataProviderGenerated_RegisterTypes_Function.Name);
        Assert.AreEqual(XamlMetadataProviderGenerated_RegisterTypes_Function, XamlMetadataProviderGenerated_RegisterTypes_Function.ParentFunction);
        Assert.IsNotNull(XamlMetadataProviderGenerated_RegisterTypes_Function.ParentType);
        Assert.AreEqual("XamlMetadataProviderGenerated", XamlMetadataProviderGenerated_RegisterTypes_Function.ParentType.Name);
        Assert.AreEqual(XamlMetadataProviderGenerated_RegisterTypes_Function, XamlMetadataProviderGenerated_RegisterTypes_Function.PrimaryBlock);
        Assert.AreEqual(0x51590u, XamlMetadataProviderGenerated_RegisterTypes_Function.RVA);
        Assert.AreEqual(0x54BCDu, XamlMetadataProviderGenerated_RegisterTypes_Function.RVAEnd);
        Assert.AreEqual(13886u, XamlMetadataProviderGenerated_RegisterTypes_Function.Size);
        Assert.AreEqual(SymbolComparisonClass.PrimaryCodeBlock, XamlMetadataProviderGenerated_RegisterTypes_Function.SymbolComparisonClass);
        Assert.AreEqual(13886u, XamlMetadataProviderGenerated_RegisterTypes_Function.VirtualSize);
    }

    [TestMethod]
    public async Task ComplexFunctionCanBeParsed()
    {
        // Try to get a function with separated blocks, verify things about the function and the blocks

        var FlowLayoutAlgorithm_Generate_PrimaryBlock = await MUXSession!.LoadSymbolByRVA(0xD710) as PrimaryCodeBlockSymbol;
        var FlowLayoutAlgorithm_Generate_Function = FlowLayoutAlgorithm_Generate_PrimaryBlock!.ParentFunction as ComplexFunctionCodeSymbol;
        Assert.AreEqual(1, FlowLayoutAlgorithm_Generate_Function!.SeparatedBlocks.Count);
        var FlowLayoutAlgorithm_Generate_SeparatedBlock = FlowLayoutAlgorithm_Generate_Function!.SeparatedBlocks[0];
        // Verify stuff about the function
        Assert.AreEqual(AccessModifier.Private, FlowLayoutAlgorithm_Generate_Function!.AccessModifier);
        Assert.IsNotNull(FlowLayoutAlgorithm_Generate_Function.ArgumentNames);
        Assert.AreEqual(8, FlowLayoutAlgorithm_Generate_Function.ArgumentNames.Count);
        Assert.AreEqual(2, FlowLayoutAlgorithm_Generate_Function.Blocks.Count);
        Assert.IsNotNull(FlowLayoutAlgorithm_Generate_Function.FunctionType);
        Assert.IsNotNull(FlowLayoutAlgorithm_Generate_Function.FunctionType.ArgumentTypes);
        Assert.AreEqual(8, FlowLayoutAlgorithm_Generate_Function.FunctionType.ArgumentTypes.Count);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.FunctionType.CanLoadLayout);
        Assert.AreEqual(0u, FlowLayoutAlgorithm_Generate_Function.FunctionType.InstanceSize);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.FunctionType.IsConst);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.FunctionType.IsVolatile);
        Assert.AreEqual("void", FlowLayoutAlgorithm_Generate_Function.FunctionType.ReturnValueType.Name);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.IsIntroVirtual);
        Assert.IsTrue(FlowLayoutAlgorithm_Generate_Function.IsMemberFunction);
        Assert.IsTrue(FlowLayoutAlgorithm_Generate_Function.IsOptimizedForSpeed);
        Assert.IsTrue(FlowLayoutAlgorithm_Generate_Function.IsPGO);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.IsPure);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.IsSealed);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.IsStatic);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_Function.IsVirtual);
        Assert.AreEqual("FlowLayoutAlgorithm::Generate(enum FlowLayoutAlgorithm::GenerateDirection, int, const winrt::Windows::Foundation::Size&, double, double, unsigned int, const bool, const std::basic_string_view<wchar_t,std::char_traits<wchar_t> >&)", FlowLayoutAlgorithm_Generate_Function.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.IsNotNull(FlowLayoutAlgorithm_Generate_Function.ParentType);
        Assert.AreEqual("FlowLayoutAlgorithm", FlowLayoutAlgorithm_Generate_Function.ParentType.Name);
        Assert.AreEqual(FlowLayoutAlgorithm_Generate_PrimaryBlock, FlowLayoutAlgorithm_Generate_Function.PrimaryBlock);
        Assert.AreEqual(0x9B5u /* primary block */ + 0x689u /* sep block */, FlowLayoutAlgorithm_Generate_Function.Size);

        // Verify stuff about the primary block
        Assert.IsTrue(FlowLayoutAlgorithm_Generate_PrimaryBlock.CanBeFolded);
        Assert.AreEqual(FlowLayoutAlgorithm_Generate_PrimaryBlock.Name, FlowLayoutAlgorithm_Generate_PrimaryBlock.CanonicalName);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_PrimaryBlock.IsCOMDATFolded);
        Assert.AreEqual($"Block of code in {FlowLayoutAlgorithm_Generate_Function.FormattedName.UniqueSignatureWithNoPrefixes}", FlowLayoutAlgorithm_Generate_PrimaryBlock.Name);
        Assert.AreEqual(FlowLayoutAlgorithm_Generate_Function, FlowLayoutAlgorithm_Generate_PrimaryBlock.ParentFunction);
        Assert.AreEqual(0xD710u, FlowLayoutAlgorithm_Generate_PrimaryBlock.RVA);
        Assert.AreEqual(0xE0C4u, FlowLayoutAlgorithm_Generate_PrimaryBlock.RVAEnd);
        Assert.AreEqual(0x9B5u, FlowLayoutAlgorithm_Generate_PrimaryBlock.Size);
        Assert.AreEqual(SymbolComparisonClass.PrimaryCodeBlock, FlowLayoutAlgorithm_Generate_PrimaryBlock.SymbolComparisonClass);
        Assert.AreEqual(0x9B5u, FlowLayoutAlgorithm_Generate_PrimaryBlock.VirtualSize);

        // Verify stuff about the separated block
        Assert.IsTrue(FlowLayoutAlgorithm_Generate_SeparatedBlock.CanBeFolded);
        Assert.AreEqual(FlowLayoutAlgorithm_Generate_SeparatedBlock.Name, FlowLayoutAlgorithm_Generate_SeparatedBlock.CanonicalName);
        Assert.IsFalse(FlowLayoutAlgorithm_Generate_SeparatedBlock.IsCOMDATFolded);
        Assert.AreEqual($"Block of code in {FlowLayoutAlgorithm_Generate_Function.FormattedName.UniqueSignatureWithNoPrefixes}", FlowLayoutAlgorithm_Generate_SeparatedBlock.Name);
        Assert.AreEqual(FlowLayoutAlgorithm_Generate_Function, FlowLayoutAlgorithm_Generate_SeparatedBlock.ParentFunction);
        Assert.AreEqual(0xD743Cu, FlowLayoutAlgorithm_Generate_SeparatedBlock.RVA);
        Assert.AreEqual(0xD7AC4u, FlowLayoutAlgorithm_Generate_SeparatedBlock.RVAEnd);
        Assert.AreEqual(0x689u, FlowLayoutAlgorithm_Generate_SeparatedBlock.Size);
        Assert.AreEqual(SymbolComparisonClass.SeparatedCodeBlock, FlowLayoutAlgorithm_Generate_SeparatedBlock.SymbolComparisonClass);
        Assert.AreEqual(0x689u, FlowLayoutAlgorithm_Generate_SeparatedBlock.VirtualSize);
    }

    [TestMethod]
    public async Task BlocksThatAreCOMDATFoldedCanBeParsedIncludingFrameHandler4XDATA()
    {
        // Try to get a block that is COMDAT folded across multiple functions - this will expose that ParentFunction is busted, note this and file a bug.  Verify things about that block (size 0, etc.)
        // This block was specifically chosen because it also has __CxxFrameHandler4 metadata so we can validate PGO'd FH4 metadata, for things like chain unwind and separated IpToState tables.

        var AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock = await MUXSession!.LoadSymbolByRVA(0xD63C6) as SeparatedCodeBlockSymbol;
        var AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock = AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock!.ParentFunction.PrimaryBlock;
        Assert.IsTrue(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.CanBeFolded);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.Name, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.CanonicalName);
        Assert.IsFalse(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.IsCOMDATFolded);
        Assert.AreEqual($"Block of code in AnimatedAcceptVisualSource_AnimatedVisual::ContainerVisual_16()", AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.Name);
        Assert.AreEqual("AnimatedAcceptVisualSource_AnimatedVisual::ContainerVisual_16()", AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.ParentFunction.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual(0xD63C6u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.RVA);
        Assert.AreEqual(0xD63D0u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.RVAEnd);
        Assert.AreEqual(11u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.Size);
        Assert.AreEqual(SymbolComparisonClass.SeparatedCodeBlock, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.SymbolComparisonClass);
        Assert.AreEqual(11u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.VirtualSize);

        var AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_FoldedColdBlocks = await MUXSession.EnumerateAllSymbolsFoldedAtRVA(0xD63C6, this.CancellationToken);
        Assert.AreEqual(4, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_FoldedColdBlocks.Count);
        var AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock = AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_FoldedColdBlocks.Single(s => s.Name.StartsWith("Block of code in AnimatedAcceptVisualSource_AnimatedVisual::ContainerVisual_19", StringComparison.Ordinal)) as SeparatedCodeBlockSymbol;
        Assert.IsTrue(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock!.CanBeFolded);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.Name, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.CanonicalName); // Note the canonical name should come from the block in 16 (the symbol we attributed the bytes to)
        Assert.IsTrue(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.IsCOMDATFolded);
        Assert.AreEqual($"Block of code in AnimatedAcceptVisualSource_AnimatedVisual::ContainerVisual_19()", AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.Name);
        Assert.AreEqual("AnimatedAcceptVisualSource_AnimatedVisual::ContainerVisual_19()", AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.ParentFunction.FormattedName.UniqueSignatureWithNoPrefixes);
        Assert.AreEqual(0xD63C6u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.RVA);
        Assert.AreEqual(0xD63C6u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.RVAEnd);
        Assert.AreEqual(0u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.Size);
        Assert.AreEqual(SymbolComparisonClass.SeparatedCodeBlock, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.SymbolComparisonClass);
        Assert.AreEqual(0u, AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_19_SeparatedBlock.VirtualSize);

        // Now for the xdata verifications

        // We begin with the unwind - the primary block has a regular [unwind], the separated block generates a [chain-unwind] that points to that primary unwind.
        var unwind = await MUXSession.LoadSymbolByRVA(0x4D2168) as UnwindInfoSymbol; // this is for the primary block
        Assert.AreEqual(28u, unwind!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.RVA, unwind.TargetStartRVA);
        Assert.AreEqual($"[unwind] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.Name}", unwind.Name);

        var chainUnwind = await MUXSession.LoadSymbolByRVA(0x4D21BC) as ChainUnwindInfoSymbol; // This chains to use the same unwind info as we find at 0x4D2168, which is how the segmented block chains to the primary
        Assert.AreEqual(16u, chainUnwind!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.RVA, chainUnwind.TargetStartRVA);
        Assert.AreEqual($"[chain-unwind] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.Name}", chainUnwind.Name);

        // Then we can find the cppxdata, which there's just one because of the chain-unwind basically 'sharing' it between the blocks.
        var cppxdata = await MUXSession.LoadSymbolByRVA(0x4D2184) as CppXdataSymbol;
        Assert.AreEqual(9u, cppxdata!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.RVA, cppxdata.TargetStartRVA);
        Assert.AreEqual($"[cppxdata] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.Name}", cppxdata.Name);

        // Then we can move on to the SeparatedIpToStateMap which in turn helps us discover the two IpToStateMaps that should exist (one for primary block, one for separated)
        var segIp2State = await MUXSession.LoadSymbolByRVA(0x4D2199) as SeparatedIpToStateMapSymbol; // This maps each segmented block to an Ip2StateMap, we could store this data in the SeparatedIpToStateMapSymbol.  It only exists for the primary block.
        Assert.AreEqual(17u, segIp2State!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.RVA, segIp2State.TargetStartRVA);
        Assert.AreEqual($"[seg2ip2state] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.Name}", segIp2State.Name);
        Assert.AreEqual(2, segIp2State!.Entries.Length);
        Assert.AreEqual(0x4D21AAu, segIp2State.Entries.Single(entry => entry.RVAOfBlock == AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.RVA).RVAOfIpToStateMap);
        Assert.AreEqual(0x4D21B1u, segIp2State.Entries.Single(entry => entry.RVAOfBlock == AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.RVA).RVAOfIpToStateMap);

        var primaryIp2StateMap = await MUXSession.LoadSymbolByRVA(0x4D21AA) as IpToStateMapSymbol;
        Assert.AreEqual(7u, primaryIp2StateMap!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.RVA, primaryIp2StateMap.TargetStartRVA);
        Assert.AreEqual($"[ip2state] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.Name}", primaryIp2StateMap.Name);

        var separatedIp2StateMap = await MUXSession.LoadSymbolByRVA(0x4D21B1) as IpToStateMapSymbol;
        Assert.AreEqual(3u, separatedIp2StateMap!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.RVA, separatedIp2StateMap.TargetStartRVA);
        Assert.AreEqual($"[ip2state] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_SeparatedBlock.Name}", separatedIp2StateMap.Name);

        // And last we have a state unwind map, just one of these because the different IptoStateMap symbols help the blocks figure out which state(s) they need as they unwind
        var stateUnwindMap = await MUXSession.LoadSymbolByRVA(0x4D218D) as StateUnwindMapSymbol;
        Assert.AreEqual(12u, stateUnwindMap!.Size);
        Assert.AreEqual(AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.RVA, stateUnwindMap.TargetStartRVA);
        Assert.AreEqual($"[stateUnwindMap] {AnimatedAcceptVisualSource_AnimatedVisual_ContainerVisual_16_PrimaryBlock.Name}", stateUnwindMap.Name);
    }

    [TestMethod]
    public async Task LoadSymbolByRVAAlwaysReturnsTheNonCOMDATFoldedSymbol()
    {
        // This RVA has many symbols folded at it, and the one that DIA would find with findSymbolByRVAEx is not the one
        // we choose to attribute the bytes to (because we do the name canonicalization process and ensure we attribute
        // the bytes to the one with the earliest alphabetical name to help with diffing stability).  This test would
        // fail if we naively ask DIA for the symbol at this RVA and return it - instead we want to get the other SymIndexID
        // that has the earliest name.

        var symbol = await MUXSession!.LoadSymbolByRVA(0x432AF0) as Symbol;

        Assert.IsNotNull(symbol);
        Assert.IsFalse(symbol.IsCOMDATFolded);
        Assert.AreEqual(0x432AF0u, symbol.RVA);
        Assert.AreEqual("EnumXamlType::`scalar deleting destructor'(unsigned int)", symbol.Name);
        Assert.AreEqual(symbol.Name, symbol.CanonicalName);
    }

    [TestMethod]
    public async Task VTableLengthsAreRightIncludingWhenCOMDATFolded()
    {
        // VTable lengths are super frustrating to get - the vtable SymTagData symbol does not have a length, so we have
        // to go hunting.  If we get it from the data symbol's "sym.type.length" then that is *also* wrong sometimes - it's
        // right sometimes too, but when it's wrong it's usually off by 1 due to __vecDelDtor.  So we really ought to look
        // at the PublicSymbol's length first as that is the one that seems to be right most often.

        // The type "type_info" in this binary has a vtable with exactly 1 slot in it (the scalar deleting destructor, because the
        // ~type_info is declared virtual).  The SymTagData symbol's "symbol.type.length" for some reason believes it is 16 bytes long, so
        // it mis-reports the length in the binary.  This test ensures that we find the correct size of 8 bytes (2 RVAs).

        var type_info_vftable = await MUXSession!.LoadSymbolByRVA(0x45C038);

        Assert.IsNotNull(type_info_vftable);
        Assert.IsFalse(type_info_vftable.IsCOMDATFolded);
        Assert.AreEqual(8u, type_info_vftable.Size);

        var type_info_slot0 = await MUXSession!.LoadSymbolForVTableSlotAsync(0x45C038, 0);

        Assert.IsNotNull(type_info_slot0);
        Assert.AreEqual("type_info::`scalar deleting destructor'(unsigned int)", type_info_slot0.Name);


        // Now we'll go find a vtable that is COMDAT folded, to confirm we can look the lengths of those up correctly and attribute the bytes to the right one.
        var primaryVTable = await MUXSession.LoadSymbolByRVA(0x48F8E0) as Symbol;

        Assert.IsNotNull(primaryVTable);
        Assert.IsFalse(primaryVTable.IsCOMDATFolded);
        Assert.AreEqual("ObservableVectorInnerImpl<VectorOptions<winrt::Microsoft::UI::Xaml::Controls::SwipeItem,0,0,1,0>,TStorageWrapperImpl<winrt::Microsoft::UI::Xaml::Controls::SwipeItem,0> >::`vftable'", primaryVTable.Name);
        Assert.AreEqual(primaryVTable.Name, primaryVTable.CanonicalName);
        Assert.AreEqual(8u, primaryVTable.Size);

        var foldedVTables = await MUXSession.EnumerateAllSymbolsFoldedAtRVA(0x48F8E0, this.CancellationToken);
        Assert.AreEqual(4, foldedVTables.Count);

        Assert.IsTrue(foldedVTables.Contains(primaryVTable));

        var folded = foldedVTables.OfType<Symbol>().Single(s => s.Name == "ObservableVectorInnerImpl<VectorOptions<winrt::Windows::UI::Xaml::UIElement,0,0,1,0>,TStorageWrapperImpl<winrt::Windows::UI::Xaml::UIElement,0> >::`vftable'");
        Assert.IsTrue(folded.IsCOMDATFolded);
        Assert.AreEqual(primaryVTable.Name, folded.CanonicalName);
        Assert.AreEqual(0u, folded.Size);

        folded = foldedVTables.OfType<Symbol>().Single(s => s.Name == "ObservableVectorInnerImpl<VectorOptions<winrt::Windows::Foundation::IInspectable,0,0,1,0>,TStorageWrapperImpl<winrt::Windows::Foundation::IInspectable,0> >::`vftable'");
        Assert.IsTrue(folded.IsCOMDATFolded);
        Assert.AreEqual(primaryVTable.Name, folded.CanonicalName);
        Assert.AreEqual(0u, folded.Size);

        folded = foldedVTables.OfType<Symbol>().Single(s => s.Name == "ObservableVectorInnerImpl<VectorOptions<winrt::hstring,0,0,0,0>,TStorageWrapperImpl<winrt::hstring,0> >::`vftable'");
        Assert.IsTrue(folded.IsCOMDATFolded);
        Assert.AreEqual(primaryVTable.Name, folded.CanonicalName);
        Assert.AreEqual(0u, folded.Size);

        // And lastly, let's make sure we calculated the name right when we're encountering a "`vftable'{for ...}" symbol.  These are tricky because the SymTagData from DIA doesn't have
        // the full name, so we have to use public symbols to get the full name.
        var disambiguatedFoldedVTables = await MUXSession.EnumerateAllSymbolsFoldedAtRVA(0x44BBB0, this.CancellationToken);

        var disambiguatedPrimary = await MUXSession.LoadSymbolByRVA(0x44BBB0) as Symbol;
        Assert.IsFalse(disambiguatedPrimary!.IsCOMDATFolded);
        Assert.AreEqual(disambiguatedPrimary.Name, disambiguatedPrimary.CanonicalName);
        Assert.AreEqual(24u, disambiguatedPrimary.Size);

        var disambiguatedButFolded = disambiguatedFoldedVTables.OfType<Symbol>().Single(s => s.Name == "SelectedTreeNodeVector::`vftable'{for `winrt::impl::producers_base<class SelectedTreeNodeVector,class std::tuple<struct winrt::Windows::Foundation::Collections::IVector<struct winrt::Microsoft::UI::Xaml::Controls::TreeViewNode>,struct winrt::Windows::Foundation::Collections::IIterable<struct winrt::Microsoft::UI::Xaml::Controls::TreeViewNode>,struct winrt::Windows::Foundation::Collections::IObservableVector<struct winrt::Microsoft::UI::Xaml::Controls::TreeViewNode>,struct IReferenceTrackerExtension> >'}");
        Assert.IsTrue(disambiguatedButFolded.IsCOMDATFolded);
        Assert.AreEqual(disambiguatedPrimary.Name, disambiguatedButFolded.CanonicalName);
        Assert.AreEqual(0u, disambiguatedButFolded.Size);
    }

    [TestMethod]
    public async Task AllRDataSymbolsCanBeEnumerated()
    {
        // During some rigorous testing, I found that the .rdata section of this binary was a good test case for all sorts of potential pitfalls, so let's just make
        // sure we can enumerate all the symbols in .rdata - especially ensuring that the sanity check in EnumerateSymbolsInRVARangeSessionTask passes here.
        var sections = await MUXSession!.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
        var rdataSymbols = await MUXSession.EnumerateSymbolsInBinarySection(sections.Single(s => s.Name == ".rdata"), this.CancellationToken);

        Assert.IsTrue(rdataSymbols.Count > 0);
    }

    [TestMethod]
    public async Task ImportSymbolsCanBeEnumerated()
    {
        // Check a descriptor for an apiset, in case they're weird
        var sections = await MUXSession!.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
        var rdataSection = sections.Single(s => s.Name == ".rdata");
        var idata2CG = rdataSection.COFFGroups.Single(s => s.Name == ".idata$2");

        var symbols = await MUXSession.EnumerateSymbolsInCOFFGroup(idata2CG, this.CancellationToken);

        var apisetProcessThreadsDescriptor = symbols.Single(s => s.Name == "[import descriptor] api-ms-win-core-processthreads-l1-1-0.dll");
        Assert.AreEqual(20u, apisetProcessThreadsDescriptor.Size);
        Assert.AreEqual(20u, apisetProcessThreadsDescriptor.VirtualSize);
        Assert.AreEqual(SymbolComparisonClass.ImportDescriptor, apisetProcessThreadsDescriptor.SymbolComparisonClass);

        var placement = await MUXSession.LookupSymbolPlacementInBinary(apisetProcessThreadsDescriptor, this.CancellationToken);
        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$2", placement.COFFGroup!.Name);
        Assert.AreEqual("api-ms-win-core-processthreads-l1-1-0.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("mincore", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);

        // And a descriptor for a regular import
        var oleaut32Descriptor = symbols.Single(s => s.Name == "[import descriptor] OLEAUT32.dll");
        Assert.AreEqual(20u, oleaut32Descriptor.Size);
        Assert.AreEqual(20u, oleaut32Descriptor.VirtualSize);
        Assert.AreEqual(SymbolComparisonClass.ImportDescriptor, oleaut32Descriptor.SymbolComparisonClass);

        placement = await MUXSession.LookupSymbolPlacementInBinary(oleaut32Descriptor, this.CancellationToken);
        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$2", placement.COFFGroup!.Name);
        Assert.AreEqual("OLEAUT32.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("mincore", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);


        // Now try some thunks - first a thunk imported by name
        var idata4CG = rdataSection.COFFGroups.Single(s => s.Name == ".idata$4");
        symbols = await MUXSession.EnumerateSymbolsInCOFFGroup(idata4CG, this.CancellationToken);

        var GetCurrentThreadIdThunk = (ImportThunkSymbol)symbols.Single(s => s.Name == "[import thunk] api-ms-win-core-processthreads-l1-1-0.dll GetCurrentThreadId, ordinal 17");
        Assert.AreEqual(8u, GetCurrentThreadIdThunk.Size);
        Assert.AreEqual(8u, GetCurrentThreadIdThunk.VirtualSize);
        Assert.IsFalse(GetCurrentThreadIdThunk.IsCOMDATFolded);
        Assert.AreEqual(SymbolComparisonClass.ImportThunk, GetCurrentThreadIdThunk.SymbolComparisonClass);

        placement = await MUXSession.LookupSymbolPlacementInBinary(GetCurrentThreadIdThunk, this.CancellationToken);
        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$4", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:api-ms-win-core-processthreads-l1-1-0.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("mincore", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);

        // And now a thunk imported only by ordinal
        var oleaut32Ordinal26Thunk = (ImportThunkSymbol)symbols.Single(s => s.Name == "[import thunk] OLEAUT32.dll Ordinal 26");
        Assert.AreEqual(8u, oleaut32Ordinal26Thunk.Size);
        Assert.AreEqual(8u, oleaut32Ordinal26Thunk.VirtualSize);
        Assert.IsFalse(oleaut32Ordinal26Thunk.IsCOMDATFolded);
        Assert.AreEqual(SymbolComparisonClass.ImportThunk, oleaut32Ordinal26Thunk.SymbolComparisonClass);

        placement = await MUXSession.LookupSymbolPlacementInBinary(oleaut32Ordinal26Thunk, this.CancellationToken);
        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$4", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:OLEAUT32.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("mincore", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);


        // And now some IMAGE_IMPORT_BY_NAMEs
        var idata6CG = rdataSection.COFFGroups.Single(s => s.Name == ".idata$6");
        symbols = await MUXSession.EnumerateSymbolsInCOFFGroup(idata6CG, this.CancellationToken);

        var GetCurrentThreadIdString = (ImportByNameSymbol)symbols.Single(s => s.Name == "`string': \"GetCurrentThreadId\"");
        Assert.AreEqual((uint)("GetCurrentThreadId".Length + 3), GetCurrentThreadIdString.Size);
        Assert.AreEqual((uint)("GetCurrentThreadId".Length + 3), GetCurrentThreadIdString.VirtualSize);
        Assert.IsFalse(GetCurrentThreadIdString.IsCOMDATFolded);
        Assert.AreEqual(SymbolComparisonClass.ImportByName, GetCurrentThreadIdString.SymbolComparisonClass);

        placement = await MUXSession.LookupSymbolPlacementInBinary(GetCurrentThreadIdString, this.CancellationToken);
        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$6", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:api-ms-win-core-processthreads-l1-1-0.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("mincore", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);
    }

    public static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests => SymbolSourcesSupportedCommonTests.DynamicDataSourceForSymbolSourcesSupportedTests;

    [TestMethod]
    [DynamicData(nameof(DynamicDataSourceForSymbolSourcesSupportedTests))]
    public Task SymbolSourcesSupportedWorks(SymbolSourcesSupported symbolSources) =>
        SymbolSourcesSupportedCommonTests.VerifyNoUnexpectedSymbolTypesCanBeMaterialized(
            Path.Combine(this.TestContext!.DeploymentDirectory!, "Microsoft.UI.Xaml.dll"),
            Path.Combine(this.TestContext!.DeploymentDirectory!, "Microsoft.UI.Xaml.pdb"),
            symbolSources,
            this.TestContext.CancellationTokenSource.Token);
}
