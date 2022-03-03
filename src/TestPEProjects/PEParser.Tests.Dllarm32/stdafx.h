#pragma once
// pch.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#define _CRT_SECURE_NO_WARNINGS // using this to intentionally generate unsafe calls to _itoa for SEH testing

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>


class DllArm_CppxdataUsage
{
	bool m_bShouldThrow = false;

public:
	DllArm_CppxdataUsage(bool shouldThrow)
		: m_bShouldThrow(shouldThrow)
	{}

	bool MaybeThrow();
	bool MaybeThrowWithSEH();
};


class DllArm_BaseClass
{
	int m_int;

public:
	DllArm_BaseClass(int i)
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

class DllArm_DerivedClass : DllArm_BaseClass
{
	int m_intDerived;

public:
	DllArm_DerivedClass(int iBase, int iDerived)
		: DllArm_BaseClass(iBase),
		m_intDerived(iDerived)
	{}

	int AVirtualFunction() override
	{
		return m_intDerived + DllArm_BaseClass::AVirtualFunction();
	}
};