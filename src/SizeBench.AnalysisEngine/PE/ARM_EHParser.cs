﻿using System.Runtime.InteropServices;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine.PE;

internal sealed unsafe class ARM_EHParser : EHSymbolParser
{
    #region ARM32 and ARM64 EH Structures, Flags, etc...
    // ******************************************               ARM (32 and 64) PDATA Record structure              ******************************************
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |31	| 30 | 29 | 28 | 27 | 26 | 25 | 24 | 23 | 22 | 21 | 20 | 19 | 18 | 17 | 16 | 15 | 14 | 13 | 12 | 11 | 10 | 9 | 8 | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                                                   Function Start RVA                                                                |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                                      Exception Information RVA / Packed Unwind Data                                         | Flag  |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ARM_RUNTIME_FUNCTION
    {
        public readonly uint FunctionStartRva;
        public readonly uint EHMetadata;
    }

    // See section ".pdata Records" of Exception Data for the architecture specific Exception Data document mentioned at the top of this file.
    [Flags]
    private enum PDataFlags : byte
    {
        EXCEPTION_INFO = 0x0, // This value indicates that the second .pdata word form an Exception Information RVA (with the low two bits implicitly 0)
        UNWIND_DATA = 0x1,
        UNWIND_DATA_FOR_NO_PROLOG_FUNCTIONS = 0x2,
        FORWARDER = 0x3 // This value indicates it's a forwarder PDATA record generated by the linker and it points to another PDATA record 
    }

    #endregion

    public ARM_EHParser(IDIAAdapter diaAdapter,
                        byte* libraryBaseAddress,
                        PEFile peFile,
                        SymbolSourcesSupported symbolSourcesSupported) : base(diaAdapter, libraryBaseAddress, peFile, symbolSourcesSupported)
    {
        this.__GSHandlerCheck_EHRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_EH", true);
        this.__GSHandlerCheck_EH4Rva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_EH4", true);
        this._cxxFrameHandler3Rva = diaAdapter.SymbolRvaFromName("__CxxFrameHandler3", true);
        this._cxxFrameHandler4Rva = diaAdapter.SymbolRvaFromName("__CxxFrameHandler4", true);
        this._c_specific_handlerRva = diaAdapter.SymbolRvaFromName("__C_specific_handler", true);
        this._c_specific_handler_noexceptRva = diaAdapter.SymbolRvaFromName("__C_specific_handler_noexcept", true);
        this.__GSHandlerCheck_SEHRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_SEH", true);
        this.__GSHandlerCheck_SEH_noexceptRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck_SEH_noexcept", true);
        this.__GSHandlerCheckRva = diaAdapter.SymbolRvaFromName("__GSHandlerCheck", true);
    }

    protected override SortedList<uint, PDataSymbol> ParsePDataForArchitecture(SessionDataCache cache)
    {
        var pdataFunctions = ParsePDATA<ARM_RUNTIME_FUNCTION>(this.LibraryBaseAddress, cache);

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
        var sizeOfRUNTIMEFUNCTION = (uint)Marshal.SizeOf<ARM_RUNTIME_FUNCTION>();

        for (var i = 0; i < pdataFunctions.Length; i++)
        {
            var pdataEntry = pdataFunctions[i];

            // Some pdata tables contain these "empty" entries - they don't seem to hurt anything, just skip over them.
            // Not sure if this holds good for ARM, but keeping this code since we saw this in AMD64 and it does not seem to hurt anything.
            if (pdataEntry.FunctionStartRva == 0 &&
                pdataEntry.EHMetadata == 0)
            {
                continue;
            }

            var pdataEntryRva = cache.PDataRVARange.RVAStart + (uint)(i * sizeOfRUNTIMEFUNCTION);

            var flags = (PDataFlags)(pdataEntry.EHMetadata & 0x3);
            var adjustedFunctionStartRva = GetAdjustedRva(pdataEntry.FunctionStartRva);

            if (flags == PDataFlags.EXCEPTION_INFO) // The remaining bits of EHMetadata in this pdata record point to an xdata record  
            {
                pdataSymbols.Add(pdataEntryRva, new PDataSymbol(adjustedFunctionStartRva, pdataEntry.EHMetadata, pdataEntryRva, (uint)Marshal.SizeOf<ARM_RUNTIME_FUNCTION>(), this.SymbolSourcesSupported));
            }
            else if (flags == PDataFlags.FORWARDER)
            {
                pdataSymbols.Add(pdataEntryRva, new ForwarderPDataSymbol(adjustedFunctionStartRva, pdataEntryRva, (uint)Marshal.SizeOf<ARM_RUNTIME_FUNCTION>(), this.SymbolSourcesSupported));
            }
            else
            {
                pdataSymbols.Add(pdataEntryRva, new PackedUnwindDataPDataSymbol(adjustedFunctionStartRva, pdataEntryRva, (uint)Marshal.SizeOf<ARM_RUNTIME_FUNCTION>(), this.SymbolSourcesSupported));
            }
        }

        return pdataSymbols;
    }

