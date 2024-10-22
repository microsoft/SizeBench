using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Function Symbol Name={FormattedName.UniqueSignatureWithNoPrefixes}, FullName={FullName}, Size={Size}")]
public sealed class ComplexFunctionCodeSymbol : IFunctionCodeSymbol
{
    public string FunctionName { get; }
    public uint Size { get; }
    public AccessModifier AccessModifier { get; }
    public bool IsIntroVirtual { get; }
    public bool IsPure { get; }
    public bool IsStatic { get; }
    public bool IsVirtual { get; }
    public bool IsSealed { get; }
    public bool IsPGO { get; }
    public bool IsOptimizedForSpeed { get; }
    public ulong DynamicInstructionCount { get; }

    [Display(Name = "Function Type")]
    public FunctionTypeSymbol? FunctionType { get; }
    public IReadOnlyList<ParameterDataSymbol>? ArgumentNames { get; }

    // For member functions, this will be set to the type they belong to.
    // This can be null, such as for free functions.
    public TypeSymbol? ParentType { get; }
    public bool IsMemberFunction => this.ParentType != null;

    public int BlockCount => this._separatedBlocks.Count + 1; /* +1 for the primary block */
    public CodeBlockSymbol PrimaryBlock { get; }
    private readonly List<SeparatedCodeBlockSymbol> _separatedBlocks;
    public IReadOnlyList<SeparatedCodeBlockSymbol> SeparatedBlocks => this._separatedBlocks;

    public IEnumerable<CodeBlockSymbol> Blocks
    {
        get
        {
            yield return this.PrimaryBlock;
            foreach (var separatedBlock in this._separatedBlocks)
            {
                yield return separatedBlock;
            }
        }
    }

    public FunctionCodeFormattedName FormattedName { get; }

    [Display(Name = "Full Name")]
    public string FullName => this.FormattedName.All;

    internal ComplexFunctionCodeSymbol(SessionDataCache cache,
                                       string name,
                                       PrimaryCodeBlockSymbol primaryBlock,
                                       List<SeparatedCodeBlockSymbol> separatedBlocks,
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
                                       bool isOptimizedForSpeed = false,
                                       ulong dynamicInstructionCount = 0)
    {
#if DEBUG
        Debug.Assert(cache.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.Code));

        if (cache.AllFunctionSymbolsBySymIndexIdOfPrimaryBlock.ContainsKey(primaryBlock.SymIndexId))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.FunctionName = name;
        this.AccessModifier = accessModifier;
        this.IsIntroVirtual = isIntroVirtual;
        this.IsPure = isPure;
        this.IsStatic = isStatic;
        this.IsVirtual = isVirtual;
        this.IsSealed = isSealed;
        this.IsPGO = isPGO;
        this.IsOptimizedForSpeed = isOptimizedForSpeed;
        this.DynamicInstructionCount = dynamicInstructionCount;
        FunctionSymbolHelper.VerifyNotInInconsistentState(this);

        this.FunctionType = functionType;
        this.ArgumentNames = argumentNames;
        this.ParentType = parentType;

        this.FormattedName = new FunctionCodeFormattedName(this);

        this.PrimaryBlock = primaryBlock;
        this.PrimaryBlock.ParentFunction = this;
        var totalSize = primaryBlock.Size;

        this._separatedBlocks = separatedBlocks;
        var sepBlockRVAsAlreadyAttributed = new List<uint>(capacity: separatedBlocks.Count);
        foreach (var sepBlock in this._separatedBlocks)
        {
            // A separated block could be used by multiple functions, we will only set the parent function
            // if it is the one DIA considers to be its lexical parent.
            if (sepBlock.ParentFunctionSymIndexId == this.PrimaryBlock.SymIndexId)
            {
                sepBlock.ParentFunction = this;
            }

            // Sometimes DIA can return the same RVA multiple times, as multiple different separated blocks with different SymIndexIDs.
            // That's not great, but we can tolerate it for now - we need to let each SeparatedBlockSymbol exist because SymIndexIDs are used
            // to key so many things in different lookup paths, but we don't want to double-count the size, so we'll at least only add
            // to the ComplexFunctionSymbol's total size if we haven't seen this RVA yet.
            if (!sepBlockRVAsAlreadyAttributed.Contains(sepBlock.RVA))
            {
                totalSize += sepBlock.Size;
                sepBlockRVAsAlreadyAttributed.Add(sepBlock.RVA);
            }
        }

        this.Size = totalSize;

        cache.AllFunctionSymbolsBySymIndexIdOfPrimaryBlock.Add(primaryBlock.SymIndexId, this);
    }

    public bool IsVeryLikelyTheSameAs(IFunctionCodeSymbol otherSymbol)
    {
        ArgumentNullException.ThrowIfNull(otherSymbol);

        return FunctionSymbolHelper.IsVeryLikelyTheSameAs(this, otherSymbol);
    }
}
