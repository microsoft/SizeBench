#include "stdafx.h"

void FunctionDefinedInSourceFile3()
{
    printf("dummy print from source file 3, duplicatedPoint.x=%d, &duplicatedPointArray=%x", duplicatedPoint.x, &duplicatedPointArray);
}

void AnotherFunctionDefinedInSourceFile3()
{
    printf("and yet another dummy print, with a different string to ensure it doesn't fold, duplicatedPoint.x=%d, &intArrayInBss=%x", duplicatedPoint.x, &intArrayInBss);
}