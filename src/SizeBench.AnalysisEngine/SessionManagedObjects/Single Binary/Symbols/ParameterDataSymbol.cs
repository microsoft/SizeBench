using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Parameter Data Symbol: {Type} {Name}")]
public sealed class ParameterDataSymbol
{
    public string Name { get; }
    public TypeSymbol Type { get; }

    internal ParameterDataSymbol(SessionDataCache cache,
                                 string name,
                                 uint symIndexId,
                                 TypeSymbol type)
    {
#if DEBUG
        if (cache.AllParameterDataSymbolsbySymIndexId.ContainsKey(symIndexId))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.Name = name;
        this.Type = type;

        cache.AllParameterDataSymbolsbySymIndexId.Add(symIndexId, this);
    }
}
