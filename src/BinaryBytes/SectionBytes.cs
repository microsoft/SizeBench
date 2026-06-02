namespace BinaryBytes;

internal sealed class SectionBytes
{
    public string SectionName { get; set; } = String.Empty;
    public IEnumerable<BytesItem> Items { get; set; } = Array.Empty<BytesItem>();
}
