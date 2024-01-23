using System.Diagnostics;
using System.Runtime.InteropServices;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine.PE;

internal sealed unsafe class AMD64_EHParser : EHSymbolParser
{
    #region AMD64 specific EH Structures, Flags, etc...

    [DebuggerDisplay("RUNTIME_FUNCTION Start=0x{FunctionStartRva.ToString(\"X\"),nq}, End=0x{FunctionEndRva.ToString(\"X\"),nq}, Unwind=0x{UnwindInfoRva.ToString(\"X\"),nq}")]
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RUNTIME_FUNCTION
    {
        public readonly uint FunctionStartRva;
        public readonly uint FunctionEndRva;
        public readonly uint UnwindInfoRva; // Points to an UNWIND_INFO
    }

    [Flags]
    private enum UNWIND_INFO_Flags : byte
    {
        UNW_FLAG_NHANDLER = 0x0, // No Handler
        UNW_FLAG_EHANDLER = 0x1,
        UNW_FLAG_UHANDLER = 0x2,
        UNW_FLAG_CHAININFO = 0x4
    }

    #endregion

    public AMD64_EHParser(IDIAAdapter diaAdapter,
                          byte* libraryBaseAddress,
                          PEFile peFile) : base(diaAdapter, libraryBaseAddress, peFile)
    {
        this.__GSHandlerCheck_EHRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_EH", true);
        this.__GSHandlerCheck_EH4Rva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_EH4", true);
        this._cxxFrameHandlerRva = diaAdapter.SymbolRvaFromName("__CxxFrameHandler", true);
        this._cxxFrameHandler2Rva = diaAdapter.SymbolRvaFromName("__CxxFrameHandler2", true);
        this._cxxFrameHandler3Rva = diaAdapter.SymbolRvaFromName("__CxxFrameHandler3", true);
        this._cxxFrameHandler4Rva = diaAdapter.SymbolRvaFromName("__CxxFrameHandler4", true);
        this._c_specific_handlerRva = diaAdapter.SymbolRvaFromName("__C_specific_handler", true);
        this._c_specific_handler_noexceptRva = diaAdapter.SymbolRvaFromName("__C_specific_handler_noexcept", true);
        this.__GSHandlerCheck_SEHRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_SEH", true);
        this.__GSHandlerCheck_SEH_noexceptRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_SEH_noexcept", true);
        this.__GSHandlerCheckRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck", true);
    }

    private void ParseOneXData(Symbol? targetSymbol, uint targetStartRva, uint unwindInfoStartRva)
    {
        Debug.Assert(targetSymbol is null || (targetStartRva >= targetSymbol.RVA && targetStartRva <= targetSymbol.RVAEnd));

        var pRawXdata = this.LibraryBaseAddress + unwindInfoStartRva;
        var unwindInfoStart = pRawXdata;
        var versionAndFlags = *pRawXdata;
        var flags = (UNWIND_INFO_Flags)(versionAndFlags >> 3); // Upper 5 bits are flags
        var version = (byte)(versionAndFlags & 0x7); // Bottom 3 bits are the version

        // If the flags are 0 and version is 0, then this is some kind of stub that might be here because of incremental linking or incremental LTCG.
        // We can ignore this one - it is only a few bytes in size so it's not material to size concerns.
        if (flags == 0 && version == 0)
        {
            return;
        }

        // Version 2 of UNWIND_INFO didn't change the size of anything, it introduced some new EpilogueCode
        // stuff, but via a union - so for the purposes of SizeBench this isn't interesting (same size).
        // We throw here because who knows, maybe v3 will exist someday with a different size.
        
        if (version is < 1 or > 2)
        {
            throw new InvalidOperationException($"SizeBench only knows how to parse version 1 and 2 UNWIND_INFO structures!  This binary has an UNWIND_INFO of version {version}.");
        }

        pRawXdata++; // skip VersionAndFlags byte
        var SizeOfProlog = *pRawXdata;
        pRawXdata++;
        var CountOfUnwindCodes = *pRawXdata;
        pRawXdata++;
        var FrameRegisterAndOffset = *pRawXdata;
        pRawXdata++;
        var UnwindCodes = new ushort[CountOfUnwindCodes];
        for (var i = 0; i < CountOfUnwindCodes; i++)
        {
            UnwindCodes[i] = *((ushort*)pRawXdata);
            pRawXdata += 2;
        }

        // The UnwindCodes array must always be in pairs, but if the CountOfUnwindCodes is odd
        // then we just skip over the last entry to continue to the EH data.
        // See MSDN docs: https://msdn.microsoft.com/en-us/library/ddssxxy8.aspx
        if (CountOfUnwindCodes % 2 == 1)
        {
            pRawXdata += 2;
        }

        var pAfterUnwindCodes = pRawXdata;

        if (flags.HasFlag(UNWIND_INFO_Flags.UNW_FLAG_CHAININFO))
        {
            ParseOneChainInfo(targetSymbol, targetStartRva, unwindInfoStartRva, pAfterUnwindCodes, unwindInfoStart);
        }
        else if (flags == 0)
        {
            // Just a simple [unwind]
            AddXData(new UnwindInfoSymbol(targetSymbol, targetStartRva, unwindInfoStartRva, (uint)(pAfterUnwindCodes - unwindInfoStart)));
        }
        else
        {
            var exceptionHandlerRva = *((uint*)pAfterUnwindCodes);
            ParseOneExceptionHandler(targetSymbol, targetStartRva, unwindInfoStartRva, pAfterUnwindCodes, exceptionHandlerRva, unwindInfoStart);
        }
    }

