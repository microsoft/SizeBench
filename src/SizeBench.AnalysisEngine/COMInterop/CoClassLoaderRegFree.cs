using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SizeBench.AnalysisEngine.COMInterop;

[Guid("00000001-0000-0000-c000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComImport]
internal interface IClassFactory
{
    void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] object? pUnkOuter, ref Guid riid,
                        [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);

    void LockServer(bool fLock);
}

[ExcludeFromCodeCoverage]
internal static class CoClassLoaderRegFree
{
    private delegate int GetClassObject(ref Guid clsid, ref Guid iid, [Out, MarshalAs(UnmanagedType.Interface)] out IClassFactory classFactory);

    public static TInstance CreateInstance<TInstance>(LibraryModule libraryModule, Guid clsid) where TInstance : class
    {
        var classFactory = GetClassFactory(libraryModule, clsid);
        var iid = new Guid("00000000-0000-0000-C000-000000000046"); // IUnknown
        classFactory.CreateInstance(null, ref iid, out var obj);
        return (TInstance)obj;
    }

    private static IClassFactory GetClassFactory(LibraryModule libraryModule, Guid clsid)
    {
        var ptr = libraryModule.GetProcAddress("DllGetClassObject");
        var callback = Marshal.GetDelegateForFunctionPointer<GetClassObject>(ptr);

        var classFactoryIid = new Guid("00000001-0000-0000-c000-000000000046");
        var hresult = callback(ref clsid, ref classFactoryIid, out var classFactory);

        if (hresult != 0)
        {
            throw new Win32Exception(hresult, $"Cannot create class factory for CLSID={clsid}");
        }
        return classFactory;
    }
}
