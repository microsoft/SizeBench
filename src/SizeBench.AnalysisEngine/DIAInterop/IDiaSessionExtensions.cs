using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Dia2Lib;
using SizeBench.AnalysisEngine.PE;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DIAInterop;

// This class is only here to give a convenient place to write adapter code so that DIA's weird way of enumerating turns into nice .NET
// IEnumerable style enumerations which work better with LINQ queries, foreach loops, and so on.
// This class should not contain meaningful logic, just simple wrapping of DIA's API for better consumption in primarily the DIAAdapter.
internal static class IDiaSessionExtensions
{
    public static IDiaSymbol? findFirstSymbolByName(this IDiaSession diaSession, IDiaSymbol scope, SymTagEnum symTag, string name)
    {
        var nameSearchOptions = NameSearchOptions.nsNone;
        if (symTag == SymTagEnum.SymTagPublicSymbol)
        {
            nameSearchOptions = NameSearchOptions.nsfUndecoratedName;
        }

        diaSession.findChildren(scope, symTag, name, (uint)nameSearchOptions, out var enumSymbols);
        while (true)
        {
            enumSymbols.Next(1, out var symbol, out var celt);
            if (celt != 1)
            {
                break;
            }

            return symbol;
        }

        return null;
    }

    public static IDiaSymbol findVoidBasicType(this IDiaSession diaSession)
    {
        diaSession.findChildren(diaSession.globalScope, SymTagEnum.SymTagBaseType, null, 0, out var enumBasicTypes);
        while (true)
        {
            enumBasicTypes.Next(1, out var basicType, out var celt);
            if (celt != 1)
            {
                break;
            }

            if ((BasicTypes)basicType.baseType == BasicTypes.btVoid)
            {
                return basicType;
            }
        }

        throw new InvalidOperationException("Unable to locate the symbol for the basic 'void' type.  This is a bug in SizeBench's implementation, not your usage of it.");
    }

    public static CompilandLanguage LanguageOfSymbolAtRva(this IDiaSession diaSession, uint rva)
    {
        diaSession!.findSymbolByRVA(rva, SymTagEnum.SymTagNull, out var findCompilandSymbol);
        while (findCompilandSymbol != null && (SymTagEnum)findCompilandSymbol.symTag != SymTagEnum.SymTagCompiland)
        {
            findCompilandSymbol = findCompilandSymbol.lexicalParent;
        }

        if (findCompilandSymbol is null)
        {
            return CompilandLanguage.Unknown;
        }

        // TODO: consolidate this with the new place we look up CompilandDetails if it makes sense
        // Now we need to get the CompilandDetails from the Compiland
        diaSession.findChildren(findCompilandSymbol, SymTagEnum.SymTagCompilandDetails, null, 0 /* compare flags */, out var enumCompilandDetails);
        try
        {
            Debug.Assert(enumCompilandDetails.count == 1);

            while (true)
            {
                enumCompilandDetails.Next(1, out var detailsSymbol, out var celt);
                if (celt != 1)
                {
                    break;
                }

                var language = (CompilandLanguage)detailsSymbol.language;
                var toolName = detailsSymbol.compilerName;

                if (language == CompilandLanguage.CV_CFL_C &&
                    toolName.StartsWith("zig", StringComparison.Ordinal))
                {
                    language = CompilandLanguage.SizeBench_Zig;
                }

                return language;
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumCompilandDetails);
        }

        return (CompilandLanguage)Int32.MaxValue;
    }

    public static IEnumerable<IDiaEnumDebugStreamData> EnumerateDebugStreamData(this IDiaSession session, CancellationToken token)
    {
        session.getEnumDebugStreams(out var enumDebugStreams);

        foreach (IDiaEnumDebugStreamData? enumDebugStreamData in enumDebugStreams)
        {
            token.ThrowIfCancellationRequested();

            if (enumDebugStreamData != null)
            {
                yield return enumDebugStreamData;
            }
        }
    }


    #region Enumerate COFF Groups

