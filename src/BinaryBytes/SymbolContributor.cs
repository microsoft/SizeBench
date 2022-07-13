namespace BinaryBytes;

internal class SymbolContributor
{
    public string LibraryName { get; private set; }
    public string CompilandName { get; private set; }

    public SymbolContributor(string libname, string objectfilename)
    {
        this.LibraryName = libname;
        this.CompilandName = objectfilename;
    }
}
