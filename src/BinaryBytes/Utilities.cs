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
        // TODO: this should use the IBinaryLocator interface and find all the locators that way instead of hardcoding the local build and windows build...

        string[] extensionsSupported = { "dll", "exe", "efi", "sys", "pyd" };

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

    internal static BytesItem CreatePaddingBytesItem(string name, string coffgroup, uint rva, ulong size)
    {
        return new BytesItem()
        {
            IsPadding = true,
            Name = name,
            CoffgroupName = coffgroup,
            RVA = rva,
            Size = size,
            LibraryFilename = String.Empty,
            CompilandName = String.Empty,
            IsPGO = false,
            IsOptimizedForSpeed = false
        };
    }

    internal static BytesItem CreateSpecialBytesItem(string name, string coffgroup, uint rva, ulong size, SymbolContributor contributor)
    {
        return new BytesItem()
        {
            IsPadding = false,
            Name = name,
            CoffgroupName = coffgroup,
            RVA = rva,
            Size = size,
            LibraryFilename = contributor.LibraryName,
            CompilandName = contributor.CompilandName,
            IsPGO = false,
            IsOptimizedForSpeed = false
        };
    }

    internal static BytesItem CreateSymbolsBytesItem(ISymbol symbol, string coffgroup, SymbolContributor contributor)
    {
        var functionSymbol = symbol as IFunctionCodeSymbol;
        if (symbol is CodeBlockSymbol block)
        {
            functionSymbol = block.ParentFunction;
        }

        return new BytesItem()
        {
            IsPadding = false,
            Name = symbol.Name,
            CoffgroupName = coffgroup,
            RVA = symbol.RVA,
            Size = symbol.Size,
            LibraryFilename = contributor.LibraryName,
            CompilandName = contributor.CompilandName,
            IsPGO = functionSymbol?.IsPGO ?? false,
            IsOptimizedForSpeed = functionSymbol?.IsOptimizedForSpeed ?? false
        };
    }

    internal static Dictionary<uint, SymbolContributor> CreateRvaToContributorMap(IReadOnlyList<Compiland> compilands)
    {
        var rvaToContributorMap = new Dictionary<uint, SymbolContributor>();

        foreach (var compiland in compilands)
        {
            foreach (var coffgroupContribution in compiland.COFFGroupContributions)
            {
                var compilandName = coffgroupContribution.Value.Compiland.Name.Split('\\').Last();
                var libName = coffgroupContribution.Value.Compiland.Lib.Name.Split('\\').Last();
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
        var rvaContributor = new SymbolContributor(String.Empty, String.Empty);
        if (rvaToContributorMap.ContainsKey(rva))
        {
            var libname = rvaToContributorMap[rva]?.LibraryName ?? String.Empty;
            var compilandName = rvaToContributorMap[rva]?.CompilandName ?? String.Empty;
            rvaContributor = new SymbolContributor(libname, compilandName);
        }

        return rvaContributor;
    }
}
