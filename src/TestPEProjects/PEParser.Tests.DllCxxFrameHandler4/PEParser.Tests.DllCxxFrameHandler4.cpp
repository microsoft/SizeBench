#include "stdafx.h"
#include <exception>

bool DllCxxFrameHandler4_CppxdataUsage::MaybeThrow()
{
    try
    {
        if (m_bShouldThrow)
        {
            throw std::exception("dummy exception");
        }
    }
    catch (std::exception except)
    {
        return false;
    }

    return true;
}

int DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeNONE()
{
    try
    {
        if (m_bShouldThrow)
        {
            throw 172;
        }
    }
    catch (int i)
    {
        switch (i)
        {
        case 3:
            return 2;
            break;
        case 4:
            return 1;
        default:
            return 172;
        }
    }

    return true;
}

int DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeONE()
{
    try
    {
        throw 1;
    }
    catch (...)
    {
        return 5;
    }
}

int DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeTWO()
{
    try
    {
        throw 1;
    }
    catch (...)
    {
        if (m_bShouldThrow)
        {
            return 5;
        }
        else
        {
            return 3;
        }
    }
}