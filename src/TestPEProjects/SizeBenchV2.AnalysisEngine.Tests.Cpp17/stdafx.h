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
#include <exception>

inline bool FreeFunctionUsingSEH(bool& maybeThrow) noexcept
{
    char a[1] = { '0' };

    __try
    {
        __try
        {
            _itoa(10, a, 10);

            if (maybeThrow)
            {
                DebugBreak();
            }
        }
        __finally
        {
            maybeThrow = false;
        }
    }
    __except (GetExceptionCode() == EXCEPTION_BREAKPOINT ?
        EXCEPTION_EXECUTE_HANDLER :
        EXCEPTION_CONTINUE_SEARCH)
    {

    }
    return true;
}

class Cpp17_CppxdataUsage
{
    bool m_bShouldThrow = false;

public:
    Cpp17_CppxdataUsage(bool shouldThrow)
        : m_bShouldThrow(shouldThrow)
    {}

    ~Cpp17_CppxdataUsage();

    bool MaybeThrow();
    bool MaybeThrowWithSEH() noexcept;
};