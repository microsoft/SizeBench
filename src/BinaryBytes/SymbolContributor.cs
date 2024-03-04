namespace BinaryBytes;

internal sealed class SymbolContributor
{
    public static SymbolContributor Default { get; } = new SymbolContributor(string.Empty, string.Empty);

    public string LibraryName { get; private set; }
    public string CompilandName { get; private set; }

    public SymbolContributor(string libname, string objectfilename)
    {
        this.LibraryName = libname;
        this.CompilandName = objectfilename;
    }
}
