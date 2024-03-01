using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

internal static class SymbolSourcesSupportedCommonTests
{
    private static readonly Dictionary<SymbolSourcesSupported, (Type[] types, string[] specialAllowedNamePrefixes)> SymbolSourcesThatShouldOnlyAppearWhenSymbolSourcesSupportedFlagIsSet = new()
    {
        [SymbolSourcesSupported.PDATA] = ([typeof(PDataSymbol)], []),
        [SymbolSourcesSupported.XDATA] = ([typeof(XDataSymbol)], []),
        [SymbolSourcesSupported.RSRC] = ([typeof(RsrcSymbolBase)], []),
        [SymbolSourcesSupported.OtherPESymbols] = ([typeof(ImportSymbolBase), typeof(LoadConfigTableSymbol), typeof(PEDirectorySymbol)],
                                                   ["[PE Directory] Debug",
                                                    "[PE Directory] [Debug Directory]"]),
        [SymbolSourcesSupported.DataSymbols] = ([typeof(StaticDataSymbol), typeof(StringSymbol)], []),
        [SymbolSourcesSupported.Code] = ([typeof(CodeBlockSymbol), typeof(IFunctionCodeSymbol), typeof(InlineSiteSymbol), typeof(ThunkSymbol)], []),
    };

    internal static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests { get; } =
        GetAllEnumCombinations<SymbolSourcesSupported>().Select(x => new object[] { x }).ToArray();

    // Use this for enormous binaries that take too long to run every test.  If we run all 64 combinations of flags today,
    // we time out in the ADO pipelines because they're limited to 1 hour.  We either need to use agents that can run longer
    // or partition the tests into multiple Jobs in the YAML to allow these to complete in time.  For now we'll test only
    // each individual type of symbol, not all combinations of flags.
    internal static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests_Slimmed { get; } =
        Enum.GetValuesAsUnderlyingType<SymbolSourcesSupported>().Cast<SymbolSourcesSupported>().Select(x => new object[] { x }).ToArray();

    public static List<T> GetAllEnumCombinations<T>() where T : struct
    {
        if (!typeof(T).IsEnum)
        {
            throw new ArgumentException("T must be an Enum type");
        }

        var values = Enum.GetValues(typeof(T)).Cast<uint>().ToArray();

        if (typeof(T).GetCustomAttributes(typeof(FlagsAttribute), false).Length == 0)
        {
            // If the enum doesn't have the [Flags] attribute, return all values
            return values.Cast<T>().ToList();
        }

        var valuesInverted = values.Select(v => ~v).ToArray();
        var max = values.Aggregate(0u, (current, value) => current | value);

        var result = new List<T>();

        for (var i = 0u; i <= max; i++)
        {
            var unaccountedBits = i;

            for (var j = 0; j < valuesInverted.Length; j++)
            {
                unaccountedBits &= valuesInverted[j];

                if (unaccountedBits == 0)
                {
                    result.Add((T)(object)i);
                    break;
                }
            }
        }

        return result;
    }

    internal static async Task VerifyNoUnexpectedSymbolTypesCanBeMaterialized(string binaryPath, string pdbPath, SymbolSourcesSupported symbolSourcesSupported, CancellationToken token)
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(binaryPath, pdbPath, new SessionOptions() { SymbolSourcesSupported = symbolSourcesSupported }, logger);

        // Try getting every symbol in every section
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(token);

        foreach (var section in sections)
        {
            var symbols = await session.EnumerateSymbolsInBinarySection(section, token);
            foreach (var symbol in symbols)
            {
                var typeOfThisSymbol = symbol.GetType();
                foreach ((var symbolSourceFlag, (var symbolTypes, var specialAllowedNamePrefixes)) in SymbolSourcesThatShouldOnlyAppearWhenSymbolSourcesSupportedFlagIsSet)
                {
                    foreach (var typeForFlag in symbolTypes)
                    {
                        if (!symbolSourcesSupported.HasFlag(symbolSourceFlag) && typeOfThisSymbol.IsAssignableTo(typeForFlag))
                        {
                            // We allow a few special names through even if they're of unexpected types, because they're extremely valuable
                            // (like the debug directory for verifying the PDB and binary match)
                            var isSpeciallyAllowedName = false;
                            foreach (var specialAllowedPrefix in specialAllowedNamePrefixes)
                            {
                                if (symbol.Name.StartsWith(specialAllowedPrefix, StringComparison.Ordinal))
                                {
                                    isSpeciallyAllowedName = true;
                                    break;
                                }
                            }

                            if (!isSpeciallyAllowedName)
                            {
                                Assert.Fail($"Unexpected symbol type materialized - found a {symbol.GetType().Name} with SymbolSourcesSupported={symbolSourcesSupported} (symbol name = '{symbol.Name}')!");
                            }
                        }
                    }
                }
            }
        }

        // We just check that these don't throw or hit asserts, no need to look deeply at their outputs
        await session.EnumerateDuplicateDataItems(token);
        await session.EnumerateWastefulVirtuals(token);
        await session.EnumerateAllUserDefinedTypes(token);
        await session.LoadAllTypeLayouts(token);
        await session.EnumerateTemplateFoldabilityItems(token);
        await session.EnumerateAnnotations(token);
        await session.EnumerateAllInlineSites(token);
    }
}
