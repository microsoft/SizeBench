using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dia2Lib;

[ComImport]
[Guid("1994DEB2-2C82-4B1D-A57F-AFF424D54A68")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDiaEnumSectionContribsHandCoded
{
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler, CustomMarshalers, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    IEnumerator GetEnumerator();

    [DispId(1)]
    int count
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    IDiaSectionContrib Item([In] uint index);

#pragma warning disable CA1716 // Identifiers should not match keywords - this is the name in the native interface from the DIA SDK
    void Next([In] uint celt, IntPtr rgelt, out uint pceltFetched);
#pragma warning restore CA1716 // Identifiers should not match keywords

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void Skip([In] uint celt);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void Reset();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSectionContribs ppenum);
}
