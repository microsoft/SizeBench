using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Member Data Symbol Name={Name}, Size={Size}, Offset={Offset}")]
public sealed class MemberDataSymbol
{
    public string Name { get; }
    public uint Size { get; }
    public bool IsStaticMember { get; }
    public int Offset { get; }
    internal readonly bool IsBitField;
    internal readonly ushort BitStartPosition;
    internal readonly TypeSymbol Type;

    internal MemberDataSymbol(SessionDataCache cache,
                              string name,
                              uint size,
                              uint symIndexId,
                              bool isStaticMember,
                              bool isBitField,
                              ushort bitStartPosition,
                              int offset,
                              TypeSymbol type)
    {
#if DEBUG
        if (cache.AllMemberDataSymbolsBySymIndexId.ContainsKey(symIndexId))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.Name = name;
        this.Size = size;
        this.IsStaticMember = isStaticMember;
        this.IsBitField = isBitField;
        this.BitStartPosition = bitStartPosition;
        this.Offset = offset;
        this.Type = type;

        cache.AllMemberDataSymbolsBySymIndexId.Add(symIndexId, this);
    }
}
