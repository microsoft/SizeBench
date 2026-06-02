using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
#if DEBUG
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Lib Diff Name={Name}, SizeDiff={SizeDiff}")]
public sealed class LibDiff
{
    [Display(AutoGenerateField = false)]
    public Library? BeforeLib { get; }
    [Display(AutoGenerateField = false)]
    public Library? AfterLib { get; }

    internal LibDiff(Library? beforeLib, Library? afterLib, List<BinarySectionDiff> sectionDiffs, DiffSessionDataCache cache)
    {
        if (beforeLib is null && afterLib is null)
        {
            throw new ArgumentException("Both before and after are null, this is not valid - just don't construct a LibDiff in this case.");
        }

        this.BeforeLib = beforeLib;
        this.AfterLib = afterLib;

        var cgDiffs = (from sectionDiff in sectionDiffs
                       select sectionDiff.COFFGroupDiffs).SelectMany(cgDiff => cgDiff).ToList();

        // This is the list of 'after' compilands still worth looking at - once we match once we'll remove it from the list to
        // reduce processing time by not looking at the same thing over and over, so the copy here is probably worth it.
        var afterCompilandsToProcess = new List<Compiland>(capacity: afterLib?.Compilands.Values.Count() ?? 1);
        if (afterLib?.Compilands.Values != null)
        {
            afterCompilandsToProcess.AddRange(afterLib.Compilands.Values);
        }

        if (beforeLib != null)
        {
            foreach (var beforeCompiland in beforeLib.Compilands.Values)
            {
                // Try doing an explicit match on the full name first
                var matchingAfterCompiland = afterCompilandsToProcess.FirstOrDefault(c => c.Name == beforeCompiland.Name);

                // If we can't find one that way, we'll use IsVeryLikelyTheSameAs - it's heuristic and can sometimes be wrong, but it's really hard to be right
                // all the time since people compile with different "enlistment roots" so exact matches fail too often.
                matchingAfterCompiland ??= afterCompilandsToProcess.FirstOrDefault(beforeCompiland.IsVeryLikelyTheSameAs);

                var newCompilandDiff = new CompilandDiff(beforeCompiland, matchingAfterCompiland, this, sectionDiffs, cache);
                this._compilandDiffs.Add(beforeCompiland.Name, newCompilandDiff);

                if (matchingAfterCompiland != null)
                {
                    afterCompilandsToProcess.Remove(matchingAfterCompiland);
                }
            }

            foreach (var beforeSectionContribution in beforeLib.SectionContributions.Values)
            {
                var matchingAfterSectionContribution = afterLib?.SectionContributions.Values.FirstOrDefault(lsc => lsc.BinarySection.Name == beforeSectionContribution.BinarySection.Name);

                var matchingSectionDiff = sectionDiffs.Where(sd => sd.Name == beforeSectionContribution.BinarySection.Name).First();

#if DEBUG
                SanityCheckSectionContribution(afterLib, beforeSectionContribution, matchingAfterSectionContribution);
#endif

                var newSectionContributionDiff = new LibSectionContributionDiff($"{this.Name} contributions to {matchingSectionDiff.Name}", beforeSectionContribution, matchingAfterSectionContribution, matchingSectionDiff, this);
                this._sectionContributionDiffs.Add(matchingSectionDiff, newSectionContributionDiff);
                this._sectionContributionDiffsByName.Add(matchingSectionDiff.Name, newSectionContributionDiff);
            }

            foreach (var beforeCGContribution in beforeLib.COFFGroupContributions.Values)
            {
                var matchingAfterCGContribution = afterLib?.COFFGroupContributions.Values.FirstOrDefault(lcgc => lcgc.COFFGroup.Name == beforeCGContribution.COFFGroup.Name);

                var matchingCOFFGroupDiff = cgDiffs.Where(sd => sd.Name == beforeCGContribution.COFFGroup.Name).First();

#if DEBUG
                SanityCheckCGContribution(afterLib, beforeCGContribution, matchingAfterCGContribution);
#endif

                var newCGContributionDiff = new LibCOFFGroupContributionDiff($"{this.Name} contributions to {matchingCOFFGroupDiff.Name}", beforeCGContribution, matchingAfterCGContribution, matchingCOFFGroupDiff, this);
                this._coffGroupContributionDiffs.Add(matchingCOFFGroupDiff, newCGContributionDiff);
                this._coffGroupContributionDiffsByName.Add(matchingCOFFGroupDiff.Name, newCGContributionDiff);
            }
        }

        if (afterLib != null)
        {
            // Now catch any Compilands that are only in the 'after' list
            foreach (var afterCompiland in afterCompilandsToProcess)
            {
                // This Compiland is only in the 'after' list - we know because we already tried the 'before' list above.
                var newCompilandDiff = new CompilandDiff(null, afterCompiland, this, sectionDiffs, cache);
                this._compilandDiffs.Add(afterCompiland.Name, newCompilandDiff);
            }

            // Now catch any SectionContribution that is only in the 'after' list
            foreach (var afterSectionContribution in afterLib.SectionContributions.Values)
            {
                var matchingSectionDiff = sectionDiffs.First(sd => sd.Name == afterSectionContribution.BinarySection.Name);

                if (this._sectionContributionDiffs.Any(scDiff => scDiff.Key == matchingSectionDiff))
                {
                    continue;
                }

                var matchingBeforeSectionContribution = beforeLib?.SectionContributions.Values.FirstOrDefault(lsc => lsc.BinarySection.Name == afterSectionContribution.BinarySection.Name);

#if DEBUG
                SanityCheckSectionContribution(beforeLib, afterSectionContribution, matchingBeforeSectionContribution);
#endif

                var newSectionContributionDiff = new LibSectionContributionDiff($"{this.Name} contributions to {matchingSectionDiff.Name}", matchingBeforeSectionContribution, afterSectionContribution, matchingSectionDiff, this);
                this._sectionContributionDiffs.Add(matchingSectionDiff, newSectionContributionDiff);
                this._sectionContributionDiffsByName.Add(matchingSectionDiff.Name, newSectionContributionDiff);
            }

            // Now catch any COFF Group Contribution that is only in the 'after' list
            foreach (var afterCGContribution in afterLib.COFFGroupContributions.Values)
            {
                var matchingCOFFGroupDiff = cgDiffs.Where(sd => sd.Name == afterCGContribution.COFFGroup.Name).First();

                if (this._coffGroupContributionDiffs.Any(cgDiff => cgDiff.Key == matchingCOFFGroupDiff))
                {
                    continue;
                }

                var matchingBeforeCGContribution = beforeLib?.COFFGroupContributions.Values.FirstOrDefault(lcgc => lcgc.COFFGroup.Name == afterCGContribution.COFFGroup.Name);

#if DEBUG
                SanityCheckCGContribution(beforeLib, afterCGContribution, matchingBeforeCGContribution);
#endif

                var newCGContributionDiff = new LibCOFFGroupContributionDiff($"{this.Name} contributions to {matchingCOFFGroupDiff.Name}", matchingBeforeCGContribution, afterCGContribution, matchingCOFFGroupDiff, this);
                this._coffGroupContributionDiffs.Add(matchingCOFFGroupDiff, newCGContributionDiff);
                this._coffGroupContributionDiffsByName.Add(matchingCOFFGroupDiff.Name, newCGContributionDiff);
            }
        }

#if DEBUG
        SanityCheckNoByteLeftBehind();
#endif
    }

#if DEBUG
    // This sanity check should never throw/fail, so code coverage on it is pointless - if it throws, it's doing its job in the test pass.
    [ExcludeFromCodeCoverage]
    private static void SanityCheckCGContribution(Library? lib, LibCOFFGroupContribution coffGroupContribution, LibCOFFGroupContribution? matchingCGContribution)
    {
        if (lib is null)
        {
            return;
        }

        if (matchingCGContribution != null && lib.COFFGroupContributions.Values.Count(lcgc => lcgc.COFFGroup.Name == coffGroupContribution.COFFGroup.Name) > 1)
        {
            throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
        }
    }

