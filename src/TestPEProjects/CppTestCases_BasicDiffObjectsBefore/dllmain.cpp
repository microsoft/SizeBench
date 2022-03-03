// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "StaticLib1.h"
#include "StaticLib2.h"

BOOL APIENTRY DllMain( HMODULE hModule,
					   DWORD  ul_reason_for_call,
					   LPVOID lpReserved
					 )
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		FunctionDefinedInSourceFile1();
		FunctionDefinedInSourceFile2();
		FunctionInStaticLib1(42);
		FunctionInStaticLib2(84);
		Base1 base1Instance;
		Base1_Derived1_MoreDerived1 moreDerived1Instance;
		printf("base1Instance.x=%d\n", base1Instance.VirtualFunctionWithNoOverrides());
		printf("moreDerived1Instance.x=%d\n", moreDerived1Instance.VirtualFunctionWithNoOverrides(1.4f));
		break;
	}
	return TRUE;
}

