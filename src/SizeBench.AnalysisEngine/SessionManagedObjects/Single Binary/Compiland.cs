using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Helpers;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Compiland Name={Name}, Size={Size}")]
public sealed class Compiland : IEquatable<Compiland>
{
    [Display(AutoGenerateField = false)]
    public static string UnknownName => "...no name found...";

    private bool _fullyConstructed;

    internal readonly HashSet<uint> SymIndexIds = new HashSet<uint>();

    public string Name { get; }

    public string ShortName => Path.GetFileName(this.Name);

    private uint _size;

    [Display(Name = "Size on disk")]
    public uint Size
    {
        get
        {
            DebugValidateSize();

            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._size;
        }
    }

    [Conditional("DEBUG")]
    private void DebugValidateSize()
    {
        var sectionContributionSum = (uint)this.SectionContributions.Values.Sum(contrib => contrib.Size);
        ulong coffGroupContributionsSum = (uint)this.COFFGroupContributions.Values.Sum(contrib => contrib.Size);
        if (coffGroupContributionsSum != sectionContributionSum ||
            this._size != sectionContributionSum)
        {
            throw new InvalidOperationException("Something has gone terribly wrong!");
        }
    }

    private uint _virtualSize;

    [Display(Name = "Size in memory")]
    public uint VirtualSize
    {
        get
        {
            DebugValidateVirtualSize();

            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._virtualSize;
        }
    }

    [Conditional("DEBUG")]
    private void DebugValidateVirtualSize()
    {
        var sectionContributionSum = (uint)this.SectionContributions.Values.Sum(contrib => contrib.VirtualSize);
        var coffGroupContributionsSum = (uint)this.COFFGroupContributions.Values.Sum(contrib => contrib.VirtualSize);
        if (coffGroupContributionsSum != sectionContributionSum ||
            this._virtualSize != sectionContributionSum)
        {
            throw new InvalidOperationException("Something has gone terribly wrong!");
        }
    }

    [Display(AutoGenerateField = false)]
    public Library Lib { get; }

    // This List version is maintained because it is much faster to iterate over a List<T> than a Dictionary<TKey, TValue> and this is critical to the PDATA attribution process when
    // assembling compilands - so this is a perf win there, but a memory hit in all other scenarios.  Perhaps we can find a way to toss this memory after that process is done as a future
    // optimization.
    private readonly List<KeyValuePair<BinarySection, CompilandSectionContribution>> _sectionContributionsAsList = new List<KeyValuePair<BinarySection, CompilandSectionContribution>>();
    private readonly Dictionary<BinarySection, CompilandSectionContribution> _sectionContributions = new Dictionary<BinarySection, CompilandSectionContribution>();

    public IReadOnlyDictionary<BinarySection, CompilandSectionContribution> SectionContributions
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

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandSectionContribution> _sectionContributionsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandSectionContribution>();
    public IReadOnlyDictionary<string, CompilandSectionContribution> SectionContributionsByName
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

    private readonly Dictionary<COFFGroup, CompilandCOFFGroupContribution> _coffGroupContributions = new Dictionary<COFFGroup, CompilandCOFFGroupContribution>();

    public IReadOnlyDictionary<COFFGroup, CompilandCOFFGroupContribution> COFFGroupContributions
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

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandCOFFGroupContribution> _coffGroupContributionsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<CompilandCOFFGroupContribution>();
    public IReadOnlyDictionary<string, CompilandCOFFGroupContribution> COFFGroupContributionsByName
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

    private readonly CommandLine _commandLine;
    public string CommandLine => this._commandLine.Raw;

    public bool RTTIEnabled => (this._commandLine as CompilerCommandLine)?.RTTIEnabled ?? false;

    public string ToolName => this._commandLine.ToolName;

    public Version ToolFrontEndVersion => this._commandLine.FrontEndVersion;

    public Version ToolBackEndVersion => this._commandLine.BackEndVersion;

    public ToolLanguage ToolLanguage => CoerceToToolLanguage(this._commandLine.Language);

