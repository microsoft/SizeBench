using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateWastefulVirtualsSessionTask : SessionTask<List<WastefulVirtualItem>>
{
    public EnumerateWastefulVirtualsSessionTask(SessionTaskParameters parameters,
                                                CancellationToken token,
                                                IProgress<SessionTaskProgress>? progressReporter)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = "Enumerate Wasteful Virtuals";
    }

    protected override List<WastefulVirtualItem> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllWastefulVirtualItems != null)
        {
            logger.Log("Found wasteful virtual items in the cache, re-using them, hooray!");
            return this.DataCache.AllWastefulVirtualItems;
        }

        //TODO: WastefulVirtual: should make some kind of "stage" concept for progress reporting so that multi-pass things like this can report monotonically increasing progress.

        ReportProgress("Discovering all user-defined types in the binary", 0, null);

        var udts = this.DIAAdapter.FindAllUserDefinedTypes(logger, this.CancellationToken).ToList();

        // We need to have all base types loaded before we can determine derived types (since we used the base type info to calculate derivation).
        // So load all the base type information first, then go hookup derived types.
        using (var taskLog = logger.StartTaskLog("Loading all base types"))
        {
            udts.LoadAllBaseTypes(this.DataCache, this.DIAAdapter, this.CancellationToken, ReportProgress);
        }

        using (var taskLong = logger.StartTaskLog("Loading all derived types"))
        {
            udts.LoadAllDerivedTypes(this.CancellationToken, ReportProgress);
        }

        // Binaries contain a lot of types that don't exist in a hierarchy - a great example being most things in the win32 headers that are C data structures.
        // We're only going to examine stuff in inheratincae hierarchies - so we only need to look at types that have either a base or a derived type.
        // Restricting how many clasess we load functions for is important since functions are plentiful and loading hundreds of thousands of them takes a lot
        // of memory and CPU time querying DIA and constructing FunctionSymbol instances.

        var classesWorthLoadingFunctionsFor = udts.Where(x => x.DerivedTypeCount > 0 || x.BaseTypes?.Length > 0).ToList();

        using (var taskLog = logger.StartTaskLog("Loading all functions"))
        {
            LoadAllFunctionsIntoTypes(classesWorthLoadingFunctionsFor);
        }

        List<WastefulVirtualItem> wastefulVirtuals;
        using (var taskLog = logger.StartTaskLog("Analyzing all types for waste"))
        {
            wastefulVirtuals = AnalyzeForWaste(classesWorthLoadingFunctionsFor);
        }

        ReportProgress($"Enumerated {udts.Count:N0}/{udts.Count:N0} user-defined types, found {wastefulVirtuals.Count:N0} types with wasteful virtuals.", (uint)udts.Count, (uint)udts.Count);
        logger.Log($"Finished enumerating {wastefulVirtuals.Count} wasteful virtuals");
        this.DataCache.AllWastefulVirtualItems = wastefulVirtuals;

        return this.DataCache.AllWastefulVirtualItems;
    }

    private List<WastefulVirtualItem> AnalyzeForWaste(List<UserDefinedTypeSymbol> classesWorthLoadingFunctionsFor)
    {
        var wastefulVirtuals = new List<WastefulVirtualItem>();

        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var udtsEnumerated = 0;

        var classesWithVirtualsWorthExploring = classesWorthLoadingFunctionsFor.Where(x => x.Functions.Any(IsVirtualFunction)).ToList();

        foreach (var udt in classesWithVirtualsWorthExploring)
        {
            udtsEnumerated++;
            if (udtsEnumerated >= nextLoggerOutput)
            {
                ReportProgress($"Analyzed {udtsEnumerated:N0}/{classesWithVirtualsWorthExploring.Count:N0} user-defined types for waste, found {wastefulVirtuals.Count:N0} types with wasteful virtuals so far.", nextLoggerOutput, (uint)classesWithVirtualsWorthExploring.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            this.CancellationToken.ThrowIfCancellationRequested();

            if (udt.Functions is null || udt.Functions.Count == 0)
            {
                continue;
            }

            var waste = AnalyzeSingleUDTForWaste(udt);
            if (waste != null && waste.WastedSize > 0)
            {
                wastefulVirtuals.Add(waste);
            }
        }

        // TODO: for types that have no derived types, but have virtual functions, we could call these 'wasteful' but we don't bother to find
        //       those at this point since the savings are pretty small.

        return wastefulVirtuals;
    }

    private WastefulVirtualItem? AnalyzeSingleUDTForWaste(UserDefinedTypeSymbol udt)
    {
        WastefulVirtualItem? item = null;

        // Destructors are ignored for this analysis since a virtual destructor is often necessary and analyzing whether
        // or not that's true is out of scope for now.  Some day it'd be great to add that.
        foreach (var function in udt.Functions.Where(IsNonDestructorVirtualFunction))
        {
            var countOfOverrides = 0;
            var functionNameFormatted = function.FormattedName.GetFormattedName(WastefulVirtualItem.NameFormattingForWastedOverrides);

            // Check if any base type has this same function as a virtual function - if so, it will be attributed there, so we can skip it.
            var baseTypeContainsSameVirtualFunction = false;
            if (udt.BaseTypes != null)
            {
                foreach (var baseType in udt.BaseTypes)
                {
                    baseTypeContainsSameVirtualFunction |= BaseTypeContainsVirtualFunction(baseType._baseTypeSymbol, functionNameFormatted);
                    if (baseTypeContainsSameVirtualFunction)
                    {
                        break;
                    }
                }
            }

            // If the base type includes this function then we're not the one to attribute it to, so move along.
            // If this type doesn't have any derived types, then nobody can possibly override it - so we'll just continue
            // looking.  Technically this could be wasteful if somebody marks 'virtual' on a leaf type but in practice it's
            // such a small gain and the expense to calculate it is modest, so let's not bother.
            if (baseTypeContainsSameVirtualFunction || udt.DerivedTypeCount == 0)
            {
                continue;
            }

            IFunctionCodeSymbol? overriddenFunction = null;

            // If we get this far, this type is the one introducing this pure virtual - now see if <= 1 derived types have an override.
            // If so, this is wasteful as you could just devirtualize onto the single child implementing it (in many cases) and save vtable
            // slots, reloc entries, and more.
            if (udt.DerivedTypes is not null)
            {
                foreach (var derivedType in udt.DerivedTypes.Where(static type => type.Functions?.Count > 0))
                {
                    var firstOverrideFound = derivedType.Functions.FirstOrDefault(f =>
                        !IsPureVirtualFunction(f) &&
                        f.FormattedName.GetFormattedName(WastefulVirtualItem.NameFormattingForWastedOverrides).Equals(functionNameFormatted, StringComparison.Ordinal));

                    if (firstOverrideFound != null)
                    {
                        overriddenFunction = firstOverrideFound;
                        countOfOverrides++;
                    }

                    // Once we've found a couple overrides we don't need to keep looking - we only care about the 0-1 cases below.
                    if (countOfOverrides > 1)
                    {
                        break;
                    }
                }
            }

            if (function.IsPure && countOfOverrides == 1)
            {
                // If this function is PURE then...
                // If countOfOverrides is 0, this is probably something defined in a header this binary pulled in, and not anything to worry about.
                // If countOfOverrides is > 1, then this is probably using virtual 'reasonably'
                // If, however, countOfOverrides is exactly 1...then it seems like this function need not be virtual and could simply be declared
                //              directly on that derived type.  This would save a vtable slot in this class, as well as the entire hierarchy on down.

                item ??= new WastefulVirtualItem(udt, IsCOMTypeHeuristicGuess(udt), this.Session.BytesPerWord);

                // Note that we want to add the override to the list of functions, not the pure version - because pure functions don't have an RVA
                // so we can't go look them up later very well.
                item.AddWastedOverrideThatIsPureWithExactlyOneOverride(overriddenFunction!);
            }
            else if (!function.IsPure && countOfOverrides == 0)
            {
                // If this is not a pure function, and it's never overridden, that's also wasteful

                item ??= new WastefulVirtualItem(udt, IsCOMTypeHeuristicGuess(udt), this.Session.BytesPerWord);

                item.AddWastedOverrideThatIsNotPureWithNoOverrides(function);
            }
        }

        return item;
    }

    private static bool IsCOMTypeHeuristicGuess(UserDefinedTypeSymbol thisClass)
    {
        if (NameIsProbablyACOMType(thisClass.Name))
        {
            return true;
        }

        if (thisClass.BaseTypes is null)
        {
            return false;
        }

        foreach (var baseType in thisClass.BaseTypes.AsSpan())
        {
            if (IsCOMTypeHeuristicGuess(baseType._baseTypeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NameIsProbablyACOMType(string name)
    {
        if (name == "IUnknown")
        {
            // The IUnknown we know and love from normal COM in the Windows headers, no namespace or anything fancy.
            return true;
        }
        else if (name == "winrt::impl::abi<winrt::Windows::Foundation::IUnknown,void>::type")
        {
            // The IUnknown from C++/WinRT, which kind of 'shadows' the real IUnknown but it still means this is a 'COM type' for the purposes of wasteful virtuals.
            return true;
        }
        else if (name.StartsWith("winrt::impl::producer<", StringComparison.Ordinal))
        {
            // The "producer<>" template is weird - it manually implements a vtable without doing type inheritance.  Ideally we'd look at the template parameter here,
            // find that type, and check if *that* type derives from IUnknown or C++/WinRT's shadow IUnknown, but that is really tedious so for now we'll hope that all
            // "producer<>" usages are COM types to reduce noise in the wasteful virtual analysis for C++/WinRT customers.
            return true;
        }
        else if (name.StartsWith("winrt::impl::root_implements<", StringComparison.Ordinal))
        {
            // Similarly root_implements<> has a couple virtuals on it that are part of the C++/WinRT machinery that helps implement COM - so we'll count these as "COM types"
            return true;
        }

        return false;
    }

    private static bool IsVirtualFunction(IFunctionCodeSymbol function)
        => function.IsVirtual && !function.IsStatic;

    private static bool IsNonDestructorVirtualFunction(IFunctionCodeSymbol function)
        => IsVirtualFunction(function) && !function.FunctionName.Contains('~', StringComparison.Ordinal);

    private static bool IsPureVirtualFunction(IFunctionCodeSymbol function)
        => function.IsPure && IsVirtualFunction(function);

    private static bool BaseTypeContainsVirtualFunction(UserDefinedTypeSymbol thisClass, string functionFormattedName)
    {
        if (thisClass.Functions != null &&
            thisClass.Functions.Any(f => IsVirtualFunction(f) &&
                                         f.FormattedName.GetFormattedName(WastefulVirtualItem.NameFormattingForWastedOverrides).Equals(functionFormattedName, StringComparison.Ordinal)))
        {
            return true;
        }

        if (thisClass.BaseTypes != null)
        {
            foreach (var baseType in thisClass.BaseTypes)
            {
                if (BaseTypeContainsVirtualFunction(baseType._baseTypeSymbol, functionFormattedName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void LoadAllFunctionsIntoTypes(List<UserDefinedTypeSymbol> classesWorthLoadingFunctionsFor)
    {
        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var udtsEnumerated = 0;

        foreach (var udt in classesWorthLoadingFunctionsFor)
        {
            udtsEnumerated++;
            if (udtsEnumerated >= nextLoggerOutput)
            {
                ReportProgress($"Functions loaded for {udtsEnumerated:N0}/{classesWorthLoadingFunctionsFor.Count:N0} user-defined types so far.", nextLoggerOutput, (uint)classesWorthLoadingFunctionsFor.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            this.CancellationToken.ThrowIfCancellationRequested();
            LoadFunctionsForOneTypeAndAllItsBaseTypes(udt);
        }
    }

    private void LoadFunctionsForOneTypeAndAllItsBaseTypes(UserDefinedTypeSymbol udt)
    {
        udt.EnsureFunctionsLoaded(this.CancellationToken);
        if (udt.BaseTypes != null)
        {
            foreach (var baseType in udt.BaseTypes.AsSpan())
            {
                LoadFunctionsForOneTypeAndAllItsBaseTypes(baseType._baseTypeSymbol);
            }
        }
    }
}
