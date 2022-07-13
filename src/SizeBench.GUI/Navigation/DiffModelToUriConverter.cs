using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.GUI.Navigation;

public static class DiffModelToUriConverter
{
    public static Uri ModelToUri(object model, IDiffSession diffSession)
    {
        ArgumentNullException.ThrowIfNull(diffSession);

        // If we have a single binary object, then we need to convert it to the appropriate diff object first so we can ensure we get the right
        // name.  For example, heuristically c:\a\a1.obj and c:\b\a1.obj are allowed to match in a CompilandDiff - but we can't look up the
        // CompilandDiff by either name, we pick one to win (the 'before' name currently).  So we go back up the chain to the owning diff object
        // first, then a second bunch of conditions below will find the right Uri for the diff object.
        ConvertSingleBinaryObjectToParentDiffObjectIfPossible(ref model, diffSession);


        if (model is Uri uri)
        {
            return uri;
        }
        else if (model is IList<BinarySectionDiff>)
        {
            return WellKnownPages.AllBinarySectionDiffsPageUri;
        }
        else if (model is BinarySectionDiff sectionDiff)
        {
            return new Uri($@"BinarySectionDiff/{SizeBenchUrlEncode(sectionDiff.Name)}", UriKind.Relative);
        }
        else if (model is IList<LibDiff>)
        {
            return WellKnownPages.AllLibDiffsPageUri;
        }
        else if (model is LibDiff libDiff)
        {
            return new Uri($@"LibDiff/{SizeBenchUrlEncode(libDiff.Name)}", UriKind.Relative);
        }
        else if (model is COFFGroupDiff coffGroupDiff)
        {
            return new Uri($@"COFFGroupDiff/{SizeBenchUrlEncode(coffGroupDiff.Name)}", UriKind.Relative);
        }
        else if (model is IList<CompilandDiff>)
        {
            return WellKnownPages.AllCompilandDiffsPageUri;
        }
        else if (model is CompilandDiff compilandDiff)
        {
            return new Uri($@"CompilandDiff/{SizeBenchUrlEncode(compilandDiff.Name)}?Lib={SizeBenchUrlEncode(compilandDiff.LibDiff.Name)}", UriKind.Relative);
        }
        else if (model is LibSectionContributionDiff libSectionContribDiff)
        {
            var libName = SizeBenchUrlEncode(libSectionContribDiff.LibDiff.Name);
            var sectionName = SizeBenchUrlEncode(libSectionContribDiff.BinarySectionDiff.Name);
            return new Uri($@"ContributionDiff?BinarySection={sectionName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is LibCOFFGroupContributionDiff libCOFFGroupContribDiff)
        {
            var libName = SizeBenchUrlEncode(libCOFFGroupContribDiff.LibDiff.Name);
            var coffGroupName = SizeBenchUrlEncode(libCOFFGroupContribDiff.COFFGroupDiff.Name);
            return new Uri($@"ContributionDiff?COFFGroup={coffGroupName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is CompilandSectionContributionDiff compilandSectionContribDiff)
        {
            var cd = compilandSectionContribDiff.CompilandDiff;
            var compilandName = SizeBenchUrlEncode(cd.Name);
            var libName = SizeBenchUrlEncode(cd.LibDiff.Name);
            var sectionName = SizeBenchUrlEncode(compilandSectionContribDiff.BinarySectionDiff.Name);
            return new Uri($@"ContributionDiff?BinarySection={sectionName}&Compiland={compilandName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is CompilandCOFFGroupContributionDiff compilandCOFFGroupContribDiff)
        {
            var cd = compilandCOFFGroupContribDiff.CompilandDiff;
            var compilandName = SizeBenchUrlEncode(cd.Name);
            var libName = SizeBenchUrlEncode(cd.LibDiff.Name);
            var coffGroupName = SizeBenchUrlEncode(compilandCOFFGroupContribDiff.COFFGroupDiff.Name);
            return new Uri($@"ContributionDiff?COFFGroup={coffGroupName}&Compiland={compilandName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is ContributionDiff)
        {
            throw new InvalidOperationException("Don't know how to navigate to a generic ContributionDiff - all ContributionDiffs should be further specified by a more derived type!");
        }
        else if (model is DuplicateDataItemDiff ddid)
        {
            if (ddid.BeforeDuplicate != null)
            {
                return new Uri($@"DuplicateDataDiff?BeforeDuplicateRVA={ddid.BeforeDuplicate.Symbol.RVA}", UriKind.Relative);
            }
            else
            {
                return new Uri($@"DuplicateDataDiff?AfterDuplicateRVA={ddid.AfterDuplicate!.Symbol.RVA}", UriKind.Relative);
            }
        }
        else if (model is WastefulVirtualItemDiff wvid)
        {
            var wastefulVirtualTypeName = SizeBenchUrlEncode(wvid.TypeName);
            return new Uri($@"WastefulVirtualDiff?TypeName={wastefulVirtualTypeName}", UriKind.Relative);
        }
        else if (model is TemplateFoldabilityItemDiff tfid)
        {
            var templateName = SizeBenchUrlEncode(tfid.TemplateName);
            return new Uri($@"TemplateFoldabilityDiff?TemplateName={templateName}", UriKind.Relative);
        }
        // It's imporant that we check for IFunctionCodeSymbol before CodeBlockSymbol - because SimpleFunctionCodeSymbols are both IFunctionCodeSymbol and CodeBlockSymbol
        // and we'd rather show them as a function in the UI.  This way we only show BlockSymbolPage for either primary or separated blocks for complex functions.
        else if (model is SymbolDiff codeBlockSymbolDiff &&
                 (codeBlockSymbolDiff.BeforeSymbol is null || codeBlockSymbolDiff.BeforeSymbol is CodeBlockSymbol) &&
                 (codeBlockSymbolDiff.AfterSymbol is null || codeBlockSymbolDiff.AfterSymbol is CodeBlockSymbol))
        {
            if (codeBlockSymbolDiff.BeforeSymbol != null && codeBlockSymbolDiff.AfterSymbol != null)
            {
                return new Uri($@"Symbols/CodeBlockSymbolDiff?BeforeRVA={codeBlockSymbolDiff.BeforeSymbol.RVA}&AfterRVA={codeBlockSymbolDiff.AfterSymbol.RVA}", UriKind.Relative);
            }
            else if (codeBlockSymbolDiff.BeforeSymbol is null && codeBlockSymbolDiff.AfterSymbol != null)
            {
                return new Uri($@"Symbols/CodeBlockSymbolDiff?AfterRVA={codeBlockSymbolDiff.AfterSymbol.RVA}", UriKind.Relative);
            }
            else
            {
                return new Uri($@"Symbols/CodeBlockSymbolDiff?BeforeRVA={codeBlockSymbolDiff.BeforeSymbol!.RVA}", UriKind.Relative);
            }
        }
        else if (model is FunctionCodeSymbolDiff functionCodeSymbolDiff)
        {
            if (functionCodeSymbolDiff.BeforeSymbol != null && functionCodeSymbolDiff.AfterSymbol != null)
            {
                return new Uri($@"Symbols/FunctionCodeSymbolDiff?BeforeRVA={functionCodeSymbolDiff.BeforeSymbol.PrimaryBlock.RVA}&AfterRVA={functionCodeSymbolDiff.AfterSymbol.PrimaryBlock.RVA}", UriKind.Relative);
            }
            else if (functionCodeSymbolDiff.BeforeSymbol is null && functionCodeSymbolDiff.AfterSymbol != null)
            {
                return new Uri($@"Symbols/FunctionCodeSymbolDiff?AfterRVA={functionCodeSymbolDiff.AfterSymbol.PrimaryBlock.RVA}", UriKind.Relative);
            }
            else
            {
                return new Uri($@"Symbols/FunctionCodeSymbolDiff?BeforeRVA={functionCodeSymbolDiff.BeforeSymbol!.PrimaryBlock.RVA}", UriKind.Relative);
            }
        }
        // It's important that SymbolDiff be very late in this list of "if/else if/else if/..." so that
        // any special-cases for specific types of symbols (like code blocks above) will hit first, and these
        // rather mediocre pages only get hit if we have some other kind of SymbolDiff.
        else if (model is SymbolDiff symbolDiff)
        {
            if (symbolDiff.BeforeSymbol != null && symbolDiff.AfterSymbol != null)
            {
                return new Uri($@"Symbols/SymbolDiff?BeforeRVA={symbolDiff.BeforeSymbol.RVA}&AfterRVA={symbolDiff.AfterSymbol.RVA}", UriKind.Relative);
            }
            else if (symbolDiff.BeforeSymbol is null && symbolDiff.AfterSymbol != null)
            {
                return new Uri($@"Symbols/SymbolDiff?AfterRVA={symbolDiff.AfterSymbol.RVA}", UriKind.Relative);
            }
            else
            {
                return new Uri($@"Symbols/SymbolDiff?BeforeRVA={symbolDiff.BeforeSymbol!.RVA}", UriKind.Relative);
            }
        }
        else
        {
            return new Uri($"Error/{Uri.EscapeDataString(model?.GetType().FullName ?? "null")}", UriKind.Relative);
        }
    }

    private static void ConvertSingleBinaryObjectToParentDiffObjectIfPossible(ref object model, IDiffSession diffSession)
    {
        if (model is BinarySection section)
        {
            model = diffSession.GetBinarySectionDiffFromBinarySection(section) ?? throw new InvalidOperationException();
        }
        else if (model is COFFGroup coffGroup)
        {
            model = diffSession.GetCOFFGroupDiffFromCOFFGroup(coffGroup) ?? throw new InvalidOperationException();
        }
        else if (model is Library library)
        {
            model = diffSession.GetLibraryDiffFromLibrary(library) ?? throw new InvalidOperationException();
        }
        else if (model is Compiland compiland)
        {
            model = diffSession.GetCompilandDiffFromCompiland(compiland) ?? throw new InvalidOperationException();
        }
        else if (model is LibSectionContribution libSectionContrib)
        {
            var libDiff = diffSession.GetLibraryDiffFromLibrary(libSectionContrib.Lib) ?? throw new InvalidOperationException();
            var sectionDiff = diffSession.GetBinarySectionDiffFromBinarySection(libSectionContrib.BinarySection) ?? throw new InvalidOperationException();
            model = libDiff.SectionContributionDiffs[sectionDiff];
        }
        else if (model is LibCOFFGroupContribution libCOFFGroupContrib)
        {
            var libDiff = diffSession.GetLibraryDiffFromLibrary(libCOFFGroupContrib.Lib) ?? throw new InvalidOperationException();
            var coffGroupDiff = diffSession.GetCOFFGroupDiffFromCOFFGroup(libCOFFGroupContrib.COFFGroup) ?? throw new InvalidOperationException();
            model = libDiff.COFFGroupContributionDiffs[coffGroupDiff];
        }
        else if (model is CompilandSectionContribution compilandSectionContrib)
        {
            var compilandDiff = diffSession.GetCompilandDiffFromCompiland(compilandSectionContrib.Compiland) ?? throw new InvalidOperationException();
            var sectionDiff = diffSession.GetBinarySectionDiffFromBinarySection(compilandSectionContrib.BinarySection) ?? throw new InvalidOperationException();
            model = compilandDiff.SectionContributionDiffs[sectionDiff];
        }
        else if (model is CompilandCOFFGroupContribution compilandCOFFGroupContrib)
        {
            var compilandDiff = diffSession.GetCompilandDiffFromCompiland(compilandCOFFGroupContrib.Compiland) ?? throw new InvalidOperationException();
            var coffGroupDiff = diffSession.GetCOFFGroupDiffFromCOFFGroup(compilandCOFFGroupContrib.COFFGroup) ?? throw new InvalidOperationException();
            model = compilandDiff.COFFGroupContributionDiffs[coffGroupDiff];
        }
        else if (model is Contribution)
        {
            throw new InvalidOperationException("Don't know how to navigate to a generic Contribution - all Contributions should be further specified by a more derived type!");
        }
        else if (model is DuplicateDataItem ddi)
        {
            model = diffSession.GetDuplicateDataItemDiffFromDuplicateDataItem(ddi) ?? throw new InvalidOperationException();
        }
        else if (model is WastefulVirtualItem wvi)
        {
            model = diffSession.GetWastefulVirtualItemDiffFromWastefulVirtualItem(wvi) ?? throw new InvalidOperationException();
        }
        else if (model is TemplateFoldabilityItem tfi)
        {
            model = diffSession.GetTemplateFoldabilityItemDiffFromTemplateFoldabilityItem(tfi) ?? throw new InvalidOperationException();
        }
        else if (model is ISymbol sym)
        {
            model = diffSession.GetSymbolDiffFromSymbol(sym) ?? throw new InvalidOperationException();
        }
    }

    private static string SizeBenchUrlEncode(string input) =>
        // The '$' character occurs pretty regularly in this world (.text$mn, for example), and encoding it looks ugly so this one
        // character is special-cased back to '$' after Uri.EscapeDataString.  $ is reserved, but has no use is URIs yet, so this
        // seems to not cause any ambiguity.
        Uri.EscapeDataString(input).Replace("%24", "$", StringComparison.Ordinal);
}
