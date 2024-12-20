﻿using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DIAInterop;

internal interface IDIAAdapter
{
    IEnumerable<BinarySection> FindBinarySections(IPEFile peFile, ILogger logger, CancellationToken token);
    IEnumerable<COFFGroup> FindCOFFGroups(IPEFile peFile, ILogger logger, CancellationToken token);
    IEnumerable<RawSectionContribution> FindSectionContributions(ILogger logger, CancellationToken token);
    IEnumerable<SourceFile> FindSourceFiles(ILogger logger, CancellationToken token);
    IEnumerable<RVARange> FindRVARangesForSourceFileAndCompiland(SourceFile sourceFile, Compiland compiland, CancellationToken token);
    IEnumerable<MemberDataSymbol> FindAllMemberDataSymbolsWithinUDT(UserDefinedTypeSymbol udt, CancellationToken cancellationToken);
    IEnumerable<(uint typeId, uint offset)> FindAllBaseTypeIDsForUDT(UserDefinedTypeSymbol udt);
    IEnumerable<StaticDataSymbol> FindAllStaticDataSymbolsWithinCompiland(Compiland compiland, CancellationToken cancellation);
    IEnumerable<IFunctionCodeSymbol> FindAllFunctionsWithinUDT(uint symIndexId, CancellationToken cancellationToken);
    IEnumerable<IFunctionCodeSymbol> FindAllTemplatedFunctions(CancellationToken cancellationToken);

    IEnumerable<UserDefinedTypeSymbol> FindAllUserDefinedTypes(ILogger logger, CancellationToken token);
    IEnumerable<UserDefinedTypeSymbol> FindUserDefinedTypesByName(ILogger logger, string name, CancellationToken token);
    IEnumerable<AnnotationSymbol> FindAllAnnotations(ILogger parentLogger, CancellationToken token);
    SortedList<uint, List<string>> FindAllDisambiguatingVTablePublicSymbolNamesByRVA(ILogger parentLogger, CancellationToken token);
    byte FindCountOfVTablesWithin(uint symIndexId);
    ISymbol? FindSymbolByRVA(uint rva, bool allowFindingNearest, CancellationToken cancellationToken);
    TSymbol FindSymbolBySymIndexId<TSymbol>(uint symIndexId, CancellationToken cancellationToken) where TSymbol : class, ISymbol;
    TSymbol FindTypeSymbolBySymIndexId<TSymbol>(uint symIndexId, CancellationToken cancellationToken) where TSymbol : TypeSymbol;
    List<InlineSiteSymbol>? FindAllInlineSitesForBlock(CodeBlockSymbol codeBlock, CancellationToken cancellationToken);
    List<InlineSiteSymbol> FindAllInlineSites(CancellationToken cancellationToken);

    IEnumerable<(ISymbol symbol, uint amountOfRVARangeExplored)> FindSymbolsInRVARange(RVARange range, CancellationToken cancellationToken);

    CommandLine FindCommandLineForCompilandByID(uint compilandSymIndexId);

    string SymbolNameFromRva(uint rva);
    uint SymbolRvaFromName(string name, bool preferFunction);
    CompilandLanguage LanguageOfSymbolAtRva(uint rva);

    uint? LoadPublicSymbolTargetRVAIfPossible(uint rva);
    List<IMAGE_SECTION_HEADER> FindAllImageSectionHeadersFromPDB(CancellationToken token);
}
