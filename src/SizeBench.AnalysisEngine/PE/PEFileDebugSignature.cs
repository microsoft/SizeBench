namespace SizeBench.AnalysisEngine.PE;

public sealed record class PEFileDebugSignature
{
    public Guid PdbGuid { get; }
    public uint Age { get; }
    public string PdbPath { get; }

    internal PEFileDebugSignature(Guid guid, uint age, string pdbPath)
    {
        this.PdbGuid = guid;
        this.Age = age;
        this.PdbPath = pdbPath;
    }
}
