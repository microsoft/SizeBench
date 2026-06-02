namespace SizeBench.AnalysisEngine.Symbols;

// When we compare symbols for "IsVeryLikelyTheSameAs" in diffs, we need some way to know efficiently which ones are worth comparing to each other,
// because the lists of symbols in large binaries are so enormous that if we do the naive check of every 'before' symbol vs. every 'after' symbol it
// takes ages.
// But we don't simply want to use symbol.GetType() to partition things, because some symbols can be compared (nay, must be!) even if their CLR type is
// different - so we bucket symbols up into our own enum.  As a bonus, it's cheaper to get this enum property from our types than it is to query GetType()
// a lot.
// Examples of places where we want to compare two symbols even if they're of different CLR types:
//    SimpleFunctionCodeSymbol vs. PrimaryBlockSymbol that belongs to a ComplexFunctionCodeSymbol
//
// In the future it may be wise to see if IpToStateMap should be in the same comparison class as SeparatedIpToStateMap for diffing between binaries with
// changed PGO counts, or initial PGO introduction.  Likewise for PData vs. ForwarderPData vs. PackedUnwindDataPData, and UnwindInfo vs. ChainUnwindInfo.
// For now these are treated as separate kinds of symbols since there's no test coverage to verify anything else is correct.
public enum SymbolComparisonClass
{
    Unknown,

    // DIA Symbol types
    PrimaryCodeBlock,
    SeparatedCodeBlock,
    PublicSymbol,
    Thunk,
    StaticData,

    // EH Symbol types
    ChainUnwindInfo,
    CppXdata,
    ForwarderPData,
    HandlerMap,
    IpToStateMap,
    PackedUnwindDataPData,
    PData,
    SeparatedIpToStateMap,
    StateUnwindMap,
    TryMap,
    UnwindInfo,

    // Rsrc Symbol types
    RsrcDirectory,
    RsrcDirectoryEntry,
    RsrcDataEntry,
    RsrcData,
    RsrcString,

    // Other PE Symbol types
    PEDirectory,
    ImportDescriptor,
    ImportThunk,
    ImportByName,
    ImportString,
    LoadConfigTable
}
