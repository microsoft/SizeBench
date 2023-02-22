using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dia2Lib;
using SizeBench.AnalysisEngine.COMInterop;
using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DIAInterop;

internal static class CancelExtention
{
    public static IEnumerable<T> WithCancellation<T>(this IEnumerable<T> en, CancellationToken token)
    {
        foreach (var item in en)
        {
            token.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}

internal static class LoggingExtension
{
    public static IEnumerable<T> WithLogging<T>(this IEnumerable<T> en, ILogger parentLogger, string nameOfTheseObjects, [CallerMemberName] string callerMemberName = "")
    {
        using var log = parentLogger.StartTaskLog($"Enumerating {nameOfTheseObjects}", callerMemberName);
        var count = 0;
        foreach (var item in en)
        {
            count++;
            yield return item;
        }

        log.Log($"Finished enumerating {count} {nameOfTheseObjects}.", LogLevel.Info, callerMemberName);
    }
}

internal sealed class DIAAdapter : IDIAAdapter, IDisposable
{
    // Once we load DIA we never try to unload it, because it's very hard to deterministically collect all the COM objects before we unload the DLL.  That's why this is static.
    private static readonly LibraryModule _diaLibraryModule = LibraryModule.LoadModule(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", @"msdia140.dll"));
    private IDiaDataSource? _diaDataSource;
    private Session? _session;
    private SessionDataCache? _cache;
    private IDiaSession? _diaSession;
    private IDiaSymbol? _globalScope;
    private uint _fileAlignment;
    private uint _sectionAlignment;
    private readonly int _affinitizedThreadId;

    private void ThrowIfOnWrongThread()
    {
        if (Environment.CurrentManagedThreadId != this._affinitizedThreadId)
        {
            throw new InvalidOperationException($"This operation is not permitted on this thread.  This object expects to only interact with DIA on ManagedThreadId {this._affinitizedThreadId}, but it is being called on ManagedThreadId {Environment.CurrentManagedThreadId}.  This is a bug in SizeBench, not in your usage of it.");
        }
    }

    // There are several fields of this type that we want to access a lot without checking for null or using "!" everywhere, so these properties let us do that safely.
    // These can only be null during/after disposal, so these properties all throw if we're disposing.
    private IDiaSession DiaSession
    {
        get
        {
            ThrowIfDisposingOrDisposed();
            return this._diaSession!;
        }
    }

    private IDiaSymbol DiaGlobalScope
    {
        get
        {
            ThrowIfDisposingOrDisposed();
            return this._globalScope!;
        }
    }

    private SessionDataCache DataCache
    {
        get
        {
            ThrowIfDisposingOrDisposed();
            return this._cache!;
        }
    }

    private Session Session
    {
        get
        {
            ThrowIfDisposingOrDisposed();
            return this._session!;
        }
    }

    public static readonly Guid Dia140Clsid = new Guid("{E6756135-1E65-4D17-8576-610761398C3C}");

    #region Construction, opening, all the startup-y things

    internal DIAAdapter(Session session, string pdbPath)
    {
        this._session = session;
        this._cache = session.DataCache;
        this._affinitizedThreadId = Environment.CurrentManagedThreadId;

        try
        {
            this._diaDataSource = CoClassLoaderRegFree.CreateInstance<IDiaDataSource>(_diaLibraryModule, Dia140Clsid);

            this._diaDataSource.loadDataFromPdb(pdbPath);

            this._diaDataSource.openSession(out this._diaSession);
        }
        catch (COMException comException)
        {
            // If this is one of the well-known DIA HRESULTs at least we can return a friendly string that
            // the CLR can't since it doesn't know about these.
            var diaHRESULTValues = Enum.GetValues(typeof(DIAHRESULTs));
            for (var i = 0; i < diaHRESULTValues.Length; i++)
            {
                if (((uint)comException.HResult) == Convert.ToUInt32(diaHRESULTValues.GetValue(i), CultureInfo.InvariantCulture.NumberFormat))
                {
                    throw new PDBNotSuitableForAnalysisException($"Unable to open PDB from '{session.PdbPath}'" + Environment.NewLine +
                                                                 $"DIA returned this error: {Enum.GetName((DIAHRESULTs)comException.HResult)}", comException);

                }
            }
            throw;
        }

        this._globalScope = this._diaSession.globalScope;
        var machineType = this._globalScope.machineType;

        // See http://msdn.microsoft.com/en-us/library/windows/desktop/ms680313(v=vs.85).aspx
        switch (machineType)
        {
            case 0x014c: // IMAGE_FILE_MACHINE_I386
            case 0x8664: // IMAGE_FILE_MACHINE_AMD64
            case 0x01c4: // IMAGE_FILE_MACHINE_ARM
            case 0xAA64: // IMAGE_FILE_MACHINE_ARM64
                         // We know how to parse these, it's cool to go on
                break;
            case 0: // Machine type not set in the PDB - ngen seems to do this, maybe other toolchains do too?
                throw new BinaryNotAnalyzableException("This binary does not have a machine type set in the PDB.  SizeBench does not yet know how to analyze this code.  Is this an ngen'd binary?");
            case 0xC0EE: // IMAGE_FILE_MACHINE_CEE, aka managed code
                throw new BinaryNotAnalyzableException("This binary appears to contain managed code.  SizeBench does not yet know how to analyze managed code.");
            default:
                throw new InvalidOperationException("Unknown machine type!");
        }
    }

    internal void Initialize(PEFile peFile, ILogger logger)
    {
        ThrowIfOnWrongThread();
        ThrowIfDisposingOrDisposed();

        this._fileAlignment = peFile.FileAlignment;
        this._sectionAlignment = peFile.SectionAlignment;

        ThrowIfPDBNotSuitableForAnalysis();

        // If we found a debug signature, let's try to validate that it matches the PDB being loaded.  If not, then the user
        // has selected a mismatched PDB/Binary pair which isn't a good idea - we'll likely discover weird mismatches later, so
        // instead fail out very early here to make this clear.
        if (this.Session.PEFile!.DebugSignature != null)
        {
            if (this.Session.PEFile.DebugSignature.Guid != this.DiaGlobalScope.guid)
            {
                throw new BinaryAndPDBSignatureMismatchException($"Binary and PDB do not match debug signatures, they appear to be from different builds.  SizeBench requires that a binary and PDB match exactly." + Environment.NewLine +
                                                                 $"Binary guid={this.Session.PEFile.DebugSignature.Guid}, PDB guid={this.DiaGlobalScope.guid}");
            }

            if (this.Session.PEFile.DebugSignature.Age != this.DiaGlobalScope.age)
            {
                throw new BinaryAndPDBSignatureMismatchException($"Binary and PDB do not match debug signatures, they appear to be from different builds.  SizeBench requires that a binary and PDB match exactly." + Environment.NewLine +
                                                                 $"Binary age={this.Session.PEFile.DebugSignature.Age}, PDB age={this.DiaGlobalScope.age}");
            }
        }

        var xdataRVARangeFromCoffGroupsIfAvailable = GetXDataRVARangeFromCoffGroupsIfAvailable();

        // We know that the COFF Groups are populated by this point, because we needed them to get the XDATA RVA Range above.
        // So using these doesn't incur any additional overhead in parsing stuff, just a bit of manipulation of the RVA Ranges
        // to make the RVARangeSet of virtual RVA Ranges needed during symbol parsing (to know if a symbol is in on-disk space
        // or only in virtual space in memory).
        var fullyVirtualRVARanges = new List<RVARange>();
        foreach (var cg in this.DataCache.AllCOFFGroups!)
        {
            if (cg.IsVirtualSizeOnly)
            {
                fullyVirtualRVARanges.Add(RVARange.FromRVAAndSize(cg.RVA, cg.VirtualSize, isVirtualSize: true));
            }
        }

        this.DataCache.RVARangesThatAreOnlyVirtualSize = RVARangeSet.FromListOfRVARanges(fullyVirtualRVARanges, this.Session.PEFile.BytesPerWord);

        // PublicSymbols can be needed at deep parts of the symbol creation callstacks (like getting the length of a vtable).
        // Rather than trying to get it right in every case, we just ensure all PublicSymbols are parsed right here.
#pragma warning disable IDE0063 // Use simple 'using' statement - the scoping of this using helps be clearer about the duration in the log.
        using (var loadAllPublicSymbolsTaskLog = logger.StartTaskLog("Loading all PublicSymbols for ambiguous vtables"))
#pragma warning restore IDE0063 // Use simple 'using' statement
        {
            // This call has the side-effect of putting these PublicSymbols in the SessionDataCache.  Kinda ugly to depend
            // on a side-effect here, but good enough for now.
            FindAllDisambiguatingVTablePublicSymbolNamesByRVA(loadAllPublicSymbolsTaskLog, CancellationToken.None);
        }

#pragma warning disable IDE0063 // Use simple 'using' statement - the scoping of this using helps be clearer about the duration in the log.
        using (var canonicalNamesTaskLog = logger.StartTaskLog("Loading all canonical names after accounting for Identical COMDAT Folding (ICF)"))
#pragma warning restore IDE0063 // Use simple 'using' statement
        {
            FindCanonicalNamesForFoldableRVAs(canonicalNamesTaskLog, CancellationToken.None);
        }

        // It is important that this comes after RVARangesThatAreOnlyVirtualSize is set up, as some of the EH Symbols may need
        // to parse Public Symbols, and before we can do that, we better know what's virtual or real size or else we might
        // start setting up the DataCache with bad data.
        using (var ehParsingLog = logger.StartTaskLog("Parsing Exception Handling symbols"))
        {
            this.Session.PEFile.ParseEHSymbols(this.Session, this, xdataRVARangeFromCoffGroupsIfAvailable, ehParsingLog);
            ehParsingLog.Log($"Found {this.DataCache.PDataSymbolsByRVA!.Count} PDATA symbols and {this.DataCache.XDataSymbolsByRVA!.Count} XDATA symbols.");
        }

        using (logger.StartTaskLog("Parsing Win32 Resources (.rsrc) symbols"))
        {
            this.DataCache.RsrcSymbolsByRVA = this.Session.PEFile.RsrcSymbols;
        }

        this.DataCache.OtherPESymbolsByRVA = this.Session.PEFile.OtherPESymbols;
        this.DataCache.OtherPESymbolsRVARanges = this.Session.PEFile.OtherPESymbolsRVARanges;
    }

    private LinkerCommandLine? GetLinkerCommandLine()
    {
        ThrowIfDisposingOrDisposed();

        var linkerCompilandSymbol = this.DiaSession.findFirstSymbolByName(this.DiaGlobalScope, SymTagEnum.SymTagCompiland, "* Linker *");

        if (linkerCompilandSymbol is null)
        {
            return null;
        }

        return FindCommandLineForCompilandByID(linkerCompilandSymbol.symIndexId) as LinkerCommandLine;
    }

    private void ThrowIfPDBNotSuitableForAnalysis()
    {
        var linkerCommandLine = GetLinkerCommandLine();

        if (AreSymbolsStripped())
        {
            throw new PDBNotSuitableForAnalysisException("This PDB is stripped - it contains only public symbols.  This is not suitable for analysis purposes, a full PDB with private symbols is required.");
        }

        if (IsBBTedBinary())
        {
            throw new PDBNotSuitableForAnalysisException("This PDB is for a binary that has gone through BBT - SizeBench doesn't yet support this.  Please use the pre-BBT'd binary and PDB, which your build system should be producing already.");
        }

        // Older linkers don't record the linker command line at all in a mini-PDB, so we'll catch that last here, to try to catch more specific failure
        // cases with more specific error messages above.
        if (linkerCommandLine is null)
        {
            throw new PDBNotSuitableForAnalysisException("Unable to find the linker command-line used, this binary was probably produced by using /debug:fastlink in your link command - please generate a full PDB with /debug:full for use with SizeBench.");
        }

        if (linkerCommandLine.GetSwitchState(new string[] { "/debug:fastlink" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.LastWins, StringComparison.OrdinalIgnoreCase) == CommandLineSwitchState.SwitchEnabled)
        {
            throw new PDBNotSuitableForAnalysisException("This PDB is a 'Mini PDB' which is not suitable for static analysis purposes.  This PDB was probably produced by using /debug:fastlink in your link command - please generate a full PDB with /debug:full for use with SizeBench.");
        }

        if (linkerCommandLine.IsPGInstrumented)
        {
            throw new PDBNotSuitableForAnalysisException("This binary is a PGI binary, meaning it is instrumented for PGO training by using /ltcg:pgi or /[fast]genprofile on your linker command line.  This is not a useful kind of binary to analyze, and SizeBench has a lot of trouble parsing such binaries.  Please use a regular binary either before or after PGO training has been applied.");
        }

        if (linkerCommandLine is not MSVC_LINK_CommandLine)
        {
            throw new PDBNotSuitableForAnalysisException($"This binary was linked with '{linkerCommandLine.ToolName}'.  Currently, SizeBench requires linking with Microsoft's link.exe as only those PDBs contain sufficient information.  Note that when using clang/lld-link, you can continue to compile with clang, just the link must be done with Microsoft's linker.");
        }

        if (linkerCommandLine.IncrementallyLinked)
        {
            throw new PDBNotSuitableForAnalysisException("This binary is using incremental linking (such as /ltcg:incremental, or /debug without specifying /incremental:no).  This is not valid for SizeBench's static analysis purposes - please use /ltcg or /incremental:no or otherwise ensure a full link happens.");
        }
    }

    private bool AreSymbolsStripped() => this.DiaGlobalScope.isStripped != 0;

    // Returns true if the function has been processed by BBT - the best way I know to detect this is if an OMAPTO or OMAPFROM stream is in the PDB, since that's
    // the tables that BBT inserts.
    private bool IsBBTedBinary()
    {
        var omapToOrFromData = this.DiaSession.EnumerateDebugStreamData(CancellationToken.None).FirstOrDefault(data => data.name is "OMAPTO" or "OMAPFROM");

        if (omapToOrFromData != null)
        {
            return true;
        }

        return false;
    }

    private RVARange? GetXDataRVARangeFromCoffGroupsIfAvailable()
    {
        var sections = this.Session.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None).Result;

        // We are only looking for a COFF group named .xdata whose content parsing needs special handling. 
        // the other .xdata COFF group .xdata$x can be enumerated using DIA, hence not included in this range.
        // This seems like it could be potentially wrong for [cppxdata] stuff that's in .rdata and/or .data (I still don't
        // know why it can end up there sometimes) - but so far in practice this seems to be enough so I guess I'll just
        // live with the mystery of why this is enough...
        return (from bs in sections from COFFGroup cg in bs.COFFGroups.Where(cg => cg.Name == ".xdata") select RVARange.FromRVAAndSize(cg.RVA, cg.Size)).FirstOrDefault();
    }

    #endregion

    #region Finding Binary Sections

    private sealed class RawBinarySection
    {
        public string Name { get; set; }
        public uint Size { get; private set; }
        public uint VirtualSize { get; private set; }
        public uint RVAStart { get; }
        public DataSectionFlags Characteristics { get; }

        public RawBinarySection(string name, uint rva, uint size, uint virtualSize, DataSectionFlags characteristics)
        {
            this.Name = name;
            this.Size = size;
            this.VirtualSize = virtualSize;
            this.RVAStart = rva;
            this.Characteristics = characteristics;
        }

        public void ExpandToInclude(uint rva, uint size, uint virtualSize)
        {
            // There might be padding between these so we can't just add the [virtual]size, we need to calculate the length as (new final RVA - original starting RVA)
            var newSize = rva + size - this.RVAStart;
            var newVirtualSize = rva + virtualSize - this.RVAStart;
            this.Size = newSize;
            this.VirtualSize = newVirtualSize;
        }
    }

    public IEnumerable<BinarySection> FindBinarySections(ILogger parentLogger, CancellationToken token)
    {
        ThrowIfOnWrongThread();
        ThrowIfDisposingOrDisposed();

        if (this.DataCache.AllBinarySections != null)
        {
            return this.DataCache.AllBinarySections;
        }

        // We need the COFF Group names because some section names are too short due to PE file limitations and the COFF Group contains the full
        // name.  This happens especially with some types of code obfuscation.
        var coffGroups = FindCompressedRawCOFFGroups(parentLogger, token);

        var almostFinal = new List<RawBinarySection>(capacity: 20);

        // Sometimes multiple sections in a binary can share a name - obfuscated code can do this, and it seems kernel-mode code sometimes chooses to as well.
        // SizeBench doesn't really want to deal with the complexity of multiple sections with the same name, since we rather regularly key off of name in
        // dictionary lookups, database storage for SKUCrawler, UI in the GUI tool and such.  Because sections are sorted by name, all identically-named 
        // sections will be adjacent anyway, so we'll just smush them together into one big section with that name.
        //
        // Additionally, some binaries have multiple sections with the same name but different characteristics - like sdbus.sys in Windows.  This is
        // allowed, though it does generate the LNK4078 linker warning.  We don't want to merge those sections together because we depend on characteristics
        // sometimes - so if the characteristics differ, we'll conjure up a new name to keep them unique.
        RawBinarySection? pendingSection = null;
        foreach (var section in this.DiaSession.EnumerateImageSectionHeaders(token)
                                               .WithCancellation(token)
                                               .WithLogging(parentLogger, "Binary Sections")
                                               .OrderBy(s => s.VirtualAddress))
        {
            if (pendingSection != null &&
                pendingSection.Name == section.Section &&
                pendingSection.Characteristics == section.Characteristics)
            {
                // If the name is identical to the one before this, merge the length.
                pendingSection.ExpandToInclude(section.VirtualAddress, section.SizeOfRawData, section.VirtualSize);
            }
            else
            {
                if (pendingSection != null)
                {
                    almostFinal.Add(pendingSection);
                    pendingSection = null;
                }

                pendingSection = new RawBinarySection(section.Section, section.VirtualAddress, section.SizeOfRawData, section.VirtualSize, section.Characteristics);
            }
        }

        if (pendingSection != null)
        {
            almostFinal.Add(pendingSection);
        }

        var final = new List<BinarySection>();
        var namesSeen = new List<string>(capacity: 10);

        for (var i = 0; i < almostFinal.Count; i++)
        {
            var finalSection = almostFinal[i];

            if (finalSection.Name.Length == 8)
            {
                // In some binaries, especially obfuscated ones, the section names can exceed the maximum length (8 characters) and be truncated by the linker.
                // So for example, in mfcore.dll there are two sections called "?g_Encry" because both start with "?g_Encrypted".
                // The linker has truncated this data, so the IMAGE_SECTION_HEADERS only contain the truncated data.  If we can find a COFF Group that has the same
                // RVA and start of the name as a section, we'll prefer the COFF Group's name if it's longer.
                foreach (var coffGroup in coffGroups)
                {
                    if (coffGroup.RVAStart == finalSection.RVAStart &&
                        coffGroup.Name.Length > finalSection.Name.Length &&
                        coffGroup.Name.StartsWith(finalSection.Name, StringComparison.Ordinal))
                    {
                        finalSection.Name = coffGroup.Name;
                        break;
                    }
                }
            }

            if (namesSeen.Contains(finalSection.Name))
            {
                // These still have the same name - for that to be possible by the time we end up here, they must have differed in their characteristics, so
                // the final name will take into account the characteristics as a differentiator to ensure unique names.
                finalSection.Name += $" ({(uint)finalSection.Characteristics:X})";
            }

            final.Add(new BinarySection(this._cache, finalSection.Name, finalSection.Size, finalSection.VirtualSize, finalSection.RVAStart, this._fileAlignment, this._sectionAlignment, finalSection.Characteristics));
            namesSeen.Add(finalSection.Name);
        }

        this.DataCache.AllBinarySections = final;

        return this.DataCache.AllBinarySections;
    }

    #endregion

    #region Finding COFF Groups

    private sealed class RawCOFFGroup
    {
        public string Name { get; }
        public uint Length { get; private set; }
        public uint RVAStart { get; }
        public DataSectionFlags Characteristics { get; }

        public RawCOFFGroup(string name, uint length, uint rva, DataSectionFlags characteristics)
        {
            this.Name = name;
            this.Length = length;
            this.RVAStart = rva;
            this.Characteristics = characteristics;
        }

        public void ExpandToInclude(uint rva, uint length)
        {
            // There might be padding between these so we can't just add the length, we need to calculate the length as (new final RVA - original starting RVA)
            Debug.Assert(rva > this.RVAStart);
            var newLength = rva + length - this.RVAStart;
            this.Length = newLength;
        }
    }

    private List<RawCOFFGroup> FindCompressedRawCOFFGroups(ILogger parentLogger, CancellationToken token)
    {
        var almostFinal = new List<RawCOFFGroup>();

        // Some obfuscation technologies cause thousands or tens of thousands of COFF Groups to be created which are all
        // extremely small (hundreds of bytes at the most).  This can cause pathologically bad performance in SizeBench and
        // no consumer can possibly care about a specific one of these COFF Groups' size because their names are not intended to
        // be for human consumption anyway.  So, we'll compress all of these into one COFF Group - the good news is we know that
        // they all get put in a contiguous block within a section, so if we find one of these, we'll smush it together with all
        // the other similar ones.
        //
        // Additionally, some binaries have identically named COFF Groups because they may have sections with identical names but
        // different DataSectionFlags (characteristics).  We'll need to handle that here to ensure every COFF Group has a unique
        // name since we key off the name in so many places.
        var prefixToMerge = String.Empty;
        RawCOFFGroup? pendingRawCG = null;
        foreach (var coffGroup in this.DiaSession.EnumerateCoffGroupSymbols(token)
                                                 .WithCancellation(token)
                                                 .WithLogging(parentLogger, "COFF Groups")
                                                 .OrderBy(cg => cg.relativeVirtualAddress))
        {
            Debug.Assert((SymTagEnum)coffGroup.symTag == SymTagEnum.SymTagCoffGroup);
            if (coffGroup.name.Contains("$wbrd", StringComparison.Ordinal) ||
                coffGroup.name.StartsWith("?g_EncryptedSegment", StringComparison.Ordinal))
            {
                if (coffGroup.name.StartsWith(prefixToMerge, StringComparison.Ordinal) && pendingRawCG != null)
                {
                    // If this is part of the existing section's wbrd COFF Group chunk, we'll merge it.
                    pendingRawCG.ExpandToInclude(coffGroup.relativeVirtualAddress, (uint)coffGroup.length);
                }
                else
                {
                    // We've found the start of a new wbrd COFF Group chunk, so create the 'pseudo-CG' that we begin to merge

                    // First yield the previous one, if there is one.  This way if .data$wbrd123 comes immediately before .rdata$wbrd123, we will yield
                    // back the .data one.
                    if (pendingRawCG != null)
                    {
                        almostFinal.Add(pendingRawCG);
                        pendingRawCG = null;
                        prefixToMerge = String.Empty;
                    }

                    if (coffGroup.name.Contains("$wbrd", StringComparison.Ordinal))
                    {
                        prefixToMerge = coffGroup.name[..(coffGroup.name.IndexOf("$wbrd", StringComparison.Ordinal) + "$wbrd".Length)];
                    }
                    else if (coffGroup.name.StartsWith("?g_EncryptedSegment", StringComparison.Ordinal))
                    {
                        // Example:
                        // ?g_EncryptedSegmentSystemCall_160@WarbirdRuntime@@3U_ENCRYPTION_SEGMENT@1@C
                        prefixToMerge = coffGroup.name[..(coffGroup.name.IndexOf("_", startIndex: "?g_E".Length, StringComparison.Ordinal) + "_".Length)];
                    }
                    pendingRawCG = new RawCOFFGroup(prefixToMerge + "<all>", (uint)coffGroup.length, coffGroup.relativeVirtualAddress, (DataSectionFlags)coffGroup.characteristics);
                }
            }
            else
            {
                if (pendingRawCG != null)
                {
                    almostFinal.Add(pendingRawCG);
                    pendingRawCG = null;
                    prefixToMerge = String.Empty;
                }

                almostFinal.Add(new RawCOFFGroup(coffGroup.name, (uint)coffGroup.length, coffGroup.relativeVirtualAddress, (DataSectionFlags)coffGroup.characteristics));
            }
        }

        if (pendingRawCG != null)
        {
            // In case the last thing we found was one of these COFF Groups, we still need to yield it back.
            almostFinal.Add(pendingRawCG);
        }

        var final = new List<RawCOFFGroup>();
        var namesSeen = new List<string>(capacity: 10);

        for (var i = 0; i < almostFinal.Count; i++)
        {
            var finalCG = almostFinal[i];

            if (namesSeen.Contains(finalCG.Name))
            {
                // These still have the same name - for that to be possible by the time we end up here, they must have differed in their characteristics, so
                // the final name will take into account the characteristics as a differentiator to ensure unique names.
                finalCG = new RawCOFFGroup(finalCG.Name + $" ({(uint)finalCG.Characteristics:X})", finalCG.Length, finalCG.RVAStart, finalCG.Characteristics);
            }

            final.Add(finalCG);
            namesSeen.Add(finalCG.Name);
        }

        return final;
    }

    public IEnumerable<COFFGroup> FindCOFFGroups(ILogger parentLogger, CancellationToken token)
    {
        ThrowIfOnWrongThread();

        if (this.DataCache.AllCOFFGroups is null)
        {
            this.DataCache.AllCOFFGroups = FindCompressedRawCOFFGroups(parentLogger, token)
                                           .Select(cg => new COFFGroup(this.DataCache, cg.Name, cg.Length, cg.RVAStart, this._fileAlignment, this._sectionAlignment, cg.Characteristics))
                                           .ToList();
        }

        return this.DataCache.AllCOFFGroups;
    }

    #endregion

    #region Finding Section Contributions

    public IEnumerable<RawSectionContribution> FindSectionContributions(ILogger parentLogger, CancellationToken token)
    {
        ThrowIfOnWrongThread();

        return this.DiaSession.EnumerateSectionContributions(parentLogger)
                              .WithCancellation(token)
                              .WithLogging(parentLogger, "Section Contributions")
                              .Select(sc => new RawSectionContribution(sc.compiland.libraryName, sc.compiland.name, sc.compilandId, sc.relativeVirtualAddress, sc.length));
    }

    #endregion

    #region Finding Source Files

    public IEnumerable<SourceFile> FindSourceFiles(ILogger parentLogger, CancellationToken token)
    {
        ThrowIfOnWrongThread();

        return this.DiaSession.EnumerateDiaSourceFiles(parentLogger)
                              .WithCancellation(token)
                              .WithLogging(parentLogger, "Source Files")
                              .Select(diaSF => new SourceFile(this.DataCache, diaSF.fileName, diaSF.uniqueId, FetchCompilandsFromDiaSourceFile(diaSF)));
    }

    private IEnumerable<Compiland> FetchCompilandsFromDiaSourceFile(IDiaSourceFile sourceFile)
    {
        if (this.DataCache.AllCompilands is null)
        {
            throw new InvalidOperationException("Attempted to enumerate source files before enumerating compilands - that's not valid, as source files need compilands to be created first.  This is a bug in SizeBench's implementation, not your usage.");
        }

        foreach (var diaCompiland in sourceFile.compilands)
        {
            if (diaCompiland != null)
            {
                if (diaCompiland is IDiaSymbol diaCompilandSymbol)
                {
                    var compiland = this.DataCache.FindCompilandBySymIndexId(diaCompilandSymbol.symIndexId);
                    if (compiland is null)
                    {
                        // We've found a compiland that does not contribute any binary size to the image (since we've enumerated compilands by now, but did not find
                        // this one, so it has no contributions).  We'll skip this source file since SizeBench cares about things that contribute to the binary.
                        continue;
                    }

                    yield return compiland;
                }
            }
        }
    }

    #endregion

    #region Finding RVA ranges with a source file and compiland

    public IEnumerable<RVARange> FindRVARangesForSourceFileAndCompiland(SourceFile sourceFile, Compiland compiland, CancellationToken token)
    {
        ThrowIfOnWrongThread();

        this.DiaSession.findFileById(sourceFile.FileId, out var diaSourceFile);
        this.DiaSession.symbolById(compiland.SymIndexId, out var diaCompiland);

        this.DiaSession.findLines(diaCompiland, diaSourceFile, out var diaEnumLineNumbers);

        var ranges = new List<RVARange>();

        foreach (var diaLineNumObject in diaEnumLineNumbers)
        {
            token.ThrowIfCancellationRequested();
            if (diaLineNumObject != null)
            {
                if (diaLineNumObject is IDiaLineNumber diaLineNum)
                {
                    // How do I know if this is virtual size here?  At the moment it seems that line number enumerations only ever find
                    // code, so it's all 'real size' so passing false for isVirtualSize is fine.
                    if (diaLineNum.length > 0)
                    {
                        var rva = diaLineNum.relativeVirtualAddress;

                        // When things are COMDAT-folded this gets tricky.  The line number was actaully used in multiple source files, but only
                        // one of them actually 'holds' the contribution - so we need to check if this compiland believes it owns this RVA from
                        // its section contributions.  If it does not, then this line number RVA was COMDAT folded elsewhere and we'll find it
                        // and attribute it there instead.
                        if (compiland.ContainsExecutableCodeAtRVA(rva))
                        {
                            var newRange = RVARange.FromRVAAndSize(rva, diaLineNum.length, isVirtualSize: false);
                            ranges.Add(newRange);
                        }
                    }
                }
            }
        }

        return RVARangeSet.CoalesceRVARangesFromList(ranges);
    }

    #endregion

    #region Finding Data Symbols

    public IEnumerable<StaticDataSymbol> FindAllStaticDataSymbolsWithinCompiland(Compiland compiland, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        // Usually when enumerating a bunch of symbols there will be hundreds or thousands of them so we'll pre-size capacity to 100 to do less
        // constant reallocations early.
        var allSymbols = new List<StaticDataSymbol>(capacity: 100);

        var symTagsToSearchThrough = new SymTagEnum[]
        {
                SymTagEnum.SymTagCoffGroup,
                SymTagEnum.SymTagCompiland,
                SymTagEnum.SymTagData
        };

        this.DiaSession.symbolById(compiland.SymIndexId, out var rootSymbol);

        void processDataSymbol(IDiaSymbol diaSymbol)
        {
            var locationType = (LocationType)diaSymbol.locationType;

            if (locationType == LocationType.LocIsStatic)
            {
                allSymbols.Add(GetOrCreateSymbol<StaticDataSymbol>(diaSymbol, cancellationToken));
            }
            else
            {
                Debug.Assert(diaSymbol.relativeVirtualAddress == 0, "A DataSymbol was found that has an RVA, but it's not one of the expected types we know how to parse - what is this?");
            }
        }

        RecursivelyFindSymbols(rootSymbol, symTagsToSearchThrough, SymTagEnum.SymTagData, cancellationToken, processDataSymbol);

        return allSymbols;
    }

    public IEnumerable<MemberDataSymbol> FindAllMemberDataSymbolsWithinUDT(UserDefinedTypeSymbol udt, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        var allSymbols = new List<MemberDataSymbol>(capacity: 50);

        var symTagsToSearchThrough = new SymTagEnum[]
        {
                SymTagEnum.SymTagCoffGroup,
                SymTagEnum.SymTagCompiland,
                SymTagEnum.SymTagData
        };

        this.DiaSession.symbolById(udt.SymIndexId, out var rootSymbol);

        void processDataSymbol(IDiaSymbol diaSymbol)
        {
            var dataKind = (DataKind)diaSymbol.dataKind;
            if (dataKind is DataKind.DataIsMember or DataKind.DataIsStaticMember)
            {
                allSymbols.Add(GetOrCreateMemberDataSymbol(diaSymbol, cancellationToken));
            }
            else
            {
                Debug.Assert(false, $"How did a UDT have a DataSymbol child with dataKind={dataKind}?");
            }
        }

        RecursivelyFindSymbols(rootSymbol, symTagsToSearchThrough, SymTagEnum.SymTagData, cancellationToken, processDataSymbol);

        return allSymbols;
    }

    #endregion

    #region Finding Function Symbols

    public IEnumerable<IFunctionCodeSymbol> FindAllFunctionsWithinUDT(uint symIndexIdOfUDT, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        // Usually when enumerating a bunch of symbols there could be hundreds or thousands of them so we'll pre-size capacity to 100 to do less
        // constant reallocations early.
        var allSymbols = new List<IFunctionCodeSymbol>(capacity: 100);

        // Functions can exist in multiple things - you may want to find all the functions within
        // a compiland, for example, or a COFF Group.  This function only searches within a UDT,
        // so we can be very restrictive about the SymTags to look through - which helps significantly
        // with perf of the Wasteful Virtuals analysis where FindAllFunctionsWithin is going to be
        // called on every UDT in a binary, and there can be *a lot* of UDTs in a large binary.
        var symTagsToSearchThrough = new SymTagEnum[]
        {
                SymTagEnum.SymTagFunction
        };

        this.DiaSession.symbolById(symIndexIdOfUDT, out var rootSymbol);

        if ((SymTagEnum)rootSymbol.symTag != SymTagEnum.SymTagUDT)
        {
            throw new ArgumentException($"The symIndexId for {rootSymbol.undecoratedName ?? rootSymbol.name ?? "<unknown name>"} given does not correspond to a UDT, instead it is {(SymTagEnum)rootSymbol.symTag}.");
        }

        void processFunctionSymbol(IDiaSymbol diaSymbol)
        {
            allSymbols.Add(GetOrCreateFunctionSymbol(diaSymbol, cancellationToken));
        }

        RecursivelyFindSymbols(rootSymbol, symTagsToSearchThrough, SymTagEnum.SymTagFunction, cancellationToken, processFunctionSymbol);

        return allSymbols;
    }

    public IEnumerable<IFunctionCodeSymbol> FindAllTemplatedFunctions(CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        // Usually when enumerating a bunch of symbols there will be hundreds or thousands of them so we'll pre-size capacity to 100 to do less
        // constant reallocations early.
        var allSymbols = new List<IFunctionCodeSymbol>(capacity: 100);

        this.DiaSession.findChildren(this._globalScope, SymTagEnum.SymTagFunction, name: "*<*", compareFlags: (int)NameSearchOptions.nsfRegularExpression, ppResult: out var diaEnum);

        foreach (IDiaSymbol? diaSymbol in diaEnum)
        {
            if (diaSymbol != null)
            {
                allSymbols.Add(GetOrCreateFunctionSymbol(diaSymbol, cancellationToken));
            }
        }

        Marshal.FinalReleaseComObject(diaEnum);

        return allSymbols;
    }

    private IFunctionCodeSymbol GetOrCreateFunctionSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        if (this.DataCache.AllFunctionSymbolsBySymIndexIdOfPrimaryBlock.TryGetValue(diaSymbol.symIndexId, out var parsedSymbol))
        {
            return parsedSymbol;
        }


        return ParseFunctionSymbol(diaSymbol, cancellationToken);
    }

    private IFunctionCodeSymbol ParseFunctionSymbol(IDiaSymbol primaryBlockSymbol, CancellationToken cancellationToken)
    {
        var symbolName = GetSymbolName(primaryBlockSymbol);
        var primaryBlockRVA = primaryBlockSymbol.relativeVirtualAddress;
        var primaryBlockLength = GetSymbolLength(primaryBlockSymbol, symbolName);
        var functionType = primaryBlockSymbol.type;
        var functionClassParent = primaryBlockSymbol.classParent;
        TypeSymbol? parentType = null;

        List<uint>? symIndexIdsOfBlockOverlappingPrimaryBlock = null;
        var separatedBlocks = ParseSeparatedBlocksFromFunction(primaryBlockSymbol, RVARange.FromRVAAndSize(primaryBlockRVA, primaryBlockLength),
                                                               ref symIndexIdsOfBlockOverlappingPrimaryBlock,
                                                               cancellationToken);

        if (functionClassParent != null)
        {
            parentType = GetOrCreateTypeSymbol<TypeSymbol>(functionClassParent, cancellationToken);
        }

        // The assembler can generate function types that are empty.  This is also the true for compiler-generated functions (like "*filt$0*" functions), so we need to
        // skip generating the FunctionTypeSymbol if we're in this situation.
        var functionTypeSymTag = (SymTagEnum?)functionType?.symTag ?? SymTagEnum.SymTagNull;
        var functionTypeSymbol = functionTypeSymTag == SymTagEnum.SymTagFunctionType && functionType != null ? GetOrCreateTypeSymbol<FunctionTypeSymbol>(functionType, cancellationToken) : null;

        var argumentNames = BuildFunctionArgumentNamesArray(primaryBlockSymbol, cancellationToken);

        var diaSymbol6 = (IDiaSymbol6)primaryBlockSymbol;

        var isIntroVirtual = primaryBlockSymbol.intro != 0;
        var isStatic = diaSymbol6.isStaticMemberFunc != 0;
        var isVirtual = primaryBlockSymbol.@virtual != 0;
        var isPure = !isStatic && isVirtual && primaryBlockSymbol.pure != 0;
        var isSealed = primaryBlockSymbol.@sealed != 0;
        var isPGO = primaryBlockSymbol.isPGO != 0;
        var isOptimizedForSpeed = primaryBlockSymbol.isOptimizedForSpeed != 0;

        if (separatedBlocks is null)
        {
            return new SimpleFunctionCodeSymbol(this.DataCache, symbolName ?? "<unknown name>", primaryBlockRVA, primaryBlockLength, primaryBlockSymbol.symIndexId,
                                                functionTypeSymbol, argumentNames, parentType, (AccessModifier)primaryBlockSymbol.access,
                                                isIntroVirtual: isIntroVirtual,
                                                isPure: isPure,
                                                isStatic: isStatic,
                                                isVirtual: isVirtual,
                                                isSealed: isSealed,
                                                isPGO: isPGO,
                                                isOptimizedForSpeed: isOptimizedForSpeed);
        }
        else
        {
            var primaryBlock = new PrimaryCodeBlockSymbol(this.DataCache, primaryBlockRVA, primaryBlockLength, primaryBlockSymbol.symIndexId);

            if (symIndexIdsOfBlockOverlappingPrimaryBlock != null)
            {
                foreach (var symIndexIdOfBlockOverlappingPrimaryBlock in symIndexIdsOfBlockOverlappingPrimaryBlock)
                {
                    // We still want to be able to look up any block's SymIndexId and find *some* block, so we'll put all the overlapping blocks
                    // pointing to the PrimaryCodeBlockSymbol.  This comes up especially in obfuscated binaries.
                    this.DataCache.AllSymbolsBySymIndexId.Add(symIndexIdOfBlockOverlappingPrimaryBlock, primaryBlock);
                }
            }

            return new ComplexFunctionCodeSymbol(this.DataCache, symbolName ?? "<unknown name>", primaryBlock, separatedBlocks,
                                                 functionTypeSymbol, argumentNames, parentType, (AccessModifier)primaryBlockSymbol.access,
                                                 isIntroVirtual: isIntroVirtual,
                                                 isPure: isPure,
                                                 isStatic: isStatic,
                                                 isVirtual: isVirtual,
                                                 isSealed: isSealed,
                                                 isPGO: isPGO,
                                                 isOptimizedForSpeed: isOptimizedForSpeed);
        }
    }

    private List<SeparatedCodeBlockSymbol>? ParseSeparatedBlocksFromFunction(IDiaSymbol primaryBlockSymbol,
                                                                             RVARange primaryBlockRVARange,
                                                                             ref List<uint>? symIndexIdsOfBlockOverlappingPrimaryBlock,
                                                                             CancellationToken cancellationToken)
    {
        List<SeparatedCodeBlockSymbol>? separatedBlockSymbols = null;
        primaryBlockSymbol.findChildren(SymTagEnum.SymTagBlock, null, 0, out var enumBlockSymbols);
        foreach (IDiaSymbol? blockDiaSymbol in enumBlockSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (blockDiaSymbol is null)
            {
                continue;
            }

            // Note we cannot use GetOrCreateSymbol here as it may call into ParseBlockSymbol, which in turn needs to find its ParentFunction and
            // we end up back here in an infinite loop.  So we construct the BlockSymbol by hand, and check the cache by hand.  This seems awkward.
            if (this.DataCache.AllSymbolsBySymIndexId.TryGetValue(blockDiaSymbol.symIndexId, out var symbol))
            {
                var sepBlock = (SeparatedCodeBlockSymbol)symbol;
                separatedBlockSymbols ??= new List<SeparatedCodeBlockSymbol>();
                separatedBlockSymbols.Add(sepBlock);
            }
            else
            {
                var blockRVA = blockDiaSymbol.relativeVirtualAddress;
                var blockLength = (uint)blockDiaSymbol.length;
                // It's possible that this block is fully contained in the (rva,size) range that DIA discovered for this function - for example this
                // seems to happen with some [catch] funclets.  If the block is already accounted for in the function's "base" RVARange, then we skip
                // it.
                if (primaryBlockRVARange.Contains(blockRVA, blockLength) == false)
                {
                    separatedBlockSymbols ??= new List<SeparatedCodeBlockSymbol>();

                    var parentFunction = blockDiaSymbol.lexicalParent;
                    while (parentFunction != null && (SymTagEnum)parentFunction.symTag != SymTagEnum.SymTagFunction)
                    {
                        parentFunction = parentFunction.lexicalParent;
                    }

                    if (parentFunction is null)
                    {
                        throw new InvalidOperationException("A separated block was found, but it could not find its parent function. This is a bug in SizeBench's implementation, not your use of it.");
                    }

                    var separatedBlock = new SeparatedCodeBlockSymbol(this.DataCache, blockRVA, blockLength, blockDiaSymbol.symIndexId, parentFunction.symIndexId);
                    separatedBlockSymbols.Add(separatedBlock);
                }
                else if (blockDiaSymbol.symIndexId != primaryBlockSymbol.symIndexId)
                {
                    symIndexIdsOfBlockOverlappingPrimaryBlock ??= new List<uint>();
                    symIndexIdsOfBlockOverlappingPrimaryBlock.Add(blockDiaSymbol.symIndexId);
                }
            }
        }

        return separatedBlockSymbols;
    }

    private CodeBlockSymbol ParseBlockSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        var parentSymbol = diaSymbol.lexicalParent;
        while (parentSymbol != null && ((SymTagEnum)parentSymbol.symTag) != SymTagEnum.SymTagFunction)
        {
            parentSymbol = parentSymbol.lexicalParent;
        }

        if (parentSymbol != null && (SymTagEnum)parentSymbol.symTag == SymTagEnum.SymTagFunction)
        {
            // This will in turn parse all the blocks for the function
            // This may be a Complex function with primary and separated blocks (if MSVC arrived here),
            // or this may be a Simple function when clang generates code as it can generate a lone
            // SymTagBlock under a function.
            var function = GetOrCreateFunctionSymbol(parentSymbol, cancellationToken);

            // If we found a simple function, we'll return it right away.  We only go further for MSVC
            // when we need to find the block within the complex function.
            if (function is SimpleFunctionCodeSymbol simpleFn)
            {
                return simpleFn;
            }
        }
        else
        {
            throw new InvalidOperationException("We've found a separated block that does not have a function as its parent - how is this possible?  This is a bug in SizeBench's implementation, not your use of it.");
        }

        var block = (CodeBlockSymbol)this.DataCache.AllSymbolsBySymIndexId[diaSymbol.symIndexId];
        Debug.Assert(!String.IsNullOrEmpty(block.Name));
        return block;
    }

    private TypeSymbol[]? BuildFunctionArgumentTypesArray(IDiaSymbol functionType, CancellationToken cancellationToken)
    {
        TypeSymbol[]? argumentTypes = null;
        var argIndex = 0;
        functionType.findChildren(SymTagEnum.SymTagFunctionArgType, null, 0, out var enumArgs);

        foreach (IDiaSymbol? arg in enumArgs)
        {
            if (arg is null)
            {
                continue;
            }

            argumentTypes ??= new TypeSymbol[enumArgs.count];

            argumentTypes[argIndex] = GetOrCreateTypeSymbol<TypeSymbol>(arg.type, cancellationToken);
            argIndex++;
            Marshal.FinalReleaseComObject(arg);
        }

        Marshal.FinalReleaseComObject(enumArgs);

        return argumentTypes;
    }

    private FunctionTypeSymbol ParseFunctionTypeSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        // I don't bother to pass in an RVA or name because to my knowledge all FunctionType symbols don't have these.
        // So let's assert this - if they ever have a name or an RVA then this function should change to use those.
        Debug.Assert(String.IsNullOrEmpty(diaSymbol.name));
        Debug.Assert(diaSymbol.relativeVirtualAddress == 0);

        if (diaSymbol.unmodifiedType != null)
        {
            throw new InvalidOperationException("Need to add ModifiedTypeSymbol code here.  This is a bug in SizeBench's implementation, not your use of it.");
        }

        var argumentTypes = BuildFunctionArgumentTypesArray(diaSymbol, cancellationToken);
        var returnValueType = GetOrCreateTypeSymbol<TypeSymbol>(diaSymbol.type, cancellationToken);

        // SymTagFunction and SymTagFunctionType symbols are not where the cv-qualifiers for a function are stored, despite what
        // the docs for DIA may suggest.  Per the owners of DIA, the right way to detect if a function is const or
        // volatile is to look deeper as so:
        // (SymTagFunctionType symbol).objectPointerType is of type SymTagPointerType (assuming it's not null - it's only filled in
        // for functions of types, free-functions this'd be null).
        // objectPointerType.type is the type of the "this" pointer passed to member functions.  And THAT type may be a modified
        // type with cv-qualifiers.  So we dig into "objectPointerType?.type?." to get const and volatile below.
        bool functionIsConst, functionIsVolatile, functionIsUnaligned;
        var diaObjectPointerType = diaSymbol.objectPointerType;
        var diaObjectPointerTypeType = diaObjectPointerType?.type;
        if (diaObjectPointerTypeType != null)
        {
            functionIsConst = diaObjectPointerTypeType.constType != 0;
            functionIsVolatile = diaObjectPointerTypeType.volatileType != 0;
            functionIsUnaligned = diaObjectPointerTypeType.unalignedType != 0;
        }
        else
        {
            functionIsConst = false;
            functionIsVolatile = false;
            functionIsUnaligned = false;
        }

        // objectPointerTargetType below is how to calculate the type of "this" for a member function - but it's not used so commenting it out for now to save some perf.
        // If it's useful in the future to know the type of "this" from a member function, here's the code.
        //TypeSymbol objectPointerTargetType = diaObjectPointerType != null ? GetOrCreateSymbol<PointerTypeSymbol>(binaryDataLoader, session, cancellationToken, cache, diaAdapter, diaSession, diaObjectPointerType).PointerTargetType : null;

        return new FunctionTypeSymbol(this.DataCache,
                                      BuildFunctionTypeName(argumentTypes, returnValueType, functionIsConst, functionIsVolatile, functionIsUnaligned),
                                      size: 0,
                                      symIndexId: diaSymbol.symIndexId,
                                      isConst: functionIsConst,
                                      isVolatile: functionIsVolatile,
                                      argumentTypes: argumentTypes,
                                      returnValueType: returnValueType);
    }

