#include "pch.h"
#include <exception>

bool Dllx64_CppxdataUsage::MaybeThrow()
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

bool Dllx64_CppxdataUsage::MaybeThrowWithSEH()
{
    char a[1] = { '0' };

    __try
    {
        __try
        {
            _itoa(10, a, 10);

            if (m_bShouldThrow)
            {
                DebugBreak();
            }
        }
        __finally
        {
            m_bShouldThrow = false;
        }
    }
    __except (GetExceptionCode() == EXCEPTION_BREAKPOINT ?
        EXCEPTION_EXECUTE_HANDLER :
        EXCEPTION_CONTINUE_SEARCH)
    {

    }
    return true;
}

_Float16 TypeWithClangExtensions::GetFloat16() const
{
    return m_Float16;
}

float TypeWithClangExtensions::GetFloat() const
{
    return m_float;
}

void TypeWithClangExtensions::SetFloat16(_Float16 value)
{
    m_Float16 = value;
}