using System.Diagnostics.CodeAnalysis;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.TestDataCommon;
#pragma warning disable 0649 // Field <x> is never assigned to -- but many of the fields in this test type are assigned to by other test code
[ExcludeFromCodeCoverage]
internal class TestDIAAdapter : IDIAAdapter
{
    public IEnumerable<BinarySection>? BinarySectionsToFind;

    public IEnumerable<BinarySection> FindBinarySections(ILogger logger, CancellationToken token)
    {
        if (this.BinarySectionsToFind is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.BinarySectionsToFind;
    }

    public IEnumerable<COFFGroup>? COFFGroupsToFind;

    public IEnumerable<COFFGroup> FindCOFFGroups(ILogger logger, CancellationToken token)
    {
        if (this.COFFGroupsToFind is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.COFFGroupsToFind;
    }

    public IEnumerable<RawSectionContribution>? SectionContributionsToFind;

    public IEnumerable<RawSectionContribution> FindSectionContributions(ILogger logger, CancellationToken token)
    {
        if (this.SectionContributionsToFind is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.SectionContributionsToFind;
    }

    public IEnumerable<SourceFile>? SourceFilesToFind;
    public IEnumerable<SourceFile> FindSourceFiles(ILogger parentLogger, CancellationToken token)
    {
        if (this.SourceFilesToFind is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.SourceFilesToFind;
    }

    public Dictionary<UserDefinedTypeSymbol, IEnumerable<MemberDataSymbol>> MemberDataSymbolsToFindByUDT = new Dictionary<UserDefinedTypeSymbol, IEnumerable<MemberDataSymbol>>();

    public IEnumerable<MemberDataSymbol> FindAllMemberDataSymbolsWithinUDT(UserDefinedTypeSymbol udt, CancellationToken cancellationToken)
    {
        if (this.MemberDataSymbolsToFindByUDT.TryGetValue(udt, out var dataMembers))
        {
            return dataMembers;
        }
        else
        {
            return new List<MemberDataSymbol>();
        }
    }

    public Dictionary<Compiland, IEnumerable<StaticDataSymbol>> StaticDataSymbolsToFindByCompiland = new Dictionary<Compiland, IEnumerable<StaticDataSymbol>>();

    public IEnumerable<StaticDataSymbol> FindAllStaticDataSymbolsWithinCompiland(Compiland compiland, CancellationToken cancellation)
    {
        if (this.StaticDataSymbolsToFindByCompiland.TryGetValue(compiland, out var staticDataSymbols))
        {
            return staticDataSymbols;
        }
        else
        {
            return new List<StaticDataSymbol>();
        }
    }

    public Dictionary<uint, IEnumerable<IFunctionCodeSymbol>> FunctionsToFindBySymIndexId = new Dictionary<uint, IEnumerable<IFunctionCodeSymbol>>();

    public IEnumerable<IFunctionCodeSymbol> FindAllFunctionsWithinUDT(uint symIndexId, CancellationToken token)
    {
        if (this.FunctionsToFindBySymIndexId.ContainsKey(symIndexId))
        {
            return this.FunctionsToFindBySymIndexId[symIndexId];
        }
        else
        {
            return new List<IFunctionCodeSymbol>();
        }
    }

    public Dictionary<uint, ISymbol> SymbolsToFindBySymIndexId = new Dictionary<uint, ISymbol>();

    public TSymbol FindSymbolBySymIndexId<TSymbol>(uint symIndexId, CancellationToken token) where TSymbol : class, ISymbol
    {
        if (this.SymbolsToFindBySymIndexId.ContainsKey(symIndexId))
        {
            return (TSymbol)(this.SymbolsToFindBySymIndexId[symIndexId]);
        }
        else
        {
            throw new InvalidOperationException("Tests should never reach this");
        }
    }

    public Dictionary<uint, TypeSymbol> TypeSymbolsToFindBySymIndexId = new Dictionary<uint, TypeSymbol>();

    public TSymbol FindTypeSymbolBySymIndexId<TSymbol>(uint symIndexId, CancellationToken token) where TSymbol : TypeSymbol
    {
        if (this.TypeSymbolsToFindBySymIndexId.ContainsKey(symIndexId))
        {
            return (TSymbol)(this.TypeSymbolsToFindBySymIndexId[symIndexId]);
        }
        else
        {
            throw new InvalidOperationException("Tests should never reach this");
        }
    }

    public IEnumerable<IFunctionCodeSymbol> TemplatedFunctionsToFind = new List<IFunctionCodeSymbol>();

    public IEnumerable<IFunctionCodeSymbol> FindAllTemplatedFunctions(CancellationToken token) => this.TemplatedFunctionsToFind;

    public IEnumerable<UserDefinedTypeSymbol>? UserDefinedTypesToFind;

    public IEnumerable<UserDefinedTypeSymbol> FindAllUserDefinedTypes(ILogger logger, CancellationToken token)
    {
        if (this.UserDefinedTypesToFind is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.UserDefinedTypesToFind;
    }

    public Dictionary<string, IEnumerable<UserDefinedTypeSymbol>> UserDefinedTypesToFindByName = new Dictionary<string, IEnumerable<UserDefinedTypeSymbol>>();

    public IEnumerable<UserDefinedTypeSymbol> FindUserDefinedTypesByName(ILogger logger, string name, CancellationToken token)
    {
        if (this.UserDefinedTypesToFindByName.ContainsKey(name))
        {
            return this.UserDefinedTypesToFindByName[name];
        }
        else
        {
            return new List<UserDefinedTypeSymbol>();
        }
    }

    public IEnumerable<AnnotationSymbol>? AnnotationsToFind;
    public IEnumerable<AnnotationSymbol> FindAllAnnotations(ILogger logger, CancellationToken token)
    {
        if (this.AnnotationsToFind is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.AnnotationsToFind;
    }

    public SortedList<uint, List<string>>? DisambiguatingVTablePublicSymbolNamessByRVA;
    public SortedList<uint, List<string>> FindAllDisambiguatingVTablePublicSymbolNamesByRVA(ILogger logger, CancellationToken token)
    {
        if (this.DisambiguatingVTablePublicSymbolNamessByRVA is null)
        {
            throw new InvalidOperationException("Tests should never reach this");
        }

        return this.DisambiguatingVTablePublicSymbolNamessByRVA;
    }

    public Dictionary<uint, ISymbol> SymbolsToFindByRVA = new Dictionary<uint, ISymbol>();

    public ISymbol? FindSymbolByRVA(uint rva, bool allowFindingNearest, CancellationToken token)
    {
        if (this.SymbolsToFindByRVA.ContainsKey(rva))
        {
            return this.SymbolsToFindByRVA[rva];
        }
        else
        {
            return null;
        }
    }

    public Dictionary<RVARange, IEnumerable<ValueTuple<ISymbol, uint>>> SymbolsToFindByRVARange = new Dictionary<RVARange, IEnumerable<(ISymbol, uint)>>();

    public IEnumerable<ValueTuple<ISymbol, uint>> FindSymbolsInRVARange(RVARange range, CancellationToken token)
    {
        foreach (var dictionaryEntry in this.SymbolsToFindByRVARange)
        {
            // If these ranges overlap, we'll find the subset of symbols within that we should return
            if (dictionaryEntry.Key.Contains(range.RVAStart) ||
                dictionaryEntry.Key.Contains(range.RVAEnd) ||
                range.Contains(dictionaryEntry.Key))
            {
                return dictionaryEntry.Value.Where((tuple) => range.Contains(tuple.Item1.RVA, tuple.Item1.Size));
            }
        }

        return new List<ValueTuple<ISymbol, uint>>();
    }

    public Dictionary<Tuple<SourceFile, Compiland>, IEnumerable<RVARange>> RVARangesToFindForSourceFileCompilandCombinations = new Dictionary<Tuple<SourceFile, Compiland>, IEnumerable<RVARange>>();
    public IEnumerable<RVARange> FindRVARangesForSourceFileAndCompiland(SourceFile sourceFile, Compiland compiland, CancellationToken token)
    {
        var key = Tuple.Create(sourceFile, compiland);
        if (this.RVARangesToFindForSourceFileCompilandCombinations.ContainsKey(key))
        {
            return this.RVARangesToFindForSourceFileCompilandCombinations[key];
        }

        return Enumerable.Empty<RVARange>();
    }

    public Dictionary<uint, byte> CountOfVTablesToFind = new Dictionary<uint, byte>();
    public byte FindCountOfVTablesWithin(uint symIndexId)
    {
        if (this.CountOfVTablesToFind.ContainsKey(symIndexId))
        {
            return this.CountOfVTablesToFind[symIndexId];
        }
        else
        {
            return 0;
        }
    }

    public SortedList<uint, NameCanonicalization> CanonicalNamesToFind = new SortedList<uint, NameCanonicalization>();
    public SortedList<uint, NameCanonicalization> FindCanonicalNamesForFoldableRVAs(ILogger logger, CancellationToken token) =>
        this.CanonicalNamesToFind;

    public Dictionary<uint, CommandLine> CompilandCommandLinesToFind = new Dictionary<uint, CommandLine>();
    public CommandLine FindCommandLineForCompilandByID(uint compilandSymIndexId)
    {
        if (this.CompilandCommandLinesToFind.TryGetValue(compilandSymIndexId, out var commandLine))
        {
            return commandLine;
        }
        else
        {
            return CommonCommandLines.NullCommandLine;
        }
    }

    public Dictionary<uint, string> SymbolNamesByRVA = new Dictionary<uint, string>();
    public string SymbolNameFromRva(uint rva)
    {
        if (this.SymbolNamesByRVA.TryGetValue(rva, out var name))
        {
            return name;
        }
        else
        {
            throw new InvalidOperationException("Should not end up here for tests.");
        }
    }

    public readonly Dictionary<string, uint> SymbolRvasByName = new Dictionary<string, uint>();
    public uint SymbolRvaFromName(string name, bool preferFunction)
    {
        if (this.SymbolRvasByName.TryGetValue(name, out var rva))
        {
            return rva;
        }
        else
        {
            throw new InvalidOperationException("Should not end up here for tests.");
        }
    }

    public Dictionary<uint, CompilandLanguage> CompilandLanguageByRVA = new Dictionary<uint, CompilandLanguage>();
    public CompilandLanguage LanguageOfSymbolAtRva(uint rva)
    {
        if (this.CompilandLanguageByRVA.TryGetValue(rva, out var lang))
        {
            return lang;
        }
        else
        {
            throw new InvalidOperationException("Should not end up here for tests.");
        }
    }

    public uint? LoadPublicSymbolTargetRVAIfPossible(uint rva)
        => throw new NotImplementedException();
}