    private static string BuildFunctionTypeName(TypeSymbol[]? argumentTypes,
                                                TypeSymbol returnValueType,
                                                bool functionIsConst,
                                                bool functionIsVolatile,
                                                bool functionIsUnaligned)
    {
        var sb = new StringBuilder(100);

        // TODO: consider also having the calling convention as part of the name.  It's part of the type after all...
        //       but for now I don't have a good list of how CV_call_e (in IDiaSymbol.callingConvention) translates to
        //       strings like "__cdecl".

        sb.Append(returnValueType.Name);
        sb.Append(" (*function)(");

        if (argumentTypes?.Length > 0)
        {
            for (var argumentIndex = 0; argumentIndex < argumentTypes.Length; argumentIndex++)
            {
                // Separate arguments with a comma and a space
                if (argumentIndex > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(argumentTypes[argumentIndex].Name);
            }
        }

        sb.Append(')');
        if (functionIsConst)
        {
            sb.Append(" const");
        }

        if (functionIsVolatile)
        {
            sb.Append(" volatile");
        }

        if (functionIsUnaligned)
        {
            sb.Append(" __unaligned");
        }

        return sb.ToString();
    }


    private ParameterDataSymbol[]? BuildFunctionArgumentNamesArray(IDiaSymbol primaryBlock, CancellationToken cancellationToken)
    {
        List<ParameterDataSymbol>? argumentNames = null;
        primaryBlock.findChildren(SymTagEnum.SymTagData, null, 0, out var enumArgs);

        if (enumArgs is null)
        {
            return null;
        }


        while (true)
        {
            enumArgs.Next(1, out var arg, out var celt);
            if (celt != 1)
            {
                break;
            }

            if (arg is null || (DataKind)arg.dataKind != DataKind.DataIsParam)
            {
                continue;
            }

            if (arg.type is null)
            {
                // Some binaries have been seen in the wild that use /clr:pure (now deprecated/removed as of VS 2017), where their parameters
                // don't have valid types.  If we find even one of these, we'll just assume this whole parameter name niceness is not worth
                // it and give up.
                return null;
            }

            argumentNames ??= new List<ParameterDataSymbol>();

            argumentNames.Add(GetOrCreateParameterDataSymbol(arg, cancellationToken));
            Marshal.FinalReleaseComObject(arg);
        }

        Marshal.FinalReleaseComObject(enumArgs);

        return argumentNames?.ToArray();
    }

    private ParameterDataSymbol GetOrCreateParameterDataSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        if (this.DataCache.AllParameterDataSymbolsbySymIndexId.TryGetValue(diaSymbol.symIndexId, out var symbol))
        {
            return symbol;
        }

        var dataKind = (DataKind)diaSymbol.dataKind;
        var symbolRVA = diaSymbol.relativeVirtualAddress;

        Debug.Assert(dataKind == DataKind.DataIsParam);
        Debug.Assert(symbolRVA == 0);

        var symbolName = GetSymbolName(diaSymbol);
        var symbolLength = GetSymbolLength(diaSymbol, symbolName);

        return ParseDataSymbol<ParameterDataSymbol>(diaSymbol, symbolName ?? "<unknown name>", symbolLength, symbolRVA, cancellationToken);
    }