    public static IEnumerable<RawCOFFGroup> EnumerateCoffGroupSymbols(this IDiaSession session, IPEFile peFile, CancellationToken token)
    {
        session.globalScope.findChildren(SymTagEnum.SymTagCompiland, "* Linker *", 0 /* nsNone */, out var compilandEnum);

        var diaCoffGroupRanges = new List<RVARange>(100);

        foreach (IDiaSymbol? compiland in compilandEnum)
        {
            token.ThrowIfCancellationRequested();

            if (compiland is null)
            {
                continue;
            }

            compiland.findChildren(SymTagEnum.SymTagCoffGroup, null, 0 /* nsNone */, out var coffGroupEnum);

            foreach (IDiaSymbol? coffGroup in coffGroupEnum)
            {
                token.ThrowIfCancellationRequested();

                if (coffGroup is null)
                {
                    continue;
                }

                // Length of a COFF Group can be 0 legitimately.  An example is ".edata" (export
                // data), which the linker always creates, but may not have any data in it, depending
                // on exactly what's being linked in.  We don't want to track 0-length COFF Groups
                // since they can share an RVA with a "real" (lengthy) COFF Group and that's confusing.
                if (coffGroup.length > 0)
                {
                    diaCoffGroupRanges.Add(RVARange.FromRVAAndSize(coffGroup.relativeVirtualAddress, (uint)coffGroup.length));
                    yield return new RawCOFFGroup(coffGroup.name, (uint)coffGroup.length, coffGroup.relativeVirtualAddress, (SectionCharacteristics)coffGroup.characteristics);
                }
            }

            var discoveredSet = RVARangeSet.FromListOfRVARanges(diaCoffGroupRanges, maxPaddingToMerge: 8);

            // lld-link sometimes leaves 'holes' between SymTagCoffGroup symbols that can be filled in by PE directories that we have already
            // parsed, or by certain well-known tables like the .gfids/.giats tables.  We'll synthesize COFF Groups in this case, though arguably
            // this is a bug in lld-link's PDB generation.
            foreach (var directory in peFile.PEDirectorySymbols)
            {
                var directoryRVARange = RVARange.FromRVAAndSize(directory.RVA, directory.Size);
                if (!discoveredSet.AtLeastPartiallyOverlapsWith(directoryRVARange))
                {
                    // We don't know what the characteristics ought to be, so we just won't specify any.
                    yield return new RawCOFFGroup(directory.COFFGroupFallbackName, directory.Size, directory.RVA, 0);
                }
            }

            if (peFile.GFIDSTable != null)
            {
                var gfidsRange = RVARange.FromRVAAndSize(peFile.GFIDSTable.RVA, peFile.GFIDSTable.Size);
                if (!discoveredSet.AtLeastPartiallyOverlapsWith(gfidsRange))
                {
                    yield return new RawCOFFGroup(".gfids", peFile.GFIDSTable.Size, peFile.GFIDSTable.RVA, 0);
                }
            }

            if (peFile.GIATSTable != null)
            {
                var giatsRange = RVARange.FromRVAAndSize(peFile.GIATSTable.RVA, peFile.GIATSTable.Size);
                if (!discoveredSet.AtLeastPartiallyOverlapsWith(giatsRange))
                {
                    yield return new RawCOFFGroup(".giats", peFile.GIATSTable.Size, peFile.GIATSTable.RVA, 0);
                }
            }

            var i = 0;
            foreach (var importThunksRange in peFile.DelayLoadImportThunksRVARanges)
            {
                if (!discoveredSet.AtLeastPartiallyOverlapsWith(importThunksRange))
                {
                    var synthesizedName = i == 0 ? ".sizebench-synthesized-delay-load-import-thunks" : $".sizebench-synthesized-delay-load-import-thunks-{i}";
                    i++;
                    yield return new RawCOFFGroup(synthesizedName, importThunksRange.Size, importThunksRange.RVAStart, 0);
                }
            }

            i = 0;
            foreach (var importStringsRange in peFile.DelayLoadImportStringsRVARanges)
            {
                if (!discoveredSet.AtLeastPartiallyOverlapsWith(importStringsRange))
                {
                    var synthesizedName = i == 0 ? ".sizebench-synthesized-delay-load-import-strings" : $".sizebench-synthesized-delay-load-import-strings-{i}";
                    i++;
                    yield return new RawCOFFGroup(synthesizedName, importStringsRange.Size, importStringsRange.RVAStart, 0);
                }
            }

            i = 0;
            foreach (var importModuleHandlesRange in peFile.DelayLoadModuleHandlesRVARanges)
            {
                if (!discoveredSet.AtLeastPartiallyOverlapsWith(importModuleHandlesRange))
                {
                    var synthesizedName = i == 0 ? ".sizebench-synthesized-delay-load-module-handles" : $".sizebench-synthesized-delay-load-module-handles-{i}";
                    i++;
                    yield return new RawCOFFGroup(synthesizedName, importModuleHandlesRange.Size, importModuleHandlesRange.RVAStart, 0);
                }
            }
        }
    }

