using System.Diagnostics;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("{Name}, Size={Size}")]
public sealed class SourceFileSectionContribution : Contribution
{
    public BinarySection BinarySection { get; }
    public SourceFile SourceFile { get; }

    internal SourceFileSectionContribution(string name, BinarySection binarySection, SourceFile sourceFile)
        : base(name)
    {
        this.BinarySection = binarySection;
        this.SourceFile = sourceFile;
    }

    internal bool Contains(uint rva)
    {
        // This codepath is insanely hot, being hit hundreds of thousands of times when attributing PDATA and XDATA to source files,
        // so we try really hard to do minimal work.

        var rangesToSearch = this.RVARangesRegardlessOfFinalConstructionState;

        var rangeIndex = 0;
        var countOfRanges = rangesToSearch.Count;

        // First, we blaze past any ranges that end before this RVA, without also comapring their RVAStart (that's pointless)
        while (rangeIndex < countOfRanges && rangesToSearch[rangeIndex].RVAEnd < rva)
        {
            rangeIndex++;
        }

        for (; rangeIndex < countOfRanges; rangeIndex++)
        {
            var sfscRange = rangesToSearch[rangeIndex];
            // If this range starts after the rva we're interested in, no need to traverse the rest of the list, as they're sorted and
            // we'll never find one that starts earlier.
            if (sfscRange.RVAStart > rva)
            {
                return false;
            }

            if (sfscRange.Contains(rva))
            {
                return true;
            }
        }

        return false;
    }
}
