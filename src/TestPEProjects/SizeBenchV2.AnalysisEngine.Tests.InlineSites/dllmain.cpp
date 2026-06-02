// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"

__declspec(noinline)
void noInlineFunction(int argc)
{
    if (argc > 10)
    {
        char buffer[200] = { 0 };
        _itoa_s(argc, buffer, 200, 10 /* _Radix */);

        puts("noInlineFunction");
        puts(buffer);
    }
    else
    {
        puts("noInlineFunction argc less than or equal to 10");
    }
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
					 )
{
    forceInlinedFunction(ul_reason_for_call);
    anotherForceInlinedFunction(ul_reason_for_call);

    noInlineFunction(ul_reason_for_call);

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

