using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dia2Lib;

namespace SizeBench.AnalysisEngine.DIAInterop;

[ComImport]
[Guid("1E45BD02-BE45-4D71-BA32-0E576CFCD59F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDiaEnumSymbolsByAddr2HandCoded : IDiaEnumSymbolsByAddr
{
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    new IDiaSymbol symbolByAddr([In] uint isect, [In] uint offset);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    new IDiaSymbol symbolByRVA([In] uint relativeVirtualAddress);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    new IDiaSymbol symbolByVA([In] ulong virtualAddress);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    new void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol rgelt, out uint pceltFetched);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    new void Prev([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol rgelt, out uint pceltFetched);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    new void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbolsByAddr ppenum);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    IDiaSymbol symbolByAddrEx([In] int fPromoteBlockSym, [In] uint isect, [In] uint offset);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    IDiaSymbol symbolByRVAEx([In] int fPromoteBlockSym, [In] uint relativeVirtualAddress);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    IDiaSymbol symbolByVAEx([In] int fPromoteBlockSym, [In] ulong virtualAddress);

    void NextEx([In] int fPromoteBlockSym, [In] uint celt, IntPtr rgelt, out uint pceltFetched);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void PrevEx([In] int fPromoteBlockSym, [In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol rgelt, out uint pceltFetched);
}