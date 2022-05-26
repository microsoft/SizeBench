using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

public enum StringSymbolType
{
    ANSI = 0,
    Unicode = 1
}

[DebuggerDisplay("String Symbol, StringData={StringData}, Size={Size}")]
public sealed class StringSymbol : PublicSymbol
{
    public StringSymbolType StringType { get; }
    public string StringData { get; }

    internal StringSymbol(SessionDataCache cache,
                          string name,
                          string stringData,
                          bool isUnicodeString,
                          uint rva,
                          uint size,
                          bool isVirtualSize,
                          uint symIndexId,
                          uint targetRva) : base(cache, GetFriendlyName(name, isUnicodeString, stringData), rva, size, isVirtualSize, symIndexId, targetRva)
    {
        this.StringType = isUnicodeString ? StringSymbolType.Unicode : StringSymbolType.ANSI;
        this.StringData = stringData;
    }

    private static string GetFriendlyName(string name, bool isUnicodeString, string stringData)
    {
        if (isUnicodeString)
        {
            return $"{name}: L\"{stringData}\"";
        }
        else
        {
            return $"{name}: \"{stringData}\"";
        }
    }
}
