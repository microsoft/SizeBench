using System.Runtime.InteropServices;
using Dia2Lib;

namespace SizeBench.AnalysisEngine.DIAInterop;
internal static class DiaChunkMarshaling
{
    // It's possible all these AdvanceToNewElementInChunk could be changed into custom marshalers
    // using [return: MarshalAs(UnmanagedType.CustomMarshaler, ...)] on the managed interfaces, but
    // this would require some perf measurements to see if the way that custom marshalers get allocated
    // and used is ok.  It could be nice because we could then use standard "foreach" loops to iterate
    // over the collections just like the built-in marshaler, while maintaining the chunking benefits
    // to reduce chattiness of the P/Invokes.  But for now, this is what we've got because it works.

    internal static IDiaSymbol? AdvanceToNewElementInChunk(
        IDiaEnumSymbolsByAddr2HandCoded enumSymbolsByAddr2,
        uint chunkSize,
        nint[] intPtrs, /* MUST be a pinned array! */
        ref uint celt,
        ref int currentIntPtrsIndex)
    {
        IDiaSymbol? diaSymbol;
        currentIntPtrsIndex++;
        if (currentIntPtrsIndex < chunkSize && currentIntPtrsIndex < celt)
        {
            diaSymbol = IUnknownToObject<IDiaSymbol>(intPtrs[currentIntPtrsIndex]);
        }
        else
        {
            enumSymbolsByAddr2.NextEx(fPromoteBlockSym: 0, chunkSize, Marshal.UnsafeAddrOfPinnedArrayElement(intPtrs, 0), out celt);
            if (celt > 0)
            {
                diaSymbol = IUnknownToObject<IDiaSymbol>(intPtrs[0]);
                currentIntPtrsIndex = 0;
            }
            else if (celt > 0 && celt < chunkSize)
            {
                // The last time we got a chunk out, we got less than chunkSize elements, so there can't be any more chunks.  We're done.
                diaSymbol = null;
                currentIntPtrsIndex = int.MaxValue;
            }
            else
            {
                diaSymbol = null;
                currentIntPtrsIndex = int.MaxValue;
            }
        }

        return diaSymbol;
    }

    internal static IDiaSymbol? AdvanceToNewElementInChunk(
        IDiaEnumSymbolsHandCoded enumSymbols,
        uint chunkSize,
        nint[] intPtrs, /* MUST be a pinned array! */
        ref uint celt,
        ref int currentIntPtrsIndex)
    {
        IDiaSymbol? diaSymbol;
        currentIntPtrsIndex++;
        if (currentIntPtrsIndex < chunkSize && currentIntPtrsIndex < celt)
        {
            diaSymbol = IUnknownToObject<IDiaSymbol>(intPtrs[currentIntPtrsIndex]);
        }
        else if (celt > 0 && celt < chunkSize)
        {
            // The last time we got a chunk out, we got less than chunkSize elements, so there can't be any more chunks.  We're done.
            diaSymbol = null;
            currentIntPtrsIndex = int.MaxValue;
        }
        else
        {
            enumSymbols.Next(chunkSize, Marshal.UnsafeAddrOfPinnedArrayElement(intPtrs, 0), out celt);
            if (celt > 0)
            {
                diaSymbol = IUnknownToObject<IDiaSymbol>(intPtrs[0]);
                currentIntPtrsIndex = 0;
            }
            else
            {
                diaSymbol = null;
                currentIntPtrsIndex = int.MaxValue;
            }
        }

        return diaSymbol;
    }

    internal static IDiaLineNumber? AdvanceToNewElementInChunk(
        IDiaEnumLineNumbersHandCoded enumLineNumbers,
        uint chunkSize,
        nint[] intPtrs, /* MUST be a pinned array! */
        ref uint celt,
        ref int currentIntPtrsIndex)
    {
        IDiaLineNumber? diaLineNumber;
        currentIntPtrsIndex++;
        if (currentIntPtrsIndex < chunkSize && currentIntPtrsIndex < celt)
        {
            diaLineNumber = IUnknownToObject<IDiaLineNumber>(intPtrs[currentIntPtrsIndex]);
        }
        else if (celt > 0 && celt < chunkSize)
        {
            // The last time we got a chunk out, we got less than chunkSize elements, so there can't be any more chunks.  We're done.
            diaLineNumber = null;
            currentIntPtrsIndex = int.MaxValue;
        }
        else
        {
            enumLineNumbers.Next(chunkSize, Marshal.UnsafeAddrOfPinnedArrayElement(intPtrs, 0), out celt);
            if (celt > 0)
            {
                diaLineNumber = IUnknownToObject<IDiaLineNumber>(intPtrs[0]);
                currentIntPtrsIndex = 0;
            }
            else
            {
                diaLineNumber = null;
                currentIntPtrsIndex = int.MaxValue;
            }
        }

        return diaLineNumber;
    }

    internal static unsafe IDiaSectionContrib? AdvanceToNewElementInChunk(
        IDiaEnumSectionContribsHandCoded enumSectionContribs,
        uint chunkSize,
        nint[] intPtrs,
        ref uint celt,
        ref int currentIntPtrsIndex)
    {
        IDiaSectionContrib? diaSectionContrib;
        currentIntPtrsIndex++;
        if (currentIntPtrsIndex < chunkSize && currentIntPtrsIndex < celt)
        {
            diaSectionContrib = IUnknownToObject<IDiaSectionContrib>(intPtrs[currentIntPtrsIndex]);
        }
        else
        {
            enumSectionContribs.Next(chunkSize, Marshal.UnsafeAddrOfPinnedArrayElement(intPtrs, 0), out celt);
            if (celt > 0)
            {
                diaSectionContrib = IUnknownToObject<IDiaSectionContrib>(intPtrs[0]);
                currentIntPtrsIndex = 0;
            }
            else
            {
                diaSectionContrib = null;
                currentIntPtrsIndex = int.MaxValue;
            }
        }

        return diaSectionContrib;
    }

    private static T? IUnknownToObject<T>(IntPtr iUnknown) where T : class
    {
        if (iUnknown == IntPtr.Zero)
        {
            return null;
        }

        var typedObject = (T)Marshal.GetTypedObjectForIUnknown(iUnknown, typeof(T));
        // GetTypedObjectForIUnknown does an AddRef, so we Release here because we already got a ref from the native side
        Marshal.Release(iUnknown);

        return typedObject;
    }
}
