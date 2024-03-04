using System.Reflection.PortableExecutable;
using Dia2Lib;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Navigation.Tests;

[TestClass]
public sealed class SingleBinaryModelToUriConverterTests : IDisposable
{
    SessionDataCache SessionDataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize() => this.SessionDataCache = new SessionDataCache()
    {
        AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
    };

    [TestMethod]
    public void NavigatingToSessionWorks()
        => Assert.AreEqual(new Uri(@"SingleBinaryOverview", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new Mock<ISession>().Object));

    [TestMethod]
    public void CanNavigateToBinarySection()
    {
        var firstSection = new BinarySection(this.SessionDataCache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var secondSection = new BinarySection(this.SessionDataCache, ".rdata", size: 0, virtualSize: 200, rva: 200, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead);

        Assert.AreEqual(new Uri(@"BinarySection/.text", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(firstSection));
        Assert.AreEqual(new Uri(@"BinarySection/.rdata", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(secondSection));
    }

    [TestMethod]
    public void CanNavigateToCOFFGroup()
    {
        var coffGroup = new COFFGroup(this.SessionDataCache, ".text$zz", size: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);

        Assert.AreEqual(new Uri($@"COFFGroup/.text$zz", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(coffGroup));
    }

    [TestMethod]
    public void CanNavigateToCompiland()
    {
        var lib = new Library("1.lib");
        var compiland = new Compiland(this.SessionDataCache, "1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 0);
        compiland.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Compiland/{Uri.EscapeDataString(compiland.Name)}?Lib={Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(compiland));
    }

    [TestMethod]
    public void CanNavigateToLib()
    {
        var lib = new Library("1.lib");
        lib.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Lib/{Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(lib));
    }

    [TestMethod]
    public void CanNavigateToSourceFile()
    {
        var sourceFile = new SourceFile(this.SessionDataCache, @"c:\foo\bar\baz.h", fileId: 1, compilands: new List<Compiland>());
        sourceFile.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"SourceFile/{Uri.EscapeDataString(sourceFile.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(sourceFile));
    }

    [TestMethod]
    public void CanNavigateToSimpleFunctionSymbol()
    {
        var function = new SimpleFunctionCodeSymbol(this.SessionDataCache, "test function", rva: 9216, size: 100, symIndexId: 0);

        Assert.AreEqual(new Uri($@"Symbols/FunctionSymbol?FunctionRVA={function.RVA}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(function));
    }

    [TestMethod]
    public void CanNavigateToComplexFunctionSymbol()
    {
        var primaryBlock = new PrimaryCodeBlockSymbol(this.SessionDataCache, rva: 9216, size: 100, symIndexId: 0);
        var separatedBlocks = new List<SeparatedCodeBlockSymbol>()
            {
                new SeparatedCodeBlockSymbol(this.SessionDataCache, rva: 1234, size: 30, symIndexId: 1, parentFunctionSymIndexId: 0),
                new SeparatedCodeBlockSymbol(this.SessionDataCache, rva: 2345, size: 20, symIndexId: 2, parentFunctionSymIndexId: 0),
            };
        var function = new ComplexFunctionCodeSymbol(this.SessionDataCache, "test function", primaryBlock, separatedBlocks);

        Assert.AreEqual(new Uri($@"Symbols/FunctionSymbol?FunctionRVA={primaryBlock.RVA}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(function));
    }

    [TestMethod]
    public void NavigatingToFunctionWithZeroRVAPassesAlongNameToShowHelpfulMessageAboutDeadCodeRemoval()
    {
        var intType = new BasicTypeSymbol(this.SessionDataCache, "int", 4, symIndexId: 0);
        var function = new SimpleFunctionCodeSymbol(this.SessionDataCache, "SomeNamespace::MyType::DeadFunction<AComplex::Type<float>,bool>", rva: 0, size: 100, symIndexId: 1,
                                                    functionType: new FunctionTypeSymbol(this.SessionDataCache, "()(int)", 0, symIndexId: 2, isConst: true, isVolatile: false, argumentTypes: [intType], returnValueType: intType));

        Assert.AreEqual(new Uri($@"Symbols/FunctionSymbol?FunctionRVA={function.RVA}&Name=int%20SomeNamespace%3A%3AMyType%3A%3ADeadFunction%3CAComplex%3A%3AType%3Cfloat%3E%2Cbool%3E%28int%29%20const", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(function));
    }

    [TestMethod]
    public void CanNavigateToCodeBlockSymbol()
    {
        var separatedBlocks = new List<SeparatedCodeBlockSymbol>()
            {
                new SeparatedCodeBlockSymbol(this.SessionDataCache, rva: 1234, size: 30, symIndexId: 1, parentFunctionSymIndexId: 0),
                new SeparatedCodeBlockSymbol(this.SessionDataCache, rva: 2345, size: 20, symIndexId: 2, parentFunctionSymIndexId: 0),
            };

        Assert.AreEqual(new Uri($@"Symbols/BlockSymbol?RVA={separatedBlocks[0].RVA}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(separatedBlocks[0]));
        Assert.AreEqual(new Uri($@"Symbols/BlockSymbol?RVA={separatedBlocks[1].RVA}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(separatedBlocks[1]));
    }

    [TestMethod]
    public void CanNavigateToSymbol()
    {
        var sym = new PublicSymbol(this.SessionDataCache, "test symbol", rva: 9216, size: 100, isVirtualSize: false, symIndexId: 0, targetRva: 0);

        Assert.AreEqual(new Uri($@"Symbols/Symbol?RVA={sym.RVA}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(sym));
    }

    [TestMethod]
    public void NavigatingToSymbolWithZeroRVAPassesAlongNameToShowHelpfulMessage()
    {
        var sym = new PublicSymbol(this.SessionDataCache, "test symbol", rva: 0, size: 100, isVirtualSize: false, symIndexId: 0, targetRva: 0);

        Assert.AreEqual(new Uri($@"Symbols/Symbol?RVA={sym.RVA}&Name=test%20symbol", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(sym));
    }

    [TestMethod]
    public void CanNavigateToCompilandSectionContribution()
    {
        var lib = new Library("test lib name");
        var compiland = new Compiland(this.SessionDataCache, "1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 0);
        var textSection = new BinarySection(this.SessionDataCache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var contribution = compiland.GetOrCreateSectionContribution(textSection);
        compiland.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Contribution?BinarySection=.text&Compiland={compiland.Name}&Lib={Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void CanNavigateToCompilandCOFFGroupContribution()
    {
        var lib = new Library("test lib name");
        var compiland = new Compiland(this.SessionDataCache, "1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 0);
        var textMnCG = new COFFGroup(this.SessionDataCache, ".text$mn", size: 0, rva: 100, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var contribution = compiland.GetOrCreateCOFFGroupContribution(textMnCG);
        compiland.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Contribution?COFFGroup=.text$mn&Compiland={compiland.Name}&Lib={Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void CanNavigateToLibSectionContribution()
    {
        var lib = new Library(@"c:\data\foo.lib");
        var textSection = new BinarySection(this.SessionDataCache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var contribution = lib.GetOrCreateSectionContribution(textSection);

        Assert.AreEqual(new Uri($@"Contribution?BinarySection=.text&Lib={Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void CanNavigateToLibCOFFGroupContribution()
    {
        var lib = new Library(@"c:\data\foo.lib");
        var textMnCG = new COFFGroup(this.SessionDataCache, ".text$mn", size: 0, rva: 100, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var contribution = lib.GetOrCreateCOFFGroupContribution(textMnCG);

        Assert.AreEqual(new Uri($@"Contribution?COFFGroup=.text$mn&Lib={Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void CanNavigateToSourceFileSectionContribution()
    {
        var sourceFile = new SourceFile(this.SessionDataCache, @"c:\foo\bar\baz.h", fileId: 1, compilands: new List<Compiland>());
        var textSection = new BinarySection(this.SessionDataCache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var contribution = sourceFile.GetOrCreateSectionContribution(textSection);
        sourceFile.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Contribution?BinarySection=.text&SourceFile={Uri.EscapeDataString(sourceFile.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void CanNavigateToSourceFileCOFFGroupContribution()
    {
        var sourceFile = new SourceFile(this.SessionDataCache, @"c:\foo\bar\baz.h", fileId: 1, compilands: new List<Compiland>());
        var textMnCG = new COFFGroup(this.SessionDataCache, ".text$mn", size: 0, rva: 100, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var contribution = sourceFile.GetOrCreateCOFFGroupContribution(textMnCG);
        sourceFile.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Contribution?COFFGroup=.text$mn&SourceFile={Uri.EscapeDataString(sourceFile.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void CanNavigateToSourceFileCompilandContribution()
    {
        var lib = new Library("test lib name");
        var compiland = new Compiland(this.SessionDataCache, "1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 0);
        var sourceFile = new SourceFile(this.SessionDataCache, @"c:\foo\bar\baz.h", fileId: 1, compilands: new List<Compiland>() { compiland });
        var contribution = sourceFile.GetOrCreateCompilandContribution(compiland);
        sourceFile.MarkFullyConstructed();

        Assert.AreEqual(new Uri($@"Contribution?Compiland={Uri.EscapeDataString(compiland.Name)}&SourceFile={Uri.EscapeDataString(sourceFile.Name)}&Lib={Uri.EscapeDataString(lib.Name)}", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(contribution));
    }

    [TestMethod]
    public void NavigatingToUriPassesThroughToCurrentPage()
    {
        var expectedUri = new Uri(@"A\B\C.xaml?id=123", UriKind.Relative);

        Assert.AreEqual(expectedUri, SingleBinaryModelToUriConverter.ModelToUri(expectedUri));
    }

    [TestMethod]
    public void CanNavigateToListOfBinarySections()
        => Assert.AreEqual(new Uri($@"AllBinarySections", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new List<BinarySection>()));

    [TestMethod]
    public void CanNavigateToListOfLibs()
        => Assert.AreEqual(new Uri($@"AllLibs", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new List<Library>()));

    [TestMethod]
    public void CanNavigateToDuplicateDataItem()
    {
        var lib = new Library("1.lib");
        var compiland = new Compiland(this.SessionDataCache, "1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 0);

        var data = new StaticDataSymbol(this.SessionDataCache, "dataName", rva: 6725,
                                        size: 0, isVirtualSize: false, symIndexId: 1,
                                        dataKind: DataKind.DataIsFileStatic,
                                        type: null, referencedIn: null, functionParent: null);

        Assert.AreEqual(new Uri($@"DuplicateData?DuplicateRVA=6725", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new DuplicateDataItem(data, compiland)));
    }

    [TestMethod]
    public void CanNavigateToWastefulVirtualItem()
    {
        var udt = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter: new TestDIAAdapter(), session: new Mock<ISession>().Object, name: "MyNS::MyUDT", instanceSize: 24, symIndexId: 3, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: new Dictionary<uint, uint>());

        Assert.AreEqual(new Uri($@"WastefulVirtual?TypeName=MyNS%3A%3AMyUDT", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new WastefulVirtualItem(udt, isCOMType: false, bytesPerWord: 8)));
    }

    [TestMethod]
    public void CanNavigateToTemplateFoldabilityItem()
    {
        var tfi = new TemplateFoldabilityItem("SomeNamespace::MyType::FoldableFunction<AComplex::Type<float>,bool>", new List<IFunctionCodeSymbol>(), new List<IFunctionCodeSymbol>(), 200, 0.8f);

        Assert.AreEqual(new Uri($@"TemplateFoldabilityItem?TemplateName=SomeNamespace%3A%3AMyType%3A%3AFoldableFunction%3CAComplex%3A%3AType%3Cfloat%3E%2Cbool%3E", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(tfi));
    }

    [TestMethod]
    public void CanNavigateToUDT()
    {
        var udt = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter: new TestDIAAdapter(), session: new Mock<ISession>().Object, name: "MyNS::MyUDT", instanceSize: 24, symIndexId: 3, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: new Dictionary<uint, uint>());

        Assert.AreEqual(new Uri($@"Symbols/UserDefinedTypeSymbol?Name=MyNS%3A%3AMyUDT", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(udt));
    }

    [TestMethod]
    public void CanNavigateToTemplatedUDT()
    {
        var udt1 = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter: new TestDIAAdapter(), session: new Mock<ISession>().Object, name: "MyNS::MyUDT<float>", instanceSize: 24, symIndexId: 3, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: new Dictionary<uint, uint>());
        var udt2 = new UserDefinedTypeSymbol(this.SessionDataCache, diaAdapter: new TestDIAAdapter(), session: new Mock<ISession>().Object, name: "MyNS::MyUDT<int>", instanceSize: 24, symIndexId: 4, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: new Dictionary<uint, uint>());
        var templatedUDT = new TemplatedUserDefinedTypeSymbol("MyNS::MyUDT<T1>", new List<UserDefinedTypeSymbol>() { udt1, udt2 });

        Assert.AreEqual(new Uri($@"Symbols/TemplatedUserDefinedTypeSymbol?TemplateName=MyNS%3A%3AMyUDT%3CT1%3E", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(templatedUDT));
    }

    [TestMethod]
    public void CanNavigateToCOMDATFoldedSymbol()
    {
        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(symIndexId: 0, SymTagEnum.SymTagData, name: "test symbol");
        nameCanonicalization.AddName(symIndexId: 1, SymTagEnum.SymTagData, name: "canonicalName");
        nameCanonicalization.Canonicalize();
        this.SessionDataCache.AllCanonicalNames!.Add(9216, nameCanonicalization);

        var sym = new StaticDataSymbol(this.SessionDataCache, "test symbol", rva: 9216, size: 100, isVirtualSize: false, symIndexId: 0, DataKind.DataIsFileStatic, type: null, referencedIn: null, functionParent: null);
        var canonicalSym = new StaticDataSymbol(this.SessionDataCache, "canonicalName", rva: 9216, size: 100, isVirtualSize: false, symIndexId: 1, DataKind.DataIsFileStatic, type: null, referencedIn: null, functionParent: null);

        Assert.IsTrue(sym.IsCOMDATFolded);
        Assert.IsFalse(canonicalSym.IsCOMDATFolded);

        Assert.AreEqual(new Uri($@"Symbols/COMDATFoldedSymbol?RVA={sym.RVA}&Name=test%20symbol", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(sym));
    }

    [TestMethod]
    public void CanNavigateToCOMDATFoldedFunction()
    {
        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(symIndexId: 0, SymTagEnum.SymTagFunction, name: "test function");
        nameCanonicalization.AddName(symIndexId: 1, SymTagEnum.SymTagFunction, name: "canonicalFunc");
        nameCanonicalization.Canonicalize();
        this.SessionDataCache.AllCanonicalNames!.Add(9216, nameCanonicalization);

        var function = new SimpleFunctionCodeSymbol(this.SessionDataCache, "test function", rva: 9216, size: 100, symIndexId: 0);
        var canonicalFunction = new SimpleFunctionCodeSymbol(this.SessionDataCache, "canonicalFunc", rva: 9216, size: 100, symIndexId: 1);

        Assert.IsTrue(function.IsCOMDATFolded);
        Assert.IsFalse(canonicalFunction.IsCOMDATFolded);

        Assert.AreEqual(new Uri($@"Symbols/COMDATFoldedSymbol?RVA={function.RVA}&Name=test%20function%28%29", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(function));
    }

    public class TestContribution : Contribution
    {
        public TestContribution() : base("test contribution")
        { }
    }

    [ExpectedException(typeof(InvalidOperationException), AllowDerivedTypes = false)]
    [TestMethod]
    public void NavigatingToContributionThrows()
        => SingleBinaryModelToUriConverter.ModelToUri(new TestContribution());

    [TestMethod]
    public void NavigatingToOtherStuffReturnsError()
    {
        Assert.AreEqual(new Uri("Error/System.Object", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new object()));
        Assert.AreEqual(new Uri("Error/SizeBench.AnalysisEngine.RVARange", UriKind.Relative), SingleBinaryModelToUriConverter.ModelToUri(new RVARange(0, 0)));
    }

    public void Dispose() => this.SessionDataCache.Dispose();
}
