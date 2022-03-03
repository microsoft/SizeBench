// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#define _CRT_SECURE_NO_WARNINGS // using this to intentionally generate unsafe calls to _itoa for SEH testing

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>


class DllCxxFrameHandler4_CppxdataUsage
{
    bool m_bShouldThrow = false;

public:
    DllCxxFrameHandler4_CppxdataUsage(bool shouldThrow)
        : m_bShouldThrow(shouldThrow)
    {}

    bool MaybeThrow();
    int MaybeThrowWithContTypeNONE();
    int MaybeThrowWithContTypeONE();
    int MaybeThrowWithContTypeTWO();
};