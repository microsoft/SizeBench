using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Compiland Diff: {Name}, SizeDiff={SizeDiff}")]
public sealed class CompilandDiff
{
    [Display(AutoGenerateField = false)]
    public Compiland? BeforeCompiland { get; }
    [Display(AutoGenerateField = false)]
    public Compiland? AfterCompiland { get; }

    [Display(AutoGenerateField = false)]
    public LibDiff LibDiff { get; }

    public string Name => this.BeforeCompiland?.Name ?? this.AfterCompiland!.Name;

    [Display(Order = 15)]
    public string ShortName => Path.GetFileName(this.Name);

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterCompiland?.Size ?? 0;
            long beforeSize = this.BeforeCompiland?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }
    public int VirtualSizeDiff
    {
        get
        {
            long afterSize = this.AfterCompiland?.VirtualSize ?? 0;
            long beforeSize = this.BeforeCompiland?.VirtualSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    internal CompilandDiff(Compiland? before, Compiland? after, LibDiff libDiff, List<BinarySectionDiff> sectionDiffs, DiffSessionDataCache cache)
    {
#if DEBUG
        // As with (non-diff) Compilands, "Import:<anything>" can be special and appear multiple times in a binary by name.
        // Thus the more complex check here than just the names.
        if (cache.CompilandDiffsConstructedEver.Any(cd =>
        {
            return (before != null && cd.BeforeCompiland != null && cd.BeforeCompiland.SymIndexId == before.SymIndexId) ||
                   (after != null && cd.AfterCompiland != null && cd.AfterCompiland.SymIndexId == after.SymIndexId);
        }) ||
            cache.CompilandDiffsConstructedEver.Any(cd => cd.Name == (before?.Name ?? after?.Name) &&
                                                          cd.LibDiff.Name == (before?.Lib.Name ?? after?.Lib.Name)))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.BeforeCompiland = before;
        this.AfterCompiland = after;
        this.LibDiff = libDiff;

        var cgDiffs = (from sectionDiff in sectionDiffs
                       select sectionDiff.COFFGroupDiffs).SelectMany(cgDiff => cgDiff).ToList();

        if (before != null)
        {
            foreach (var beforeSectionContribution in before.SectionContributions.Values)
            {
                var matchingAfterSectionContribution = after?.SectionContributions.Values.FirstOrDefault(lsc => lsc.BinarySection.Name == beforeSectionContribution.BinarySection.Name);

                var matchingSectionDiff = sectionDiffs.Where(sd => sd.Name == beforeSectionContribution.BinarySection.Name).First();

#if DEBUG
                SanityCheckSectionContribution(after, beforeSectionContribution, matchingAfterSectionContribution);
#endif

                var newSectionContributionDiff = new CompilandSectionContributionDiff($"{this.Name} contributions to {matchingSectionDiff.Name}", beforeSectionContribution, matchingAfterSectionContribution, matchingSectionDiff, this);
                this._sectionContributionDiffs.Add(matchingSectionDiff, newSectionContributionDiff);
                this._sectionContributionDiffsByName.Add(matchingSectionDiff.Name, newSectionContributionDiff);
            }

            foreach (var beforeCGContribution in before.COFFGroupContributions.Values)
            {
                var matchingAfterCGContribution = after?.COFFGroupContributions.Values.FirstOrDefault(lcgc => lcgc.COFFGroup.Name == beforeCGContribution.COFFGroup.Name);

                var matchingCOFFGroupDiff = cgDiffs.Where(sd => sd.Name == beforeCGContribution.COFFGroup.Name).First();

#if DEBUG
                SanityCheckCGContribution(after, beforeCGContribution, matchingAfterCGContribution);
#endif

                var newCGContributionDiff = new CompilandCOFFGroupContributionDiff($"{this.Name} contributions to {matchingCOFFGroupDiff.Name}", beforeCGContribution, matchingAfterCGContribution, matchingCOFFGroupDiff, this);
                this._coffGroupContributionDiffs.Add(matchingCOFFGroupDiff, newCGContributionDiff);
                this._coffGroupContributionDiffsByName.Add(matchingCOFFGroupDiff.Name, newCGContributionDiff);
            }
        }

        if (after != null)
        {
            // Now catch any SectionContribution that is only in the 'after' list
            foreach (var afterSectionContribution in after.SectionContributions.Values)
            {
                var matchingSectionDiff = sectionDiffs.Where(sd => sd.Name == afterSectionContribution.BinarySection.Name).First();

                if (this._sectionContributionDiffs.Any(scDiff => scDiff.Key == matchingSectionDiff))
                {
                    continue;
                }

                var matchingBeforeSectionContribution = before?.SectionContributions.Values.FirstOrDefault(lsc => lsc.BinarySection.Name == afterSectionContribution.BinarySection.Name);

#if DEBUG
                SanityCheckSectionContribution(after, afterSectionContribution, matchingBeforeSectionContribution);
#endif

                var newSectionContributionDiff = new CompilandSectionContributionDiff($"{this.Name} contributions to {matchingSectionDiff.Name}", matchingBeforeSectionContribution, afterSectionContribution, matchingSectionDiff, this);
                this._sectionContributionDiffs.Add(matchingSectionDiff, newSectionContributionDiff);
                this._sectionContributionDiffsByName.Add(matchingSectionDiff.Name, newSectionContributionDiff);
            }

            // Now catch any COFF Group Contribution that is only in the 'after' list
            foreach (var afterCGContribution in after.COFFGroupContributions.Values)
            {
                var matchingCOFFGroupDiff = cgDiffs.Where(sd => sd.Name == afterCGContribution.COFFGroup.Name).First();

                if (this._coffGroupContributionDiffs.Any(cgDiff => cgDiff.Key == matchingCOFFGroupDiff))
                {
                    continue;
                }

                var matchingBeforeCGContribution = before?.COFFGroupContributions.Values.FirstOrDefault(lcgc => lcgc.COFFGroup.Name == afterCGContribution.COFFGroup.Name);

#if DEBUG
                SanityCheckCGContribution(after, afterCGContribution, matchingBeforeCGContribution);
#endif

                var newCGContributionDiff = new CompilandCOFFGroupContributionDiff($"{this.Name} contributions to {matchingCOFFGroupDiff.Name}", matchingBeforeCGContribution, afterCGContribution, matchingCOFFGroupDiff, this);
                this._coffGroupContributionDiffs.Add(matchingCOFFGroupDiff, newCGContributionDiff);
                this._coffGroupContributionDiffsByName.Add(matchingCOFFGroupDiff.Name, newCGContributionDiff);
            }
        }

#if DEBUG
        SanityCheckNoByteLeftBehind();
#endif

        cache.RecordCompilandDiffConstructed(this);
    }

#if DEBUG
    // This sanity check should never throw/fail, so code coverage on it is pointless - if it throws, it's doing its job in the test pass.
    [ExcludeFromCodeCoverage]
    private static void SanityCheckCGContribution(Compiland? compiland, CompilandCOFFGroupContribution cgContribution, CompilandCOFFGroupContribution? matchingCGContribution)
    {
        if (compiland is null)
        {
            return;
        }

        if (matchingCGContribution != null && compiland.COFFGroupContributions.Values.Count(lcgc => lcgc.COFFGroup.Name == cgContribution.COFFGroup.Name) > 1)
        {
            throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
        }
    }

    // This sanity check should never throw/fail, so code coverage on it is pointless - if it throws, it's doing its job in the test pass.
    [ExcludeFromCodeCoverage]
    private static void SanityCheckSectionContribution(Compiland? compiland, CompilandSectionContribution sectionContribution, CompilandSectionContribution? matchingSectionContribution)
    {
        if (compiland is null)
        {
            return;
        }

        if (matchingSectionContribution != null && compiland.SectionContributions.Values.Count(lsc => lsc.BinarySection.Name == sectionContribution.BinarySection.Name) > 1)
        {
            throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
        }
    }

    // This code is going to be excluded from code coverage, since it runs in all the tests doing its job, and getting it to actually throw
    // requires introducing some ridiculous test cases that aren't worth maintaining.
    [ExcludeFromCodeCoverage]
    private void SanityCheckNoByteLeftBehind()
    {
        //Sanity check that we 'left no bytes behind' - that the usm of the contributions consumes all the bytes we know about
        long sectionContribsTotalSizeDiff = this._sectionContributionDiffs.Values.Sum(c => c.SizeDiff);
        long coffGroupContribsTotalSizeDiff = this._coffGroupContributionDiffs.Values.Sum(c => c.SizeDiff);
        if (sectionContribsTotalSizeDiff != this.SizeDiff)
        {
            throw new InvalidOperationException("Sanity check failed - section contribution diffs don't add up to total diffs");
        }
        if (coffGroupContribsTotalSizeDiff != this.SizeDiff)
        {
            throw new InvalidOperationException("Sanity check failed - COFF Group contribution diffs don't add up to total diffs");
        }
    }
#endif

    #region Section Contributions

    private readonly Dictionary<BinarySectionDiff, CompilandSectionContributionDiff> _sectionContributionDiffs = new Dictionary<BinarySectionDiff, CompilandSectionContributionDiff>();
    public IReadOnlyDictionary<BinarySectionDiff, CompilandSectionContributionDiff> SectionContributionDiffs => this._sectionContributionDiffs;

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandSectionContributionDiff> _sectionContributionDiffsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandSectionContributionDiff>();
    public IReadOnlyDictionary<string, CompilandSectionContributionDiff> SectionContributionDiffsByName => this._sectionContributionDiffsByName;

    #endregion

    #region COFF Group Contributions

    private readonly Dictionary<COFFGroupDiff, CompilandCOFFGroupContributionDiff> _coffGroupContributionDiffs = new Dictionary<COFFGroupDiff, CompilandCOFFGroupContributionDiff>();
    public IReadOnlyDictionary<COFFGroupDiff, CompilandCOFFGroupContributionDiff> COFFGroupContributionDiffs => this._coffGroupContributionDiffs;

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandCOFFGroupContributionDiff> _coffGroupContributionDiffsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandCOFFGroupContributionDiff>();
    public IReadOnlyDictionary<string, CompilandCOFFGroupContributionDiff> COFFGroupContributionDiffsByName => this._coffGroupContributionDiffsByName;

    #endregion
}
