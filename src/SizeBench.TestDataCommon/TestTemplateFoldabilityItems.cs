using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.SessionTasks;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.TestDataCommon;

internal static class TestTemplateFoldabilityItems
{
    internal static List<IFunctionCodeSymbol> GenerateSomeFoldableTemplatedFunctions(SessionDataCache dataCache, IDIAAdapter diaAdapter, ISession session, ref uint nextSymIndexId)
    {
        var boolType = new BasicTypeSymbol(dataCache, "bool", size: 1, symIndexId: nextSymIndexId++);
        var constBoolType = new ModifiedTypeSymbol(dataCache, boolType, "const bool", size: 1, symIndexId: nextSymIndexId++);
        var intType = new BasicTypeSymbol(dataCache, "int", size: 1, symIndexId: nextSymIndexId++);
        var voidType = new BasicTypeSymbol(dataCache, "void", size: 0, symIndexId: nextSymIndexId++);
        var intPointerType = new PointerTypeSymbol(dataCache, intType, "int*", instanceSize: 8, symIndexId: nextSymIndexId++);
        var boolPointerType = new PointerTypeSymbol(dataCache, boolType, "bool*", instanceSize: 8, symIndexId: nextSymIndexId++);
        var constBoolPointerType = new PointerTypeSymbol(dataCache, constBoolType, "const bool*", instanceSize: 8, symIndexId: nextSymIndexId++);
        var aComplexTypeOfInt = new UserDefinedTypeSymbol(dataCache, diaAdapter, session, "AComplex::Type<int>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);
        var aComplexTypeOfFloat = new UserDefinedTypeSymbol(dataCache, diaAdapter, session, "AComplex::Type<float>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);
        var aComplexTypeOfSomeUDT = new UserDefinedTypeSymbol(dataCache, diaAdapter, session, "AComplex::Type<SomeUDT>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);
        var functions = new List<IFunctionCodeSymbol>()
            {
                // RVA 100: MyType::MyFunction<int>(bool, int) -> MyType::MyFunction<T1>(bool, T1)
                new SimpleFunctionCodeSymbol(dataCache, "MyType::MyFunction<int>", rva: 100, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, intType }, returnValueType: voidType)),

                // RVA 200: MyType::MyFunction<bool>(bool, int) -> MyType::MyFunction<T1>(T1, int)
                new SimpleFunctionCodeSymbol(dataCache, "MyType::MyFunction<bool>", rva: 200, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, intType }, returnValueType: voidType)),

                // RVA 300: SomeNamespace::MyType::FoldableFunction<AComplex::Type<int>,bool>(bool, AComplex::Type<int>) -> SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)
                new SimpleFunctionCodeSymbol(dataCache, "SomeNamespace::MyType::FoldableFunction<AComplex::Type<int>,bool>", rva: 300, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, aComplexTypeOfInt }, returnValueType: voidType)),

                // Another thing at RVA 300 that already folded with the one above at RVA 300
                // RVA 300: SomeNamespace::MyType::FoldableFunction<AComplex::Type<float>,bool>(bool, AComplex::Type<float>) -> SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)
                new SimpleFunctionCodeSymbol(dataCache, "SomeNamespace::MyType::FoldableFunction<AComplex::Type<float>,bool>", rva: 300, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, aComplexTypeOfFloat }, returnValueType: voidType)),

                // Another thing at RVA 400 that didn't fold, but is grouped with FoldableFunction
                // RVA 400: SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool(bool, AComplex::Type<SomeUDT>) -> SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)
                new SimpleFunctionCodeSymbol(dataCache, "SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool>", rva: 400, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, aComplexTypeOfSomeUDT }, returnValueType: voidType)),

                // RVA 500: FoldableWithDuplicateType<int,bool,int>(int) const -> FoldableWithDuplicateType<T1,T2,T1>(T1) const
                new SimpleFunctionCodeSymbol(dataCache, "FoldableWithDuplicateType<int,bool,int>", rva: 500, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: new TypeSymbol[] { intType }, returnValueType: voidType)),

                // RVA 600: FoldableWithDuplicateType<int*,bool,int*>(int*) const -> FoldableWithDuplicateType<T1,T2,T1>(T1) const
                new SimpleFunctionCodeSymbol(dataCache, "FoldableWithDuplicateType<int*,bool,int*>", rva: 600, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: new TypeSymbol[] { intPointerType }, returnValueType: voidType)),

                // RVA 700: FoldableWithDuplicateType<AComplex::Type<SomeUDT>,bool,AComplex::Type<SomeUDT>>(AComplex::Type<SomeUDT>) const -> FoldableWithDuplicateType<T1,T2,T1>(T1) const
                new SimpleFunctionCodeSymbol(dataCache, "FoldableWithDuplicateType<AComplex::Type<SomeUDT>,bool,AComplex::Type<SomeUDT>>", rva: 700, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: true, isVolatile: false, argumentTypes: new TypeSymbol[] { aComplexTypeOfSomeUDT }, returnValueType: voidType)),

                // RVA 800: FoldableVolatile<int>(int*) volatile -> FoldableVolatile<T1>(T1*) volatile
                new SimpleFunctionCodeSymbol(dataCache, "FoldableVolatile<int>", rva: 800, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: new TypeSymbol[] { intPointerType }, returnValueType: voidType)),

                // RVA 900: FoldableVolatile<bool>(bool*) volatile -> FoldableVolatile<T1>(T1*) volatile
                new SimpleFunctionCodeSymbol(dataCache, "FoldableVolatile<bool>", rva: 900, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: new TypeSymbol[] { boolPointerType }, returnValueType: voidType)),

                // RVA 900 again, this folded with the one above
                // RVA 900: FoldableVolatile<const bool>(const bool*) volatile -> FoldableVolatile<T1>(T1*) volatile
                new SimpleFunctionCodeSymbol(dataCache, "FoldableVolatile<const bool>", rva: 900, size: 10, symIndexId: nextSymIndexId++,
                                         functionType: new FunctionTypeSymbol(dataCache, "type name", size: 0, symIndexId: nextSymIndexId++, isConst: false, isVolatile: true, argumentTypes: new TypeSymbol[] { constBoolPointerType }, returnValueType: voidType)),
            };

        return functions;
    }

    internal static void SetupFoldableSimilarity(Mock<ISession> mockSession, TestDIAAdapter testDiaAdapter)
    {
        var myTypeFoldableFunction1 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FormattedName.IncludeParentType == "SomeNamespace::MyType::FoldableFunction<AComplex::Type<int>,bool>");
        var myTypeFoldableFunction2 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FormattedName.IncludeParentType == "SomeNamespace::MyType::FoldableFunction<AComplex::Type<float>,bool>");
        var myTypeFoldableFunction3 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FormattedName.IncludeParentType == "SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool>");
        // SomeNamespace::MyType::FoldableFunction first vs. second - 100% similarity, they folded together
        // SomeNamespace::MyType::FoldableFunction first vs. third - 80% similarity
        mockSession.Setup(s => s.CompareSimilarityOfCodeBytesInBinary(myTypeFoldableFunction1, myTypeFoldableFunction2)).Returns(1.0f);
        mockSession.Setup(s => s.CompareSimilarityOfCodeBytesInBinary(myTypeFoldableFunction1, myTypeFoldableFunction3)).Returns(0.8f);

        var foldableWithDuplicateType1 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FormattedName.IncludeParentType == "FoldableWithDuplicateType<int,bool,int>");
        var foldableWithDuplicateType2 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FormattedName.IncludeParentType == "FoldableWithDuplicateType<int*,bool,int*>");
        var foldableWithDuplicateType3 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FormattedName.IncludeParentType == "FoldableWithDuplicateType<AComplex::Type<SomeUDT>,bool,AComplex::Type<SomeUDT>>");
        // FoldableWithDuplicateType first vs. second - 60% similarity
        // FoldableWithDuplicateType second vs. third - 40% similarity
        mockSession.Setup(s => s.CompareSimilarityOfCodeBytesInBinary(foldableWithDuplicateType1, foldableWithDuplicateType2)).Returns(0.6f);
        mockSession.Setup(s => s.CompareSimilarityOfCodeBytesInBinary(foldableWithDuplicateType2, foldableWithDuplicateType3)).Returns(0.4f);

        var foldableVolatile1 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FunctionName == "FoldableVolatile<int>");
        var foldableVolatile2 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FunctionName == "FoldableVolatile<bool>");
        var foldableVolatile3 = testDiaAdapter.TemplatedFunctionsToFind.Single(f => f.FunctionName == "FoldableVolatile<const bool>");
        // FoldableVolatile first vs. second - 90% similarity
        // FoldableVolatile second vs. third - 100% similarity since they're COMDAT-folded
        mockSession.Setup(s => s.CompareSimilarityOfCodeBytesInBinary(foldableVolatile1, foldableVolatile2)).Returns(0.9f);
        mockSession.Setup(s => s.CompareSimilarityOfCodeBytesInBinary(foldableVolatile2, foldableVolatile3)).Returns(1.0f);
    }

    internal static List<TemplateFoldabilityItem> GenerateSomeTemplateFoldabilityItems(Mock<ISession> mockSession, SessionDataCache dataCache, TestDIAAdapter testDiaAdapter, ref uint nextSymIndexId, CancellationToken token)
    {
        using var DataCache = new SessionDataCache();

        var SessionTaskParameters = new SessionTaskParameters(
            mockSession.Object,
            testDiaAdapter,
            dataCache);

        testDiaAdapter.TemplatedFunctionsToFind = GenerateSomeFoldableTemplatedFunctions(dataCache, testDiaAdapter, mockSession.Object, ref nextSymIndexId);
        SetupFoldableSimilarity(mockSession, testDiaAdapter);

        var task = new EnumerateTemplateFoldabilitySessionTask(SessionTaskParameters,
            null /*progressReporter*/,
            token);

        using var logger = new NoOpLogger();
        return task.Execute(logger);
    }
}
