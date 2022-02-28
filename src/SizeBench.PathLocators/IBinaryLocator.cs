namespace SizeBench.PathLocators;

public interface IBinaryLocator
{
    bool TryInferBinaryPathFromPDBPath(string pdbPath, out string binaryPath);
}