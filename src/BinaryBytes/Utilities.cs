using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;

namespace BinaryBytes;

internal static class Utilities
{
    /// <summary>
    /// Given a PDB path this routine tries to find the assiociated binary's PE file path (if not specified already),
    /// by looking up for the file under the local directory. If the PDB path specified is from Winbuilds share then
    /// this routine gets the binary path from the bin chunk of that build.
    /// </summary>
    internal static string? InferBinaryPath(string pdbFilePath, string? binaryFilePath, string? customBinaryDirectory = null)
    {
        string? binaryPath;
        if (!String.IsNullOrEmpty(binaryFilePath))
        {
            binaryPath = binaryFilePath;
        }
        else
        {
            binaryPath = InferBinaryPathFromPDBPathIfPossible(pdbFilePath, customBinaryDirectory);
        }

        return binaryPath;
    }

    private static string? InferBinaryPathFromPDBPathIfPossible(string pdbPath, string? customBinaryDirectory)
    {
        // TODO: this should use the IBinaryLocator interface and find all the locators that way instead of hardcoding the local build...

        string[] extensionsSupported = ["dll", "exe", "efi", "sys", "pyd"];

        string? binaryPath = null;
        string? possibleBinaryPath;
        foreach (var extension in extensionsSupported)
        {
            var binaryFileName = Path.GetFileNameWithoutExtension(pdbPath) + "." + extension;
            // Check for the path in customBinaryDirectory if one specified
            if (!String.IsNullOrEmpty(customBinaryDirectory) && File.Exists(Path.Combine(customBinaryDirectory, binaryFileName)))
            {
                binaryPath = Path.Combine(customBinaryDirectory, binaryFileName);
                break;
            }
            else
            {
                possibleBinaryPath = Path.ChangeExtension(pdbPath, extension);
                if (File.Exists(possibleBinaryPath))
                {
                    binaryPath = possibleBinaryPath;
                    break;
                }
            }
        }

        return binaryPath;
    }

    internal static BytesItem CreatePaddingBytesItem(string name, string coffgroup, uint rva, ulong virtualSize)
    {
        return new BytesItem()
        {
            IsPadding = true,
            Name = name,
            CoffGroupName = coffgroup,
            RVA = rva,
            VirtualSize = virtualSize,
            LibraryFilename = String.Empty,
            CompilandName = String.Empty,
        };
    }

    internal static BytesItem CreateSpecialBytesItem(string name, string coffgroup, uint rva, ulong virtualSize, SymbolContributor contributor)
    {
        return new BytesItem()
        {
            IsPadding = false,
            Name = name,
            CoffGroupName = coffgroup,
            RVA = rva,
            VirtualSize = virtualSize,
            LibraryFilename = contributor.LibraryName,
            CompilandName = contributor.CompilandName,
        };
    }

    internal static BytesItem CreateSymbolsBytesItem(ISymbol symbol, string coffgroup, SymbolContributor contributor)
    {
        var functionSymbol = symbol switch
        {
            IFunctionCodeSymbol f => f,
            CodeBlockSymbol b => b.ParentFunction,
            _ => null
        };

        return new BytesItem()
        {
            IsPadding = false,
            Name = symbol.Name,
            CoffGroupName = coffgroup,
            RVA = symbol.RVA,
            VirtualSize = symbol.VirtualSize,
            LibraryFilename = contributor.LibraryName,
            CompilandName = contributor.CompilandName,
            IsPGO = functionSymbol?.IsPGO ?? false,
            IsOptimizedForSpeed = functionSymbol?.IsOptimizedForSpeed ?? false,
        };
    }

    internal static Dictionary<uint, SymbolContributor> CreateRvaToContributorMap(IReadOnlyCollection<Compiland> compilands)
    {
        var rvaToContributorMap = new Dictionary<uint, SymbolContributor>();

        foreach (var compiland in compilands)
        {
            foreach (var coffgroupContribution in compiland.COFFGroupContributions)
            {
                var compilandName = coffgroupContribution.Value.Compiland.ShortName;
                var libName = coffgroupContribution.Value.Compiland.Lib.ShortName;
                foreach (var rvaRange in coffgroupContribution.Value.RVARanges)
                {
                    var rva = rvaRange.RVAStart;
                    rvaToContributorMap.Add(rva, new SymbolContributor(libName, compilandName));
                }
            }
        }

        return rvaToContributorMap;
    }

    internal static SymbolContributor GetContributorForRva(uint rva, Dictionary<uint, SymbolContributor> rvaToContributorMap)
    {
        if (rvaToContributorMap.TryGetValue(rva, out var value))
        {
            return value;
        }

        return SymbolContributor.Default;
    }
}
