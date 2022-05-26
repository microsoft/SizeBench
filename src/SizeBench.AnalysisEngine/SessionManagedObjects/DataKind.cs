namespace SizeBench.AnalysisEngine;

// This is defined by DIA (see cvconst.h), DO NOT CHANGE IT!
internal enum DataKind
{
    DataIsUnknown,
    DataIsLocal,
    DataIsStaticLocal,
    DataIsParam,
    DataIsObjectPtr,
    DataIsFileStatic,
    DataIsGlobal,
    DataIsMember,
    DataIsStaticMember,
    DataIsConstant
};
