// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>

#include <stdio.h>
#include <stdint.h>

extern const unsigned short arrayThatGrows[2];

struct Point {
    int x = 0;
    int y = 0;

    Point(int x, int y)
    {
        __annotation(L"Test annotation in shared header stdafx.h, in Point struct constructor");
        this->x = x;
        this->y = y;
    }
};

__declspec(noinline) void FunctionDefinedInSourceFile1();
void FunctionDefinedInSourceFile2();
void FunctionDefinedInSourceFile2ThatGetsInlined();

static const Point duplicatedPoint = { 5, 5 };

static const int duplicatedOnlyInBefore = 10;

const Point duplicatedPointArray[] = {
    { 1, 1 },
    { 2, 2 },
    { 3, 3 }
};

class Base1 {
public:
    virtual void VirtualFunctionWithManyOverrides() { }
    virtual int VirtualFunctionWithNoOverrides() { return 3; };
};

class Base1_Derived1 : public Base1 {
public:
    void VirtualFunctionWithManyOverrides() override { };
    virtual void PureVirtualFunctionWithOneOverride() = 0;
    virtual void VirtualFunctionWithNoOverrides2() { };
    virtual int VirtualFunctionWithNoOverrides(int x) { return x; }; // Note this has an argument, so it's not an override for the one in Base1
};

class Base1_Derived1_MoreDerived1 : public Base1_Derived1 {
public:
    void VirtualFunctionWithManyOverrides() override final { };
    virtual int VirtualFunctionWithNoOverrides() const { return 5; }; // Note this is const, so it's not an override for the one in Base1
    virtual int VirtualFunctionWithNoOverrides(float y) { return (int)y; }; // Note this has a float argument so it's not an override for the int version in Base1_Derived1 or the argument-less version in Base1
    void PureVirtualFunctionWithOneOverride() override { };
};

class Base1_Derived2 : public Base1 {
public:
    void VirtualFunctionWithManyOverrides() override final { };
};



class BaseWastefulOnlyInBefore {
public:
    virtual void VirtualFunctionWithManyOverrides() { }
    virtual int VirtualFunctionWithNoOverrides() { return 3; };
};

class BaseWastefulOnlyInBefore_Derived1 : public BaseWastefulOnlyInBefore {
public:
    void VirtualFunctionWithManyOverrides() override { };
    virtual void PureVirtualFunctionWithOneOverride() = 0;
    virtual void VirtualFunctionWithNoOverrides2() { };
    virtual int VirtualFunctionWithNoOverrides(int x) { return x; }; // Note this has an argument, so it's not an override for the one in Base1
};

class BaseWastefulOnlyInBefore_Derived1_MoreDerived1 : public BaseWastefulOnlyInBefore_Derived1 {
    void PureVirtualFunctionWithOneOverride() override { };
};

class BaseWastefulOnlyInAfter {
};


class AccessModifiersTests {
private:
    bool privateField;
    bool privateFunction() { return false; }
    static int privateStaticFunction() { return 0; }
protected:
    int protectedField;
    int protectedConstFunction() const { return 0; };
protected:
    bool protectedField2;
    static void protectedStaticFunction() {}
};

struct ArraysTest {
    float testOneDimensionalArray[4];
    float testMultiDimensionalArray[3][2][8];
};

class InterspersedBitfieldsTest {
    /* Offset=0      */ int intBitfield1 : 1;
    /* Offset=0.125  */ int intBitfield2 : 2;
    /* Offset=0.375  */ // 29 bits of padding
    /* Offset=4      */ bool flag;
    /* Offset=5      */ bool flagBitfield1 : 1;
    /* Offset=5.125  */ // 23 bits of padding
    /* Offset=8      */ int x;
    /* Offset=12     */ int xBitfield1 : 6;
    /* Offset=12.750 */ // 26 bits of tail slop padding
};

class InterspersedBitfieldsTest_Derived : public InterspersedBitfieldsTest{
    /* Offset=16     */ bool flagBitfield2 : 3;
    /* Offset=16.375 */ // 13 bits of padding
    /* Offset=18     */ short shortMember;
};

class InterspersedBitfieldsTest_Derived_Withvfptr : InterspersedBitfieldsTest {
    virtual void VirtualFunction() { };
    /* Offset=0      */ // vfptr
    /* Offset 9-23 is the base type */
    /* Offset=24     */ bool anotherFlagBitfield : 5;
    /* Offset=24.625 */ // 3 bits of padding
    /* Offset=25     */ bool anotherFlag;
    /* Offset=26     */ // 2 bytes of padding
    /* Offset=28     */ int finalInt;
};

