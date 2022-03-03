#include "stdafx.h"

const wchar_t* const wideStringInSourceFile1 = L"a wide string in SourceFile1";
const unsigned short arrayThatGrows[3] = { 101, 202, 303 };

__declspec(noinline) void FunctionDefinedInSourceFile1()
{
    printf("dummy print from source file 1, duplicatedPoint.x=%d", duplicatedPoint.x);
    printf("&duplicatedOnlyInAfter=%x", &duplicatedOnlyInAfter);
    printf("wideStringInSourceFile1=%x", &wideStringInSourceFile1);
}