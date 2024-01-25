using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.Helpers;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Source File Name={Name}, Size={Size}")]
public sealed class SourceFile
{
    private bool _fullyConstructed;

#if DEBUG
    private readonly uint _bytesPerWord;
#endif

    internal readonly uint FileId;

    public string Name { get; }

    public string ShortName => Path.GetFileName(this.Name);

    private uint _size;

    [Display(Name = "Size on disk")]
    public uint Size
    {
        get
        {
#if DEBUG
            // This is rather complex to verify - when we compress RVA ranges to tightly pack the contributions, the sizes can differ, and here's why:
            // suppose that Compiland a.obj contributes the RVA range [0,100), and Compiland b.obj contributes [104,200), all in the same section or COFF Group
            // Then when we compress the RVA ranges, the section ranges will end up having [0,200) as will the COFF Group contributions.  But the compilands
            // each still have [0,100) and [104,200) so there's 4 bytes "lost" because they don't abut each other to compress together in any single compiland.
            // So when we compare these sizes for debug sanity, we need to allow for a 'slop factor' of up to the BytesPerWord for each compiland at maximum -
            // in most cases this allows too much slop, but it's the theoretical maximum and there's no good way for this code to do better now.
            // In theory when doing RVA range compression we could track how much we compress by and store it, but that's extra memory used for each SourceFile
            // and more work to track which doesn't seem worth it, so we'll live with this much 'slop' in ensuring No Byte Left Behind here.

            var sectionContributionSum = (uint)this.SectionContributions.Values.Sum(contrib => contrib.Size);
            var coffGroupContributionsSum = (uint)this.COFFGroupContributions.Values.Sum(contrib => contrib.Size);
            var compilandContributionsSum = (uint)this.CompilandContributions.Values.Sum(contrib => contrib.Size);
            var maximumCompilandSlopAllowed = (uint)this.CompilandContributions.Count * this._bytesPerWord;
            if (coffGroupContributionsSum != sectionContributionSum ||
                Math.Abs(compilandContributionsSum - (long)sectionContributionSum) > maximumCompilandSlopAllowed ||
                this._size != sectionContributionSum)
            {
                throw new InvalidOperationException("Something has gone terribly wrong!");
            }
#endif
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._size;
        }
    }

    private uint _virtualSize;

    [Display(Name = "Size in memory")]
    public uint VirtualSize
    {
        get
        {
#if DEBUG
            // See the big comment in the Size property for why this slop is allowed
            var sectionContributionSum = (uint)this.SectionContributions.Values.Sum(contrib => contrib.VirtualSize);
            var coffGroupContributionsSum = (uint)this.COFFGroupContributions.Values.Sum(contrib => contrib.VirtualSize);
            var compilandContributionsSum = (uint)this.CompilandContributions.Values.Sum(contrib => contrib.VirtualSize);
            var maximumCompilandSlopAllowed = (uint)this.CompilandContributions.Count * this._bytesPerWord;
            if (coffGroupContributionsSum != sectionContributionSum ||
                Math.Abs(compilandContributionsSum - (long)sectionContributionSum) > maximumCompilandSlopAllowed ||
                this._virtualSize != sectionContributionSum)
            {
                throw new InvalidOperationException("Something has gone terribly wrong!");
            }
#endif
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._virtualSize;
        }
    }