    private static ToolLanguage CoerceToToolLanguage(CompilandLanguage compilandLanguage)
    {
        if (Enum.IsDefined(typeof(ToolLanguage), (int)compilandLanguage))
        {
            return (ToolLanguage)((int)compilandLanguage);
        }
        else
        {
            return ToolLanguage.Unknown;
        }
    }

    internal Compiland(SessionDataCache cache, string name, Library lib, CommandLine commandLine, uint compilandSymIndex)
    {
        name = String.IsNullOrEmpty(name) ? UnknownName : name;

#if DEBUG
        // Compilands that start with "Import:" are sort of special, since they can exist multiple times in a binary
        // and that's fine.
        // Technically, what maks a Compiland unique is the compilandId, but for most users this is difficult to visually
        // parse so we hope that most binaries only have compilands that are unique by name (including the name of the lib
        // they're part of) - note that name is a fully-qualified path, so that's not too crazy to assume.
        if (cache.CompilandsConstructedEver.Any(c => c.SymIndexIds.Contains(compilandSymIndex)) ||
            cache.CompilandsConstructedEver.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                                                     c.Lib.Name.Equals(lib.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.Name = name;
        this._commandLine = commandLine;
        this.SymIndexIds.Add(compilandSymIndex);
        this.Lib = lib;

        cache.RecordCompilandConstructed(this, compilandSymIndex);
    }

    internal void AddSymIndexId(uint compilandSymIndex)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        this.SymIndexIds.Add(compilandSymIndex);
    }

    internal CompilandSectionContribution GetOrCreateSectionContribution(BinarySection section)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._sectionContributions.TryGetValue(section, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new CompilandSectionContribution($"{this.Name} contributions to {section.Name}", section, this);
        this._sectionContributions.Add(section, contrib);
        this._sectionContributionsAsList.Add(new KeyValuePair<BinarySection, CompilandSectionContribution>(section, contrib));
        this._sectionContributionsByName.Add(section.Name, contrib);

        return contrib;
    }

    internal CompilandCOFFGroupContribution GetOrCreateCOFFGroupContribution(COFFGroup coffGroup)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._coffGroupContributions.TryGetValue(coffGroup, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new CompilandCOFFGroupContribution($"{this.Name} contributions to {coffGroup.Name}", coffGroup, this);
        this._coffGroupContributions.Add(coffGroup, contrib);
        this._coffGroupContributionsByName.Add(coffGroup.Name, contrib);

        return contrib;
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

        this._fullyConstructed = true;

        this._size = (uint)this.SectionContributions.Values.Sum(contrib => contrib.Size);
        this._virtualSize = (uint)this.SectionContributions.Values.Sum(contrib => contrib.VirtualSize);
    }

    private bool? _containsExecutableCode;
    public bool ContainsExecutableCode
    {
        get
        {
            if (this._containsExecutableCode.HasValue)
            {
                return this._containsExecutableCode.Value;
            }

            this._containsExecutableCode = false;
            foreach (var section in this._sectionContributions.Keys)
            {
                if ((section.Characteristics & SectionCharacteristics.MemExecute) == SectionCharacteristics.MemExecute)
                {
                    this._containsExecutableCode = true;
                    break;
                }
            }
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

    internal bool Contains(uint rva)
    {
        foreach (var contribution in this._sectionContributions)
        {
            var csc = contribution.Value;

            for (var i = 0; i < csc._rvaRangesUnsafe_AvailableBeforeFullyConstructed!.Count; i++)
            {
                if (csc._rvaRangesUnsafe_AvailableBeforeFullyConstructed[i].Contains(rva))
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

    internal bool IsVeryLikelyTheSameAs(Compiland otherCompiland)
        => PathHeuristicComparer.PathNamesAreVerySimilar(this.Name, otherCompiland.Name);

    public override bool Equals(object? obj) => base.Equals(obj as Compiland);

    public bool Equals(Compiland? other)
    {
        if (other is null) { return false; }

        if (ReferenceEquals(this, other)) { return true; }

        return this.Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
               this.Lib.Equals(other.Lib);
    }

    public override int GetHashCode() => HashCode.Combine(this.Name.GetHashCode(StringComparison.OrdinalIgnoreCase), this.Lib);
}