    protected override void ParseXDataForArchitecture(RVARange? XDataRVARange, SessionDataCache cache)
    {
        foreach (var pds in cache.PDataSymbolsByRVA.Values)
        {
            var adjustedFunctionStartRva = GetAdjustedRva(pds.TargetStartRVA);
            var targetSymbol = GetTargetSymbolForRVA(adjustedFunctionStartRva);

            pds.UpdateTargetSymbol(targetSymbol);

            // ForwarderPDataSymbol and PackedUnwindDataPDataSymbol instances do not generate xdata, so we only do this for PDataSymbol (the base type)
            if (pds.GetType() == typeof(PDataSymbol))
            {
                ParseOneXData(targetSymbol, pds.TargetStartRVA, pds.UnwindInfoStartRva);
            }
        }
    }

    protected override uint GetGSDataSizeAdjusted(GS_UNWIND_Flags gsdata) => 0;

    #region XDATA structure parsing methods

    // ******************************************                   ARM32 XDATA Record structure                   ******************************************
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |31	| 30 | 29 | 28 | 27 | 26 | 25 | 24 | 23 | 22 | 21 | 20 | 19 | 18 | 17 | 16 | 15 | 14 | 13 | 12 | 11 | 10 | 9 | 8 | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |Unwind Code Words  | Epilog Count	        | F	 | E  |	X  | Version |                                   Function Length                             |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // | (Reserved)                            |     (Extended UnwindCode Words)       |	     (Extended Epilog Count) (if bits 23-31 are all 0)           |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // | Epilog Start Index	                   |   Condition	   |   Res	 |                                  Epilog Start Offset                          |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                                   (Possibly followed by additional epilog scopes)                                                   |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |  Unwind Code 3                        |     	Unwind Code 2                  |	     Unwind Code 1	              |          Unwind Code 0       |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                              (Possibly followed by additional words with unwind codes)                                              |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                                       Exception Handler RVA(if X == 1)                                                              |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                           (Possibly followed by data needed by the exception handler)                                               |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    //
    //
    // ******************************************                   ARM64 XDATA Record structure                   ******************************************
    // *************************************   Except for the first 32 bits, everything else is same as that of ARM32   ************************************* 
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |31	| 30 | 29 | 28 | 27 | 26 | 25 | 24 | 23 | 22 | 21 | 20 | 19 | 18 | 17 | 16 | 15 | 14 | 13 | 12 | 11 | 10 | 9 | 8 | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |  Unwind Code Words     | Epilog Count	         | E  |	X  | Vers	 |                                   Function Length                             |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------
    // |                                                                                                                                                     |
    // |                                                     Rest of the structure is same as that of ARM32                                                  |
    // |                                                                                                                                                     |
    // -------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ParseOneXData(Symbol? targetSymbol, uint functionStartRva, uint ehMetadata)
    {
        var pRawXdata = (uint*)(this.LibraryBaseAddress + ehMetadata);
        var unwindInfoStart = (byte*)pRawXdata;

        var version = (byte)((*pRawXdata >> 18) & 0x3); // Skip 18 bits of Function length, bottom 2 bits are the version
        var hasExceptionHandler = ((*pRawXdata >> 20) & 0x1) == 1; // 1 bit after the version

        // As per the ARM and ARM64  Exception Data document (mentioned to at the top of this file) only version 0 is supported at this point; version 1-3 are not permitted. 
        if (version != 0)
        {
            throw new InvalidOperationException("SizeBench only knows how to parse version 0 of Exception Data (xdata) structure!");
        }

        if (hasExceptionHandler)
        {
            var pExceptionHandlerRva = unwindInfoStart + GetXdataRecordSize(unwindInfoStart) - 4;
            var exceptionHandlerRva = GetAdjustedRva(*((uint*)pExceptionHandlerRva));
            ParseOneExceptionHandler(targetSymbol, GetAdjustedRva(functionStartRva), ehMetadata, pExceptionHandlerRva, exceptionHandlerRva, unwindInfoStart);
        }
        else
        {
            // Just a simple [unwind]
            AddXData(new UnwindInfoSymbol(targetSymbol, GetAdjustedRva(functionStartRva), ehMetadata, GetXdataRecordSize(unwindInfoStart), this.SymbolSourcesSupported));
        }
    }

