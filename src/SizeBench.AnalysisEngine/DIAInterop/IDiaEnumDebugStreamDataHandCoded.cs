using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using Dia2Lib;

namespace SizeBench.AnalysisEngine.DIAInterop;

[DefaultMember("Item")]
[Guid("486943E8-D187-4A6B-A3C4-291259FFF60D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDiaEnumDebugStreamDataHandCoded
{
    [DispId(1)]
    int count { get; }
    [DispId(2)]
    string name { get; }

    [DispId(-4)]
    IEnumerator GetEnumerator();
    [DispId(0)]
    void Item(uint index, int cbData, out uint pcbData, [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pbData);
    int Next(uint celt, int cbData, out int pcbData, [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pbData);
    void Skip(uint celt);
    void Reset();
    void Clone(out IDiaEnumDebugStreamData ppenum);
}
