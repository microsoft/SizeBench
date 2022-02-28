using System.IO;
using SizeBench.PathLocators;

namespace SizeBench.LocalBuild;

public class LocalBuildPathLocator : IBinaryLocator
{
    public bool TryInferBinaryPathFromPDBPath(string pdbPath, out string binaryPath)
    {
        if (pdbPath is null)
        {
            binaryPath = String.Empty;
            return false;
        }

        string[] extensionsSupported = { "dll", "exe", "efi", "sys", "pyd" };
        foreach (var extension in extensionsSupported)
        {
            var possibleBinaryPath = Path.ChangeExtension(pdbPath, extension);
            if (File.Exists(possibleBinaryPath))
            {
                binaryPath = possibleBinaryPath;
                return true;
            }
        }

        // Clang seems to build PDBs with names like "msedge.dll.pdb" by just appending .pdb to the filename,
        // so let's recognize that style too.
        var lastIndexOfDotPdb = pdbPath.LastIndexOf(".pdb", StringComparison.OrdinalIgnoreCase);
        if (lastIndexOfDotPdb > 0)
        {
            var clangPossibleBinaryPath = pdbPath.Remove(lastIndexOfDotPdb);
            if (clangPossibleBinaryPath != pdbPath && File.Exists(clangPossibleBinaryPath))
            {
                binaryPath = clangPossibleBinaryPath;
                return true;
            }
        }

        binaryPath = String.Empty;
        return false;
    }
}