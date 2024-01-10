using System.Diagnostics;
using System.Runtime.InteropServices;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.PE;

internal abstract unsafe class EHSymbolParser
{
    #region Common EH Structures, Flags, etc...

    protected enum CxxDataMagic : uint
    {
        EH_MAGIC_NUMBER1 = 0x19930520,
        EH_MAGIC_NUMBER2 = 0x19930521,
        EH_MAGIC_NUMBER3 = 0x19930522,
    }

    // Taken from Visual Studio ehdata.h, "_s_FuncInfo"
    [StructLayout(LayoutKind.Sequential)]
    protected readonly struct FUNCINFO
    {
        public readonly CxxDataMagic dwMagic; // First 29 bits are version of compiler, last 3 bits are BBT
        public readonly uint dwMaxState;
        public readonly uint pumeRva; // pume == "pointer to UnwindMapEntry"
        public readonly uint dwTryBlocks;
        public readonly uint ptbmeRva; // ptbme == "pointer to TryBlockMapEntry"
        public readonly uint dwIPToStateEntries;
        public readonly uint ip2statemeRva; // ip2stateme == "IPtoStateMapEntry"
        public readonly int dispUnwindHelp; // Displacement of unwind helpers from base (new in __CxxFrameHandler2)
        public readonly uint ESTypeListPtr; // List of types for exception specifications (new in __CxxFrameHandler3)
        public readonly int EHFlags;
    }