    #endregion

    #region Finding User-Defined Type Symbols

    public IEnumerable<UserDefinedTypeSymbol> FindAllUserDefinedTypes(ILogger parentLogger, CancellationToken token)
    {
        ThrowIfOnWrongThread();

        if (this.DataCache.AllUserDefinedTypes is null)
        {
            this.DataCache.AllUserDefinedTypes = this.DiaSession.EnumerateUDTSymbols()
                                                                .WithCancellation(token)
                                                                .WithLogging(parentLogger, "User-Defined Types")
                                                                .Select((diaSymbol) => GetOrCreateTypeSymbol<UserDefinedTypeSymbol>(diaSymbol, token))
                                                                .ToArray();
        }

        return this.DataCache.AllUserDefinedTypes;
    }

    public IEnumerable<UserDefinedTypeSymbol> FindUserDefinedTypesByName(ILogger parentLogger, string name, CancellationToken token)
    {
        return this.DiaSession.EnumerateUDTSymbolsByName(name)
                              .WithCancellation(token)
                              .WithLogging(parentLogger, "User-Defined Types")
                              .Select((diaSymbol) => GetOrCreateTypeSymbol<UserDefinedTypeSymbol>(diaSymbol, token));
    }

    #endregion

    #region Finding Annotations

    public IEnumerable<AnnotationSymbol> FindAllAnnotations(ILogger parentLogger, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        if (this.DataCache.AllAnnotations is null)
        {
            var allSymbols = new List<AnnotationSymbol>(capacity: 100);

            var symTagsToSearchThrough = new SymTagEnum[]
            {
                SymTagEnum.SymTagFunction,
                SymTagEnum.SymTagAnnotation,
            };

            void processAnnotationSymbol(IDiaSymbol diaSymbol)
            {
                allSymbols.Add(GetOrCreateAnnotationSymbol(diaSymbol, cancellationToken));
            };

            RecursivelyFindSymbols(this.DiaGlobalScope, symTagsToSearchThrough, SymTagEnum.SymTagAnnotation, cancellationToken, processAnnotationSymbol);

            this.DataCache.AllAnnotations = allSymbols;
        }

        return this.DataCache.AllAnnotations;
    }

