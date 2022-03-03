#pragma once

#include "pch.h"
#include "TestRuntimeClass.h"

#include "MyProject.TestRuntimeClass.g.cpp"

using namespace winrt;

namespace winrt::MyProject::implementation
{
    hstring TestRuntimeClass::Name() const
    {
        return m_name;
    }

    void TestRuntimeClass::Name(hstring const& value)
    {
        m_name = value;
    }
}