    // Used by __C_specific_handler, the SCOPE_TABLE is defined as a UInt32 count and then an array of these
    // like so:
    //    typedef struct _SCOPE_TABLE
    //    {
    //        ULONG Count;
    //        struct {
    //            ULONG BeginAddress;
    //            ULONG EndAddress;
    //            ULONG HandlerAddress;
    //            ULONG JumpTarget;
    protected readonly struct ScopeRecord
    {
        public readonly uint BeginAddress;
        public readonly uint EndAddress;
        public readonly uint HandlerAddress;
        public readonly uint JumpTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    protected readonly struct UnwindMapEntry
    {
        public readonly int toState;
        public readonly uint actionRva;
    }

    [StructLayout(LayoutKind.Sequential)]
    protected readonly struct TryBlockMapEntry
    {
        public readonly int tryLow;
        public readonly int tryHigh;
        public readonly int catchHigh;
        public readonly int nCatches;
        public readonly uint handlerArrayRVA; // points to an array of HandlerType
    }

    [StructLayout(LayoutKind.Sequential)]
    protected readonly struct HandlerType
    {
        public readonly uint adjectives;
        public readonly uint dispType; // RVA of type descriptor
        public readonly uint dispCatchObj; // Displacement of catch object from base
        public readonly uint dispOfHandler; // RVA of 'catch' funclet
        public readonly uint dispFrame; // Displacement of address of function frame w.r.t. establisher frame
    }

    [StructLayout(LayoutKind.Sequential)]
    protected readonly struct IpToStateMapEntry
    {
        public readonly uint IpRva;
        public readonly int State;
    }

    [Flags]
    protected enum GS_UNWIND_Flags
    {
        // If this is set, there's 3 DWORDs of data in the GS cookie, otherwise there's 1 DWORD
        UNW_GSALIGNEDFRAME = 0x4
    }

    #endregion

    protected PEFile PEFile { get; }
    protected MachineType MachineType => this.PEFile.MachineType;
    private SortedList<uint, XDataSymbol> XdataSymbols { get; } = new SortedList<uint, XDataSymbol>();
    private readonly IDIAAdapter _diaAdapter;
    protected byte* LibraryBaseAddress { get; }
    protected List<uint> NoLanguageSpecificDataHandlers { get; }

    protected uint? _cxxFrameHandlerRva { get; set; }
    protected uint? _cxxFrameHandler2Rva { get; set; }
    protected uint? _cxxFrameHandler3Rva { get; set; }
    protected uint? _cxxFrameHandler4Rva { get; set; }
    protected uint? _c_specific_handlerRva { get; set; }
    protected uint? _c_specific_handler_noexceptRva { get; set; }
    protected uint? __GSHandlerCheck_SEHRva { get; set; }
    protected uint? __GSHandlerCheck_SEH_noexceptRva { get; set; }
    protected uint? __GSHandlerCheck_EHRva { get; set; }
    protected uint? __GSHandlerCheck_EH4Rva { get; set; }
    protected uint? __GSHandlerCheckRva { get; set; }

    protected EHSymbolParser(IDIAAdapter diaAdapter,
                             byte* libraryBaseAddress,
                             PEFile peFile)
    {
        this._diaAdapter = diaAdapter;
        this.LibraryBaseAddress = libraryBaseAddress;
        this.PEFile = peFile;

        // These handlers are all handlers that don't have language-specific data.  Sometimes called "KnownExceptionHandlers" in some MS-internal tools.
        this.NoLanguageSpecificDataHandlers = new List<uint>()
            {
                this._diaAdapter.SymbolRvaFromName("RtlpExceptionHandler", true),
                this._diaAdapter.SymbolRvaFromName("RtlpEmUnwindHandler", true),
                this._diaAdapter.SymbolRvaFromName("KiSystemServiceHandler", true),
                this._diaAdapter.SymbolRvaFromName("KiUserApcHandler", true),
                this._diaAdapter.SymbolRvaFromName("KiEmulateFloatExceptHandler", true),
                this._diaAdapter.SymbolRvaFromName("UMThunkUnwindFrameChainHandler", true),
                this._diaAdapter.SymbolRvaFromName("KiInterruptHandler", true),
                this._diaAdapter.SymbolRvaFromName("KiSwitchKernelStackAndCalloutHandler", true),
                this._diaAdapter.SymbolRvaFromName("RtlpUnwindHandler", true),
                this._diaAdapter.SymbolRvaFromName("ProcessCLRException", true),
                this._diaAdapter.SymbolRvaFromName("FixRedirectContextHandler", true),
                this._diaAdapter.SymbolRvaFromName("HijackHandler", true),
                this._diaAdapter.SymbolRvaFromName("FixContextHandler", true),
                this._diaAdapter.SymbolRvaFromName("KiFatalExceptionHandler", true),
                this._diaAdapter.SymbolRvaFromName("_guard_icall_handler", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler0", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler1", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler2", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler3", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler4", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler5", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler6", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler7", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler8", true),
                this._diaAdapter.SymbolRvaFromName("KiCustomAccessHandler9", true),
                this._diaAdapter.SymbolRvaFromName("KiFilterFiberContext", true)
            };
    }

    protected abstract SortedList<uint, PDataSymbol> ParsePDataForArchitecture(SessionDataCache cache);
    protected abstract void ParseXDataForArchitecture(RVARange? XDataRVARange, SessionDataCache cache);
    protected abstract uint GetGSDataSizeAdjusted(GS_UNWIND_Flags gsdata);

    internal void Parse(RVARange? XDataRVARange, SessionDataCache cache, ILogger logger)
    {
        // First we parse PDATA because we may need it fully completed before we begin wandering into XDATA (such as if the XDATA
        // targets a data symbol and to materialize the data symbol we need to enumerate all libs and compilands, which in turn
        // needs to know the PDATA to establish section and COFF Group contributions)
        using (logger.StartTaskLog("Parsing PDATA"))
        {
            cache.PDataSymbolsByRVA = ParsePDataForArchitecture(cache);
        }

        using (logger.StartTaskLog("Parsing XDATA"))
        {
            ParseXDataForArchitecture(XDataRVARange, cache);

            var xdataRanges = new List<RVARange>();

            if (XDataRVARange != null)
            {
                xdataRanges.Add(XDataRVARange);
            }

            foreach (var symbol in this.XdataSymbols.Values)
            {
                xdataRanges.Add(new RVARange(symbol.RVA, symbol.RVAEnd));
            }

            cache.XDataRVARanges = RVARangeSet.FromListOfRVARanges(xdataRanges, maxPaddingToMerge: 8);
        }

        cache.XDataSymbolsByRVA = this.XdataSymbols;

        DebugValidateAllXDataSymbolsContainedInXDataRanges(cache);
    }

    [Conditional("DEBUG")]
    private static void DebugValidateAllXDataSymbolsContainedInXDataRanges(SessionDataCache cache)
    {
        // In debug mode, let's check that every symbol we actually found is represented in the RVA ranges - if it's not
        // then we've found a straggler in our logic for calculating where XData symbols are.
        foreach (var xdataSymbol in cache.XDataSymbolsByRVA!.Values)
        {
            Debug.Assert(cache.XDataRVARanges!.Contains(xdataSymbol.RVA));
            Debug.Assert(cache.XDataRVARanges!.Contains(xdataSymbol.RVAEnd));
        }
    }

    protected Symbol? GetTargetSymbolForRVA(uint rva)
    {
        var targetSymbol = this._diaAdapter.FindSymbolByRVA(rva, allowFindingNearest: true, CancellationToken.None);

        // In some binaries there is code that does not have a name or a symbol - it's unclear how this happens, but it can happen and WinDbg cannot
        // see any name for that code.  In that case, the best we can do is pass a null Symbol along and we will have to conjure a name based on the TargetSymbolRVA alone.
        if (targetSymbol != null && rva >= targetSymbol.RVA && rva <= targetSymbol.RVAEnd)
        {
            return (Symbol)targetSymbol;
        }
        else
        {
            return null;
        }
    }

    protected T[] ParsePDATA<T>(byte* libraryBaseAddress, SessionDataCache cache)
    {
        var exceptionDirectory = this.PEFile.ExceptionDirectory;

        // We use the size out of the ExceptionDirectory instead of the one from ImageDirectoryEntryToDataEx, because the one from the P/Invoke
        // is limited to ushort and the exception directory is often above 64KB in size so it can't be represented appropriately there.
        PInvokes.ImageDirectoryEntryToDataEx(libraryBaseAddress, false, IMAGE_DIRECTORY_ENTRY.Exception, out _ /* size */, out var headerPtr);

        // If we don't find an Exception directory, then this binary has no pdata.  An example of this is an apiset DLL like
        // api-ms-win-core-fibers-l1-1-0.dll
        if (headerPtr == IntPtr.Zero)
        {
            cache.PDataRVARange = new RVARange(0, 0);
            cache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>();
            cache.XDataRVARanges = new RVARangeSet();
            cache.XDataSymbolsByRVA = new SortedList<uint, XDataSymbol>();
            return Array.Empty<T>();
        }

        var header = Marshal.PtrToStructure<IMAGE_SECTION_HEADER>(headerPtr);
        var pdataAddress = new IntPtr(libraryBaseAddress + header.VirtualAddress);
        var arr = new T[exceptionDirectory.Size / Marshal.SizeOf<T>()];
        var handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
        try
        {
            var arrPtr = handle.AddrOfPinnedObject();
            PInvokes.memcpy(arrPtr, pdataAddress, new UIntPtr(exceptionDirectory.Size));
        }
        finally
        {
            handle.Free();
        }

        cache.PDataRVARange = RVARange.FromRVAAndSize(exceptionDirectory.VirtualAddress, exceptionDirectory.Size);
        return arr;
    }

    protected void AddXData(XDataSymbol xds) => this.XdataSymbols.TryAdd(xds.RVA, xds);

    protected void ParseOneExceptionHandler(Symbol? targetSymbol, uint rfStartRva, uint rfUnwindInfoRva, byte* pExceptionHandlerRva, uint exceptionHandlerRva, byte* unwindInfoStart)
    {
        var pLSData = pExceptionHandlerRva + 4;
        uint sizeOfLanguageSpecificData = 0;
        uint sizeOfGSData = 0;

        // We only know what to expect in the "language-specific data" part of the UNWIND_INFO if it's one of
        // a very few specific types today (see the list of functions in the constructor of this parser).
        // If a new type is found, a new parser needs to be added for accurate pdata/xdata - do it here

        var isGSEH = exceptionHandlerRva == this.__GSHandlerCheck_EHRva;
        var isGSEH4 = exceptionHandlerRva == this.__GSHandlerCheck_EH4Rva;
        var isCxx = exceptionHandlerRva == this._cxxFrameHandlerRva;
        var isCxx2 = exceptionHandlerRva == this._cxxFrameHandler2Rva;
        var isCxx3 = exceptionHandlerRva == this._cxxFrameHandler3Rva;
        var isCxx4 = exceptionHandlerRva == this._cxxFrameHandler4Rva;
        var isCsh = exceptionHandlerRva == this._c_specific_handlerRva || exceptionHandlerRva == this._c_specific_handler_noexceptRva;
        var isGSSEH = exceptionHandlerRva == this.__GSHandlerCheck_SEHRva || exceptionHandlerRva == this.__GSHandlerCheck_SEH_noexceptRva;
        var isGSH = exceptionHandlerRva == this.__GSHandlerCheckRva;

        if (!isGSEH && !isGSEH4 && !isCxx && !isCxx2 && !isCxx3 && !isCxx4 && !isCsh && !isGSSEH && !isGSH &&
            !this.NoLanguageSpecificDataHandlers.Contains(exceptionHandlerRva))
        {
            var symbolLanguage = this._diaAdapter.LanguageOfSymbolAtRva(rfStartRva);

            // Code built by MASM seems to end up with really strange xdata - if we end up here, but the symbol is from a compiland with language == MASM, we'll just
            // move along.
            if (symbolLanguage == CompilandLanguage.CV_CFL_MASM)
            {
                return;
            }

            // Some code doesn't have a language defined - we'll not try to parse xdata in this case.  This isn't great, but clang doesn't
            // include Compiland symbols at all and it seems to generate some xdata we can't deal with - to unblock being able to open pdbs
            // and binaries built by Clang, moving on from here keeps most of SizeBench functional while reducing its ability to help peer
            // into xdata.
            if (symbolLanguage == CompilandLanguage.Unknown)
            {
                return;
            }

            // If the exceptionHandlerRva points to a public symbol, it's possible this PDB/binary was built using incremental linking.  
            // So we can end up finding Incremental Linking Thunks (ILTs) as the exception handler - but they're not interesting, so
            // we need to go to the 'target' of that public symbol - as that's the real thing we're dealing with.
            // This has been seen in a locally-built debug version of hxcomm.dll/.pdb, where the exceptionHandlerRva pointed to a public
            // symbol that had the name "@ILT+224720(__GSHandlerCheck)" - and the target of that was __GSHandlerCheck, which of course
            // we know how to parse.
            //
            // Looking up the symbol for exceptionHandlerRva is expensive so we try to avoid it unless we're in this 'about to die' path.
            // It's safe to look at the Result directly and force sync, since we know this code in the EHSymbolParser
            // can only run on the DIA thread.
            var exceptionHandlerPublicSymbolTargetRVA = this._diaAdapter.LoadPublicSymbolTargetRVAIfPossible(exceptionHandlerRva);
            if (exceptionHandlerPublicSymbolTargetRVA is not null)
            {
                ParseOneExceptionHandler(targetSymbol, rfStartRva, rfUnwindInfoRva, pExceptionHandlerRva, exceptionHandlerPublicSymbolTargetRVA.Value, unwindInfoStart);
                return;
            }

            throw new InvalidOperationException($"New xdata handler type found - no parser available yet.\n" +
                                                $"Exception Handler: {this._diaAdapter.SymbolNameFromRva(exceptionHandlerRva)} (RVA:0x{exceptionHandlerRva:X})\n" +
                                                $"Function with that handler: {this._diaAdapter.SymbolNameFromRva(rfStartRva)} (RVA:0x{rfStartRva:X})");
        }

        if (isGSEH || isCxx || isCxx2 || isCxx3)
        {
            var cppxdataRva = (uint)(*(int*)pLSData);
            var cppxdataPtr = (int*)(this.LibraryBaseAddress + cppxdataRva);
            var CppXdata = (FUNCINFO*)cppxdataPtr;

            if (CppXdata->dwMagic != CxxDataMagic.EH_MAGIC_NUMBER3)
            {
                throw new InvalidOperationException("New CxxDataMagic found, need to write a parser!  This is a bug in SizeBench's implementation, not your use of it.");
            }

            if (!this.XdataSymbols.ContainsKey(cppxdataRva))
            {
                // Because of the check above, we know we have EH_MAGIC_NUMBER3 here, so parse it as v3.
                ParseCppXdataV3(targetSymbol, rfStartRva, CppXdata, cppxdataRva);
            }

            sizeOfLanguageSpecificData = 4; // size of the FuncInfoRva
        }
        else if (isGSEH4 || isCxx4)
        {
            var cppxdataRva = (uint)(*(int*)pLSData);

            if (!this.XdataSymbols.ContainsKey(cppxdataRva))
            {
                ParseCppXdataV4(targetSymbol, rfStartRva, cppxdataRva);
            }

            sizeOfLanguageSpecificData = 4; // size of the FuncInfo4 Rva
        }
        else if (isCsh || isGSSEH)
        {
            // deal with scope tables that can be GS or C scope tables!
            var countOfScopeRecords = *((uint*)pLSData);
            sizeOfLanguageSpecificData = 4 /* size of count */ + (uint)(countOfScopeRecords * Marshal.SizeOf<ScopeRecord>());
        }

        if (isGSEH || isGSEH4 || isGSSEH || isGSH)
        {
            var gsdata = (GS_UNWIND_Flags)0;

            if (isGSEH || isGSEH4)
            {
                // add GSData right after the FUNCINFO/FuncInfo4
                gsdata = (GS_UNWIND_Flags)(*(int*)(pLSData + 4));
            }
            else if (isGSSEH)
            {
                // Add GS data after the scope table for SEH
                var countOfScopeRecords = *((uint*)pLSData);
                var sizeOfScopeTable = 4 /* size of count */ + (uint)(countOfScopeRecords * Marshal.SizeOf<ScopeRecord>());

                gsdata = (GS_UNWIND_Flags)(*(int*)(pLSData + sizeOfScopeTable));
            }
            else if (isGSH)
            {
                // Add GS data as the only language-specific data
                gsdata = (GS_UNWIND_Flags)(*(int*)(pLSData));
            }

            sizeOfGSData = sizeof(uint) + GetGSDataSizeAdjusted(gsdata);
        }

        AddXData(new UnwindInfoSymbol(targetSymbol, rfStartRva, rfUnwindInfoRva, (uint)(pLSData - unwindInfoStart + sizeOfLanguageSpecificData + sizeOfGSData)));
    }

    protected void ParseCppXdataV3(Symbol? targetSymbol, uint runtimeFuncionStartRva, FUNCINFO* CppXdata, uint cppxdataRva)
    {
        // [cppxdata]
        AddXData(new CppXdataSymbol(targetSymbol, runtimeFuncionStartRva, cppxdataRva, (uint)Marshal.SizeOf<FUNCINFO>()));

        // [stateUnwindMap], if one is present
        if (CppXdata->dwMaxState > 0 && CppXdata->pumeRva > 0)
        {
            AddXData(new StateUnwindMapSymbol(targetSymbol, runtimeFuncionStartRva, CppXdata->pumeRva, (uint)(CppXdata->dwMaxState * Marshal.SizeOf<UnwindMapEntry>())));
        }

        // [tryMap], if one is present
        if (CppXdata->dwTryBlocks > 0 && CppXdata->ptbmeRva > 0)
        {
            AddXData(new TryMapSymbol(targetSymbol, runtimeFuncionStartRva, CppXdata->ptbmeRva, (uint)(CppXdata->dwTryBlocks * Marshal.SizeOf<TryBlockMapEntry>())));

            var tryBlockMap = (TryBlockMapEntry*)(this.LibraryBaseAddress + CppXdata->ptbmeRva);
            // [handlerMap]
            if (tryBlockMap->nCatches > 0 && tryBlockMap->handlerArrayRVA > 0)
            {
                AddXData(new HandlerMapSymbol(targetSymbol, runtimeFuncionStartRva, tryBlockMap->handlerArrayRVA, (uint)(tryBlockMap->nCatches * Marshal.SizeOf<HandlerType>())));
            }
        }

        // [ip2StateMap]
        if (CppXdata->dwIPToStateEntries > 0 && CppXdata->ip2statemeRva > 0)
        {
            AddXData(new IpToStateMapSymbol(targetSymbol, runtimeFuncionStartRva, CppXdata->ip2statemeRva, (uint)(CppXdata->dwIPToStateEntries * Marshal.SizeOf<IpToStateMapEntry>())));
        }
    }

    #region __CxxFrameHandler 4 support

    // This code is, to the best of my ability, a faithful port of ehdata4_export.h from C++ -> C#.  Function names are left identical to ehdata4_export.h to aid with comparing the two.

    // From ehdata4_export.h in Visual Studio 2019
    [StructLayout(LayoutKind.Sequential)]
    protected struct FuncInfo4
    {
        // The header isn't meant to be read directly, use the properties to get the specific bits
        public byte FuncInfoHeader;
        public readonly bool isCatch => (this.FuncInfoHeader & 1) != 0; // True if this is a catch funclet, otherwise false
        public readonly bool isSeparated => (this.FuncInfoHeader & 1 << 1) != 0; // True if this function has separated code segments, false otherwise
        public readonly bool BBT => (this.FuncInfoHeader & 1 << 2) != 0; // True if set by Basic Block Transformations
        public readonly bool UnwindMap => (this.FuncInfoHeader & 1 << 3) != 0; // True if there is an Unwind Map RVA
        public readonly bool TryBlockMap => (this.FuncInfoHeader & 1 << 4) != 0; // True if these is a Try Black Map RVA
        public readonly bool EHs => (this.FuncInfoHeader & 1 << 5) != 0; // True if EHs flag is set
        public readonly bool NoExcept => (this.FuncInfoHeader & 1 << 6) != 0; // True if noexcept

        /* FuncInfoHeader last bit is reserved */

        public uint bbtFlags; // Flags that may be set by BBT processing
        public int dwMaxState;
        public int dispUnwindMap; // RVA of the unwind map
        public int dispTryBlockMap; // RVA of the handler map
        public int dispIPtoStateMap; // RVA of the IP to state map
        public SepIPToStateMap4? SeparatedIP2StateMap;
        public uint dispFrame; // Displacement of address of function frame wrt establisher frame, only used for catch funclets
    }

    protected readonly struct UnwindMapEntry4
    {
        private enum Type
        {
            NoUW = 0b00,
            DtorWithObj = 0b01,
            DtorWithPtrToObj = 0b10,
            RVA = 0b11
        };

#pragma warning disable IDE0052 // Remove unread private members - these are very helpful for debugging and may be used in future visualizations of xdata.
        private readonly uint nextOffset;         // State this action takes us to (now in offset form!)
        private readonly Type type;               // Type of entry
        private readonly int action;              // Image relative offset of funclet
        private readonly uint _object;             // Frame offset of object pointer to be destructed
#pragma warning restore IDE0052 // Remove unread private members

        public UnwindMapEntry4(byte** buffer)
        {
            var offsetAndType = ReadUnsigned(buffer);
            this.type = (Type)(offsetAndType & 0b11);
            this.nextOffset = offsetAndType >> 2;

            this.action = 0;
            this._object = 0;

            if (this.type is Type.DtorWithObj or Type.DtorWithPtrToObj)
            {
                this.action = ReadInt(buffer);
                this._object = ReadUnsigned(buffer);
            }
            else if (this.type == Type.RVA)
            {
                this.action = ReadInt(buffer);
            }
        }
    }

    protected readonly struct UWMap4
    {
        public UWMap4(FuncInfo4 funcInfo, byte* imageBase)
        {
            if (funcInfo.dispUnwindMap != 0)
            {
                var buffer = imageBase + funcInfo.dispUnwindMap;
                this.NumEntries = ReadUnsigned(&buffer);
                this.Entries = new UnwindMapEntry4[this.NumEntries];
                for (var i = 0; i < this.NumEntries; i++)
                {
                    this.Entries[i] = new UnwindMapEntry4(&buffer);
                }

                this.Size = (uint)(buffer - (imageBase + funcInfo.dispUnwindMap));
            }
            else
            {
                this.NumEntries = 0;
                this.Entries = null;
                this.Size = 0;
            }
        }

        public uint NumEntries { get; }
        public UnwindMapEntry4[]? Entries { get; }
        public uint Size { get; }
    }

    protected readonly struct TryBlockMapEntry4
    {
#pragma warning disable IDE0052 // Remove unread private members - these are useful when debugging and could later be things we want to surface in a UI so they're intentionally here.
        private readonly uint tryLow;             // Lowest state index of try
        private readonly uint tryHigh;            // Highest state index of try
        private readonly uint catchHigh;          // Highest state index of any associated catch
        public readonly int dispHandlerArray;   // Image relative offset of list of handlers for this try
#pragma warning restore IDE0052

        public TryBlockMapEntry4(byte** buffer)
        {
            this.tryLow = ReadUnsigned(buffer);
            this.tryHigh = ReadUnsigned(buffer);
            this.catchHigh = ReadUnsigned(buffer);
            this.dispHandlerArray = ReadInt(buffer);
        }
    }

    protected readonly struct TryBlockMap4
    {
        public TryBlockMap4(FuncInfo4 funcInfo, byte* imageBase)
        {
            if (funcInfo.dispTryBlockMap != 0)
            {
                var buffer = imageBase + funcInfo.dispTryBlockMap;
                this.NumTryBlocks = ReadUnsigned(&buffer);
                this.Entries = new TryBlockMapEntry4[this.NumTryBlocks];
                for (var i = 0; i < this.NumTryBlocks; i++)
                {
                    this.Entries[i] = new TryBlockMapEntry4(&buffer);
                }

                this.Size = (uint)(buffer - (imageBase + funcInfo.dispTryBlockMap));
            }
            else
            {
                this.NumTryBlocks = 0;
                this.Entries = null;
                this.Size = 0;
            }
        }

        public uint NumTryBlocks { get; }
        public TryBlockMapEntry4[]? Entries { get; }
        public uint Size { get; }
    }

    private const uint MAX_CONT_ADDRESSES = 2;

    protected readonly struct HandlerMapEntry4
    {
        private enum contType
        {
            NONE = 0b00,    // 1.   00: no continuation address in metadata, use what the catch funclet returns
            ONE = 0b01,     // 2.   01: one function-relative continuation address
            TWO = 0b10,     // 3.   10: two function-relative continuation addresses
            RESERVED = 0b11 // 4.   11: reserved
        }

        // The header isn't meant to be read directly, use the properties to get the specific bits
        private readonly byte header;
        private bool header_adjectives => (this.header & 1) != 0; // Existence of Handler Type adjectives (bitfield)
        private bool header_dispType => (this.header & 1 << 1) != 0; // Existence of Image relative offset of the corresponding type descriptor
        private bool header_dispCatchObj => (this.header & 1 << 2) != 0; // Existence of Displacement of catch object from base
        private bool header_contIsRVA => (this.header & 1 << 3) != 0; // Continuation addresses are RVAs rather than function relative, used for separated code
        private contType header_contAddr => ((contType)((this.header & 0b00110000) >> 4));

        private readonly uint adjectives; // Handler Type adjectives (bitfield)
        private readonly int dispType; // Image relative offset of the corresponding type descriptor
        private readonly uint dispCatchObj; // Displacement of catch object from base
        private readonly int dispOfHandler; // Image relative offset of 'catch' code
        private readonly uint[] continuationAddresses; // Continuation address(es) of catch funclet

        public HandlerMapEntry4(byte** buffer)
        {
            this.header = 0;
            this.adjectives = 0;
            this.dispType = 0;
            this.dispCatchObj = 0;
            this.dispOfHandler = 0;
            this.continuationAddresses = new uint[MAX_CONT_ADDRESSES];

            this.header = **buffer;
            *buffer = *buffer + 1;

            if (this.header_adjectives)
            {
                this.adjectives = ReadUnsigned(buffer);
            }

            if (this.header_dispType)
            {
                this.dispType = ReadInt(buffer);
            }

            if (this.header_dispCatchObj)
            {
                this.dispCatchObj = ReadUnsigned(buffer);
            }

            this.dispOfHandler = ReadInt(buffer);

            if (this.header_contIsRVA)
            {
                if (this.header_contAddr == contType.ONE)
                {
                    this.continuationAddresses[0] = (uint)ReadInt(buffer);
                }
                else if (this.header_contAddr == contType.TWO)
                {
                    this.continuationAddresses[0] = (uint)ReadInt(buffer);
                    this.continuationAddresses[1] = (uint)ReadInt(buffer);
                }
                else
                {
                    // no encoded cont addresses or unknown
                }
            }
            else
            {
                if (this.header_contAddr == contType.ONE)
                {
                    this.continuationAddresses[0] = ReadUnsigned(buffer);
                }
                else if (this.header_contAddr == contType.TWO)
                {
                    this.continuationAddresses[0] = ReadUnsigned(buffer);
                    this.continuationAddresses[1] = ReadUnsigned(buffer);
                }
                else
                {
                    // no encoded cont addresses or unknown
                }
            }
        }
    }

    protected readonly struct HandlerMap4
    {
        public HandlerMap4(TryBlockMapEntry4 tryMap, byte* imageBase)
        {
            if (tryMap.dispHandlerArray != 0)
            {
                var buffer = imageBase + tryMap.dispHandlerArray;
                this.NumHandlers = ReadUnsigned(&buffer);
                this.Handlers = new HandlerMapEntry4[this.NumHandlers];
                for (var i = 0; i < this.NumHandlers; i++)
                {
                    this.Handlers[i] = new HandlerMapEntry4(&buffer);
                }

                this.Size = (uint)(buffer - (imageBase + tryMap.dispHandlerArray));
            }
            else
            {
                this.NumHandlers = 0;
                this.Handlers = null;
                this.Size = 0;
            }
        }

        public uint NumHandlers { get; }
        public HandlerMapEntry4[]? Handlers { get; }
        public uint Size { get; }
    }

    protected readonly struct IPToStateMapEntry4
    {
        public readonly int Ip; // Image relative offset of IP
        public readonly int State;

        public IPToStateMapEntry4(byte** buffer, uint prevIp, int functionStart)
        {
            this.Ip = (int)(functionStart + prevIp + ReadUnsigned(buffer));
            // States are encoded +1 so as to not encode a negative
            this.State = (int)ReadUnsigned(buffer) - 1;
        }
    }

    protected readonly struct IPToStateMap4
    {
        public IPToStateMap4(SepIPToStateMapEntry4 mapEntry, byte* imageBase)
        {
            var functionStart = mapEntry.addrStartRVA;

            var buffer = imageBase + mapEntry.dispOfIPMap;
            this.NumEntries = ReadUnsigned(&buffer);
            this.Entries = new IPToStateMapEntry4[this.NumEntries];
            uint prevIp = 0;
            for (var i = 0; i < this.NumEntries; i++)
            {
                this.Entries[i] = new IPToStateMapEntry4(&buffer, prevIp, functionStart);
                prevIp = (uint)(this.Entries[i].Ip - functionStart);
            }

            this.Size = (uint)(buffer - (imageBase + mapEntry.dispOfIPMap));
        }

        public uint NumEntries { get; }
        public IPToStateMapEntry4[] Entries { get; }
        public uint Size { get; }
    }

    internal readonly struct SepIPToStateMapEntry4
    {
        public readonly int addrStartRVA; // Start address of the function contribution
        public readonly int dispOfIPMap; // RVA to IP map corresponding to this function contribution

        private readonly IPToStateMap4? stateMap;

        public uint Size { get; }

        internal SepIPToStateMapEntry4(int addrStartRVA, int dispOfIPMap, byte* imageBase)
        {
            this.addrStartRVA = addrStartRVA;
            this.dispOfIPMap = dispOfIPMap;
            this.stateMap = null;
            this.Size = 0;
            this.stateMap = new IPToStateMap4(this, imageBase);
            this.Size = this.stateMap.Value.Size;
        }

        internal SepIPToStateMapEntry4(byte** buffer, byte* imageBase)
        {
            var bufferStart = *buffer;
            this.addrStartRVA = ReadInt(buffer);
            this.dispOfIPMap = ReadInt(buffer);
            this.stateMap = null;
            this.Size = 0;
            this.stateMap = new IPToStateMap4(this, imageBase);
            this.Size = (uint)(*buffer - bufferStart) + this.stateMap.Value.Size;
        }
    }

    internal readonly struct SepIPToStateMap4
    {
        internal SepIPToStateMap4(bool isSeparated, int dispIPToStateMapOrSepIPToStateMap, byte* imageBase, uint functionStart)
        {
            if (isSeparated)
            {
                var segBufferStart = imageBase + dispIPToStateMapOrSepIPToStateMap;
                var segBuffer = segBufferStart;
                this.NumEntries = ReadUnsigned(&segBuffer);
                this.Entries = new SepIPToStateMapEntry4[this.NumEntries];
                for (var i = 0; i < this.NumEntries; i++)
                {
                    this.Entries[i] = new SepIPToStateMapEntry4(&segBuffer, imageBase);
                }
                this.Size = (uint)(segBuffer - segBufferStart);
            }
            else
            {
                this.NumEntries = 1;
                this.Entries = new SepIPToStateMapEntry4[1];
                this.Entries[0] = new SepIPToStateMapEntry4((int)functionStart, dispIPToStateMapOrSepIPToStateMap, imageBase);
                this.Size = 0;
            }
        }

        public uint NumEntries { get; }
        public SepIPToStateMapEntry4[] Entries { get; }
        public uint Size { get; }
    }

    // Constants for decompression.
    private static readonly sbyte[] s_negLengthTab = new sbyte[16]
    {
            -1,    // 0
            -2,    // 1
            -1,    // 2
            -3,    // 3

            -1,    // 4
            -2,    // 5
            -1,    // 6
            -4,    // 7

            -1,    // 8
            -2,    // 9
            -1,    // 10
            -3,    // 11

            -1,    // 12
            -2,    // 13
            -1,    // 14
            -5,    // 15
    };
    private static readonly byte[] s_shiftTab = new byte[16]
    {
            32 - 7 * 1,    // 0
            32 - 7 * 2,    // 1
            32 - 7 * 1,    // 2
            32 - 7 * 3,    // 3

            32 - 7 * 1,    // 4
            32 - 7 * 2,    // 5
            32 - 7 * 1,    // 6
            32 - 7 * 4,    // 7

            32 - 7 * 1,    // 8
            32 - 7 * 2,    // 9
            32 - 7 * 1,    // 10
            32 - 7 * 3,    // 11

            32 - 7 * 1,    // 12
            32 - 7 * 2,    // 13
            32 - 7 * 1,    // 14
            0,             // 15
    };

    private static int ReadInt(byte** buffer)
    {
        var value = *((int*)(*buffer));
        *buffer += sizeof(int);
        return value;
    }

    private static uint ReadUnsigned(byte** pbEncoding)
    {
        var lengthBits = (byte)(**pbEncoding & 0x0F);
        var negLength = s_negLengthTab[lengthBits];
        uint shift = s_shiftTab[lengthBits];
        var result = *((uint*)(*pbEncoding - negLength - 4));

        result >>= (int)shift;
        *pbEncoding -= negLength;

        return result;
    }

    private static uint DecompFuncInfo(byte* buffer, ref FuncInfo4 FuncInfoDe, byte* imageBase, uint functionStart)
    {
        var buffer_start = buffer;

        FuncInfoDe.FuncInfoHeader = *buffer;
        buffer++;

        if (FuncInfoDe.BBT)
        {
            FuncInfoDe.bbtFlags = ReadUnsigned(&buffer);
        }

        if (FuncInfoDe.UnwindMap)
        {
            FuncInfoDe.dispUnwindMap = ReadInt(&buffer);
        }

        if (FuncInfoDe.TryBlockMap)
        {
            FuncInfoDe.dispTryBlockMap = ReadInt(&buffer);
        }

        FuncInfoDe.dispIPtoStateMap = ReadInt(&buffer);
        FuncInfoDe.SeparatedIP2StateMap = new SepIPToStateMap4(FuncInfoDe.isSeparated, FuncInfoDe.dispIPtoStateMap, imageBase, functionStart);

        if (FuncInfoDe.isCatch)
        {
            FuncInfoDe.dispFrame = ReadUnsigned(&buffer);
        }

        return (uint)(buffer - buffer_start);
    }

    protected void ParseCppXdataV4(Symbol? targetSymbol, uint runtimeFunctionStartRva, uint cppxdataRva)
    {
        var fi4 = new FuncInfo4();
        var buffer = this.LibraryBaseAddress + cppxdataRva;
        var lengthOfFuncInfo4 = DecompFuncInfo(buffer, ref fi4, this.LibraryBaseAddress, runtimeFunctionStartRva);

        AddXData(new CppXdataSymbol(targetSymbol, runtimeFunctionStartRva, cppxdataRva, lengthOfFuncInfo4));

        // [stateUnwindMap], if one is present
        if (fi4.UnwindMap)
        {
            var unwindMapRva = (uint)fi4.dispUnwindMap;
            AddXData(new StateUnwindMapSymbol(targetSymbol, runtimeFunctionStartRva, unwindMapRva, new UWMap4(fi4, this.LibraryBaseAddress).Size));
        }

        // [tryMap], if one is present
        if (fi4.TryBlockMap)
        {
            var tryBlockMapRva = (uint)fi4.dispTryBlockMap;
            if (!this.XdataSymbols.ContainsKey(tryBlockMapRva))
            {
                var tryBlockMap = new TryBlockMap4(fi4, this.LibraryBaseAddress);
                AddXData(new TryMapSymbol(targetSymbol, runtimeFunctionStartRva, tryBlockMapRva, tryBlockMap.Size));

                if (tryBlockMap.Entries != null)
                {
                    foreach (var tryBlock in tryBlockMap.Entries)
                    {
                        // [handlerMap], if present
                        if (tryBlock.dispHandlerArray != 0)
                        {
                            var handlerMapRva = (uint)tryBlock.dispHandlerArray;
                            AddXData(new HandlerMapSymbol(targetSymbol, runtimeFunctionStartRva, handlerMapRva, new HandlerMap4(tryBlock, this.LibraryBaseAddress).Size));
                        }
                    }
                }
            }
        }

        // [ip2StateMap], if one is present.
        // Note there can be more than one of these, if PGO is involved and creating separated code
        // within an individual function.
        if (fi4.SeparatedIP2StateMap != null)
        {
            // If this is separated code (PGO'd), then we will also have a SeparatedIpToStateMap table
            if (fi4.isSeparated)
            {
                AddXData(new SeparatedIpToStateMapSymbol(targetSymbol, runtimeFunctionStartRva, (uint)fi4.dispIPtoStateMap, fi4.SeparatedIP2StateMap.Value));
            }

            foreach (var ip2StateMap in fi4.SeparatedIP2StateMap.Value.Entries)
            {
                var ipToStateMapRva = (uint)ip2StateMap.dispOfIPMap;
                AddXData(new IpToStateMapSymbol(targetSymbol, (uint)ip2StateMap.addrStartRVA, ipToStateMapRva, new IPToStateMap4(ip2StateMap, this.LibraryBaseAddress).Size));
            }
        }
    }

    #endregion
}
