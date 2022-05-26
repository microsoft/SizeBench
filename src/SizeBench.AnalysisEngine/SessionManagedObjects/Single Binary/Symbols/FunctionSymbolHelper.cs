namespace SizeBench.AnalysisEngine.Symbols;

// Simple and Complex functions have many similar properties, this is where to put common code since they can't have a common base class.
internal static class FunctionSymbolHelper
{
    public static bool IsVeryLikelyTheSameAs(IFunctionCodeSymbol first, IFunctionCodeSymbol second)
    {
        // Putting these here helps with debugging
        var firstUniqueSignature = first.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature | FunctionCodeNameFormatting.IncludeReturnType);
        var secondUniqueSignature = second.FormattedName.GetFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature | FunctionCodeNameFormatting.IncludeReturnType);

        return String.Equals(firstUniqueSignature, secondUniqueSignature, StringComparison.Ordinal);
    }

    public static void VerifyNotInInconsistentState(IFunctionCodeSymbol function)
    {
        if (function.IsStatic && function.IsVirtual)
        {
            throw new InvalidOperationException("A function cannot be both static and virtual, that makes no sense - how did this happen?");
        }

        if (function.IsIntroVirtual && !function.IsVirtual)
        {
            throw new InvalidOperationException("A function cannot be IsIntroVirtual, unless it IsVirtual - how did this happen?");
        }

        // If a function were both pure and sealed, then the type is abstract and can't be used or derived from, which
        // seems impossible.
        if (function.IsSealed && function.IsPure)
        {
            throw new InvalidOperationException("A function cannot be IsSealed and IsPure - how did this happen?");
        }
    }
}
