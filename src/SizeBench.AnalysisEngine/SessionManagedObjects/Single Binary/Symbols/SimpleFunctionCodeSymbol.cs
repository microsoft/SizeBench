using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Function Symbol Name={Name}, FullName={FullName}, Size={Size}")]
public sealed class SimpleFunctionCodeSymbol : CodeBlockSymbol, IFunctionCodeSymbol
{
    // We want to allow this and PrimaryCodeBlock to compare to each other in case a function goes from
    // being simple to being complex (or vice-versa) when processing a diff.
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PrimaryCodeBlock;

    public string FunctionName { get; }

    public AccessModifier AccessModifier { get; }
    public bool IsIntroVirtual { get; }
    public bool IsPure { get; }
    public bool IsStatic { get; }
    public bool IsVirtual { get; }
    public bool IsSealed { get; }
    public bool IsPGO { get; }
    public bool IsOptimizedForSpeed { get; }

    [Display(Name = "Function Type")]
    public FunctionTypeSymbol? FunctionType { get; }
    public IReadOnlyList<ParameterDataSymbol>? ArgumentNames { get; }

    // For member functions, this will be set to the type they belong to.
    // This can be null, such as for free functions.
    // Note that this can be an EnumTypeSymbol in Rust - in C and C++ it's a UserDefinedType if it's non-null,
    // but you can't assume that across all languages
    public TypeSymbol? ParentType { get; }
    public bool IsMemberFunction => this.ParentType != null;

    public CodeBlockSymbol PrimaryBlock => this;

    private readonly List<CodeBlockSymbol> _blocks;
    public IReadOnlyList<CodeBlockSymbol> Blocks => this._blocks;

    public FunctionCodeFormattedName FormattedName { get; }

    [Display(Name = "Full Name")]
    public string FullName => this.FormattedName.All;

    internal SimpleFunctionCodeSymbol(SessionDataCache cache,
                                      string name,
                                      uint rva,
                                      uint size,
                                      uint symIndexId,
                                      FunctionTypeSymbol? functionType = null,
                                      ParameterDataSymbol[]? argumentNames = null,
                                      TypeSymbol? parentType = null,
                                      AccessModifier accessModifier = 0,
                                      bool isIntroVirtual = false,
                                      bool isPure = false,
                                      bool isStatic = false,
                                      bool isVirtual = false,
                                      bool isSealed = false,
                                      bool isPGO = false,
                                      bool isOptimizedForSpeed = false) : base(cache, rva, size, symIndexId: symIndexId)
    {
#if DEBUG
        Debug.Assert(cache.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.Code));

        if (cache.AllFunctionSymbolsBySymIndexIdOfPrimaryBlock.ContainsKey(symIndexId))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.FunctionName = name;
        this._blocks = new List<CodeBlockSymbol>(capacity: 1) { this };
        this.AccessModifier = accessModifier;
        this.IsIntroVirtual = isIntroVirtual;
        this.IsPure = isPure;
        this.IsStatic = isStatic;
        this.IsVirtual = isVirtual;
        this.IsSealed = isSealed;
        this.IsPGO = isPGO;
        this.IsOptimizedForSpeed = isOptimizedForSpeed;
        FunctionSymbolHelper.VerifyNotInInconsistentState(this);

        this.FunctionType = functionType;
        this.ArgumentNames = argumentNames;
        this.ParentType = parentType;

        this.FormattedName = new FunctionCodeFormattedName(this);
        this.ParentFunction = this;

        // We'll use the unique signature as the Name, except we don't want anything that can add a prefix, otherwise sort order gets weird when "static " is at the start of names.
        this.Name = this.FormattedName.UniqueSignatureWithNoPrefixes;

        if (this.IsCOMDATFolded)
        {
            this.CanonicalName = this.DataCache.AllCanonicalNames![rva].CanonicalName;
        }
        else
        {
            this.CanonicalName = this.Name;
        }

        cache.AllFunctionSymbolsBySymIndexIdOfPrimaryBlock.Add(symIndexId, this);
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is SimpleFunctionCodeSymbol otherFunction)
        {
            return IsVeryLikelyTheSameAs(otherFunction);
        }

        // If this function was 'simple' in one side and complex in the other, we'll allow them to match primary block
        // of the complex side to this simple function.  That way a function that goes from PGO-dead to PGO-live and split
        // into two chunks can still be somewhat compared.
        return otherSymbol is PrimaryCodeBlockSymbol otherPrimaryBlock &&
               IsVeryLikelyTheSameAs(otherPrimaryBlock.ParentFunction);
    }

    public bool IsVeryLikelyTheSameAs(IFunctionCodeSymbol otherSymbol)
    {
        ArgumentNullException.ThrowIfNull(otherSymbol);

        return FunctionSymbolHelper.IsVeryLikelyTheSameAs(this, otherSymbol);
    }
}
