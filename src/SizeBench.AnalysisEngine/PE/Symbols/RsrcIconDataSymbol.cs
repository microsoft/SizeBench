using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcIconDataSymbol : RsrcDataSymbol
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public ushort Width { get; }
    public ushort Height { get; }
    public ushort BitsPerPixel { get; }
    internal RsrcIconDataSymbol(uint rva, uint size, string language, Win32ResourceType rsrcType, string? rsrcTypeName, string dataName, ushort width, ushort height, ushort bpp)
        : base(rva, size, language, rsrcType, rsrcTypeName, dataName, nameSuffix: $" {width}x{height} {bpp}bpp")
    {
        this.Width = width;
        this.Height = height;
        this.BitsPerPixel = bpp;
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not RsrcIconDataSymbol otherRsrcSymbol)
        {
            return false;
        }

        return this.Width == otherRsrcSymbol.Width &&
               this.Height == otherRsrcSymbol.Height &&
               this.BitsPerPixel == otherRsrcSymbol.BitsPerPixel &&
               base.IsVeryLikelyTheSameAs(otherSymbol);
    }
}