    private void ParseOneChainInfo(Symbol? targetSymbol, uint targetStartRva, uint unwindInfoStartRva, byte* pAfterUnwindCodes, byte* unwindInfoStart)
    {
        var rfChain = Marshal.PtrToStructure<RUNTIME_FUNCTION>(new IntPtr(pAfterUnwindCodes));
        pAfterUnwindCodes += Marshal.SizeOf<RUNTIME_FUNCTION>();

        AddXData(new ChainUnwindInfoSymbol(targetSymbol, targetStartRva, unwindInfoStartRva, (uint)(pAfterUnwindCodes - unwindInfoStart)));

        var targetSymbolForChain = GetTargetSymbolForRVA(rfChain.FunctionStartRva);
        ParseOneXData(targetSymbolForChain, rfChain.FunctionStartRva, rfChain.UnwindInfoRva);
    }

    protected override SortedList<uint, PDataSymbol> ParsePDataForArchitecture(SessionDataCache cache)
    {
        var pdataFunctions = ParsePDATA<RUNTIME_FUNCTION>(this.LibraryBaseAddress, cache);

        // There's no pdata, so we're done
        if (pdataFunctions is null)
        {
            return new SortedList<uint, PDataSymbol>();
        }

        //TODO: Perf: We can be more efficient here.  We know every RUNTIME_FUNCTION will become a
        //            PDataSymbol, so why not just allocate all of them in bulk here (one big array)
        //            then fill them in with their FunctionStartRVA.  Then ParseOneRUNTIME_FUNCTION
        //            wouldn't end up allocating individual PDataSymbol objects tens of thousands of
        //            times.

        var pdataSymbols = new SortedList<uint, PDataSymbol>(capacity: pdataFunctions.Length);
        var sizeOfRUNTIMEFUNCTION = (uint)Marshal.SizeOf<RUNTIME_FUNCTION>();

        for (var i = 0; i < pdataFunctions.Length; i++)
        {
            var pdataEntry = pdataFunctions[i];

            // BBT can create chained PDATA entries, but SizeBench hasn't been taught how to parse these yet.
            if ((pdataEntry.UnwindInfoRva & 0x1) == 0x1)
            {
                throw new InvalidOperationException("SizeBench doesn't yet know how to parse chained PDATA records!");
            }

            // Some pdata tables contain these "empty" entries - they don't seem to hurt anything, just skip over them.
            if (pdataEntry.FunctionStartRva == 0 &&
                pdataEntry.FunctionEndRva == 0 &&
                pdataEntry.UnwindInfoRva == 0)
            {
                continue;
            }

            var pdataEntryRva = cache.PDataRVARange!.RVAStart + (uint)(i * sizeOfRUNTIMEFUNCTION);
            pdataSymbols.Add(pdataEntryRva, new PDataSymbol(pdataEntry.FunctionStartRva, pdataEntry.UnwindInfoRva, pdataEntryRva, sizeOfRUNTIMEFUNCTION));
        }

        return pdataSymbols;
    }

    protected override void ParseXDataForArchitecture(RVARange? XDataRVARange, SessionDataCache cache)
    {
        foreach (var pds in cache.PDataSymbolsByRVA!.Values)
        {
            var targetSymbol = GetTargetSymbolForRVA(pds.TargetStartRVA);
            pds.UpdateTargetSymbol(targetSymbol);
            ParseOneXData(targetSymbol, pds.TargetStartRVA, pds.UnwindInfoStartRva);
        }
    }

    protected override uint GetGSDataSizeAdjusted(GS_UNWIND_Flags gsdata)
    {
        if (gsdata.HasFlag(GS_UNWIND_Flags.UNW_GSALIGNEDFRAME))
        {
            return (2 * sizeof(uint));
        }
        else
        {
            return 0;
        }
    }
}
