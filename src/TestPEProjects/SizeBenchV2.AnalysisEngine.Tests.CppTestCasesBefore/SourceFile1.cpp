#include "stdafx.h"
#include <vector>

const wchar_t* const wideStringInSourceFile1 = L"a wide string in SourceFile1";
const wchar_t* const wideStringInSourceFile1WithEmbeddedHorizontalTab = L"a wide string with an\tembedded horizontal tab";
const char* const ansiStringInSourceFile1WithEmbeddedHorizontalTab = "an ANSI string with an\tembedded horizontal tab";
const wchar_t* const wideStringInSourceFile1WithEmbeddedUnicodeCharacters = L"a wide string with embedded vertical tab: \x2B7F, and a fancy star: \x2B51";
wchar_t globalMutableString[] = L"Test";

__declspec(noinline) void FunctionDefinedInSourceFile1()
{
    std::vector<wchar_t> vectorOfWideChars;
    for (int i = 0; i < 100; i++)
        vectorOfWideChars.push_back(L'a' + i);
    printf("dummy print from source file 1, duplicatedPoint.x=%d, &duplicatedPointArray=%x", duplicatedPoint.x, &duplicatedPointArray);
    printf("&duplicatedOnlyInBefore=%x", &duplicatedOnlyInBefore);
    printf("wideStringInSourceFile1=%x", &wideStringInSourceFile1);
    printf("wideStringInSourceFile1WithEmbeddedHorizontalTab=%x", &wideStringInSourceFile1WithEmbeddedHorizontalTab);
    printf("ansiStringInSourceFile1WithEmbeddedHorizontalTab=%x", &ansiStringInSourceFile1WithEmbeddedHorizontalTab);
    printf("wideStringInSourceFile1WithEmbeddedUnicodeCharacters=%x", &wideStringInSourceFile1WithEmbeddedUnicodeCharacters);
    printf("vectorOfWideChars.size()=%d", vectorOfWideChars.size());

    __annotation(L"This is a test annotation in SourceFile1.cpp");
    globalMutableString[0] = L'B';
}