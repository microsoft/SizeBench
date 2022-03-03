#include "stdafx.h"
#include <vector>

const unsigned short arrayThatGrows[2] = { 101, 202 };

void FunctionDefinedInSourceFile2()
{
    std::vector<double> vectorOfDoubles;
    for (int i = 0; i < 100; i++)
        vectorOfDoubles.push_back(1.234 + i);
    printf("dummy print from source file 2, duplicatedPoint.x=%d, &duplicatedPointArray=%x", duplicatedPoint.x, &duplicatedPointArray);
    printf("&duplicatedOnlyInBefore=%x", &duplicatedOnlyInBefore);
    printf("vectorOfDoubles.size()=%d", vectorOfDoubles.size());
}

void FunctionDefinedInSourceFile2ThatGetsInlined()
{
    __annotation(L"This is a test annotation in SourceFile2.cpp");
    printf("printed from FunctionDefinedInSourceFile2ThatGetsInlined");
}