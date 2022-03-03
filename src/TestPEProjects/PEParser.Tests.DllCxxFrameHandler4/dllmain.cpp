// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"

BOOL APIENTRY DllMain( HMODULE hModule,
					   DWORD  ul_reason_for_call,
					   LPVOID lpReserved
					 )
{
	DllCxxFrameHandler4_CppxdataUsage cppxdataUsage(true);
    cppxdataUsage.MaybeThrow();
    cppxdataUsage.MaybeThrowWithContTypeNONE();
    cppxdataUsage.MaybeThrowWithContTypeONE();
    cppxdataUsage.MaybeThrowWithContTypeTWO();

	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

