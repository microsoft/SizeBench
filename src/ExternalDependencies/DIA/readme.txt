The current version of DIA: Visual Studio 2022 17.2 Preview 3

Here's how to take a new drop of DIA:
1. Get msdia<version>.dll from somewhere.
2. Get dia2.idl that matches that DLL, and all the headers (cvconst.h, etc.)
3. Update this file to indicate what location/build you took the new drop from.
4. Run these commands:
   cd <SizeBenchRoot>\ExternalDependencies\DIA
   midl dia2.idl /tlb dia2.tlb
   tlbimp dia2.tlb
7. Open the resulting Dia2Lib.dll in Visual Studio's Object Browser, or ILSpy, verify it has the new things you expect