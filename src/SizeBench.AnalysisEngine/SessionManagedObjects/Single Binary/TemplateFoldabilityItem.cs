using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Template Foldability: {TemplateName}")]
public sealed class TemplateFoldabilityItem
{
    public string TemplateName { get; }
    public uint TotalSize { get; }
    public IReadOnlyList<IFunctionCodeSymbol> Symbols { get; }
    public IReadOnlyList<IFunctionCodeSymbol> UniqueSymbols { get; } // Symbols that are unique based on their primary RVA, so we don't look at two things ICF'd together
    public uint CountOfUniqueSymbolsAfterFolding { get; }

    [DisplayFormat(DataFormatString = "{0:P1}")] // Format as percentage
    public float PercentageSimilarity { get; }

    public uint WastedSize => (uint)(this.TotalSize * this.PercentageSimilarity);

    internal TemplateFoldabilityItem(string templateName,
                                     IReadOnlyList<IFunctionCodeSymbol> symbols,
                                     IReadOnlyList<IFunctionCodeSymbol> uniqueSymbols,
                                     uint totalSize,
                                     float percentageSimilarity)
    {
        this.TemplateName = templateName;
        this.Symbols = symbols;
        this.UniqueSymbols = uniqueSymbols;
        this.TotalSize = totalSize;
        this.PercentageSimilarity = percentageSimilarity;
    }
}