    internal List<Compiland> _compilands = new List<Compiland>();
    [Display(AutoGenerateField = false)]
    public IReadOnlyList<Compiland> Compilands
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._compilands;
        }
    }

    // This List version is maintained because it is much faster to iterate over a List<T> than a Dictionary<TKey, TValue> and this is critical to the PDATA attribution process when
    // assembling source files - so this is a perf win there, but a memory hit in all other scenarios.  Perhaps we can find a way to toss this memory after that process is done as a future
    // optimization.
    private readonly List<KeyValuePair<BinarySection, SourceFileSectionContribution>> _sectionContributionsAsList = new List<KeyValuePair<BinarySection, SourceFileSectionContribution>>();
    private readonly Dictionary<BinarySection, SourceFileSectionContribution> _sectionContributions = new Dictionary<BinarySection, SourceFileSectionContribution>();
    public IReadOnlyDictionary<BinarySection, SourceFileSectionContribution> SectionContributions
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._sectionContributions;

        }
    }

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<SourceFileSectionContribution> _sectionContributionsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<SourceFileSectionContribution>();
    public IReadOnlyDictionary<string, SourceFileSectionContribution> SectionContributionsByName
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._sectionContributionsByName;
        }
    }

    private readonly Dictionary<COFFGroup, SourceFileCOFFGroupContribution> _coffGroupContributions = new Dictionary<COFFGroup, SourceFileCOFFGroupContribution>();
    public IReadOnlyDictionary<COFFGroup, SourceFileCOFFGroupContribution> COFFGroupContributions
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._coffGroupContributions;
        }
    }

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<SourceFileCOFFGroupContribution> _coffGroupContributionsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<SourceFileCOFFGroupContribution>();
    public IReadOnlyDictionary<string, SourceFileCOFFGroupContribution> COFFGroupContributionsByName
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._coffGroupContributionsByName;
        }
    }

    private readonly Dictionary<Compiland, SourceFileCompilandContribution> _compilandContributions = new Dictionary<Compiland, SourceFileCompilandContribution>();
    public IReadOnlyDictionary<Compiland, SourceFileCompilandContribution> CompilandContributions
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._compilandContributions;
        }
    }

    internal SourceFile(SessionDataCache cache, string name, uint fileId, IEnumerable<Compiland> compilands)
    {
#if DEBUG
        if (cache.SourceFilesConstructedEver.Any(sf => sf.Name == name) == true)
        {
            throw new ObjectAlreadyExistsException();
        }

        this._bytesPerWord = cache.BytesPerWord;
#endif

        this.Name = name;
        this.FileId = fileId;
        this._compilands.AddRange(compilands);

        cache.RecordSourceFileConstructed(this);
    }

    internal SourceFileSectionContribution GetOrCreateSectionContribution(BinarySection section)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._sectionContributions.TryGetValue(section, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new SourceFileSectionContribution($"{this.Name} contributions to {section.Name}", section, this);
        this._sectionContributions.Add(section, contrib);
        this._sectionContributionsAsList.Add(new KeyValuePair<BinarySection, SourceFileSectionContribution>(section, contrib));
        this._sectionContributionsByName.Add(section.Name, contrib);

        return contrib;
    }

    internal SourceFileCOFFGroupContribution GetOrCreateCOFFGroupContribution(COFFGroup coffGroup)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._coffGroupContributions.TryGetValue(coffGroup, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new SourceFileCOFFGroupContribution($"{this.Name} contributions to {coffGroup.Name}", coffGroup, this);
        this._coffGroupContributions.Add(coffGroup, contrib);
        this._coffGroupContributionsByName.Add(coffGroup.Name, contrib);

        return contrib;
    }

    internal SourceFileCompilandContribution GetOrCreateCompilandContribution(Compiland compiland)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._compilandContributions.TryGetValue(compiland, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new SourceFileCompilandContribution($"{this.Name} contributions to {compiland.Name}", compiland, this);
        this._compilandContributions.Add(compiland, contrib);

        return contrib;
    }

    internal void CompressRVARanges()
    {
        foreach (var sectionContrib in this._sectionContributions.Values)
        {
            sectionContrib.CompressRVARanges();
        }

        foreach (var coffgroupContrib in this._coffGroupContributions.Values)
        {
            coffgroupContrib.CompressRVARanges();
        }

        foreach (var compilandContrib in this._compilandContributions.Values)
        {
            compilandContrib.CompressRVARanges();
        }
    }

    internal void MarkFullyConstructed()
    {
        foreach (var sectionContrib in this._sectionContributions.Values)
        {
            sectionContrib.MarkFullyConstructed();
        }

        foreach (var coffGroupContrib in this._coffGroupContributions.Values)
        {
            coffGroupContrib.MarkFullyConstructed();
        }

        foreach (var compilandContrib in this._compilandContributions.Values)
        {
            compilandContrib.MarkFullyConstructed();
        }

        this._fullyConstructed = true;

        this._size = (uint)this.SectionContributions.Values.Sum(contrib => contrib.Size);
        this._virtualSize = (uint)this.SectionContributions.Values.Sum(contrib => contrib.VirtualSize);
    }

    private bool? _containsExecutableCode;
    internal bool ContainsExecutableCode
    {
        get
        {
            if (this._containsExecutableCode.HasValue)
            {
                return this._containsExecutableCode.Value;
            }

            this._containsExecutableCode = this._sectionContributions.Keys.Any(s => (s.Characteristics & SectionCharacteristics.MemExecute) == SectionCharacteristics.MemExecute);
            return this._containsExecutableCode.Value;
        }
    }

    internal bool ContainsExecutableCodeAtRVA(uint rva)
    {
        for (var i = 0; i < this._sectionContributionsAsList.Count; i++)
        {
            var kvp = this._sectionContributionsAsList[i];
            if ((kvp.Key.Characteristics & SectionCharacteristics.MemExecute) == SectionCharacteristics.MemExecute)
            {
                if (kvp.Value.Contains(rva))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal bool Contains(uint rva, uint size)
    {
        foreach (var contribution in this._sectionContributions)
        {
            var csc = contribution.Value;

            for (var i = 0; i < csc.RVARanges.Count; i++)
            {
                if (csc.RVARanges[i].Contains(rva, size))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal bool IsVeryLikelyTheSameAs(SourceFile otherSourceFile)
        => PathHeuristicComparer.PathNamesAreVerySimilar(this.Name, otherSourceFile.Name);
}
