// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include <vector>

BOOL APIENTRY DllMain( HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved
)
{
    class TestIsStaticOrLocal {
    public:
        __declspec(noinline) virtual void virtualNonStatic()
        {
            printf("TestIsStaticOrLocal::virtualNonStatic()\n");
        }

        __declspec(noinline) static void staticFunction()
        {
            printf("TestIsStaticOrLocal::staticFunction()\n");
        }
    };

    TestIsStaticOrLocal* p = new TestIsStaticOrLocal;

    p->virtualNonStatic();
    p->staticFunction();

    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        FunctionDefinedInSourceFile1();
        FunctionDefinedInSourceFile2();

        __annotation(L"annotation in DllMain itself, on the line that gets inlined...");
        FunctionDefinedInSourceFile2ThatGetsInlined();

        Base1 base1Instance;
        Base1_Derived1_MoreDerived1 moreDerived1Instance;
        xstack<int> stackOfInts;
        printf("base1Instance.x=%d\n", base1Instance.VirtualFunctionWithNoOverrides());
        printf("moreDerived1Instance.x=%d\n", moreDerived1Instance.VirtualFunctionWithNoOverrides(1.4f));
        std::vector<int> vectorOfInts;
        std::vector<xstack<int>*> vectorOfPointerToXStackOfInts;
        vectorOfInts.push_back(123);
        vectorOfPointerToXStackOfInts.push_back(&stackOfInts);
        printf("vectorOfInts[0]=%d\n", vectorOfInts[0]);
        printf("vectorOfPointerToXStackOfInts[0]=%p\n", vectorOfPointerToXStackOfInts[0]);

        printf("arrayThatGrows=%d, %d\n", arrayThatGrows[0], arrayThatGrows[1]);
        break;
    }
    return TRUE;
}