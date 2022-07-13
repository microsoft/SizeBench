namespace BinaryBytes;

public class BytesItem
{
    public string CoffgroupName { get; set; } = String.Empty;
    public string Name { get; set; } = String.Empty;
    public uint RVA { get; set; }
    public ulong Size { get; set; }
    public string LibraryFilename { get; set; } = String.Empty;
    public string CompilandName { get; set; } = String.Empty;
    public bool IsPadding { get; set; }
    public bool IsPGO { get; set; }
    public bool IsOptimizedForSpeed { get; set; }
}
