using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace SizeBench.GUI.Navigation;

[ExcludeFromCodeCoverage] // Just some constants, no need to test this.
public static class WellKnownPages
{
    // Single Binary Pages

    public static Uri AllBinarySectionsPageUri => new Uri(@"AllBinarySections", UriKind.Relative);
    public static Uri AllLibsPageUri => new Uri(@"AllLibs", UriKind.Relative);
    public static Uri AllCompilandsPageUri => new Uri(@"AllCompilands", UriKind.Relative);
    public static Uri AllSourceFilesPageUri => new Uri(@"AllSourceFiles", UriKind.Relative);
    public static Uri AllUserDefinedTypesPageUri => new Uri(@"AllUserDefinedTypes", UriKind.Relative);
    public static Uri AllDuplicateDataPageUri => new Uri(@"AllDuplicateData", UriKind.Relative);
    // We default to excluding COM types since most people don't want to deal with doing something fancy like interface forwarding to save on these.
    public static Uri AllWastefulVirtualsPageUri => new Uri(@"AllWastefulVirtuals#ExcludeCOMTypes", UriKind.Relative);
    public static Uri AllTemplateFoldabilityPageUri => new Uri(@"AllTemplateFoldability", UriKind.Relative);
    public static Uri TypeLayoutPageUri => new Uri(@"TypeLayout", UriKind.Relative);
    public static Uri AllAnnotationsPageUri => new Uri(@"AllAnnotations", UriKind.Relative);

    // Diff Pages
    public static Uri AllBinarySectionDiffsPageUri => new Uri(@"AllBinarySectionDiffs", UriKind.Relative);
    public static Uri AllLibDiffsPageUri => new Uri(@"AllLibDiffs", UriKind.Relative);
    public static Uri AllCompilandDiffsPageUri => new Uri(@"AllCompilandDiffs", UriKind.Relative);
    public static Uri AllDuplicateDataDiffsPageUri => new Uri(@"AllDuplicateDataDiffs", UriKind.Relative);
    public static Uri AllWastefulVirtualDiffsPageUri => new Uri(@"AllWastefulVirtualDiffs#ExcludeCOMTypes", UriKind.Relative);
    public static Uri AllTemplateFoldabilityDiffsPageUri => new Uri(@"AllTemplateFoldabilityDiffs", UriKind.Relative);
    public static Uri TypeLayoutDiffPageUri => new Uri(@"TypeLayoutDiff", UriKind.Relative);

    // Help Pages
    public static Uri HelpStartingPage => new Uri(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, @"Help\index.html"));
}
