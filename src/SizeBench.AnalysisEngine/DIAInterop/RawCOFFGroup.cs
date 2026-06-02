using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace SizeBench.AnalysisEngine.DIAInterop;

[DebuggerDisplay("RawCOFFGroup: {Name}, RVA={RVAStart}, Size={Length}")]
internal sealed class RawCOFFGroup
{
    public string Name { get; }
    public uint Length { get; private set; }
    public uint RVAStart { get; }
    public SectionCharacteristics Characteristics { get; }

    public RawCOFFGroup(string name, uint length, uint rva, SectionCharacteristics characteristics)
    {
        this.Name = name;
        this.Length = length;
        this.RVAStart = rva;
        this.Characteristics = characteristics;
    }

    public void ExpandToInclude(uint rva, uint length)
    {
        // There might be padding between these so we can't just add the length, we need to calculate the length as (new final RVA - original starting RVA)
        Debug.Assert(rva > this.RVAStart);
        var newLength = rva + length - this.RVAStart;
        this.Length = newLength;
    }
}
