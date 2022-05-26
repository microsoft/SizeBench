using System.ComponentModel.DataAnnotations;

namespace SizeBench.AnalysisEngine.Symbols;

public abstract class TypeSymbol
{
    public string Name { get; }
    public uint InstanceSize { get; }
    internal uint SymIndexId;

    internal TypeSymbol(SessionDataCache cache,
                        string name,
                        uint instanceSize,
                        uint symIndexId)
    {
#if DEBUG
        if (cache.AllTypesBySymIndexId.ContainsKey(symIndexId) == true)
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.Name = name;
        this.InstanceSize = instanceSize;
        this.SymIndexId = symIndexId;

        cache.AllTypesBySymIndexId.Add(symIndexId, this);
    }

    // Does this type have a layout of data members, base types, etc...?
    // Basically, can this be passed to ISession.LoadTypeLayout?
    [Display(AutoGenerateField = false)] // Don't show this in UIs, it's not relevant to users of any tool, just the implementation
    public abstract bool CanLoadLayout { get; }

    internal virtual bool IsVeryLikelyTheSameAs(TypeSymbol otherSymbol)
    {
        return GetType() == otherSymbol.GetType() &&
               String.Equals(this.Name, otherSymbol.Name, StringComparison.Ordinal);
    }
}