    private AnnotationSymbol GetOrCreateAnnotationSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        if (this.DataCache.AllAnnotationsBySymIndexId.TryGetValue(diaSymbol.symIndexId, out var symbol))
        {
            return symbol;
        }

        return ParseAnnotation(diaSymbol, (uint)diaSymbol.length, diaSymbol.relativeVirtualAddress, cancellationToken);
    }

    private AnnotationSymbol ParseAnnotation(IDiaSymbol annotationDiaSymbol,
                                             uint symbolLength,
                                             uint symbolRVA,
                                             CancellationToken cancellationToken)
    {
        if (this.DataCache.AllSourceFiles is null)
        {
            // This has the side-effect of populating SessionDataCache.AllSourceFiles, which we need when parsing annotations.
            _ = this.Session.EnumerateSourceFiles(cancellationToken).Result;
        }

        this.DiaSession.findSymbolByRVA(symbolRVA, SymTagEnum.SymTagFunction, out var enclosingDiaFunctionSymbol);

        this.DiaSession.findLinesByRVA(symbolRVA, symbolLength, out var enumLineNumbers);
        var diaLineNumber = enumLineNumbers.count > 0 ? enumLineNumbers.Item(0) : null;

        SourceFile? sourceFile = null;
        uint lineNumber;
        var isInlinedOrAnnotatingInlineSite = false;
        // We'll use the first line number found, it's unlikely (impossible?) that an annotation exists across multiple files.
        if (diaLineNumber != null)
        {
            sourceFile = this.DataCache.FindSourceFileByFilename(diaLineNumber.sourceFile?.fileName);
            lineNumber = diaLineNumber.lineNumber;

            // Find the function that contains this annotation (if there is one), which may require going up the lexicalParent chain multiple times
            var parent = annotationDiaSymbol.lexicalParent;
            while (enclosingDiaFunctionSymbol is null && parent != null)
            {
                if ((SymTagEnum)parent.symTag == SymTagEnum.SymTagFunction)
                {
                    enclosingDiaFunctionSymbol = parent;
                }

                parent = parent.lexicalParent;
            }

            if (enclosingDiaFunctionSymbol != null)
            {
                this.DiaSession.findChildren(enclosingDiaFunctionSymbol, SymTagEnum.SymTagInlineSite, null, 0, out var enumInlineSites);
                while (true)
                {
                    enumInlineSites.Next(1, out var inlineSite, out var celt);
                    if (celt != 1)
                    {
                        break;
                    }

                    this.DiaSession.findInlineeLines(inlineSite, out var enumInlineeLineNumbers);
                    while (true)
                    {
                        enumInlineeLineNumbers.Next(1, out var inlineeDiaLineNumber, out var celtInlineeLineNumbers);
                        if (celtInlineeLineNumbers != 1)
                        {
                            break;
                        }

                        var inlineeRVA = inlineeDiaLineNumber.relativeVirtualAddress;
                        var inlineeLength = inlineeDiaLineNumber.length;
                        if (RVARange.FromRVAAndSize(inlineeRVA, inlineeLength).Contains(symbolRVA))
                        {
                            isInlinedOrAnnotatingInlineSite = true;
                            sourceFile = this.DataCache.AllSourceFiles?.FirstOrDefault(sf => sf.Name == inlineeDiaLineNumber.sourceFile?.fileName);
                            lineNumber = inlineeDiaLineNumber.lineNumber;
                        }
                    }
                }
            }

            Marshal.FinalReleaseComObject(diaLineNumber);
        }
        else
        {
            lineNumber = 0;
        }

        Marshal.FinalReleaseComObject(enumLineNumbers);

        // Now we need to find out what text is actually in the annotation
        var annotationText = String.Empty;

        annotationDiaSymbol.findChildren(SymTagEnum.SymTagData, null, 0, out var enumDataChildren);

        while (annotationText.Length == 0)
        {
            enumDataChildren.Next(1, out var dataChild, out var celt);
            if (celt != 1)
            {
                break;
            }

            annotationText = dataChild.value.ToString() ?? String.Empty;
            Marshal.FinalReleaseComObject(dataChild);
        }

        Marshal.FinalReleaseComObject(enumDataChildren);

        return new AnnotationSymbol(this.DataCache,
                                    annotationText,
                                    sourceFile,
                                    lineNumber,
                                    isInlinedOrAnnotatingInlineSite,
                                    annotationDiaSymbol.symIndexId);
    }

    #endregion

    #region Finding Public Symbols

    public SortedList<uint, List<string>> FindAllDisambiguatingVTablePublicSymbolNamesByRVA(ILogger parentLogger, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        if (this.DataCache.AllDisambiguatingVTablePublicSymbolNamesByRVA is null)
        {
            // We can't use RecursivelyFindSymbols here or create actual SizeBench Symbol objects (like PublicSymbol objects) because this step in opening a session is too early and we
            // need this information to then establish canonical names, which we in turn need to process symbols fully.  So here our only goal is to find all the disambiguated VTable names
            // for each RVA and stop, without the entire ISymbol object coming into existence.
            var disambiguatingVTableNamesByRVA = new SortedList<uint, List<string>>();

            this.DiaSession.findChildrenEx(this._globalScope, SymTagEnum.SymTagPublicSymbol, "*`vftable'{for*", (uint)(NameSearchOptions.nsfRegularExpression | NameSearchOptions.nsfUndecoratedName), out var enumDisambiguatingVTables);

            foreach (IDiaSymbol? publicSym in enumDisambiguatingVTables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (publicSym is null)
                {
                    continue;
                }

                var publicSymRVA = publicSym.relativeVirtualAddress;
                var publicSymName = publicSym.undecoratedName;

                // The "const " prefix is not useful to anything we do with vtable names, so we strip it if present.
                if (publicSymName.StartsWith("const ", StringComparison.Ordinal))
                {
                    publicSymName = publicSymName["const ".Length..];
                }

                if (!disambiguatingVTableNamesByRVA.TryGetValue(publicSymRVA, out var namesList))
                {
                    namesList = new List<string>();
                    disambiguatingVTableNamesByRVA.Add(publicSymRVA, namesList);
                }

                namesList.Add(publicSymName);
            }

            this.DataCache.AllDisambiguatingVTablePublicSymbolNamesByRVA = disambiguatingVTableNamesByRVA;
        }

        return this.DataCache.AllDisambiguatingVTablePublicSymbolNamesByRVA;
    }

    #endregion

    public TSymbol FindSymbolBySymIndexId<TSymbol>(uint symIndexId, CancellationToken token) where TSymbol : class, ISymbol
    {
        ThrowIfOnWrongThread();

        this.DiaSession.symbolById(symIndexId, out var diaSymbol);

        try
        {
            return GetOrCreateSymbol<TSymbol>(diaSymbol, token);
        }
        finally
        {
            Marshal.FinalReleaseComObject(diaSymbol);
        }
    }

    public TSymbol FindTypeSymbolBySymIndexId<TSymbol>(uint symIndexId, CancellationToken token) where TSymbol : TypeSymbol
    {
        ThrowIfOnWrongThread();

        this.DiaSession.symbolById(symIndexId, out var diaSymbol);

        try
        {
            return GetOrCreateTypeSymbol<TSymbol>(diaSymbol, token);
        }
        finally
        {
            Marshal.FinalReleaseComObject(diaSymbol);
        }
    }

    private void RecursivelyFindSymbols(IDiaSymbol parentSymbol,
                                        SymTagEnum[] symTagsToSearchThrough,
                                        SymTagEnum symTagToProcess,
                                        CancellationToken cancellationToken,
                                        Action<IDiaSymbol> processSymbol,
                                        string? nameFilter = null,
                                        bool filterWithUndecoratedNames = false,
                                        uint currentDepthOfRecursion = 0)
    {
        // The reason we pass around 'symTagsToSearchThrough' is that iterating through every symbol in a large binary can be extraordinarily slow and 
        // allocates a ton of very short-lived COM objects, when callers will know what sym tags can ever contain the things they're searching for.
        // Or, worst case, can just provide the list of all sym tags if they really want to search everything.

        // Sometimes it seems like we can get into an infinite loop here, but it's unclear how this is possible.  A Windows OS binary
        // exhibited this symptom with some Protobuf code that was heavily templated.  If we get really deep let's just give up and hope
        // we can report *something* to the user, rather than a stack overflow.
        if (currentDepthOfRecursion > 100)
        {
            return;
        }

        var nameSearchOptions = NameSearchOptions.nsNone;
        if (nameFilter is not null)
        {
            nameSearchOptions |= NameSearchOptions.nsfRegularExpression;
            if (filterWithUndecoratedNames)
            {
                nameSearchOptions |= NameSearchOptions.nsfUndecoratedName;
            }
        }

        for (var i = 0; i < symTagsToSearchThrough.Length; i++)
        {
            parentSymbol.findChildren(symTagsToSearchThrough[i], name: nameFilter, compareFlags: (uint)nameSearchOptions, ppResult: out var diaEnum);

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    diaEnum.Next(1, out var symbol, out var celt);
                    if (celt != 1)
                    {
                        break;
                    }

                    var symTagOfThisSymbol = (SymTagEnum)symbol.symTag;

                    if (symTagOfThisSymbol == symTagToProcess)
                    {
                        processSymbol(symbol);
                    }
                    else
                    {
                        RecursivelyFindSymbols(symbol, symTagsToSearchThrough, symTagToProcess, cancellationToken, processSymbol, nameFilter, filterWithUndecoratedNames, currentDepthOfRecursion + 1);
                    }
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(diaEnum);
            }
        }
    }

    #region Finding Symbol By RVA, and in an RVA Range

    public ISymbol? FindSymbolByRVA(uint rva, bool allowFindingNearest, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        IDiaSymbol? diaSymbol;
        var displacement = 0;

        // If this RVA is home to multiple COMDAT-folded symbols, we want to find the one we counted as primary, that we attributed all the bytes to.
        if (this.DataCache.AllCanonicalNames!.TryGetValue(rva, out var nameCanonicalization))
        {
            this.DiaSession.symbolById(nameCanonicalization.CanonicalSymIndexID, out diaSymbol);
        }
        else
        {
            this.DiaSession.findSymbolByRVAEx(rva, SymTagEnum.SymTagNull, out diaSymbol, out displacement);
        }

        if (diaSymbol is null ||
            (allowFindingNearest == false && (diaSymbol.relativeVirtualAddress != rva || displacement != 0)))
        {
            return null;
        }

        return GetOrCreateSymbol<ISymbol>(diaSymbol, cancellationToken);
    }

    public IEnumerable<(ISymbol, uint)> FindSymbolsInRVARange(RVARange range, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        this.DiaSession.getSymbolsByAddr(out var enumSymbolsByAddr);
        var enumSymbolsByAddr2 = (IDiaEnumSymbolsByAddr2)enumSymbolsByAddr;

        var diaSymbol = enumSymbolsByAddr2.symbolByRVAEx(fPromoteBlockSym: 0, range.RVAStart);
        uint celt = 1;

        // Sometimes we get a symbol that is prior to where we wanted to start - if so, just keep advancing until we're within range.
        while (diaSymbol != null && diaSymbol.relativeVirtualAddress < range.RVAStart && celt == 1)
        {
            enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, 1, out diaSymbol, out celt);
        }

        ISymbol? previousNonFoldedSymbolYielded = null;

        while (diaSymbol != null && celt == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var symbolRVA = diaSymbol.relativeVirtualAddress;

            // Sometimes DIA seems to return symbols that start before the range we asked for (in particular for __guard_fids_table?).  We can
            // ignore these.
            if (symbolRVA < range.RVAStart)
            {
                enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, 1, out diaSymbol, out celt);
                continue;
            }

