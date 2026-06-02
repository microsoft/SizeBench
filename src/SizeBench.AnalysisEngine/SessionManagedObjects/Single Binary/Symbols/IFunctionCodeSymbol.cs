namespace SizeBench.AnalysisEngine.Symbols;

public interface IFunctionCodeSymbol
{
    // This is the most basic name for a function possible and will not uniquely identify it.  If this is a member function, this does not include the
    // type name, so for example "CFoo::DoTheThing" and "CBar::DoTheThing" will both have an IFunctionCodeSymbol.FunctionName of "DoTheThing".
    string FunctionName { get; }

    FunctionCodeFormattedName FormattedName { get; }

    string FullName { get; }

    uint Size { get; }

    AccessModifier AccessModifier { get; }
    bool IsIntroVirtual { get; }
    bool IsPure { get; }
    bool IsStatic { get; }
    bool IsVirtual { get; }
    bool IsSealed { get; }
    bool IsPGO { get; }
    bool IsOptimizedForSpeed { get; }
    ulong DynamicInstructionCount { get; }

    FunctionTypeSymbol? FunctionType { get; }
    IReadOnlyList<ParameterDataSymbol>? ArgumentNames { get; }

    // For member functions, this will be set to the type they belong to.
    // For C++, this should always be a UserDefinedTypeSymbol, but Rust for example can have EnumTypeSymbol
    // This can be null, such as for free functions.
    TypeSymbol? ParentType { get; }
    bool IsMemberFunction { get; }

    int BlockCount { get; }
    CodeBlockSymbol PrimaryBlock { get; }
    IEnumerable<CodeBlockSymbol> Blocks { get; }

    bool IsVeryLikelyTheSameAs(IFunctionCodeSymbol otherSymbol);
}
