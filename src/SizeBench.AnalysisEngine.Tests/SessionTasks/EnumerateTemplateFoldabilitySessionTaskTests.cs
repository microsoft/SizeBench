using SizeBench.AnalysisEngine.SessionTasks;
using SizeBench.AnalysisEngine.SessionTasks.Tests;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.Tests.SessionTasks;

[TestClass]
public sealed class EnumerateTemplateFoldabilitySessionTaskTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();

        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);
    }

    [TestMethod]
    public void CanCancelInTheMiddleOfEnumeratingSymbols()
    {
        using var cts = new CancellationTokenSource();
        SetupFunctionsWithSomeFoldable(cancelInTheMiddle: true, cts: cts);

        var task = new EnumerateTemplateFoldabilitySessionTask(this.SessionTaskParameters!,
            null /*progressReporter*/,
            cts.Token);

        List<TemplateFoldabilityItem>? foldables = null;
        OperationCanceledException? exceptionCaught = null;

        try
        {
            using var logger = new NoOpLogger();
            task.Execute(logger);
        }
        catch (OperationCanceledException ex)
        {
            exceptionCaught = ex;
        }

        Assert.IsNull(foldables);
        Assert.IsNotNull(exceptionCaught);
    }

    [TestMethod]
    public void FoldableFunctionsAreFoundCorrectly()
    {
        using var cts = new CancellationTokenSource();
        SetupFunctionsWithSomeFoldable(cancelInTheMiddle: false, cts: cts);

        var task = new EnumerateTemplateFoldabilitySessionTask(this.SessionTaskParameters!,
            null /*progressReporter*/,
            cts.Token);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        using var logger = new NoOpLogger();
        var foldables = task.Execute(logger);

        Assert.AreEqual(3, foldables.Count);

        var myTypeFoldableFunction = foldables.Single(tfi => tfi.TemplateName == "SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)");
        var foldableWithDuplicateType = foldables.Single(tfi => tfi.TemplateName == "FoldableWithDuplicateType<T1,T2,T1>(T1) const");
        var foldableVolatile = foldables.Single(tfi => tfi.TemplateName == "FoldableVolatile<T1>(T1*) volatile");

        Assert.AreEqual(3, myTypeFoldableFunction.Symbols.Count);
        Assert.AreEqual(2, myTypeFoldableFunction.UniqueSymbols.Count);
        Assert.AreEqual(3, myTypeFoldableFunction.Symbols.Count);
        Assert.IsTrue(myTypeFoldableFunction.Symbols.Any(s => s.FormattedName.IncludeParentType == "SomeNamespace::MyType::FoldableFunction<AComplex::Type<int>,bool>"));
        Assert.IsTrue(myTypeFoldableFunction.Symbols.Any(s => s.FormattedName.IncludeParentType == "SomeNamespace::MyType::FoldableFunction<AComplex::Type<float>,bool>"));
        Assert.IsTrue(myTypeFoldableFunction.Symbols.Any(s => s.FormattedName.IncludeParentType == "SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool>"));
        Assert.AreEqual(20u, myTypeFoldableFunction.TotalSize);
        Assert.AreEqual(0.8f, myTypeFoldableFunction.PercentageSimilarity);
        Assert.AreEqual((uint)(20 * 0.8f), myTypeFoldableFunction.WastedSize);

        Assert.AreEqual(3, foldableWithDuplicateType.Symbols.Count);
        Assert.AreEqual(3, foldableWithDuplicateType.UniqueSymbols.Count);
        Assert.AreEqual(3, foldableWithDuplicateType.Symbols.Count);
        Assert.IsTrue(foldableWithDuplicateType.Symbols.Any(s => s.FormattedName.IncludeParentType == "FoldableWithDuplicateType<int,bool,int>"));
        Assert.IsTrue(foldableWithDuplicateType.Symbols.Any(s => s.FormattedName.IncludeParentType == "FoldableWithDuplicateType<int*,bool,int*>"));
        Assert.IsTrue(foldableWithDuplicateType.Symbols.Any(s => s.FormattedName.IncludeParentType == "FoldableWithDuplicateType<AComplex::Type<SomeUDT>,bool,AComplex::Type<SomeUDT>>"));
        Assert.AreEqual(30u, foldableWithDuplicateType.TotalSize);
        Assert.AreEqual(0.5f /* avg. of 60% and 40% */, foldableWithDuplicateType.PercentageSimilarity);
        Assert.AreEqual((uint)(30 * 0.5f), foldableWithDuplicateType.WastedSize);

        Assert.AreEqual(3, foldableVolatile.Symbols.Count);
        Assert.AreEqual(2, foldableVolatile.UniqueSymbols.Count);
        Assert.AreEqual(3, foldableVolatile.Symbols.Count);
        Assert.IsTrue(foldableVolatile.Symbols.Any(s => s.FormattedName.IncludeParentType == "FoldableVolatile<int>"));
        Assert.IsTrue(foldableVolatile.Symbols.Any(s => s.FormattedName.IncludeParentType == "FoldableVolatile<bool>"));
        Assert.IsTrue(foldableVolatile.Symbols.Any(s => s.FormattedName.IncludeParentType == "FoldableVolatile<const bool>"));
        Assert.AreEqual(20u, foldableVolatile.TotalSize);
        Assert.AreEqual(0.9f, foldableVolatile.PercentageSimilarity);
        Assert.AreEqual((uint)(20 * 0.9f), foldableVolatile.WastedSize);
    }

    [TestMethod]
    public void CacheIsReusedAfterOneRunWhenThereAreFoldables()
    {
        using var cts = new CancellationTokenSource();
        SetupFunctionsWithSomeFoldable(cancelInTheMiddle: false, cts: cts);

        var task = new EnumerateTemplateFoldabilitySessionTask(this.SessionTaskParameters!,
            null /*progressReporter*/,
            CancellationToken.None);

        Assert.IsNull(this.DataCache.AllTemplateFoldabilityItems);

        using var logger = new NoOpLogger();
        var foldables = task.Execute(logger);

        Assert.IsNotNull(this.DataCache.AllTemplateFoldabilityItems);

        var foldables2 = new EnumerateTemplateFoldabilitySessionTask(this.SessionTaskParameters!,
            null /*progressReporter*/,
            CancellationToken.None).Execute(logger);

        Assert.IsTrue(ReferenceEquals(foldables, foldables2));
        Assert.IsTrue(ReferenceEquals(foldables2, this.DataCache.AllTemplateFoldabilityItems));
    }

    [TestMethod]
    public void CacheIsReusedAfterOneRunWhenThereAreNoFoldables()
    {
        SetupFunctionsWithNoFoldables();

        var task = new EnumerateTemplateFoldabilitySessionTask(this.SessionTaskParameters!,
            null /*progressReporter*/,
            CancellationToken.None);

        Assert.IsNull(this.DataCache.AllTemplateFoldabilityItems);

        using var logger = new NoOpLogger();
        var foldables = task.Execute(logger);

        Assert.IsNotNull(this.DataCache.AllTemplateFoldabilityItems);

        var foldables2 = new EnumerateTemplateFoldabilitySessionTask(this.SessionTaskParameters!,
            null /*progressReporter*/,
            CancellationToken.None).Execute(logger);

        Assert.IsTrue(ReferenceEquals(foldables, foldables2));
        Assert.IsTrue(ReferenceEquals(foldables2, this.DataCache.AllTemplateFoldabilityItems));
    }

    private void SetupFunctionsWithSomeFoldable(bool cancelInTheMiddle, CancellationTokenSource cts)
    {
        uint nextSymIndexId = 1;
        var functions = TestTemplateFoldabilityItems.GenerateSomeFoldableTemplatedFunctions(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, ref nextSymIndexId);

        this.TestDIAAdapter.TemplatedFunctionsToFind = cancelInTheMiddle ? functions.EnumerateListButCancelInTheMiddleOfEnumerating(cts, cancelAfter: 1) : functions;

        // If we're going to cancel, then enumerating the TFIs to set up the similarity data will cancel, and we won't get there anyway so just don't set that up.
        if (!cancelInTheMiddle)
        {
            TestTemplateFoldabilityItems.SetupFoldableSimilarity(this.MockSession, this.TestDIAAdapter);
        }
    }

    private void SetupFunctionsWithNoFoldables()
    {
        uint nextSymIndexId = 1;
        this.TestDIAAdapter.TemplatedFunctionsToFind = new List<IFunctionCodeSymbol>()
            {
                new SimpleFunctionCodeSymbol(this.DataCache, "MyFunction<int>::DoTheThing", rva: 100, size: 10, symIndexId: nextSymIndexId++),
                new SimpleFunctionCodeSymbol(this.DataCache, "AnotherFunction<bool>::DoTheThing", rva: 200, size: 10, symIndexId: nextSymIndexId++)
            };
    }

    public void Dispose() => this.DataCache.Dispose();
}
