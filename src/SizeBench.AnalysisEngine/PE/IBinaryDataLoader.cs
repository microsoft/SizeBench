namespace SizeBench.AnalysisEngine.PE;

internal interface IBinaryDataLoader
{
    string LoadStringByRVA(long RVA, ulong length, out bool isUnicodeString);
}
