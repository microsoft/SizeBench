#include "pch.h"
#include "TestRuntimeClass.h"

using namespace winrt;
using namespace Windows::Foundation;
using namespace winrt::MyProject;

int main()
{
    init_apartment();
    Uri uri(L"http://aka.ms/cppwinrt");
    printf("Hello, %ls!\n", uri.AbsoluteUri().c_str());

    TestRuntimeClass trc;
    trc.Name(L"test name");

    printf("TestRuntimeClass instance.Name=%ls\n", trc.Name().c_str());
}
