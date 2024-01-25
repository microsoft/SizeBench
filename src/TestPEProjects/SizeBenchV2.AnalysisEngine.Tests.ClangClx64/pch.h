// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

class Dllx64_CppxdataUsage
{
    bool m_bShouldThrow = false;

public:
    Dllx64_CppxdataUsage(bool shouldThrow)
        : m_bShouldThrow(shouldThrow)
    {}

    bool MaybeThrow();
    bool MaybeThrowWithSEH();
};


class Dllx64_BaseClass
{
    int m_int;

public:
    Dllx64_BaseClass(int i)
        : m_int(i)
    {}

    virtual int AVirtualFunction()
    {
        return m_int;
    }

    virtual int ASecondVirtualFunction()
    {
        return m_int * 2;
    }
};

class Dllx64_DerivedClass : Dllx64_BaseClass
{
    int m_intDerived;

public:
    Dllx64_DerivedClass(int iBase, int iDerived)
        : Dllx64_BaseClass(iBase),
        m_intDerived(iDerived)
    {}

    int AVirtualFunction() override
    {
        return m_intDerived + Dllx64_BaseClass::AVirtualFunction();
    }
};

class TypeWithClangExtensions
{
    _Float16 m_Float16;
    float m_float;

public:
    _Float16 GetFloat16() const;
    float GetFloat() const;
    void SetFloat16(_Float16 f);
};

#endif //PCH_H