class TightlyPackedBitfields {
    int bit0 : 1;
    int bits1_to_10  : 10;
    int bits11_to_30 : 20;
    int bit31 : 1;
};

struct SimpleUnionTest {
    union {
        int x;
        short y;
    };
};

struct SimpleUnionTest_Derived : public SimpleUnionTest {
    int z;
};

struct _UMS_SYSTEM_THREAD_INFORMATIONTest {
    ULONG UmsVersion;
    union {
        struct {
            ULONG IsUmsSchedulerThread : 1;
            ULONG IsUmsWorkerThread : 1;
        };
        ULONG ThreadUmsFlags;
    };
};

struct _XSTATE_FEATURETest {
    ULONG Offset;
    ULONG Size;
};

struct _XSTATE_CONFIGURATIONTest {
    // Mask of all enabled features
    ULONG64 EnabledFeatures;

    // Mask of volatile enabled features
    ULONG64 EnabledVolatileFeatures;

    // Total size of the save area for user states
    ULONG Size;

    // Control Flags
    union {
        ULONG ControlFlags;
        struct
        {
            ULONG OptimizedSave : 1;
            ULONG CompactionEnabled : 1;
        };
    };

    // List of features
    _XSTATE_FEATURETest Features[MAXIMUM_XSTATE_FEATURES];

    // Mask of all supervisor features
    ULONG64 EnabledSupervisorFeatures;

    // Mask of features that require start address to be 64 byte aligned
    ULONG64 AlignedFeatures;

    // Total size of the save area for user and supervisor states
    ULONG AllFeatureSize;

    // List which holds size of each user and supervisor state supported by CPU        
    ULONG AllFeatures[MAXIMUM_XSTATE_FEATURES];

    // 4 bytes of padding to get to 8-byte alignment

};

class TrailingUnionWithBitfieldBase {
    int x;
    union {
        int y;
        int yBitfield : 1;
    };
    // no padding!
};

class TrailingUnionWithBitfieldBase_Derived : public TrailingUnionWithBitfieldBase {
    int z;
};

class AlignasUnspecifiedType {
    int x;
    char y;
    // 3 bytes of tail slop padding here since the type is going to align to the int, so 4-byte alignment
};

// TODO (Product Backlog Item 1500): support alignas(X) correctly in Class Layout View.
/*
class alignas(8) Alignas8Type {
    char x;
    char y;
    // 6 bytes of tail slop padding here since this type is aligned to 8-bytes
};

class alignas(1) Alignas1Type {
    char x;
    char y;
    // No padding, this is perfectly packed
};

class Alignas8Type_Derived : public Alignas8Type {
    char z;
    // 7 bytes of tail slop padding since Alignas8Type forces this to 8-byte alignment
};

class alignas(4) Alignas4TypeWithBitfields {
    bool x;
    bool y : 1;
    bool z : 1;
    // 2.75 bytes of tail slop padding here
};

class alignas(2) Alignas2TypeWithBitfields {
    bool x;
    bool y : 1;
    bool z : 1;
    // 0.75 bytes of tail slop padding here
};

class Alignas2TypeWithBitfields_Derived : public Alignas2TypeWithBitfields {
    bool z : 1;
    // 1.875 bytes of tail slop padding here, since the base type puts us at 2-byte alignment requirements
};
*/

class TestMemberTypes {
    int plainInt;
    int* pointerToInt;
    int* arrayOfPointerToInt[5];
    const int constInt;
    int& intRef;
    const int arrayOfConstInt[5];
};

template <typename T>
class _xvector_base
{
public:
    typedef size_t(*fGrowthPolicy)(size_t oldSize);

protected:
    uint8_t* m_data;
    uint32_t m_count;
    uint32_t m_actualSize;
    uint32_t m_StartOffset;
    fGrowthPolicy m_GrowthPolicyFunction;
};

template <typename T>
class xvector : public _xvector_base<T>
{

};

template <typename TData>
class xstack
    : private xvector<TData>
{
private:
    // Poison constructors
    xstack& operator=(_In_ const xstack& other);
    xstack(_In_ const xstack& other);
public:
    xstack() {}
    virtual ~xstack() {} // Introduces the vfptr in this class, not in a base type
};

