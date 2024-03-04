using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Models;

namespace SizeBench.GUI.Navigation;

public static class SingleBinaryModelToUriConverter
{
    public static Uri ModelToUri(object model)
    {
        if (model is Uri uri)
        {
            return uri;
        }
        else if (model is ISession)
        {
            return new Uri(@"SingleBinaryOverview", UriKind.Relative);
        }
        else if (model is IList<BinarySection>)
        {
            return WellKnownPages.AllBinarySectionsPageUri;
        }
        else if (model is BinarySection section)
        {
            return new Uri($@"BinarySection/{SizeBenchUrlEncode(section.Name)}", UriKind.Relative);
        }
        else if (model is IList<Library>)
        {
            return WellKnownPages.AllLibsPageUri;
        }
        else if (model is Library library)
        {
            return new Uri($@"Lib/{SizeBenchUrlEncode(library.Name)}", UriKind.Relative);
        }
        else if (model is COFFGroup group)
        {
            return new Uri($@"COFFGroup/{SizeBenchUrlEncode(group.Name)}", UriKind.Relative);
        }
        else if (model is Compiland)
        {
            var c = (Compiland)model;
            return new Uri($@"Compiland/{SizeBenchUrlEncode(c.Name)}?Lib={SizeBenchUrlEncode(c.Lib.Name)}", UriKind.Relative);
        }
        else if (model is SourceFile sourceFile)
        {
            return new Uri($@"SourceFile/{SizeBenchUrlEncode(sourceFile.Name)}", UriKind.Relative);
        }
        else if (model is LibSectionContribution libSectionContrib)
        {
            var libName = SizeBenchUrlEncode(libSectionContrib.Lib.Name);
            var sectionName = SizeBenchUrlEncode(libSectionContrib.BinarySection.Name);
            return new Uri($@"Contribution?BinarySection={sectionName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is LibCOFFGroupContribution libCOFFGroupContrib)
        {
            var libName = SizeBenchUrlEncode(libCOFFGroupContrib.Lib.Name);
            var coffGroupName = SizeBenchUrlEncode(libCOFFGroupContrib.COFFGroup.Name);
            return new Uri($@"Contribution?COFFGroup={coffGroupName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is CompilandSectionContribution compilandSectionContrib)
        {
            var c = compilandSectionContrib.Compiland;
            var compilandName = SizeBenchUrlEncode(c.Name);
            var libName = SizeBenchUrlEncode(c.Lib.Name);
            var sectionName = SizeBenchUrlEncode(compilandSectionContrib.BinarySection.Name);
            return new Uri($@"Contribution?BinarySection={sectionName}&Compiland={compilandName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is CompilandCOFFGroupContribution compilandCOFFGroupContrib)
        {
            var c = compilandCOFFGroupContrib.Compiland;
            var compilandName = SizeBenchUrlEncode(c.Name);
            var libName = SizeBenchUrlEncode(c.Lib.Name);
            var coffGroupName = SizeBenchUrlEncode(compilandCOFFGroupContrib.COFFGroup.Name);
            return new Uri($@"Contribution?COFFGroup={coffGroupName}&Compiland={compilandName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is SourceFileSectionContribution sourceFileSectionContrib)
        {
            var sourceFileName = SizeBenchUrlEncode(sourceFileSectionContrib.SourceFile.Name);
            var sectionName = SizeBenchUrlEncode(sourceFileSectionContrib.BinarySection.Name);
            return new Uri($@"Contribution?BinarySection={sectionName}&SourceFile={sourceFileName}", UriKind.Relative);
        }
        else if (model is SourceFileCOFFGroupContribution sourceFileCOFFGroupContrib)
        {
            var sourceFileName = SizeBenchUrlEncode(sourceFileCOFFGroupContrib.SourceFile.Name);
            var coffGroupName = SizeBenchUrlEncode(sourceFileCOFFGroupContrib.COFFGroup.Name);
            return new Uri($@"Contribution?COFFGroup={coffGroupName}&SourceFile={sourceFileName}", UriKind.Relative);
        }
        else if (model is SourceFileCompilandContribution sourceFileCompilandContrib)
        {
            var sourceFileName = SizeBenchUrlEncode(sourceFileCompilandContrib.SourceFile.Name);
            var compilandName = SizeBenchUrlEncode(sourceFileCompilandContrib.Compiland.Name);
            var libName = SizeBenchUrlEncode(sourceFileCompilandContrib.Compiland.Lib.Name);
            return new Uri($@"Contribution?Compiland={compilandName}&SourceFile={sourceFileName}&Lib={libName}", UriKind.Relative);
        }
        else if (model is Contribution)
        {
            throw new InvalidOperationException("Don't know how to navigate to a generic Contribution - all Contributions should be further specified by a more derived type!");
        }
        else if (model is DuplicateDataItem ddi)
        {
            return new Uri($@"DuplicateData?DuplicateRVA={ddi.Symbol.RVA}", UriKind.Relative);
        }
        else if (model is WastefulVirtualItem wvi)
        {
            var wastefulVirtualTypeName = SizeBenchUrlEncode(wvi.UserDefinedType.Name);
            return new Uri($@"WastefulVirtual?TypeName={wastefulVirtualTypeName}", UriKind.Relative);
        }
        // COMDAT-folded symbols can be of many types, so this should go above any checks for a specific type of symbol as we want to show the simple COMDAT folding UI
        // that explains what this is and lets you link from there to the canonical/primary symbol, which may hit different pages below this like the one for functions
        // or for blocks, etc.
        else if (model is ISymbol comdatFoldedSymbol && comdatFoldedSymbol.IsCOMDATFolded)
        {
            return new Uri($@"Symbols/COMDATFoldedSymbol?RVA={comdatFoldedSymbol.RVA}&Name={SizeBenchUrlEncode(comdatFoldedSymbol.Name)}", UriKind.Relative);
        }
        // It's imporant that we check for IFunctionCodeSymbol before CodeBlockSymbol - because SimpleFunctionCodeSymbols are both IFunctionCodeSymbol and CodeBlockSymbol
        // and we'd rather show them as a function in the UI.  This way we only show BlockSymbolPage for either primary or separated blocks for complex functions.
        else if (model is IFunctionCodeSymbol function)
        {
            var nameParameter = String.Empty;

            // If the primary block's RVA is 0, we have found something COMDAT folded or dead-code stripped with /OPT:REF or something like that, so we'll pass along the name
            // to allow the UI to show a more helpful message.  But otherwise that's duplicative and makes the URLs look uglier so we only append it when needed for aesthetics.
            if (function.PrimaryBlock.RVA == 0)
            {
                nameParameter = $"&Name={SizeBenchUrlEncode(function.FullName)}";
            }

            return new Uri($@"Symbols/FunctionSymbol?FunctionRVA={function.PrimaryBlock.RVA}{nameParameter}", UriKind.Relative);
        }
        else if (model is CodeBlockSymbol block)
        {
            return new Uri($@"Symbols/BlockSymbol?RVA={block.RVA}", UriKind.Relative);
        }
        else if (model is TemplateFoldabilityItem tfi)
        {
            return new Uri($@"TemplateFoldabilityItem?TemplateName={SizeBenchUrlEncode(tfi.TemplateName)}", UriKind.Relative);
        }
        else if (model is UserDefinedTypeSymbol udt)
        {
            return new Uri($@"Symbols/UserDefinedTypeSymbol?Name={SizeBenchUrlEncode(udt.Name)}", UriKind.Relative);
        }
        else if (model is TemplatedUserDefinedTypeSymbol templatedUDT)
        {
            return new Uri($@"Symbols/TemplatedUserDefinedTypeSymbol?TemplateName={SizeBenchUrlEncode(templatedUDT.TemplateName)}", UriKind.Relative);
        }
        else if (model is InlineSiteGroup inlineSiteGroup)
        {
            return new Uri($@"Symbols/InlineSiteGroup?Name={SizeBenchUrlEncode(inlineSiteGroup.InlinedFunctionName)}", UriKind.Relative);
        }
        // It's important that ISymbol is very late in this list of "if/else if/else if/..." so that
        // any special-cases for specific types of symbols (like functions and code blocks above) will hit first, and these
        // rather mediocre pages only get hit if we have some other kind of Symbol.
        else if (model is ISymbol isym)
        {
            if (isym.RVA == 0)
            {
                return new Uri($@"Symbols/Symbol?RVA={isym.RVA}&Name={SizeBenchUrlEncode(isym.Name)}", UriKind.Relative);
            }
            else
            {
                return new Uri($@"Symbols/Symbol?RVA={isym.RVA}", UriKind.Relative);
            }
        }
        else
        {
            return new Uri($"Error/{Uri.EscapeDataString(model?.GetType().FullName ?? "null")}", UriKind.Relative);
        }
    }

    private static string SizeBenchUrlEncode(string input) =>
        // The '$' character occurs pretty regularly in this world (.text$mn, for example), and encoding it looks ugly so this one
        // character is special-cased back to '$' after Uri.EscapeDataString.  $ is reserved, but has no use is URIs yet, so this
        // seems to not cause any ambiguity.
        Uri.EscapeDataString(input).Replace("%24", "$", StringComparison.Ordinal);
}
