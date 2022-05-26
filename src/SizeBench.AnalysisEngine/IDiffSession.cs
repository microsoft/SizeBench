using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

public interface IDiffSession : ISessionWithProgress
{
    ISession BeforeSession { get; }
    ISession AfterSession { get; }

    #region Single Binary objects -> Diff objects

    BinarySectionDiff? GetBinarySectionDiffFromBinarySection(BinarySection section);

    COFFGroupDiff? GetCOFFGroupDiffFromCOFFGroup(COFFGroup coffGroup);

    LibDiff? GetLibraryDiffFromLibrary(Library library);

    CompilandDiff? GetCompilandDiffFromCompiland(Compiland compiland);

    DuplicateDataItemDiff? GetDuplicateDataItemDiffFromDuplicateDataItem(DuplicateDataItem duplicateDataItem);

    WastefulVirtualItemDiff? GetWastefulVirtualItemDiffFromWastefulVirtualItem(WastefulVirtualItem wastefulVirtualItem);

    TemplateFoldabilityItemDiff? GetTemplateFoldabilityItemDiffFromTemplateFoldabilityItem(TemplateFoldabilityItem templateFoldabilityItem);

    SymbolDiff? GetSymbolDiffFromSymbol(ISymbol symbol);

    #endregion

    Task<IReadOnlyList<BinarySectionDiff>> EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken token);

    Task<BinarySectionDiff?> LoadBinarySectionDiffByName(string name, CancellationToken token);

    Task<IReadOnlyList<LibDiff>> EnumerateLibDiffs(CancellationToken token);

    Task<IReadOnlyList<CompilandDiff>> EnumerateCompilandDiffs(CancellationToken token);

    #region Enumerate symbols

    Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInBinarySectionDiff(BinarySectionDiff sectionDiff,
                                                                            CancellationToken token);

    Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInLibDiff(LibDiff libDiff,
                                                                  CancellationToken token);

    Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInCompilandDiff(CompilandDiff compilandDiff,
                                                                        CancellationToken token);

    Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInCOFFGroupDiff(COFFGroupDiff coffGroupDiff,
                                                                        CancellationToken token);

    Task<IReadOnlyList<SymbolDiff>> EnumerateSymbolDiffsInContributionDiff(ContributionDiff contributionDiff,
                                                                           CancellationToken token);

    #endregion

    Task<IReadOnlyList<DuplicateDataItemDiff>> EnumerateDuplicateDataItemDiffs(CancellationToken token);

    Task<IReadOnlyList<WastefulVirtualItemDiff>> EnumerateWastefulVirtualItemDiffs(CancellationToken token);
    Task<IReadOnlyList<TemplateFoldabilityItemDiff>> EnumerateTemplateFoldabilityItemDiffs(CancellationToken token);

    Task<IReadOnlyList<TypeLayoutItemDiff>> LoadAllTypeLayoutDiffs(CancellationToken token);

    Task<IReadOnlyList<TypeLayoutItemDiff>> LoadTypeLayoutDiffsByName(string typeName, CancellationToken token);
    Task<TypeLayoutItemDiff> LoadTypeLayoutDiff(TypeSymbolDiff typeSymbol, CancellationToken token);
    Task<TypeLayoutItemDiff> LoadMemberTypeLayoutDiff(TypeLayoutItemMemberDiff member, CancellationToken token);

    Task<SymbolDiff?> LoadSymbolDiffByBeforeAndAfterRVA(uint? beforeRVA, uint? afterRVA, CancellationToken token);
}
