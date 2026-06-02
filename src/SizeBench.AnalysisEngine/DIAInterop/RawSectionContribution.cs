namespace SizeBench.AnalysisEngine.DIAInterop;

internal readonly struct RawSectionContribution
{
    public RawSectionContribution(string libName, string compilandName, uint compilandSymIndexId, uint rva, uint length)
    {
        ArgumentNullException.ThrowIfNull(libName);
        ArgumentNullException.ThrowIfNull(compilandName);

        this.LibName = libName;
        this.CompilandName = compilandName;
        this.CompilandSymIndexId = compilandSymIndexId;
        this.RVA = rva;
        this.Length = length;
    }

    public readonly string LibName;
    public readonly string CompilandName;
    public readonly uint CompilandSymIndexId;
    public readonly uint RVA;
    public readonly uint Length;
}