    // This sanity check should never throw/fail, so code coverage on it is pointless - if it throws, it's doing its job in the test pass.
    [ExcludeFromCodeCoverage]
    private static void SanityCheckSectionContribution(Library? lib, LibSectionContribution sectionContribution, LibSectionContribution? matchingSectionContribution)
    {
        if (lib is null)
        {
            return;
        }

        if (matchingSectionContribution != null && lib.SectionContributions.Values.Count(lsc => lsc.BinarySection.Name == sectionContribution.BinarySection.Name) > 1)
        {
            throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
        }
    }
#endif

#if DEBUG
    // This code is going to be excluded from code coverage, since it runs in all the tests doing its job, and getting it to actually throw
    // requires introducing some ridiculous test cases that aren't worth maintaining.
    [ExcludeFromCodeCoverage]
    private void SanityCheckNoByteLeftBehind()
    {
        if (this.SizeDiff != this._compilandDiffs.Sum(cd => cd.Value.SizeDiff))
        {
            throw new InvalidOperationException("The compilands don't account for all the space changes in the lib - that should be impossible");
        }

        if (this.SizeDiff != this._sectionContributionDiffs.Sum(scd => scd.Value.SizeDiff))
        {
            throw new InvalidOperationException("The section contribution diffs don't account for all the space changes in the lib - that should be impossible");
        }

        if (this.SizeDiff != this._coffGroupContributionDiffs.Sum(cgd => cgd.Value.SizeDiff))
        {
            throw new InvalidOperationException("The COFF Group contribution diffs don't account for all the space changes in the lib - that should be impossible");
        }
    }
#endif

