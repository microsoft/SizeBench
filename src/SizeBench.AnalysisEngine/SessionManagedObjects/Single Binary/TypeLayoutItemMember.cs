using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("TypeLayout Member: {Name}, Offset={Offset}, Size={Size}")]
public sealed class TypeLayoutItemMember
{
    public static string AlignmentPaddingName => "<alignment padding>";
    public static string TailSlopAlignmentName => "<tail slop alignment padding>";

    private TypeLayoutItemMember()
    {

    }

    internal static TypeLayoutItemMember FromDataSymbol(MemberDataSymbol dataSymbol, uint baseOffset)
    {
        return new TypeLayoutItemMember()
        {
            IsBitField = dataSymbol.IsBitField,
            BitStartPosition = dataSymbol.IsBitField ? dataSymbol.BitStartPosition : (ushort)0,
            NumberOfBits = dataSymbol.IsBitField ? (ushort)dataSymbol.Size : (ushort)0,
            Size = dataSymbol.IsBitField ? dataSymbol.Size * ((decimal)1.0 / (decimal)8.0) : dataSymbol.Size,
            Offset = baseOffset + dataSymbol.Offset + (dataSymbol.IsBitField ? (dataSymbol.BitStartPosition * (decimal)0.125) : 0),
            Name = dataSymbol.Name,
            IsAlignmentMember = false,
            Type = dataSymbol.Type
        };
    }

    internal static TypeLayoutItemMember CreateAlignmentMember(decimal amountOfAlignment,
                                                               decimal offsetOfAlignment,
                                                               bool isBitfield,
                                                               ushort bitStartPosition,
                                                               bool isTailSlop)
    {
        return new TypeLayoutItemMember()
        {
            Name = isTailSlop ? TailSlopAlignmentName : AlignmentPaddingName,
            IsAlignmentMember = true,
            IsTailSlopAlignmentMember = isTailSlop,
            IsBitField = isBitfield,
            BitStartPosition = bitStartPosition,
            NumberOfBits = isBitfield ? (ushort)(amountOfAlignment / 0.125m) : (ushort)0,
            Size = amountOfAlignment,
            Offset = offsetOfAlignment
        };
    }

    internal static TypeLayoutItemMember CreateVfptrMember(uint baseOffset, uint size)
    {
        return new TypeLayoutItemMember()
        {
            Name = "vfptr",
            Offset = baseOffset,
            Size = size
        };
    }

    public bool IsBitField { get; init; }
    public ushort BitStartPosition { get; init; }
    public ushort NumberOfBits { get; init; }
    public decimal Size { get; init; }
    public decimal Offset { get; init; }
    public string Name { get; init; } = String.Empty;
    public bool IsAlignmentMember { get; init; }
    public bool IsTailSlopAlignmentMember { get; init; }
    public TypeSymbol? Type { get; init; }
}