enum ValueType : uint32_t
{
    valueAny = 0,    // Allows GetValue to specify return type.
};


// Regression test for a bug in DataSymbol parsing for UDT members of type "nullptr_t".
template <ValueType valueType> struct ValueTypeInfo {};

template <>
struct ValueTypeInfo<valueAny>
{
    static constexpr ValueType myValueType = valueAny;
    using Type = std::nullptr_t;
    static constexpr Type Empty = nullptr;
};

class TypeLayoutDiff_Basics
{
    int x;
    bool y;
    int z;
    int onlyInBefore;
};

class TypeLayoutDiff_Basics_Derived : public TypeLayoutDiff_Basics
{
};

class TypeLayoutDiff_Base
{
    uint16_t baseMember;
};

class TypeLayoutDiff_Derived : public TypeLayoutDiff_Base
{
    bool derivedMember;
};

class TypeLayoutDiff_MoreDerived : public TypeLayoutDiff_Derived
{
    bool moreDerivedMember;
    int moreDerivedMember2;
};

class TypeLayoutDiff_MoreDerived2 : public TypeLayoutDiff_Base
{
    bool moreDerived2Member2;
};

class TypeLayoutDiff_WantsToBeTightlyPacked
{
    int myInt;
    bool myBool;
    // 3 bytes of alignment here
    int myInt2;
    bool myBool2;
    // 3 bytes of tail slop alignment here
};

// Regression test for types that have padding as their first member (this used to crash when loading the type layout)
typedef struct TypeWithPaddingAsFirstMember
{
    UINT             : 1;
    BOOL A3BitBool   : 3;
    UINT A4BitUINT_1 : 4;
    UINT A4BitUINT_2 : 4;
    UINT A4BitUINT_3 : 4;
    UINT A4BitUINT_4 : 4;
    UINT A4BitUINT_5 : 4;
    UINT A4BitUINT_6 : 4;
    UINT A4BitUINT_7 : 4;
} TypeWithPaddingAsFirstMember;

// These next 3 types are to regression test against a bug where a "middle" type in a type
// hierarchy can be the one that introduces a vfptr, while a more-base type has members.
// That situation requires some careful handling of deterimining where the "last member"
// in the base types are at.
// This was found through some types in an xbox game that had this pattern.
struct BaseTypeWithNoVirtualsAndMembers {
    int x;
    int y;
    int* zp;
};

struct DerivedTypeWithVfptr : public BaseTypeWithNoVirtualsAndMembers {
    virtual void IntroduceAVfptr() { }
};

struct MoreDerivedType : public DerivedTypeWithVfptr {
    int* anotherMember;
};

#define enum1Bits 4
#define enum2Bits 3
#define enum3Bits 3

enum enum1 : unsigned int
{
    enum1Value0 = 0,
};

enum enum2 : unsigned int
{
    enum2Value0 = 0,
};

enum enum3 : unsigned int
{
    enum3Value0 = 0,
};

typedef struct ComplicatedTypeLayoutWithBitfieldsBase
{
    union
    {
        DWORD Exchange;

        struct
        {
            enum1 m_enum1 : enum1Bits;

            enum2 m_enum2 : enum2Bits;

            ULONG m_flag1 : 1;
            ULONG m_flag2 : 1;
            ULONG m_flag3 : 1;
            ULONG m_flag4 : 1;
            ULONG m_flag5 : 1;
            ULONG m_flag6 : 1;
            ULONG m_flag7 : 1;
            ULONG m_flag8 : 1;
            ULONG m_flag9 : 1;
            ULONG m_flag10 : 1;

            enum3 m_eSessionIdle : enum3Bits;
        };
    };
} ComplicatedTypeLayoutWithBitfieldsBase;

// This (and the types above needed to declare this) is a simplified repro from Office, as a regression test for a bug in
// TypeLayout loading, when a base type has a union that contains bitfields and non-bitfields, to ensure we calculate
// sizes and waste correctly.
class ComplicatedTypeLayoutWithBitfieldsDerived : public ComplicatedTypeLayoutWithBitfieldsBase
{
protected:
    void*                                         m_voidPtr1;
    void* const                                   m_voidPtr2Const;
    const ComplicatedTypeLayoutWithBitfieldsBase  m_pOld{};
    const bool                                    m_flagDerived1;
    bool                                          m_flagDerived2;
    bool                                          m_flagDerived3;
};