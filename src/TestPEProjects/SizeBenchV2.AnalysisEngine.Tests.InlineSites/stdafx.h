// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>

#include "stdio.h"
#include <stdlib.h>

__forceinline
bool forceInlinedFunction(int argc)
{
    if (argc > 0)
    {
        printf("forceInlinedFunction: %d\n", argc);
        return true;
    }
    else
    {
        printf("forceInlinedFunction argc less than or equal to zero\n");
        return false;
    }
}

__forceinline
bool anotherForceInlinedFunction(int argc)
{
    if (argc > 0)
    {
        printf("anotherForceInlinedFunction: %d\n", argc);
        return true;
    }
    else
    {
        printf("anotherForceInlinedFunction argc less than or equal to zero\n");
        return false;
    }
}