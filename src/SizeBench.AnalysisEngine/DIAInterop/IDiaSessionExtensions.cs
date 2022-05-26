using System.Diagnostics;
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

                return (CompilandLanguage)detailsSymbol.language;
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumCompilandDetails);
        }

        return (CompilandLanguage)Int32.MaxValue;
    }

    #region Enumerate IMAGE_SECTION_HEADERs

    // TODO: consider replacing some or all of the IMAGE_SECTION_HEADER code here with System.Reflection.PortableExecutable that exists now in .NET Core

    public static IEnumerable<IMAGE_SECTION_HEADER> EnumerateImageSectionHeaders(this IDiaSession session, CancellationToken token)
    {
        var sectionHeadersData = session.EnumerateDebugStreamData(token).FirstOrDefault(data => data.name == "SECTIONHEADERS");

        return sectionHeadersData?.EnumerateImageSectionHeaders(token) ?? Enumerable.Empty<IMAGE_SECTION_HEADER>();
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

    private static IEnumerable<IMAGE_SECTION_HEADER> EnumerateImageSectionHeaders(this IDiaEnumDebugStreamData enumDebugStreamData,
                                                                                  CancellationToken token)
    {
        var handCoded = (IDiaEnumDebugStreamDataHandCoded)enumDebugStreamData;

        var output = new byte[Marshal.SizeOf<IMAGE_SECTION_HEADER>()];

        var celtSectionHeader = handCoded.Next(1, Marshal.SizeOf<IMAGE_SECTION_HEADER>(), out var bytesRead, output);
        while (celtSectionHeader == 1 && bytesRead == Marshal.SizeOf<IMAGE_SECTION_HEADER>())
        {
            token.ThrowIfCancellationRequested();

            yield return MarshalSectionHeader(output);

            celtSectionHeader = handCoded.Next(1, Marshal.SizeOf<IMAGE_SECTION_HEADER>(), out bytesRead, output);
        }
    }

    private static IMAGE_SECTION_HEADER MarshalSectionHeader(byte[] bytes)
    {
        unsafe
        {
            fixed (byte* bp = bytes)
            {
                return Marshal.PtrToStructure<IMAGE_SECTION_HEADER>((IntPtr)bp);
            }
        }
    }

    #endregion

    #region Enumerate COFF Groups

    public static IEnumerable<IDiaSymbol> EnumerateCoffGroupSymbols(this IDiaSession session, CancellationToken token)
    {
        session.globalScope.findChildren(SymTagEnum.SymTagCompiland, "* Linker *", 0 /* nsNone */, out var compilandEnum);

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
                    yield return coffGroup;
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
                yield return contrib;
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
