using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Public Symbol Name={Name}, Size={Size}")]
public class PublicSymbol : Symbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PublicSymbol;

    internal override bool CanBeFolded => true;
    public uint TargetRVA { get; }

    internal PublicSymbol(SessionDataCache cache,
                          string name,
                          uint rva,
                          uint size,
                          bool isVirtualSize,
                          uint symIndexId,
                          uint targetRva) : base(cache, GetFriendlyName(name), rva, size, isVirtualSize, symIndexId)
    {
        // It appears that TargetRVA only exists for thunks, but an Incremental Linking Thunk (ILT) can also be found as a PublicSymbol,
        // so public symbols *can* have a TargetRVA.
        this.TargetRVA = targetRva;
    }

    private static string GetFriendlyName(string name)
    {
        if (name.Contains("`vftable'", StringComparison.Ordinal))
        {
            // vtables are goofy.  Sometimes they show up with a name like this:
            //   ARC::TFactory2D3D<class ARC::D2D1_D3D10::Factory,1>::`vftable'{for `ARC::D3D10::Factory'}
            // But the corresponding DataSymbol has a name like this:
            //   ARC::TFactory2D3D<ARC::D2D1_D3D10::Factory,1>::`vftable'
            // Note the "class " that's in the public symbol's template parameter.  How annoying that it's almost
            // a match, but not quite.  This makes it hard to look up the public symbol when finding a data symbol
            // which we need to do to be able to find the "{for `ARC::D3D10::Factory'}" part of the name.
            // So, here we strip out all "class " and "struct " words from the names of vtables - they seem like
            // useless noise anyway.
            return name.Replace("class ", String.Empty, StringComparison.Ordinal)
                       .Replace("struct ", String.Empty, StringComparison.Ordinal);
        }
        else
        {
            return name;
        }
    }
}
