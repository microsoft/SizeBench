#pragma once

#include "MyProject.TestRuntimeClass.g.h"

namespace winrt::MyProject::implementation
{
    struct TestRuntimeClass : TestRuntimeClassT<TestRuntimeClass>
    {
        TestRuntimeClass() = default;

        winrt::hstring Name() const;
        void Name(winrt::hstring const& value);

    private:
        hstring m_name;
    };
}

namespace winrt::MyProject::factory_implementation
{
    struct TestRuntimeClass : TestRuntimeClassT<TestRuntimeClass, implementation::TestRuntimeClass>
    {
    };
}