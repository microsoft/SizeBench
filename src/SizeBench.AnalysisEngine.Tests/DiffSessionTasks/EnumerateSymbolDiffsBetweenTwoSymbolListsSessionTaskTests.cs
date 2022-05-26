using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.DiffSessionTasks.Tests;

[TestClass]
public sealed class EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTaskTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator();
    private TestDIAAdapter BeforeDIAAdapter = new TestDIAAdapter();
    private TestDIAAdapter AfterDIAAdapter = new TestDIAAdapter();
    private List<Symbol> SymbolsInBefore = new List<Symbol>();
    private List<Symbol> SymbolsInAfter = new List<Symbol>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.BeforeDIAAdapter = new TestDIAAdapter();
        this.AfterDIAAdapter = new TestDIAAdapter();

        this._generator = new DiffTestDataGenerator(beforeDIAAdapter: this.BeforeDIAAdapter, afterDIAAdapter: this.AfterDIAAdapter);
    }

    [TestMethod]
    public async Task CanExecuteWithoutProgressReporting()
    {
        var sectionDiff = this._generator.TextSectionDiff;

        Assert.IsNotNull(sectionDiff.BeforeSection);
        Assert.IsNotNull(sectionDiff.AfterSection);

        this.SymbolsInBefore = this._generator.GenerateSymbolsInBinarySection(sectionDiff.BeforeSection);
        this.SymbolsInAfter = this._generator.GenerateSymbolsInBinarySection(sectionDiff.AfterSection);

        this._generator.MockBeforeSession.Setup(s => s.EnumerateSymbolsInBinarySection(It.IsAny<BinarySection>(), It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(this.SymbolsInBefore.Cast<ISymbol>().ToList() as IReadOnlyList<ISymbol>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateSymbolsInBinarySection(It.IsAny<BinarySection>(), It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(this.SymbolsInAfter.Cast<ISymbol>().ToList() as IReadOnlyList<ISymbol>));

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  The session functions return an IReadOnlyList<ISymbol>, but the diff task expects a IReadOnlyList<ISymbol>? here, so the guarantee is stronger, meaning this is safe.
        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateSymbolsInBinarySection(sectionDiff.BeforeSection, CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateSymbolsInBinarySection(sectionDiff.AfterSection, CancellationToken.None, logger),
            nameOfThingBeingEnumerated: $"Binary Section '{sectionDiff.Name}'",
            progress: null,
            token: CancellationToken.None);
#pragma warning restore CS8619

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.AreEqual(5, results.Count);
        for (var i = 0; i < 5; i++)
        {
            Assert.AreEqual(-2, results[i].SizeDiff);
        }
    }

    [TestMethod]
    public async Task CanDiffSymbolsPresentOnlyInBeforeOrOnlyInAfter()
    {
        var sectionDiff = this._generator.TextSectionDiff;

        Assert.IsNotNull(sectionDiff.BeforeSection);
        Assert.IsNotNull(sectionDiff.AfterSection);

        this.SymbolsInBefore = this._generator.GenerateSymbolsInBinarySection(sectionDiff.BeforeSection);
        this.SymbolsInAfter = this._generator.GenerateSymbolsInBinarySection(sectionDiff.AfterSection);

        // Before has more symbols in .text$zz with no 'after' counterparts
        this.SymbolsInBefore.AddRange(this._generator.GenerateABunchOfBeforeSymbols(new List<RVARange>() { RVARange.FromRVAAndSize(900, 100) }, namePrefix: "[before-only] "));
        // After has more symbols in .text$mn (at the end of it) with no 'before' counterparts
        this.SymbolsInAfter.AddRange(this._generator.GenerateABunchOfAfterSymbols(new List<RVARange>() { RVARange.FromRVAAndSize(900, 100) }, namePrefix: "[after-only] "));


        this._generator.MockBeforeSession.Setup(s => s.EnumerateSymbolsInBinarySection(It.IsAny<BinarySection>(), It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(this.SymbolsInBefore.Cast<ISymbol>().ToList() as IReadOnlyList<ISymbol>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateSymbolsInBinarySection(It.IsAny<BinarySection>(), It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(this.SymbolsInAfter.Cast<ISymbol>().ToList() as IReadOnlyList<ISymbol>));

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.  The session functions return an IReadOnlyList<ISymbol>, but the diff task expects a IReadOnlyList<ISymbol>? here, so the guarantee is stronger, meaning this is safe.
        var task = new EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateSymbolsInBinarySection(sectionDiff.BeforeSection, CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateSymbolsInBinarySection(sectionDiff.AfterSection, CancellationToken.None, logger),
            nameOfThingBeingEnumerated: $"Binary Section '{sectionDiff.Name}'",
            progress: null,
            token: CancellationToken.None);
#pragma warning restore CS8619

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.AreEqual(15, results.Count);
        Assert.AreEqual(5, results.Where(sd => sd.Name.StartsWith("[before-only]", StringComparison.Ordinal)).Count());
        Assert.AreEqual(5, results.Where(sd => sd.Name.StartsWith("[after-only]", StringComparison.Ordinal)).Count());
        for (var i = 0; i < 15; i++)
        {
            if (results[i].Name.StartsWith("[before-only]", StringComparison.Ordinal))
            {
                Assert.IsNull(results[i].AfterSymbol);
                Assert.IsNotNull(results[i].BeforeSymbol);
                Assert.AreEqual(-5, results[i].SizeDiff);
            }
            else if (results[i].Name.StartsWith("[after-only]", StringComparison.Ordinal))
            {
                Assert.IsNotNull(results[i].AfterSymbol);
                Assert.IsNull(results[i].BeforeSymbol);
                Assert.AreEqual(3, results[i].SizeDiff);
            }
            else
            {
                Assert.IsNotNull(results[i].AfterSymbol);
                Assert.IsNotNull(results[i].BeforeSymbol);
                Assert.AreEqual(-2, results[i].SizeDiff);
            }
        }
    }

    public void Dispose() => this._generator.Dispose();
}