    private uint GetAdjustedRva(uint rva)
    {
        if (this.MachineType == MachineType.ARM)
        {
            // Mask the lowest bit off from the value because for ARM32 Thumb2 LSB set to 1 in the address. This means the target is in thumb code instead of ARM code.
            return rva & 0xFFFFFFFE;
        }
        else
        {
            return rva;
        }
    }

    private uint GetXdataRecordSize(byte* pXdataStartRva)
    {
        uint epilogScopeCount;
        uint xdataRecordSize;
        uint unwindCodeWords;

        var pXdata = (uint*)pXdataStartRva;

        var hasExceptionHandler = ((*pXdata >> 20) & 0x1) == 1; // Bit X
        var hasSingleEpilogScope = ((*pXdata >> 21) & 0x1) == 1; // Bit E
        var epilogAndUnwindCodes = this.MachineType == MachineType.ARM ? (*pXdata >> 23) & 0x1ff : (*pXdata >> 22) & 0x3ff; // All of (Unwind Code Words) and (Epilog Count) bits

        // If All of (Unwind Code Words) and (Epilog Count) bits are 0 then (Extended UnwindCode Words) and (Extended Epilog Count) are in play
        if (epilogAndUnwindCodes != 0)
        {
            xdataRecordSize = 4; // Account for the first 32-bit word (4 bytes) explaining the structure of rest of this xdata record
            if (this.MachineType == MachineType.ARM)
            {
                epilogScopeCount = (*pXdata >> 23) & 0x1f;
                unwindCodeWords = (*pXdata >> 28) & 0xf;
            }
            else
            {
                epilogScopeCount = (*pXdata >> 22) & 0x1f;
                unwindCodeWords = (*pXdata >> 27) & 0x1f;
            }
        }
        else
        {
            xdataRecordSize = 8; // Since the extended unwind codes and epilogs are used, account for the first two 32-bit words (8 bytes) explaining the structure of rest of this xdata record
            pXdata++; // Move to the next 32-bits of the record
            epilogScopeCount = *pXdata & 0xffff;
            unwindCodeWords = (*pXdata >> 16) & 0xff;
        }

        // Bit E == 1 indicate Singe Epilog Scope was packed into ther header so no epilog records in xdata structure;
        // Otherwise, there will be "epilogScopeCount" number of words explaning the epilogs.
        if (!hasSingleEpilogScope)
        {
            xdataRecordSize += 4 * epilogScopeCount;
        }

        xdataRecordSize += 4 * unwindCodeWords;

        // Bit X == 1 indicate Exception Handler was in use hence a 32-bit field was being used for storing it's RVA
        if (hasExceptionHandler)
        {
            xdataRecordSize += 4;
        }

        return xdataRecordSize;
    }

    #endregion
}
