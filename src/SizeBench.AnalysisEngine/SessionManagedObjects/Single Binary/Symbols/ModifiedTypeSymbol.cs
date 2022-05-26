namespace SizeBench.AnalysisEngine.Symbols;

// A 'modified type' is a type that has 'const' or 'volatile' or '__unaligned' as a qualifier on it.
// Basically, something with a cv-qualifier.  We want to have a useful name for this in some cases,
// like "const char*" or "const MyUDTTypeHere".
// But in many places in the codebase we don't care about const or volatile or unaligned.  So this class
// hierarchy is inverted from DIA where every symbol has an "unmodified type" potentially under it.
// Instead, all things (basic types, UDTs, enums, etc.) are unqualified/unmodified by design and their
// modified variants are ModifiedTypeSymbols that point back to them.
// Thus, if someone loads all UserDefinedTypeSymbol objects they'll not see modified types.
public sealed class ModifiedTypeSymbol : TypeSymbol
{
    internal ModifiedTypeSymbol(SessionDataCache cache,
                                TypeSymbol unmodifiedTypeSymbol,
                                string name,
                                uint size,
                                uint symIndexId) : base(cache, name, size, symIndexId)
    {
        this.UnmodifiedTypeSymbol = unmodifiedTypeSymbol;
    }

    public TypeSymbol UnmodifiedTypeSymbol { get; }

    public override bool CanLoadLayout => this.UnmodifiedTypeSymbol.CanLoadLayout;
}
