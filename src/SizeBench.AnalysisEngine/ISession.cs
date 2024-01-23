using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine;

public interface ISession : ISessionWithProgress
{
    string PdbPath { get; }
    string BinaryPath { get; }

    byte BytesPerWord { get; }

    IPEFile PEFile { get; }

    Task<IReadOnlyList<BinarySection>> EnumerateBinarySectionsAndCOFFGroups(CancellationToken token);
    Task<IReadOnlyList<BinarySection>> EnumerateBinarySectionsAndCOFFGroups(CancellationToken token, ILogger? parentLogger);
    Task<BinarySection?> LoadBinarySectionByName(string name, CancellationToken token);

    #region Enumerate symbols in Compiland, COFF Group, Lib, Contribution, Binary Section, Source File

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCompiland(Compiland compiland,
                                                             CancellationToken token);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCompiland(Compiland compiland,
                                                             SymbolEnumerationOptions options,
                                                             CancellationToken token);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCompiland(Compiland compiland,
                                                             SymbolEnumerationOptions options,
                                                             CancellationToken token,
                                                             ILogger? parentLogger);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCOFFGroup(COFFGroup coffGroup,
                                                             CancellationToken token);
    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInCOFFGroup(COFFGroup coffGroup,
                                                             CancellationToken token,
                                                             ILogger? parentLogger);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInLib(Library library,
                                                       CancellationToken token);
    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInLib(Library library,
                                                       CancellationToken token,
                                                       ILogger? parentLogger);


    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInContribution(Contribution contribution,
                                                                CancellationToken token);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInContribution(Contribution contribution,
                                                                CancellationToken token,
                                                                ILogger? parentLogger);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInBinarySection(BinarySection section,
                                                                 CancellationToken token);
    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInBinarySection(BinarySection section,
                                                                 CancellationToken token,
                                                                 ILogger? parentLogger);

    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInSourceFile(SourceFile sourceFile,
                                                              CancellationToken token);
    Task<IReadOnlyList<ISymbol>> EnumerateSymbolsInSourceFile(SourceFile sourceFile,
                                                              CancellationToken token,
                                                              ILogger? parentLogger);

    #endregion

    Task<IReadOnlyList<Library>> EnumerateLibs(CancellationToken token);
    Task<IReadOnlyList<Library>> EnumerateLibs(CancellationToken token, ILogger? parentLogger);

    Task<IReadOnlyList<Compiland>> EnumerateCompilands(CancellationToken token);

    Task<IReadOnlyList<SourceFile>> EnumerateSourceFiles(CancellationToken token);

    Task<SymbolPlacement> LookupSymbolPlacementInBinary(ISymbol symbol, CancellationToken token);

    Task<ISymbol?> LoadSymbolByRVA(uint rva);
    Task<ISymbol?> LoadSymbolByRVA(uint rva, CancellationToken token, ILogger? parentLogger);

    Task<IReadOnlyList<DuplicateDataItem>> EnumerateDuplicateDataItems(CancellationToken token);
    Task<IReadOnlyList<DuplicateDataItem>> EnumerateDuplicateDataItems(CancellationToken token, ILogger? parentLogger);

    Task<IReadOnlyList<WastefulVirtualItem>> EnumerateWastefulVirtuals(CancellationToken token);
    Task<IReadOnlyList<WastefulVirtualItem>> EnumerateWastefulVirtuals(CancellationToken token, ILogger? parentLogger);

    Task<IReadOnlyList<UserDefinedTypeSymbol>> EnumerateAllUserDefinedTypes(CancellationToken token);
    Task<IReadOnlyList<UserDefinedTypeGrouping>> EnumerateAllUserDefinedTypeGroupings(CancellationToken token);

    Task<IReadOnlyList<IFunctionCodeSymbol>> EnumerateFunctionsFromUserDefinedType(UserDefinedTypeSymbol udt, CancellationToken token);

    Task<IReadOnlyList<TypeLayoutItem>> LoadAllTypeLayouts(CancellationToken token);
    Task<IReadOnlyList<TypeLayoutItem>> LoadAllTypeLayouts(CancellationToken token, ILogger? parentLogger);

    Task<IReadOnlyList<TypeLayoutItem>> LoadTypeLayoutsByName(string typeName, CancellationToken token);
    Task<IReadOnlyList<TypeLayoutItem>> LoadTypeLayoutsByName(string typeName, CancellationToken token, ILogger? parentLogger);

    Task<TypeLayoutItem> LoadTypeLayout(TypeSymbol typeSymbol, CancellationToken token);
    Task<TypeLayoutItem> LoadTypeLayout(TypeSymbol typeSymbol, CancellationToken token, ILogger? parentLogger);

    Task<TypeLayoutItem> LoadMemberTypeLayout(TypeLayoutItemMember member, CancellationToken token);
    Task<TypeLayoutItem> LoadMemberTypeLayout(TypeLayoutItemMember member, CancellationToken token, ILogger? parentLogger);

    Task<IReadOnlyList<TemplateFoldabilityItem>> EnumerateTemplateFoldabilityItems(CancellationToken token);
    Task<IReadOnlyList<TemplateFoldabilityItem>> EnumerateTemplateFoldabilityItems(CancellationToken token, ILogger? parentLogger);

    Task<string> DisassembleFunction(IFunctionCodeSymbol functionSymbol, DisassembleFunctionOptions options, CancellationToken token);

    Task<IReadOnlyList<AnnotationSymbol>> EnumerateAnnotations(CancellationToken token);

    Task<IReadOnlyList<ISymbol>> EnumerateAllSymbolsFoldedAtRVA(uint rva, CancellationToken token);

    float CompareSimilarityOfCodeBytesInBinary(IFunctionCodeSymbol firstSymbol, IFunctionCodeSymbol secondSymbol);

    bool CompareData(long RVA1, long RVA2, uint length);
}
