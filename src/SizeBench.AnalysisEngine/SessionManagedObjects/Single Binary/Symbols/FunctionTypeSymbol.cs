using System.Collections.ObjectModel;

namespace SizeBench.AnalysisEngine.Symbols;

public sealed class FunctionTypeSymbol : TypeSymbol
{
    internal FunctionTypeSymbol(SessionDataCache cache,
                                string name,
                                uint size,
                                uint symIndexId,
                                bool isConst,
                                bool isVolatile,
                                TypeSymbol[]? argumentTypes,
                                TypeSymbol returnValueType) : base(cache, name, size, symIndexId)
    {
        this.IsConst = isConst;
        this.IsVolatile = isVolatile;
        this.ArgumentTypes = argumentTypes is null ? null : Array.AsReadOnly(argumentTypes);
        this.ReturnValueType = returnValueType;
    }

    public ReadOnlyCollection<TypeSymbol>? ArgumentTypes { get; }
    public TypeSymbol ReturnValueType { get; }

    public bool IsConst { get; }
    public bool IsVolatile { get; }

    // FunctionTypeSymbols don't have layouts, they're just a pointer essentially.
    public override bool CanLoadLayout => false;
}