    public string Name => this.BeforeLib?.Name ?? this.AfterLib!.Name;

    [Display(Order = 15)]
    public string ShortName => Path.GetFileNameWithoutExtension(this.Name);

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterLib?.Size ?? 0;
            long beforeSize = this.BeforeLib?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public int VirtualSizeDiff
    {
        get
        {
            long afterSize = this.AfterLib?.VirtualSize ?? 0;
            long beforeSize = this.BeforeLib?.VirtualSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    #region Compilands
    private readonly Dictionary<string, CompilandDiff> _compilandDiffs = new Dictionary<string, CompilandDiff>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, CompilandDiff> CompilandDiffs => this._compilandDiffs;

    #endregion

    #region Section Contributions

    private readonly Dictionary<BinarySectionDiff, LibSectionContributionDiff> _sectionContributionDiffs = new Dictionary<BinarySectionDiff, LibSectionContributionDiff>();
    public IReadOnlyDictionary<BinarySectionDiff, LibSectionContributionDiff> SectionContributionDiffs => this._sectionContributionDiffs;

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<LibSectionContributionDiff> _sectionContributionDiffsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<LibSectionContributionDiff>();
    public IReadOnlyDictionary<string, LibSectionContributionDiff> SectionContributionDiffsByName => this._sectionContributionDiffsByName;

    #endregion

    #region COFF Group Contributions

    private readonly Dictionary<COFFGroupDiff, LibCOFFGroupContributionDiff> _coffGroupContributionDiffs = new Dictionary<COFFGroupDiff, LibCOFFGroupContributionDiff>();
    public IReadOnlyDictionary<COFFGroupDiff, LibCOFFGroupContributionDiff> COFFGroupContributionDiffs => this._coffGroupContributionDiffs;

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<LibCOFFGroupContributionDiff> _coffGroupContributionDiffsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<LibCOFFGroupContributionDiff>();
    public IReadOnlyDictionary<string, LibCOFFGroupContributionDiff> COFFGroupContributionDiffsByName => this._coffGroupContributionDiffsByName;

    #endregion
}
