namespace SizeBench.PathLocators;

public interface IBinaryLocator
{
    bool TryInferBinaryPathFromPDBPath(string pdbPath, out string binaryPath);
    bool TryInferPDBPathFromBinaryPath(string binaryPath, out string pdbPath);
}
