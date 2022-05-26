namespace SizeBench.AnalysisEngine.DIAInterop;

[Flags]
internal enum NameSearchOptions : uint
{
    nsNone = 0,
    nsfCaseSensitive = 0x1,
    nsfCaseInsensitive = 0x2,
    nsfNameExt = 0x4, // Treats names as paths and applies a filename.ext name match
    nsfRegularExpression = 0x8, // Applies a case-sensitive name match using asterisks (*) and question marks (?) as wildcards
    nsfUndecoratedName = 0x10 // Applies only to symbols that have both decorated and undecorated names
}
