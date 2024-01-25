Each PE file produced here has a specific purpose for testing.  Here's what they are:

Single Binary Testing
---------------------
PEParser.* - testing the PE parser, basically things hand-parsed out of the PE (PDATA, XDATA, RSRC) of different CPU architectures
PEParser.Tests.DllCxxFrameHandler4 - tests __CxxFrameHandler4 support introduced in VS 2019.
Cpp32BitDll - an x86 32-bit DLL with basic C++ code in it
CppDll - an x64 64-bit DLL with basic C++ code in it
CppWinRT - a DLL using C++/WinRT which we test because it's getting increasing usage and introduces complexity for Wasteful Virtual analysis due to how C++/WinRT implements COM
Dllx64Managed - for testing that managed code is rejected for now (until SizeBench can be updated to do something useful with managed code)
Dllx64MinimalPDB - for testing "minimal PDB" support in DIA
Dllx64CustomAlign - for testing link.exe /ALIGN usage
FortranDll - a DLL built using Intel's FORTRAN compiler (ifort).  This is not in the VS solution because I don't want users to have to have the Intel Fortran extensions installed
             to open the solution in VS to work on SizeBench, and the likelihood is that this will very rarely change.  This project can be opened in Intel's Visual Studio extension
             manually if you really need to rebuild it later.
Rust - for testing Rust code, for scenarios like "enums with methods" that can't be emitted into PDBs by C/C++.
       To build this use "cargo build --release" and get the DLL and PDB out of the target\release folder.
ClangClx64 - for testing an x64 DLL built with clang-cl and lld-link, to test things like Clang language extensions (e.g. _Float16)

Diff Testing
------------
These two binaries should be used for "before"/"after" tests that won't depend on RVAs or specific section sizes or things like that,
because there'll be lots of tests that don't depend on that and adding them here will allow the test suite to not constantly need to be
tweaked for slight binary layout differences.
Examples of stuff to put in here: wasteful virtuals, type layouts, function names and types/parameter lists, stuff like that

    CppTestCasesBefore - The "before" case for testing diffs
    CppTestCasesAfter - The "after" case for testing diffs

Tests for "basic diff objects" - section, COFF Group, Compiland, and Lib.  These things all depend a lot on exact sizes and RVAs to validate
the AnalysisEngine, so ideally this test binary changes rarely since it'll cascade across a lot of test verifications...
    CppTestCases_BasicDiffObjectsBefore
    CppTestCases_BasicDiffObjectsAfter


Static Libs
-----------
Used for testing various parts of the analysis engine that look at "libs" (which aren't always static libs, but this is a convenient way
to test anyway).

StaticLib1 - Meant to be included in both "before" and "after" for diffs, to test diffing on a lib with the same name.
StaticLib2 - Meant to be included only in "before" to test the case where a static lib is removed in "after".
StaticLib3 - Meant to be included only in "after" to test the case where a new static lib is introduced.