#pragma warning disable IDE0007 // Use implicit type - explicitly using "string?" here since diaSymbol.name can return null and that's not obvious because of the property signature that tlbimp generates.
            string? symbolName = diaSymbol.name;
#pragma warning restore IDE0007 // Use implicit type
            if (symbolName?.Equals("$xdatasym", StringComparison.Ordinal) == true)
            {
                // We don't want to find this symbol, this is a sentinel in DIA that has little value.  We already hand-parse XDATA out of the
                // binary to get useful stuff.  So skip this one.
                enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, 1, out diaSymbol, out celt);
                continue;
            }

            var length = GetSymbolLength(diaSymbol, symbolName);

            // Note we need to add 1 to the RVAEnd when we compare against (symbolRVA + GetSymbolLength) because
            // the (RVA + length) points to the byte *after* the end of the symbol.
            // We can't just subtract 1 from the (RVA + length) because that can occasionally be 0 and this would
            // underflow.  So, the simplest way to make the math work out is to add 1 to RVAEnd for the comparison of
            // whether or not we're 'past the end' of the range, without losing a symbol that exactly abuts the end
            // of the range.
            if (symbolRVA + length > range.RVAEnd + 1)
            {
                // We're past the end of the range we wanted to enumerate, so we're done.
                yield break;
            }

            var newSymbol = GetOrCreateSymbol<Symbol>(diaSymbol, cancellationToken);

            // If this symbol is 'within' the previous one, we don't need to return it as all the space is already accounted for.
            // This can happen with SymTagLabel symbols within a function - such as MyTestEntry or SomeAltEntry in the MASM tests.
            if (previousNonFoldedSymbolYielded != null &&
                previousNonFoldedSymbolYielded.RVA <= newSymbol.RVA)
            {
                // If we're fully contained within, ignore it regardless of type.
                if (previousNonFoldedSymbolYielded.RVAEnd >= newSymbol.RVAEnd)
                {
                    enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, 1, out diaSymbol, out celt);
                    continue;
                }

                // In some cases due to alignment requirements, the RVAEnd of the symbol we get back will be *past* the end
                // of the previous symbol, but that's a lie - we do our best to confirm this hopefully-odd situation by
                // ensuring we have a PublicSymbol and that it is for a Label before we ignore it.
                if (newSymbol is PublicSymbol)
                {
                    this.DiaSession.findSymbolByRVAEx(newSymbol.RVA, SymTagEnum.SymTagLabel, out var labelSymbol, out var displacement);

                    if (displacement == 0 && labelSymbol != null && labelSymbol.relativeVirtualAddress == newSymbol.RVA)
                    {
                        enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, 1, out diaSymbol, out celt);
                        continue;
                    }
                }
            }

            yield return (newSymbol, newSymbol.RVAEnd - range.RVAStart);
            previousNonFoldedSymbolYielded = newSymbol;

            if (this.DataCache.AllCanonicalNames!.TryGetValue(newSymbol.RVA, out var nameCanonicalization))
            {
                foreach (var kvp in nameCanonicalization.NamesBySymIndexID)
                {
                    if (kvp.Key == newSymbol.SymIndexId)
                    {
                        continue; // We already yielded this one to the caller, we only want to find other SymIndexIDs folded at this RVA
                    }

                    this.DiaSession.symbolById(kvp.Key, out diaSymbol);
                    var foldedSymbol = GetOrCreateSymbol<Symbol>(diaSymbol, cancellationToken);

                    // Note the subtle use of "newSymbol.RVAEnd" instead of "foldedSymbol.RVAEnd" - this is because folded symbols have 0 size, so their RVAEnd == RVA, which means the
                    // progress would go backwards from the "newSymbol" if we use that here.
                    yield return (foldedSymbol, newSymbol.RVAEnd - range.RVAStart);
                }
            }

            enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, 1, out diaSymbol, out celt);
        }
    }

    #endregion

    #region Finding VTable Count

    public byte FindCountOfVTablesWithin(uint symIndexId)
    {
        ThrowIfOnWrongThread();

        this.DiaSession.symbolById(symIndexId, out var rootSymbol);

        // We should only ever try to look up VTables for UserDefinedTypes, nothing else has them that I know of.
        if (SymTagEnum.SymTagUDT != (SymTagEnum)rootSymbol.symTag)
        {
            throw new InvalidOperationException($"This shouldn't be called on anything but UDTs.  Somehow it has been called on " +
                                                $"{rootSymbol.undecoratedName ?? rootSymbol.name ?? "<unknown name>"} which is a {(SymTagEnum)rootSymbol.symTag}." +
                                                "This is a bug in SizeBench's implementation, not your usage of it.");
        }

        this.DiaSession.findChildren(rootSymbol, SymTagEnum.SymTagVTable, name: null, compareFlags: 0, ppResult: out var enumVTables);

        var countOfVTables = (byte)enumVTables.count;

        Marshal.FinalReleaseComObject(rootSymbol);
        Marshal.FinalReleaseComObject(enumVTables);

        return countOfVTables;
    }

    #endregion

    #region Finding all names for an RVA

    public SortedList<uint, NameCanonicalization> FindCanonicalNamesForFoldableRVAs(ILogger logger, CancellationToken cancellationToken)
    {
        ThrowIfOnWrongThread();

        if (this.DataCache.AllCanonicalNames != null)
        {
            return this.DataCache.AllCanonicalNames;
        }

        var results = new SortedDictionary<uint, NameCanonicalization>();
        var symIndexIdsProcessedAlready = new SortedList<uint, bool>(capacity: 1000);

        // Anything that contains code or data could be ICF'd, so we'll enumerate all of those.  The one exception is that we don't look at
        // SymTagLabel currently, as SizeBench never uses SymTagLabel for anything.  Should that change somedy, it could be added here.
        var symTagsToEnumerateForFolding = new SymTagEnum[]
        {
                SymTagEnum.SymTagBlock,
                SymTagEnum.SymTagData,
                SymTagEnum.SymTagFunction,
                SymTagEnum.SymTagThunk,
                // It is important that PublicSymbols are enumerated last. Each NameCanonicalization will keep track of all the possible names,
                // but the names of PublicSymbols include things like "public:" and "virtual" at the start of their names which does not sort
                // well with names from thunks, functions, and data.  So, we will ignore public symbol names if we already found non-public
                // symbols at that RVA.  By doing this last, we can readily discard these names for anything already found via another SymTag.
                SymTagEnum.SymTagPublicSymbol,
        };

        // Some things like Functions are generally found in the global scope, but things like thunks and blocks are often found only within a compiland, so we will
        // search through the global scope as well as every compiland.
        var scopesToSearch = new List<IDiaSymbol>(capacity: 1000) { this.DiaGlobalScope };
        this.DiaSession.findChildren(this._globalScope, SymTagEnum.SymTagCompiland, name: null, compareFlags: 0, ppResult: out var diaCompilandEnum);
        foreach (IDiaSymbol? compiland in diaCompilandEnum)
        {
            if (compiland is null)
            {
                continue;
            }

            scopesToSearch.Add(compiland);
        }

        foreach (var scope in scopesToSearch)
        {
            foreach (var symTag in symTagsToEnumerateForFolding)
            {
                scope.findChildren(symTag, name: null, compareFlags: 0, ppResult: out var diaEnum);

                foreach (IDiaSymbol? diaSymbol in diaEnum)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (diaSymbol is null)
                    {
                        continue;
                    }

                    ProcessOneSymbolForCanonicalNameSearch(diaSymbol, results, symIndexIdsProcessedAlready, cancellationToken);
                }
            }
        }

        // Remove any entries where the RVA had just one symbol, to reduce burden on people enumerating these later - that way a TryGetValue(rvaWithNothingFolded) will return false.
        // This is also when we convert from a SortedDictionary to a SortedList because we know the RVAs are in order and we can end up with the SortedList in memory for reduced memory
        // footprint for the rest of this Session's lifetime.
        var resultsAsSortedList = new SortedList<uint, NameCanonicalization>(capacity: results.Count);
        foreach (var resultEntry in results)
        {
            if (resultEntry.Value.NamesBySymIndexID.Count > 1)
            {
                resultEntry.Value.Canonicalize();
                resultsAsSortedList.Add(resultEntry.Key, resultEntry.Value);
            }
        }

        resultsAsSortedList.TrimExcess();
        this.DataCache.AllCanonicalNames = resultsAsSortedList;

        return this.DataCache.AllCanonicalNames;
    }

    private bool ProcessOneSymbolForCanonicalNameSearch(IDiaSymbol diaSymbol,
                                                        SortedDictionary<uint, NameCanonicalization> results,
                                                        SortedList<uint, bool> symIndexIdsProcessedAlready,
                                                        CancellationToken cancellationToken)
    {
        var rva = diaSymbol.relativeVirtualAddress;
        if (rva == 0)
        {
            return false;
        }

        var symIndexId = diaSymbol.symIndexId;

        if (symIndexIdsProcessedAlready.ContainsKey(symIndexId))
        {
            // We already saw this symIndexId before (probably in another scope), so don't process it again.
            return false;
        }

        symIndexIdsProcessedAlready.Add(symIndexId, true);

        var symTag = (SymTagEnum)diaSymbol.symTag;

        //TODO: we could optimize this more by not getting the name for public symbols (which have to do a full undecoration of their name), if we know
        //      we're going to throw that name away anyway.
        string? symbolName;

        if (symTag is SymTagEnum.SymTagBlock or SymTagEnum.SymTagFunction)
        {
            // Functions and blocks are especially difficult, as we really need to find their entire UniqueSignatureWithNoPrefixes formatted name to sort
            // things including parent types and such.  GetSymbolName can't be doing all that, as it's where the function gets its simple FunctionName from,
            // so for the purposes of name canonicalization we'll conjure that more complex name for blocks and functions manually here.
            //
            // We can be very careful about constructing type symbols here as types are never COMDAT folded so they can't need anything that we're in the process
            // of setting up.  We'll do that to have maximum re-use between this name conjuring code and that used later by true CodeBlockSymbols and SimpleFunctionCodeSymbols.

            // We don't need the block anymore if we're on a block, we've already cached off the 'symTag' above to know to prepend the 'Block of code in' prefix - so we now walk
            // up to the parent function since that's where we'll get all our name data anyway.
            while (diaSymbol != null && SymTagEnum.SymTagFunction != (SymTagEnum)diaSymbol.symTag)
            {
                diaSymbol = diaSymbol.lexicalParent;
            }

            if (diaSymbol == null)
            {
                throw new InvalidOperationException($"Somehow when attempting to find the name of this symbol, we could not find its parent function.  This is a bug in SizeBench's implementation, not your usage of it.");
            }

            var functionClassParent = diaSymbol.classParent;
            TypeSymbol? parentType = null;
            if (functionClassParent != null)
            {
                // This could be a UserDefinedTypeSymbol in C and C++, in Rust it could also be an EnumTypeSymbol
                parentType = GetOrCreateTypeSymbol<TypeSymbol>(functionClassParent, cancellationToken);
            }

            var functionType = diaSymbol.type;
            var functionTypeSymTag = (SymTagEnum?)functionType?.symTag ?? SymTagEnum.SymTagNull;
            var functionTypeSymbol = functionTypeSymTag == SymTagEnum.SymTagFunctionType && functionType != null ? GetOrCreateTypeSymbol<FunctionTypeSymbol>(functionType, cancellationToken) : null;

            symbolName = FunctionCodeFormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignatureWithNoPrefixes,
                                                                    isStatic: false /* unused here for this type of name */,
                                                                    isIntroVirtual: false /* unused here for this type of name */,
                                                                    functionTypeSymbol,
                                                                    parentType,
                                                                    GetSymbolName(diaSymbol)!,
                                                                    argumentNames: null /* unused here for this type of name */,
                                                                    isVirtual: false /* unused here for this type of name */,
                                                                    isSealed: false /* unused here for this type of name */);

            if (symTag == SymTagEnum.SymTagBlock)
            {
                symbolName = $"{CodeBlockSymbol.BlockOfCodePrefix}{symbolName}";
            }
        }
        else
        {
            symbolName = GetSymbolName(diaSymbol);
        }

        if (symbolName is null)
        {
            return false;
        }

        if (results.TryGetValue(rva, out var nameCanonicalization) == false)
        {
            nameCanonicalization = new NameCanonicalization();
            results.Add(rva, nameCanonicalization);
        }

        nameCanonicalization.AddName(symIndexId, symbolName, symTag);

        return true;
    }

    #endregion

    #region Finding command line for a compiland

    public CommandLine FindCommandLineForCompilandByID(uint compilandSymIndexId)
    {
        ThrowIfOnWrongThread();

        this.DiaSession.symbolById(compilandSymIndexId, out var compiland);

        // The CompilandEnv with the special name "cmd" is the command-line that was passed to the compiler/linker, if it's present.
        compiland.findChildren(SymTagEnum.SymTagCompilandEnv, "cmd", compareFlags: 0, out var enumCompilandEnv);
        // If this ever exists more than once let's figure out how and whether this logic is really correct.  But I think it can only exist once.
        Debug.Assert(enumCompilandEnv.count <= 1);
        enumCompilandEnv.Next(1, out var compilandEnv, out _);

        var commandLineRaw = compilandEnv?.value?.ToString() ?? String.Empty;

        compiland.findChildren(SymTagEnum.SymTagCompilandDetails, null, compareFlags: 0, out var enumCompilandDetails);
        // For now we only look at the first CompilandDetails, just like BinSkim does.  In some cases (like "IMPORT:" compilands) there seem to be multiple duplicate
        // CompilandDetails records - but they're all identical, so I hope it's ok to not bother detecting that and just use the first one in all cases, for perf.
        enumCompilandDetails.Next(1, out var compilandDetails, out _);

        var language = CompilandLanguage.Unknown;
        var toolName = String.Empty;
        var frontEndVersion = new Version(0, 0);
        var backEndVersion = new Version(0, 0);

        if (compilandDetails != null)
        {
            language = (CompilandLanguage)compilandDetails.language;
            toolName = compilandDetails.compilerName;
            checked
            {
                backEndVersion = new Version(
                    (int)compilandDetails.backEndMajor,
                    (int)compilandDetails.backEndMinor,
                    (int)compilandDetails.backEndBuild,
                    (int)compilandDetails.backEndQFE
                    );

                frontEndVersion = new Version(
                    (int)compilandDetails.frontEndMajor,
                    (int)compilandDetails.frontEndMinor,
                    (int)compilandDetails.frontEndBuild,
                    (int)compilandDetails.frontEndQFE
                    );
            }
        }

        return CommandLine.FromLanguageAndToolName(language, toolName, frontEndVersion, backEndVersion, commandLineRaw);
    }

    #endregion

    #region Loading Public Symbol By Target RVA

    public uint? LoadPublicSymbolTargetRVAIfPossible(uint rva)
    {
        ThrowIfDisposingOrDisposed();
        ThrowIfOnWrongThread();

        this.DiaSession.findSymbolByRVA(rva, SymTagEnum.SymTagPublicSymbol, out var publicSymbol);

        return publicSymbol?.targetRelativeVirtualAddress;
    }

    #endregion

    #region Symbol Length calculations

    private bool TryGetSymbolLengthForSpecialSymbols(string symbolName,
                                                     out uint specialSymbolLength)
    {
        const uint sizeOfRVA = 4; // RVAs are always 4 bytes, regardless of bitness of the binary

        // These 3 symbols are special and inserted by the linker.  Their length cannot be determined
        // through the DIA API because they are of type "void *" but thye're important enough that it's worth
        // special-casing them here.  We must inspect peer symbols to figure out the real length.
        if (symbolName == "__guard_fids_table")
        {
            var fidsCountSymbol = this.DiaSession.findFirstSymbolByName(this.DiaGlobalScope, SymTagEnum.SymTagData, "__guard_fids_count");

            if (fidsCountSymbol is null)
            {
                specialSymbolLength = 0;
                return false;
            }

            // Yes this is weird, the owner of DIA tells me this is where to look...
            // the virtualAddress property is acutally the count of entries in this array, and it's
            // an array of RVAs which are always 4 bytes.
            var fidsCount = fidsCountSymbol.virtualAddress;
            specialSymbolLength = (uint)(fidsCount * sizeOfRVA);
            return true;
        }
        else if (symbolName == "__guard_iat_table")
        {
            var iatCountSymbol = this.DiaSession.findFirstSymbolByName(this.DiaGlobalScope, SymTagEnum.SymTagData, "__guard_iat_count");

            if (iatCountSymbol is null)
            {
                specialSymbolLength = 0;
                return false;
            }

            var iatCount = iatCountSymbol.virtualAddress;
            specialSymbolLength = (uint)(iatCount * sizeOfRVA);
            return true;
        }
        else if (symbolName == "__guard_longjmp_table")
        {
            var longjmpCountSymbol = this.DiaSession.findFirstSymbolByName(this.DiaGlobalScope, SymTagEnum.SymTagData, "__guard_longjmp_count");

            if (longjmpCountSymbol is null)
            {
                specialSymbolLength = 0;
                return false;
            }

            var longjmpCount = longjmpCountSymbol.virtualAddress;
            specialSymbolLength = (uint)(longjmpCount * sizeOfRVA);
            return true;
        }

        specialSymbolLength = 0;
        return false;
    }

    private uint GetSymbolLength(IDiaSymbol symbol,
                                 string? symbolName)
    {
        if (symbolName != null && TryGetSymbolLengthForSpecialSymbols(symbolName, out var specialSymbolLength))
        {
            return specialSymbolLength;
        }

        switch ((SymTagEnum)symbol.symTag)
        {
            case SymTagEnum.SymTagData:
                // For bitfields "size"/"length" represents the number of bits occupied, which means we should not look
                // at the type - it will always be wrong.
                if ((LocationType)symbol.locationType == LocationType.LocIsBitField)
                {
                    return (uint)symbol.length;
                }
                else if ((LocationType)symbol.locationType == LocationType.LocIsStatic &&
                         symbolName != null &&
                         symbolName.Contains("`vftable", StringComparison.Ordinal))
                {
                    this.DiaSession.findSymbolByRVA(symbol.relativeVirtualAddress, SymTagEnum.SymTagPublicSymbol, out var vtablePublicSymbol);

                    if (vtablePublicSymbol != null && vtablePublicSymbol.relativeVirtualAddress == symbol.relativeVirtualAddress)
                    {
                        return (uint)vtablePublicSymbol.length;
                    }

                    return (uint?)symbol.type?.length ?? (uint)symbol.length;
                }
                else
                {
                    return (uint?)symbol.type?.length ?? (uint)symbol.length;
                }
            default:
                return (uint)symbol.length;
        }
    }

    #endregion

    #region Symbol Names

    // It may not look like GetSymbolName can return null, but it can if it gets an unusual type like a
    // SymTagBaseType with baseType == btNoType.  IDiaSymbol.name comes from tlbimp and is "string" but
    // it should really be "string?" as DIA can return null strings.  So that means GetSymbolName can
    // return a null too, despite what intellisense tells you.
    private string? GetSymbolName(IDiaSymbol diaSymbol)
    {
        var symTag = (SymTagEnum)diaSymbol.symTag;

        // According to DIA2Dump, only SymTagFunction, SymTagData and SymTagPublicSymbol will ever have an undecoratedName
        // However, we don't want to use undecoratedName for functions since we aready have our own custom way of recording
        // the FunctionType and recombining to a "FullName" elsewhere.
        // Data symbols also get complicated because the names for vftable symbols can be too short and we need to refer to
        // the PublicSymbols to find the full name.
        switch (symTag)
        {
            case SymTagEnum.SymTagPublicSymbol:
                return diaSymbol.undecoratedName ?? diaSymbol.name;
            case SymTagEnum.SymTagData:
                var dataSymbolName = diaSymbol.undecoratedName ?? diaSymbol.name;
                var dataKind = (DataKind)diaSymbol.dataKind;
                if (dataKind == DataKind.DataIsGlobal && dataSymbolName.EndsWith("`vftable'", StringComparison.Ordinal))
                {
                    // vtables can have two types of names:
                    // 1) Foo::Bar::`vftable'
                    // 2) Foo::Bar::`vftable'{for `IBaz'}
                    // If this global data's name ends with `vftable', then it's possible it is of the second form but its name seems to be truncated in the DataSymbol
                    // records in the PDB.  We can go look up the full name by finding the PublicSymbol that has the same RVA, where the correct longer name is recorded.
                    // This is extremely important for diffs when a type derives from multiple interfaces, otherwise each of these `vftable' DataSymbols has the same name
                    // and we'll end up basically randomly diffing one vs. the other and it's nonsense.  By having the full names we can ensure we only compare vtables for
                    // the same interfaces.

                    // Note that there can be multiple public symbols that share the same RVA and also contain the name of the data symbol - for example, in COM there are
                    // two marker interfaces called IAgileObject and ICrossApartmentProtocolHandler, which are both just empty marker interfaces that derive from IUnknown,
                    // so these two vtables point to the same addresses, so the vtables themselves are folded together.  But we get two names like:
                    // 1) Foo::Bar::`vftable'{for 'IAgileObject'}
                    // 2) Foo::Bar::`vftable'{for 'ICrossApartmentProtocolHandler'}
                    // In this situation, because we skew towards 'canonicalized' names, we want to pick the first name alphabetically.  This aids in binary diffing being
                    // more stable.

                    var dataSymbolRVA = diaSymbol.relativeVirtualAddress;

                    if (this.DataCache.AllDisambiguatingVTablePublicSymbolNamesByRVA!.TryGetValue(dataSymbolRVA, out var possibleNames))
                    {
                        string? disambiguatedName = null;
                        foreach (var possibleName in possibleNames)
                        {
                            if (possibleName.Contains(dataSymbolName, StringComparison.Ordinal))
                            {
                                if (disambiguatedName == null || String.CompareOrdinal(disambiguatedName, possibleName) > 0)
                                {
                                    disambiguatedName = possibleName;
                                }
                            }
                        }

                        if (disambiguatedName != null)
                        {
                            dataSymbolName = disambiguatedName;
                        }
                    }
                }
                return dataSymbolName;
            case SymTagEnum.SymTagFunction:
                var diaName = diaSymbol.name.Replace(" __ptr64", String.Empty, StringComparison.Ordinal);
                var finalFunctionName = diaName;

                // Sometimes for member functions, DIA has just the function name (CFoo::DoTheThing has IDiaSymbol.name of DoTheThing), and sometimes it has
                // the parent type's name included.  We want to be consistent so we need to also check if this has a parent type and if so, strip that type's
                // name off the beginning.
                //
                // Some binaries seem to generate corrupted/missing classParent objects that end up being a SymTagBaseType with baseType == btNoType, which has
                // no name.  It should only make sense to look further if we've found a UDT, as no other type of parent would make sense to have a function on it,
                // so if we find one of those bad/missing class parents we'll just skip attempting to clean up the name.  We did the best we could.
                var parentClass = diaSymbol.classParent;
                if (parentClass != null &&
                    (SymTagEnum)parentClass.symTag == SymTagEnum.SymTagUDT &&
                    diaName.Contains("::", StringComparison.Ordinal))
                {
                    var parentClassName = GetSymbolName(parentClass);
                    if (parentClassName != null)
                    {
                        var index = finalFunctionName.IndexOf(parentClassName, StringComparison.Ordinal);
                        if (index >= 0)
                        {
                            finalFunctionName = finalFunctionName.Remove(index, parentClassName.Length);
                        }
                    }
                }

                return finalFunctionName.TrimStart(':'); // We may end up with errant "::" at the beginning of C++ symbols after trimming off the parent UDT's name.  Get rid of those.;
            case SymTagEnum.SymTagBlock:
                var parentFunction = diaSymbol.lexicalParent;
                while (parentFunction != null && (SymTagEnum)parentFunction.symTag != SymTagEnum.SymTagFunction)
                {
                    parentFunction = parentFunction.lexicalParent;
                }

                if (parentFunction != null)
                {
                    return $"{CodeBlockSymbol.BlockOfCodePrefix}{GetSymbolName(parentFunction)}";
                }
                else
                {
                    throw new InvalidOperationException("Block found without a parent function - how is this possible?  This is a bug in SizeBench's implementation, not your usage of it.");
                }
            default:
                return diaSymbol.name;
        }
    }

    #endregion

    #region Parsing Type symbols

    private TSymbol GetOrCreateTypeSymbol<TSymbol>(IDiaSymbol diaSymbol, CancellationToken cancellationToken) where TSymbol : TypeSymbol
    {
        TSymbol? returnValue;
        if (this.DataCache.AllTypesBySymIndexId.TryGetValue(diaSymbol.symIndexId, out var parsedSymbol))
        {
            returnValue = parsedSymbol as TSymbol;
        }
        else
        {
            parsedSymbol = ParseTypeSymbol(diaSymbol, cancellationToken);
            returnValue = parsedSymbol as TSymbol;
        }

        if (returnValue is null)
        {
            throw new InvalidOperationException($"We were asked to parse a {typeof(TSymbol).Name} for {diaSymbol.name ?? "<unknown name>"}, but we got back a {parsedSymbol?.GetType().Name ?? "null"} instead, that seems like a mistake.");
        }

        return returnValue;
    }

    private TypeSymbol ParseTypeSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        // At first it may look weird to assign these to local variables, but trust me it's super useful for debugging since VS doesn't like
        // evaluating COM object properties in conditional breakpoints.  This allows conditional breakpoints to be useful here in this very
        // common function.

        var symTag = (SymTagEnum)diaSymbol.symTag;
        var symbolName = diaSymbol.name;
        var symbolLength = (uint)diaSymbol.length;

        return symTag switch
        {
            SymTagEnum.SymTagUDT => ParseUDT(diaSymbol, symbolName, symbolLength, cancellationToken),
            SymTagEnum.SymTagBaseType => ParseBaseType(diaSymbol, symbolLength, cancellationToken),
            SymTagEnum.SymTagEnum => ParseEnumType(diaSymbol, symbolName, symbolLength, cancellationToken),
            SymTagEnum.SymTagArrayType => ParseArrayTypeSymbol(diaSymbol, symbolLength, cancellationToken),
            SymTagEnum.SymTagFunctionType => ParseFunctionTypeSymbol(diaSymbol, cancellationToken),
            SymTagEnum.SymTagPointerType => ParsePointerTypeSymbol(diaSymbol, symbolLength, cancellationToken),
            SymTagEnum.SymTagCustomType => ParseCustomType(diaSymbol, symbolLength),
            _ => throw new InvalidOperationException($"{nameof(ParseTypeSymbol)} shouldn't ever be trying to parse a {symTag}.  This is a bug in SizeBench's implementation, not your use of it."),
        };
    }

    #region ArrayType

    private TypeSymbol ParseArrayTypeSymbol(IDiaSymbol diaSymbol,
                                            uint symbolLength,
                                            CancellationToken cancellationToken)
    {
        // I don't bother to pass in an RVA or name.
        // To my knowledge all ArrayType symbols don't have an RVA (types don't have locations), and the only
        // ArrayType I've ever seen with a name is a FORTRAN array (like a "CHARACTER(len=10)" named "SOME_STRING"),
        // which is a rare use case and I don't care to spend time making the output of that look nice or testing it.
        // So let's assert that RVA is 0, and just ignore any name that may be lurking in the diaSymbol.name here.
        // If someone feels inclined to make FORTRAN output a little nicer, perhaps the diaSymbol.name here will help.
        Debug.Assert(diaSymbol.relativeVirtualAddress == 0);

        if (diaSymbol.unmodifiedType != null)
        {
            var name = new StringBuilder(50);

            var unmodifiedArrayType = GetOrCreateTypeSymbol<TypeSymbol>(diaSymbol.unmodifiedType, cancellationToken);

            if (diaSymbol.constType != 0)
            {
                name.Append("const ");
            }

            if (diaSymbol.volatileType != 0)
            {
                name.Append("volatile ");
            }

            if (diaSymbol.unalignedType != 0)
            {
                name.Append("__unaligned ");
            }

            name.Append(unmodifiedArrayType.Name);
            return new ModifiedTypeSymbol(this.DataCache, unmodifiedArrayType, name.ToString(), symbolLength, diaSymbol.symIndexId);
        }

        var arrayElementTypeSymbol = diaSymbol.type;
        var elementType = GetOrCreateTypeSymbol<TypeSymbol>(arrayElementTypeSymbol, cancellationToken);

        return new ArrayTypeSymbol(this.DataCache,
                                   BuildArrayTypeName(diaSymbol, elementType),
                                   size: symbolLength,
                                   symIndexId: diaSymbol.symIndexId,
                                   elementType: elementType,
                                   elementCount: diaSymbol.count);
    }

    private static string BuildArrayTypeName(IDiaSymbol arrayTypeSymbol, TypeSymbol elementType)
    {
        var sb = new StringBuilder(100);

        // If an array in C++ is multi-dimensional, like this:
        // float[3][2][8]
        // Then it will be represented by an ArrayTypeSymbol that is 8 elements long, then an ArrayTypeSymbol 
        // of *those* that is 2 elements long, then an ArrayTypeSymbol of *those* that is 3 elements long.  But
        // if we naively appended the dimension of the array we'd then get "float[8][2][3]" which is backwards.
        // So we need to dig into the ranks and build them up into the string in inverse order for this scenario.

        var mostFundamentalElementType = elementType;
        while (mostFundamentalElementType is ArrayTypeSymbol mostFundamentalArrayType)
        {
            mostFundamentalElementType = mostFundamentalArrayType.ElementType;
        }

        sb.Append(mostFundamentalElementType.Name);

        FillInArrayDimensions(arrayTypeSymbol, sb, elementType);
        return sb.ToString();
    }

    private static void FillInArrayDimensions(IDiaSymbol arrayTypeSymbol, StringBuilder sb, TypeSymbol elementType)
    {
        if (arrayTypeSymbol.rank > 0)
        {
            // As far as I can tell, only FORTRAN creates arrays that have rank > 0, so this is pretty rarely hit.
            arrayTypeSymbol.findChildren(SymTagEnum.SymTagDimension, null, 0 /* nsNone */, out var enumDimensions);
            foreach (IDiaSymbol? dimensionSymbol in enumDimensions)
            {
                if (dimensionSymbol != null)
                {
                    var lowerBound = dimensionSymbol.lowerBound;
                    var upperBound = dimensionSymbol.upperBound;
                    if (lowerBound != null && upperBound != null)
                    {
                        var lowerBoundStr = ArrayBoundToString(lowerBound);
                        var upperBoundStr = ArrayBoundToString(upperBound);

                        if (lowerBoundStr.Equals("1", StringComparison.Ordinal))
                        {
                            sb.AppendFormat(CultureInfo.InvariantCulture, $"[{upperBoundStr}]");
                        }
                        else
                        {
                            sb.AppendFormat(CultureInfo.InvariantCulture, $"[{lowerBoundStr} .. {upperBoundStr}]");
                        }
                    }
                }
            }
        }
        else if (arrayTypeSymbol.count > 0)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "[{0}]", arrayTypeSymbol.count);
        }
        else if (arrayTypeSymbol.length > 0)
        {
            if (elementType.InstanceSize == 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "[{0}]", arrayTypeSymbol.length);
            }
            else
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "[{0}]", arrayTypeSymbol.length / elementType.InstanceSize);
            }
        }

        while (elementType is ArrayTypeSymbol elemTypeAsArray)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "[{0}]", elemTypeAsArray.ElementCount);
            elementType = elemTypeAsArray.ElementType;
        }
    }

    private static string ArrayBoundToString(IDiaSymbol bound)
    {
        if ((SymTagEnum)bound.symTag == SymTagEnum.SymTagData && (LocationType)bound.locationType == LocationType.LocIsConstant)
        {
            return bound.value.ToString() ?? String.Empty;
        }
        else
        {
            return bound.undecoratedName ?? bound.name;
        }
    }

    #endregion

    #region Basic Types

    private static readonly string[] _basicTypeNames = {
            "NoType", // 0
            "void",
            "char",
            "WCHAR",
            "signed char",
            "unsigned char", // 5
            "int",
            "unsigned int",
            "float",
            "BCD",
            "bool", // 10
            "short",
            "unsigned short",
            "long",
            "unsigned long",
            "__int8", // 15
            "__int16",
            "__int32",
            "__int64",
            "__int128",
            "unsigned __int8", // 20
            "unsigned __int16",
            "unsigned __int32",
            "unsigned __int64",
            "unsigned __int128",
            "currency", // 25
            "date",
            "variant",
            "complex",
            "bit",
            "BSTR",
            "HRESULT",
            "char16_t",
            "char32_t",
            "char8_t",
        };

    private TypeSymbol ParseBaseType(IDiaSymbol diaSymbol,
                                     uint symbolLength,
                                     CancellationToken cancellationToken)
    {
        var name = new StringBuilder(50);

        if (diaSymbol.unmodifiedType != null)
        {
            var unmodifiedBasicType = GetOrCreateTypeSymbol<TypeSymbol>(diaSymbol.unmodifiedType, cancellationToken);

            if (diaSymbol.constType != 0)
            {
                name.Append("const ");
            }

            if (diaSymbol.volatileType != 0)
            {
                name.Append("volatile ");
            }

            if (diaSymbol.unalignedType != 0)
            {
                name.Append("__unaligned ");
            }

            name.Append(unmodifiedBasicType.Name);
            return new ModifiedTypeSymbol(this.DataCache, unmodifiedBasicType, name.ToString(), symbolLength, diaSymbol.symIndexId);
        }

        switch ((BasicTypes)diaSymbol.baseType)
        {
            case BasicTypes.btUInt:
                name.Append("unsigned ");
                goto case BasicTypes.btInt;
            case BasicTypes.btInt:
                switch (symbolLength)
                {
                    case 1:
                        name.Append("char");
                        break;
                    case 2:
                        name.Append("short");
                        break;
                    case 4:
                        name.Append("int");
                        break;
                    case 8:
                        name.Append("int64");
                        break;
                    case 16:
                        name.Append("int128");
                        break;
                    default:
                        throw new InvalidOperationException($"unknown integer type of length {symbolLength}!");
                }
                break;
            case BasicTypes.btFloat:
                switch (symbolLength)
                {
                    case 4:
                        name.Append("float");
                        break;
                    case 8:
                        name.Append("double");
                        break;
                    case 10:
                        name.Append("tbyte [MASM]");
                        break;
                    default:
                        throw new InvalidOperationException($"unknown floating point type of length {symbolLength}!");
                }
                break;
            default:
                name.Append(_basicTypeNames[diaSymbol.baseType]);
                break;
        }

        return new BasicTypeSymbol(this.DataCache, name.ToString(), symbolLength, diaSymbol.symIndexId);
    }

    #endregion

    #region PointerType

    private TypeSymbol ParsePointerTypeSymbol(IDiaSymbol diaSymbol,
                                              uint symbolLength,
                                              CancellationToken cancellationToken)
    {
        // I don't bother to pass in an RVA or name because to my knowledge all PointerType symbols don't have these.
        // So let's assert this - if they ever have a name or an RVA then this function should change to use those.
        Debug.Assert(String.IsNullOrEmpty(diaSymbol.name));
        Debug.Assert(diaSymbol.relativeVirtualAddress == 0);

        if (diaSymbol.unmodifiedType != null)
        {
            var unmodifiedPointerType = GetOrCreateTypeSymbol<TypeSymbol>(diaSymbol.unmodifiedType, cancellationToken);

            var name = new StringBuilder(capacity: unmodifiedPointerType.Name.Length + 50);

            name.Append(unmodifiedPointerType.Name);
            if (diaSymbol.reference != 0)
            {
                name.Append('&');
            }
            else
            {
                name.Append('*');
            }

            if (diaSymbol.constType != 0)
            {
                name.Append(" const");
            }

            if (diaSymbol.volatileType != 0)
            {
                name.Append(" volatile");
            }

            if (diaSymbol.unalignedType != 0)
            {
                name.Append(" __unaligned");
            }

            return new ModifiedTypeSymbol(this.DataCache, unmodifiedPointerType, name.ToString(), symbolLength, diaSymbol.symIndexId);
        }

        var pointerTypeName = BuildPointerTypeName(diaSymbol, out var pointerTargetType, cancellationToken);

        return new PointerTypeSymbol(this.DataCache,
                                     pointerTargetType,
                                     pointerTypeName,
                                     instanceSize: symbolLength,
                                     symIndexId: diaSymbol.symIndexId);
    }

    private string BuildPointerTypeName(IDiaSymbol pointerTypeSymbol,
                                        out TypeSymbol pointerTargetTypeSymbol,
                                        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(100);
        pointerTargetTypeSymbol = GetOrCreateTypeSymbol<TypeSymbol>(pointerTypeSymbol.type, cancellationToken);

        sb.Append(pointerTargetTypeSymbol.Name);
        if (pointerTypeSymbol.reference != 0)
        {
            sb.Append('&');
        }
        else
        {
            sb.Append('*');
        }

        if (pointerTypeSymbol.constType != 0)
        {
            sb.Append(" const");
        }

        if (pointerTypeSymbol.volatileType != 0)
        {
            sb.Append(" volatile");
        }

        if (pointerTypeSymbol.unalignedType != 0)
        {
            sb.Append(" __unaligned");
        }

        return sb.ToString();
    }

    #endregion

    #region EnumType

    private TypeSymbol ParseEnumType(IDiaSymbol diaSymbol,
                                     string symbolName,
                                     uint symbolLength,
                                     CancellationToken cancellationToken)
    {
        var name = new StringBuilder(50);

        if (diaSymbol.unmodifiedType != null)
        {
            var unmodifiedEnumType = GetOrCreateTypeSymbol<TypeSymbol>(diaSymbol.unmodifiedType, cancellationToken);

            if (diaSymbol.constType != 0)
            {
                name.Append("const ");
            }

            if (diaSymbol.volatileType != 0)
            {
                name.Append("volatile ");
            }

            if (diaSymbol.unalignedType != 0)
            {
                name.Append("__unaligned ");
            }

            name.Append(unmodifiedEnumType.Name);
            return new ModifiedTypeSymbol(this.DataCache, unmodifiedEnumType, name.ToString(), symbolLength, diaSymbol.symIndexId);
        }

        return new EnumTypeSymbol(this.DataCache, $"enum {symbolName}", symbolLength, diaSymbol.symIndexId);
    }

    #endregion

    #region User-Defined Types

    private TypeSymbol ParseUDT(IDiaSymbol diaSymbol,
                                string symbolName,
                                uint symbolLength,
                                CancellationToken cancellationToken)
    {
        if (diaSymbol.unmodifiedType != null)
        {
            // Note the type of this is only TypeSymbol, not UserDefinedTypeSymbol, because it may be a ModifiedType (this could be a modification of
            // a modification...)
            var unmodifiedUDT = GetOrCreateTypeSymbol<TypeSymbol>(diaSymbol.unmodifiedType, cancellationToken);

            var name = new StringBuilder(capacity: unmodifiedUDT.Name.Length + 50);

            if (diaSymbol.constType != 0)
            {
                name.Append("const ");
            }

            if (diaSymbol.volatileType != 0)
            {
                name.Append("volatile ");
            }

            if (diaSymbol.unalignedType != 0)
            {
                name.Append("__unaligned ");
            }

            name.Append(unmodifiedUDT.Name);
            return new ModifiedTypeSymbol(this.DataCache, unmodifiedUDT, name.ToString(), symbolLength, diaSymbol.symIndexId);
        }

        return new UserDefinedTypeSymbol(this.DataCache,
                                         this,
                                         this.Session,
                                         symbolName,
                                         symbolLength,
                                         diaSymbol.symIndexId,
                                         (UserDefinedTypeKind)diaSymbol.udtKind,
                                         GetBaseTypeIDs(diaSymbol, symbolName));
    }

    // Base types need two pieces of information to be most useful - the typeID of the base type (uint), and
    // the offset for multi-inheritance situations.  So this is a Dictionary<typeID, offset>
    private static Dictionary<uint, uint>? GetBaseTypeIDs(IDiaSymbol diaSymbol, string symbolName)
    {
        diaSymbol.findChildren(SymTagEnum.SymTagBaseClass, null /* name */, 0 /* compare flags */, out var enumBaseClasses);

        Dictionary<uint, uint>? BaseTypeIDs = null;

        foreach (IDiaSymbol? baseClass in enumBaseClasses)
        {
            if (baseClass is null)
            {
                continue;
            }

            var baseClassTypeId = baseClass.typeId;

            // Don't preallocate this to the size of enumBaseClasses, because enumBaseClasses.count ends up traversing the entire
            // list in DIA.  This is painfully slow.  Just guess that a class will have 4 base types, and let Dictionary resize
            // if needed, it's still faster that way.
            BaseTypeIDs ??= new Dictionary<uint, uint>(4);

            if (baseClass.offset < 0)
            {
                throw new InvalidOperationException($"Base Type offset < 0 when loading the base types for {symbolName}.  This should not be possible, and is a bug in SizeBench's implementation, not your use of it.");
            }

            BaseTypeIDs.Add(baseClassTypeId, (uint)baseClass.offset);
            Marshal.FinalReleaseComObject(baseClass);
        }

        Marshal.FinalReleaseComObject(enumBaseClasses);

        return BaseTypeIDs;
    }

    #endregion

    #region Custom Types

    private TypeSymbol ParseCustomType(IDiaSymbol diaSymbol, uint symbolLength)
    {
        Debug.Assert(diaSymbol.unmodifiedType is null);

        var oemId = diaSymbol.oemId;
        var oemSymbolId = diaSymbol.oemSymbolId;

        // Dia2Dump looks at get_dataBytes and get_types as well, but these aren't marshalling properly at this time and this is such a rare
        // use case that I'm going to ignore this.  At some point, it'd be better to figure out how to fix the marshalling of those two
        // methods, so they can give additional information on this vendor-defined type.

        // There are a few well-known OEM IDs, as documented here: https://github.com/Microsoft/microsoft-pdb/blob/master/include/cvinfo.h
        // We'll give those a friendly name at least.
        var oemName = oemId switch
        {
            0xF090 => "OEM_MS_FORTRAN90",
            0x0010 => "OEM_ODI",
            0x5453 => "OEM_THOMSON_SOFTWARE",
            _ => $"OEM 0x{oemId.ToString("X", CultureInfo.InvariantCulture)}"
        };

        return new CustomTypeSymbol(this.DataCache, oemName, oemSymbolId, symbolLength, diaSymbol.symIndexId);
    }

    #endregion

    #endregion

    #region Parsing Other Symbols

    private TSymbol GetOrCreateSymbol<TSymbol>(IDiaSymbol diaSymbol,
                                               CancellationToken cancellationToken) where TSymbol : class, ISymbol
    {
        TSymbol? returnValue;
        if (this.DataCache.AllSymbolsBySymIndexId.TryGetValue(diaSymbol.symIndexId, out var parsedSymbol))
        {
            returnValue = parsedSymbol as TSymbol;
        }
        else
        {
            parsedSymbol = ParseSymbol(diaSymbol, cancellationToken);
            returnValue = parsedSymbol as TSymbol;
        }

        if (returnValue is null)
        {
            throw new InvalidOperationException($"We were asked to parse a {typeof(TSymbol).Name}, but we got back a {parsedSymbol?.GetType().Name ?? "null"} instead, that seems like a mistake.");
        }

        return returnValue;
    }

    private ISymbol ParseSymbol(IDiaSymbol diaSymbol, CancellationToken cancellationToken)
    {
        // At first it may look weird to assign these to local variables, but trust me it's super useful for debugging since VS doesn't like
        // evaluating COM object properties in conditional breakpoints.  This allows conditional breakpoints to be useful here in this very
        // common function.
        var symTag = (SymTagEnum)diaSymbol.symTag;

        var symbolName = GetSymbolName(diaSymbol);

        try
        {
            //TODO: believe it or not, this next line is a crazy-hot spot for perf.  Looking up the relativeVirtualAddress
            //      of a DataSymbol seems to call into some very slow DIA codepaths.  Perhaps we should investigate why and
            //      file a DIA bug at some point.
            var symbolRVA = diaSymbol.relativeVirtualAddress;

            var symbolLength = GetSymbolLength(diaSymbol, symbolName);

            return (SymTagEnum)diaSymbol.symTag switch
            {
                SymTagEnum.SymTagFunction => ParseFunctionSymbol(diaSymbol, cancellationToken).PrimaryBlock,
                SymTagEnum.SymTagBlock => ParseBlockSymbol(diaSymbol, cancellationToken),
                SymTagEnum.SymTagThunk => new ThunkSymbol(this.DataCache, symbolName ?? "<unknown name>", symbolRVA, symbolLength, diaSymbol.symIndexId),
                SymTagEnum.SymTagData => ParseDataSymbol<StaticDataSymbol>(diaSymbol, symbolName ?? "<unknown name>", symbolLength, symbolRVA, cancellationToken),
                SymTagEnum.SymTagPublicSymbol => ParsePublicSymbol(diaSymbol, symbolName ?? "<unknown name>", symbolLength, symbolRVA),
                SymTagEnum.SymTagUDT or
                SymTagEnum.SymTagArrayType or
                SymTagEnum.SymTagBaseType or
                SymTagEnum.SymTagCustomType or
                SymTagEnum.SymTagEnum or
                SymTagEnum.SymTagFunctionType or
                SymTagEnum.SymTagPointerType => throw new InvalidOperationException($"When parsing a {symTag}, use {nameof(GetOrCreateTypeSymbol)}.  This is a bug in SizeBench's implementation, not your use of it."),
                SymTagEnum.SymTagAnnotation => throw new InvalidOperationException($"When parsing a {symTag}, use {nameof(GetOrCreateAnnotationSymbol)}.  This is a bug in SizeBench's implementation, not your use of it."),
                SymTagEnum.SymTagBaseInterface or
                SymTagEnum.SymTagBaseClass or
                SymTagEnum.SymTagDimension or
                SymTagEnum.SymTagCallee or
                SymTagEnum.SymTagCaller or
                SymTagEnum.SymTagCallSite or
                SymTagEnum.SymTagInlineSite or
                SymTagEnum.SymTagCompilandDetails or
                SymTagEnum.SymTagCompilandEnv or
                SymTagEnum.SymTagFuncDebugStart or
                SymTagEnum.SymTagFuncDebugEnd or
                SymTagEnum.SymTagFunctionArgType => throw new InvalidOperationException($"We shouldn't ever be parsing symbols of this type ({(SymTagEnum)diaSymbol.symTag}) at this point.  This is a bug in SizeBench's implementation, not your use of it."),
                _ => new Symbol(this.DataCache, symbolName ?? "<unknown name>", symbolRVA, symbolLength, this.DataCache.RVARangesThatAreOnlyVirtualSize!.FullyContains(symbolRVA, symbolLength), diaSymbol.symIndexId),
            };
        }
        catch (Exception ex)
        {
            // We try to capture some additional diagnostics when we fail to parse a symbol, because this can happen in a deep stack that doesn't have the symbol's name, and the name
            // is incredibly valuable in determining what to look at, especially when a customer reports a bug that isn't able to share source/binary/pdb.
            throw new InvalidOperationException($"Failed to parse symbol '{symbolName}', which is a {symTag}.  See the inner exception for more details.", ex);
        }
    }

    private MemberDataSymbol GetOrCreateMemberDataSymbol(IDiaSymbol diaSymbol,
                                                         CancellationToken cancellationToken)
    {
        if (this.DataCache.AllMemberDataSymbolsBySymIndexId.TryGetValue(diaSymbol.symIndexId, out var symbol))
        {
            return symbol;
        }

        var dataKind = (DataKind)diaSymbol.dataKind;

        Debug.Assert(dataKind is DataKind.DataIsMember or DataKind.DataIsStaticMember);

        var symbolRVA = diaSymbol.relativeVirtualAddress;

        // Static members have storage somewhere, so they can have an RVA.  We don't care here, but let's ensure all non-static members always have RVA of 0, that's how things are understood
        // now and if it's wrong this data model should change.
        Debug.Assert(dataKind == DataKind.DataIsStaticMember || symbolRVA == 0);

        var symbolName = GetSymbolName(diaSymbol);
        var symbolLength = GetSymbolLength(diaSymbol, symbolName);

        return ParseDataSymbol<MemberDataSymbol>(diaSymbol, symbolName ?? "<unknown name>", symbolLength, symbolRVA, cancellationToken);
    }

    #region Data Symbols

    private TSymbol ParseDataSymbol<TSymbol>(IDiaSymbol diaSymbol,
                                             string symbolName,
                                             uint symbolLength,
                                             uint symbolRVA,
                                             CancellationToken cancellationToken) where TSymbol : class
    {
        object? parsedSymbol = null;
        var dataKind = (DataKind)diaSymbol.dataKind;
        var locType = (LocationType)diaSymbol.locationType;

        var symbolType = diaSymbol.type;
        TypeSymbol? dataSymbolType = null;

        if (symbolRVA != 0 && symbolType != null && dataKind == DataKind.DataIsGlobal && locType == LocationType.LocIsStatic)
        {
            if ((SymTagEnum)symbolType.symTag == SymTagEnum.SymTagPointerType)
            {
                var pointerTargetType = symbolType.type;
                if (pointerTargetType != null &&
                    ((SymTagEnum)pointerTargetType.symTag == SymTagEnum.SymTagNull ||
                     ((SymTagEnum)pointerTargetType.symTag == SymTagEnum.SymTagBaseType && (BasicTypes)pointerTargetType.baseType == BasicTypes.btVoid)))
                {
                    dataSymbolType = ParseNullDataPointerTargetTypeIfPossible(diaSymbol, pointerTargetType, cancellationToken);
                    if (dataSymbolType != null)
                    {
                        // These data symbols are special, they're just a way to refer to an address, they don't themselves take up space - they're sort of like
                        // __declspec(allocate("minATL$__a")) that WRL uses to get a linker-fixed-up pointer to a specific part of the binary.
                        symbolLength = 0;
                    }
                }
            }
            else if ((SymTagEnum)symbolType.symTag == SymTagEnum.SymTagBaseType)
            {
                // Sometimes when we find a basic symbol, it will suggest that we are bigger than we really are.  An example is in coreclr.dll, which uses
                // the LEAF_END_MARKED macro to make symbols like "JIT_MemSet" and "JIT_MemSet_End" where the End is not really taking up space - it is a
                // label for the end of the procedure.  But, it can get encoded by MASM as a uint64 for example, so we think it's 8 bytes long.  That's no
                // good if the next procedure starts just 6 or 7 bytes later as we see them overlapping when that's not reality.
                // So, if we found a basic type for a global/static bit of data, we'll check if there is a corresponding public symbol, which will have the
                // 'true' length.  We'll use that if it says it's smaller.
                var pubSym = this.DiaSession.findFirstSymbolByName(this.DiaGlobalScope, SymTagEnum.SymTagPublicSymbol, symbolName);
                if (pubSym != null)
                {
                    var publicSymbolLength = (uint)pubSym.length;
                    symbolLength = Math.Min(publicSymbolLength, symbolLength);
                }
            }
        }

        if (symbolType != null && dataSymbolType is null)
        {
            dataSymbolType = GetOrCreateTypeSymbol<TypeSymbol>(symbolType, cancellationToken);
        }

#if DEBUG
        // The compiler for "/clr" mode can generate symbols with names like "$SigTok$<stuff>" which is zero bytes long, at RVA 0, and it does not have a type (diaSymbol.type will be null here).
        // We're tolerant of a null type anyway, so we'll just let this pass through, but in debug builds let's validate that it's really got a 0 length and 0 RVA in case there's other cases
        // that should be better-understood of this "missing type" problem.
        if (symbolType is null && symbolLength != 0)
        {
            throw new InvalidOperationException("A data symbol was found without a type, but it's not zero-length.  How is this possible?");
        }
#endif

        // For static data, we know how to retrieve the Compiland it came from - but not all data symbols can trace back to a compiland (like DataIsConstant, or a DataIsMember)
        if (dataKind is DataKind.DataIsFileStatic or DataKind.DataIsGlobal or DataKind.DataIsStaticLocal)
        {
            Debug.Assert(locType == LocationType.LocIsStatic);
            var lexicalParent = diaSymbol.lexicalParent;
            Compiland? referencedIn = null;
            IFunctionCodeSymbol? functionParent = null;

            // The lexical parent could be a function if this is a DataIsStaticLocal in which case we can disambiguate the name based on the function it is inside (this happens, for example,
            // with function-level static const/constexpr arrays).
            // We can also sometimes find a compiland up the parent chain (direct or indirect from the function).
            // But in some cases we won't find either if, for example, the parent is a SymTagExe.  In that case, c'est la vie, we'll go without both.
            while (lexicalParent != null)
            {
                var parentSymTag = (SymTagEnum)lexicalParent.symTag;
                if (parentSymTag == SymTagEnum.SymTagCompiland)
                {
                    if (this.DataCache.AllCompilands is null)
                    {
                        // This has the side-effect of updating cache.AllCompilands
                        _ = this.Session.EnumerateCompilands(cancellationToken).Result;
                    }

                    // It's possible to be unable to get a referencedIn compiland, if the PDB does not contain any compiland information, such
                    // as the PDBs currently produced by lld-link.  So we'll do our best here, but if we can't find one, the DataSymbol will
                    // lack this information.  We did our best.
                    referencedIn = this.DataCache.AllCompilands!.FirstOrDefault(c => c.SymIndexId == lexicalParent.symIndexId);
                }
                else if (parentSymTag == SymTagEnum.SymTagFunction)
                {
                    functionParent = GetOrCreateFunctionSymbol(lexicalParent, cancellationToken);
                }

                lexicalParent = lexicalParent.lexicalParent;
            }

            parsedSymbol = new StaticDataSymbol(this.DataCache,
                                                symbolName,
                                                symbolRVA,
                                                symbolLength,
                                                this.DataCache.RVARangesThatAreOnlyVirtualSize!.FullyContains(symbolRVA, symbolLength),
                                                diaSymbol.symIndexId,
                                                dataKind,
                                                dataSymbolType,
                                                referencedIn,
                                                functionParent);
        }
        else if (dataKind is DataKind.DataIsMember or DataKind.DataIsStaticMember)
        {
            var offset = dataKind == DataKind.DataIsMember ? diaSymbol.offset : 0;
            var isBitField = diaSymbol.locationType == (int)LocationType.LocIsBitField;
            Debug.Assert(dataKind == DataKind.DataIsStaticMember || symbolRVA == 0);

            if (dataSymbolType is null)
            {
                throw new InvalidOperationException($"A member data symbol ({symbolName}) was found, but its type was not - this shouldn't ever happen.  This is a bug in SizeBench's implementation, not your use of it.");
            }

            parsedSymbol = new MemberDataSymbol(this.DataCache,
                                                symbolName,
                                                symbolLength,
                                                diaSymbol.symIndexId,
                                                dataKind == DataKind.DataIsStaticMember,
                                                isBitField,
                                                isBitField ? (ushort)diaSymbol.bitPosition : (ushort)0,
                                                offset,
                                                dataSymbolType);
        }
        else if (dataKind == DataKind.DataIsParam)
        {
            if (dataSymbolType is null)
            {
                throw new InvalidOperationException($"A parameter data symbol ({symbolName}) was found, but its type was not - this shouldn't ever happen.  This is a bug in SizeBench's implementation, not your use of it.");
            }

            parsedSymbol = new ParameterDataSymbol(this.DataCache, symbolName, diaSymbol.symIndexId, dataSymbolType);
        }

        if (parsedSymbol is TSymbol parsedAsTSymbol)
        {
            return parsedAsTSymbol;
        }
        else
        {
            throw new InvalidOperationException($"Caller asked for a {typeof(TSymbol).Name}, but we parsed this as a {parsedSymbol?.GetType().Name ?? "null"} " +
                                                $"with DataKind={dataKind}, which should not have happened.  This is a bug in SizeBench's implementation, not your use of it.");
        }
    }

    private TypeSymbol? ParseNullDataPointerTargetTypeIfPossible(IDiaSymbol dataDiaSymbol,
                                                                 IDiaSymbol symbolTypePointerTargetType,
                                                                 CancellationToken cancellationToken)
    {
        // It is very rare to end up here, and so far this has only been observed with a construct from MASM.
        // Imagine you have this in MASM:
        //      public  MyLabelEntry
        //      MyLabelEntry label ptr proc
        // This is a pointer to a procedure, so MASM generates a data symbol for the pointer, and gives it a SymTagPointer,
        // but the Pointer's TargetType is SymTagNull, so we end up here.
        // MASM sometimes seems to also generate these symbols as a void* (SymTagPointer pointing to SymTagBaseType with btVoid).
        // Given these MASM cases are the only situations we know of that can generate this, we'll check if the language of
        // the symbol here is MASM.  If it is, then we'll assume it's a SymTagNull/SymTagBaseType(void) that's pointing to a
        // procedure and give back something.

        if (symbolTypePointerTargetType.unmodifiedType is not null)
        {
            return null;
        }

        // See if we can find a PublicSymbol with a matching name, which will help further confirm our guess that this is a pointer to code
        // before we commit to that guess.  If that symbol says it's code and a dataExport it's pretty likely we're in this weird MASM case.
        var pubSym = this.DiaSession.findFirstSymbolByName(this.DiaGlobalScope, SymTagEnum.SymTagPublicSymbol, dataDiaSymbol.name);
        if (pubSym != null && pubSym.code == 1 && pubSym.dataExport == 1)
        {
            // But let's be even more paranoid about only doing this when we're sure, so we'll check the langauge is MASM too.
            var symbolLanguage = this.DiaSession.LanguageOfSymbolAtRva(dataDiaSymbol.relativeVirtualAddress);
            if (symbolLanguage == CompilandLanguage.CV_CFL_MASM)
            {
                if (this.DataCache.AllTypesBySymIndexId.TryGetValue(symbolTypePointerTargetType.symIndexId, out var foundType))
                {
                    return foundType;
                }

                var voidTypeDiaSymbol = this.DiaSession.findVoidBasicType();
                var voidType = GetOrCreateTypeSymbol<TypeSymbol>(voidTypeDiaSymbol, cancellationToken);
                while (voidType is ModifiedTypeSymbol modifiedVoid)
                {
                    voidType = modifiedVoid.UnmodifiedTypeSymbol;
                }
                var functionTypeName = BuildFunctionTypeName(argumentTypes: null, returnValueType: voidType, functionIsConst: false, functionIsVolatile: false, functionIsUnaligned: false);
                return new FunctionTypeSymbol(this.DataCache, functionTypeName, 0, symbolTypePointerTargetType.symIndexId, isConst: false, isVolatile: false, argumentTypes: null, returnValueType: voidType);
            }
        }

        return null;
    }

    #endregion

    #region Public Symbols

    private PublicSymbol ParsePublicSymbol(IDiaSymbol diaSymbol,
                                           string symbolName,
                                           uint symbolLength,
                                           uint symbolRVA)
    {
        // `string' is among the worst things for us because it's useless to a user since it's so generic, and it's impossible to meaningfully
        // diff with that name.  So, we special-case this symbol name to go look up the real string data in the binary.  It'd be nice if this
        // mechanism of special-casing certain "bad symbol patterns" was more generic but this is enough for the moment.  If this list of
        // special cases grows much, prefer an extensibility mechanism for easier testing/maintenance...
        if (String.Equals(symbolName, "`string'", StringComparison.Ordinal))
        {
            var stringData = this.Session.PEFile!.LoadStringByRVA(symbolRVA, symbolLength, out var isUnicodeString)
                                                 .Replace("\n", @"\n", StringComparison.Ordinal)
                                                 .Replace("\r", @"\r", StringComparison.Ordinal)
                                                 .Replace("\t", @"\t", StringComparison.Ordinal);

            return new StringSymbol(this.DataCache,
                                    symbolName,
                                    stringData,
                                    isUnicodeString,
                                    symbolRVA,
                                    symbolLength,
                                    this.DataCache.RVARangesThatAreOnlyVirtualSize!.FullyContains(symbolRVA, symbolLength),
                                    diaSymbol.symIndexId,
                                    diaSymbol.targetRelativeVirtualAddress);
        }

        return new PublicSymbol(this.DataCache,
                                symbolName,
                                symbolRVA,
                                symbolLength,
                                this.DataCache.RVARangesThatAreOnlyVirtualSize!.FullyContains(symbolRVA, symbolLength),
                                diaSymbol.symIndexId,
                                diaSymbol.targetRelativeVirtualAddress);
    }

    #endregion

    #endregion

    #region RVA -> Name

    public string SymbolNameFromRva(uint rva)
    {
        ThrowIfOnWrongThread();
        this.DiaSession.findSymbolByRVA(rva, SymTagEnum.SymTagNull, out var symbol);
        return GetSymbolName(symbol) ?? "<unknown name>";
    }

    public uint SymbolRvaFromName(string name, bool preferFunction)
    {
        ThrowIfOnWrongThread();
        this.DiaSession.findChildren(this._globalScope, SymTagEnum.SymTagNull, name, (uint)NameSearchOptions.nsfCaseSensitive, out var enumSymbols);
        var symbolsFound = enumSymbols.count;
        if (symbolsFound == 0)
        {
            return UInt32.MaxValue;
        }
        else if (symbolsFound == 1 || (symbolsFound > 1 && false == preferFunction))
        {
            var symbol = enumSymbols.Item(0);
            return symbol.relativeVirtualAddress;
        }
        else
        {
            // We have multiple symbols and we prefer functions, see if we can find a function among
            // the results, otherwise just use whatever.

            var bestCandidate = enumSymbols.Item(0);
            for (uint i = 0; i < enumSymbols.count; i++)
            {
                var candidate = enumSymbols.Item(i);
                if (preferFunction && (SymTagEnum)candidate.symTag == SymTagEnum.SymTagFunction)
                {
                    bestCandidate = candidate;
                    break;
                }
                else if (!preferFunction)
                {
                    bestCandidate = candidate;
                }
            }

            return bestCandidate.relativeVirtualAddress;
        }

    }

    public CompilandLanguage LanguageOfSymbolAtRva(uint rva)
        => this.DiaSession.LanguageOfSymbolAtRva(rva);

    #endregion

    #region IDisposable

    private void ThrowIfDisposingOrDisposed()
    {
        if (this.IsDisposing || this.IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    private bool IsDisposing;
    private bool IsDisposed;

    private void Dispose(bool disposing)
    {
        if (this.IsDisposing || this.IsDisposed)
        {
            return;
        }

        this.IsDisposing = true;

        if (disposing)
        {
            this._cache = null;
            this._session = null;
        }

        this._globalScope = null;
        this._diaSession = null;
        this._diaDataSource = null;

        this.IsDisposing = false;
        this.IsDisposed = true;
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~DIAAdapter()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