    #endregion

    #region Enumerate UDTs

    public static IEnumerable<IDiaSymbol> EnumerateUDTSymbols(this IDiaSession session)
        => session.EnumerateUDTSymbolsByName(null);

    public static IEnumerable<IDiaSymbol> EnumerateUDTSymbolsByName(this IDiaSession session, string? name)
    {
        var nameSearchOptions = name is null ?
                                NameSearchOptions.nsNone :
                                NameSearchOptions.nsfRegularExpression | NameSearchOptions.nsfCaseInsensitive | NameSearchOptions.nsfUndecoratedName;

        session.globalScope.findChildren(SymTagEnum.SymTagUDT, name: name, compareFlags: (uint)nameSearchOptions, ppResult: out var udtEnum);

        foreach (IDiaSymbol? udt in udtEnum)
        {
            if (udt is null)
            {
                yield break;
            }

            // UDTs can end up in a binary multiple times for each of their cv-qualified versions.  So if you have a UDT called "Point"
            // you will get UDT entries in the PDB for "Point" and "const Point" and "volatile Point" and so on.  SizeBench really doesn't care
            // about this level of specificity, so we're going to ignore everything except those the unmodified versions of a type
            // In DIA-speak, "unmodified" means "without cv-qualifiers".
            // Thus, if this UDT has an unmodified type beneath it, we'll just ignore it - and we'll get the unmodified one at some point while
            // enumerating anyway.
            if (udt.unmodifiedType is null)
            {
                yield return udt;
            }
        }
    }

    #endregion

    #region Enumerate Raw Section Contributions

    public static IEnumerable<IDiaSectionContrib> EnumerateSectionContributions(this IDiaSession session, ILogger logger)
    {
        var enumSectionContribs = session.FindTable<IDiaEnumSectionContribs>(logger);

        if (enumSectionContribs is null)
        {
            yield break;
        }

        foreach (IDiaSectionContrib? contrib in enumSectionContribs)
        {
            if (contrib != null)
            {
                // Sometimes LLD records contribs with zero size - we can ignore these as there is nothing interesting about
                // something without a size.
                if (contrib.length != 0)
                {
                    yield return contrib;
                }
            }
            else
            {
                yield break;
            }
        }
    }

    #endregion

    #region Enumerate Raw Source Files

    public static IEnumerable<IDiaSourceFile> EnumerateDiaSourceFiles(this IDiaSession session, ILogger logger)
    {
        var enumSourceFiles = session.FindTable<IDiaEnumSourceFiles>(logger);

        if (enumSourceFiles is null)
        {
            yield break;
        }

        foreach (IDiaSourceFile? sourceFile in enumSourceFiles)
        {
            if (sourceFile != null)
            {
                yield return sourceFile;
            }
            else
            {
                yield break;
            }
        }
    }

    #endregion

    #region DIA Tables

    private static T? FindTable<T>(this IDiaSession session, ILogger logger) where T : class
    {
        session.getEnumTables(out var enumTables);

        logger.Log($"Searching for table of type {typeof(T).Name}");

        try
        {
            foreach (IDiaTable? table in enumTables)
            {
                if (table is null)
                {
                    continue;
                }

                if (table is T tableOfType)
                {
                    logger.Log("Found the right table");
                    return tableOfType;
                }

                Marshal.FinalReleaseComObject(table);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumTables);
        }

        logger.Log("No such table found, this isn't going to go well...");
        return null;
    }

    #endregion
}
