using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Tests;

// This is a place to collect up all the constants that will be used by RealPE tests, so they can be updated in one
// place as the test binaries change, instead of scattered around each test.
// These numbers should be gathered from one of two sources:
//    link /dump /headers /coffgroup CppTestCases_BasicDiffObjects[Before|After].dll
//    MapView (if you have access to that MS-internal tool)
// Note that MapView is sometimes wrong so if anything differs from MapView it should be commented about why you're
// sure the number here is right and MapView is wrong, but it will happen sometimes.
// link /dump /headers /coffgroup is always 100% correct, so if that's the data source it is the ground truth.
[ExcludeFromCodeCoverage]
public static class RealPETestingConstants
{
    // ********************************************
    // Sections
    // ********************************************
    public static int BeforeTextSize => 0x1A00;
    public static int BeforeTextVirtualSize => 0x198D;
    public static int BeforeRdataSize => 0x1200;
    public static int BeforeRdataVirtualSize => 0x10B4;
    public static int BeforeDataSize => 0x200;
    public static int BeforeDataVirtualSize => 0x6D8;
    public static int BeforePDataSize => 0x400;
    public static int BeforePDataVirtualSize => 0x294;
    public static int BeforeGfidsSize => 0x200;
    public static int BeforeGfidsVirtualSize => 0x10;
    public static int BeforeRsrcSize => 0x200;
    public static int BeforeRsrcVirtualSize => 0x1E0;
    public static int BeforeRelocSize => 0x200;
    public static int BeforeRelocVirtualSize => 0x2C;

    public static int AfterTextSize => 0x1E00;
    public static int AfterTextVirtualSize => 0x1C4D;
    public static int AfterRdataSize => 0x1600;
    public static int AfterRdataVirtualSize => 0x1574;
    public static int AfterDataSize => 0x200;
    public static int AfterDataVirtualSize => 0x6E8;
    public static int AfterPDataSize => 0x400;
    public static int AfterPDataVirtualSize => 0x2C4;
    public static int AfterGfidsSize => 0x200;
    public static int AfterGfidsVirtualSize => 0x10;
    public static int AfterRsrcSize => 0x200;
    public static int AfterRsrcVirtualSize => 0x1E0;
    public static int AfterRelocSize => 0x200;
    public static int AfterRelocVirtualSize => 0x2C;

    // ********************************************
    // COFF Groups
    // ********************************************
    public static int BeforeTextMnSize => 0x18A0;
    public static int BeforeTextXSize => 0xCD;
    public static int BeforeIdata5Size => 0xF8;
    public static int BeforeRdataCGSize => 0x6C8;
    public static int BeforeXdataSize => 0x1B4;
    public static int BeforeBssVirtualSize => 0x628;
    public static int BeforePDataCOFFGroupSize => 0x294;

    public static int AfterTextMnSize => 0x1B10;
    public static int AfterTextXSize => 0x11D;
    public static int AfterIdata5Size => 0x148;
    public static int AfterRdataCGSize => 0x7C8;
    public static int AfterXdataSize => 0x2DC;
    public static int AfterBssVirtualSize => 0x638;
    public static int AfterPDataCOFFGroupSize => 0x2C4;

    // ********************************************
    // LIBs
    // ********************************************
    public static int LibsShared => 6; // Number of libs that are common between 'before' and 'after' (including import libs): kernel32, ucrtd, vcruntimed, StaticLib1, MSVCRTD, "... no name found..." for LTCG
    public static int LibsUniquelyInBefore => 1; // StaticLib2
    public static int LibsUniquelyInAfter => 2; // StaticLib3, and msvcprtd due to iostream usage

    // dllmain.obj when found as a lib
    public static int DllMainLibSizeBefore => 307;
    public static int DllMainLibSizeAfter => 1440;
    public static int DllMainLibVirtualSizeBefore => 315;
    public static int DllMainLibVirtualSizeAfter => 1460;

    // ********************************************
    // OBJs/Compilands
    // ********************************************
    public static int ObjsInStaticLib1Shared => 2;
    public static int ObjsInStaticLib1Before => ObjsInStaticLib1Shared + 0;
    public static int ObjsInStaticLib1After => ObjsInStaticLib1Shared + 0;
    public static int ObjsInStaticLib2 => 2;
    public static int ObjsInStaticLib3 => 2;
    public static int ObjsUniquelyInBefore => 1 + ObjsInStaticLib2; // +1 is SourceFile2.obj
    public static int ObjsUniquelyInAfter => 1 + ObjsInStaticLib3; // +1 is SourceFile3.obj
    public static int ObjsShared => ObjsDirectlyInDLLShared + ObjsFromLinkerShared + ObjsInStaticLib1Shared + ObjsInMSVCRTD;
    public static int ObjsInMSVCRTD => 25;
    public static int ObjsFromLinkerShared => 3;
    public static int ObjsFromLinkerBefore => ObjsFromLinkerShared + 0;
    public static int ObjsFromLinkerAfter => ObjsFromLinkerShared + 0;
    public static int ObjsDirectlyInDLLShared => 3; // dllmain, SourceFile1, stdafx
    public static int ObjsDirectlyInDLLUniquelyInBefore => 1; // SourceFile2
    public static int ObjsDirectlyInDLLUniquelyInAfter => 1; // SourceFil3
    public static int ObjsDirectlyInDLLBefore => ObjsDirectlyInDLLShared + ObjsDirectlyInDLLUniquelyInBefore;
    public static int ObjsDirectylInDLLAfter => ObjsDirectlyInDLLShared + ObjsDirectlyInDLLUniquelyInAfter;
    public static int CompilandsInEachImportLib => 2;
    public static int ImportLibsShared => 3;
    public static int ImportLibsBefore => ImportLibsShared;
    public static int ImportLibsUniquelyInBefore => 0;
    public static int ImportLibsUniquelyInAfter => 1; // Added msvcprtd
    public static int ImportLibsAfter => ImportLibsBefore + ImportLibsUniquelyInAfter;
    public static int CompilandsUniquelyInBefore => ObjsUniquelyInBefore + ImportLibsUniquelyInBefore * CompilandsInEachImportLib;
    public static int CompilandsUniquelyInAfter => ObjsUniquelyInAfter + ImportLibsUniquelyInAfter * CompilandsInEachImportLib;
    public static int CompilandsShared => ObjsShared +
                                          (CompilandsInEachImportLib * ImportLibsShared);


    // dllmain.obj when found as a compiland
    public static int DllMainCompilandSizeBefore => DllMainLibSizeBefore;
    public static int DllMainCompilandSizeAfter => DllMainLibSizeAfter;
    public static int DllMainCompilandVirtualSizeBefore => DllMainLibVirtualSizeBefore;
    public static int DllMainCompilandVirtualSizeAfter => DllMainLibVirtualSizeAfter;


    // ********************************************
    // Specific symbols
    // ********************************************
    public static int DllMainFunctionSymbolSizeBefore => 123;
    public static int DllMainFunctionSymbolSizeAfter => 164;

    public static int intArrayInBssVirtualSizeBefore => 0; // did not exist in before
    public static int intArrayInBssVirtualSizeAfter => 12; // MapView says this is 16 bytes but that is wrong, it is an int[3]
}
