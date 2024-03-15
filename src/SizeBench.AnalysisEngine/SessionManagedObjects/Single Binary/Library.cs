using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Helpers;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("LIB Name={Name}, Size={Size}")]
public sealed class Library : IEquatable<Library>
{
    private bool _fullyConstructed;

    [Display(AutoGenerateField = false)]
    public static string UnknownName => "...no name found...";

    internal Library(string name)
    {
        this.Name = String.IsNullOrEmpty(name) ? UnknownName : name;
    }

    public string Name { get; }

    public string ShortName => Path.GetFileNameWithoutExtension(this.Name);

    [Display(Name = "Size on disk")]
    public uint Size
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return (uint)this.Compilands.Values.Sum(c => c.Size);
        }
    }

    [Display(Name = "Size in memory")]
    public uint VirtualSize
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return (uint)this.Compilands.Values.Sum(c => c.VirtualSize);
        }
    }

    #region Compilands

    private readonly Dictionary<string, Compiland> _compilands = new Dictionary<string, Compiland>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, Compiland> Compilands
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

    internal Compiland GetOrCreateCompiland(SessionDataCache cache, string compilandName, uint compilandSymIndexId, IDIAAdapter diaAdapter)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (String.IsNullOrEmpty(compilandName))
        {
            compilandName = Compiland.UnknownName;
        }

        if (this._compilands.TryGetValue(compilandName, out var existingCompiland))
        {
            return existingCompiland;
        }

        var commandLine = diaAdapter.FindCommandLineForCompilandByID(compilandSymIndexId);

        var compiland = new Compiland(cache, compilandName, this, commandLine, compilandSymIndexId);
        this._compilands.Add(compilandName, compiland);

        return compiland;
    }

    #endregion

    #region Section Contributions

    private readonly Dictionary<BinarySection, LibSectionContribution> _sectionContributions = new Dictionary<BinarySection, LibSectionContribution>();
    public IReadOnlyDictionary<BinarySection, LibSectionContribution> SectionContributions
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

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<LibSectionContribution> _sectionContributionsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<LibSectionContribution>();
    public IReadOnlyDictionary<string, LibSectionContribution> SectionContributionsByName
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

    internal LibSectionContribution GetOrCreateSectionContribution(BinarySection section)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._sectionContributions.TryGetValue(section, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new LibSectionContribution($"{this.Name} contributions to {section.Name}", section, this);
        this._sectionContributions.Add(section, contrib);
        this._sectionContributionsByName.Add(section.Name, contrib);

        return contrib;
    }

    #endregion

    #region COFF Group Contributions

    internal LibCOFFGroupContribution GetOrCreateCOFFGroupContribution(COFFGroup coffGroup)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        if (this._coffGroupContributions.TryGetValue(coffGroup, out var existingContribution))
        {
            return existingContribution;
        }

        var contrib = new LibCOFFGroupContribution($"{this.Name} contributions to {coffGroup.Name}", coffGroup, this);
        this._coffGroupContributions.Add(coffGroup, contrib);
        this._coffGroupContributionsByName.Add(coffGroup.Name, contrib);

        return contrib;
    }

    private readonly Dictionary<COFFGroup, LibCOFFGroupContribution> _coffGroupContributions = new Dictionary<COFFGroup, LibCOFFGroupContribution>();
    public IReadOnlyDictionary<COFFGroup, LibCOFFGroupContribution> COFFGroupContributions
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

    private readonly DictionaryThatDoesntThrowWhenKeyNotPresent<LibCOFFGroupContribution> _coffGroupContributionsByName = new DictionaryThatDoesntThrowWhenKeyNotPresent<LibCOFFGroupContribution>();
    public IReadOnlyDictionary<string, LibCOFFGroupContribution> COFFGroupContributionsByName
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

    #endregion

    internal void MarkFullyConstructed()
    {
        foreach (var compiland in this._compilands.Values)
        {
            foreach (var sectionContribution in compiland.SectionContributions)
            {
                var thisSectionContrib = GetOrCreateSectionContribution(sectionContribution.Key);
                thisSectionContrib.AddRVARanges(sectionContribution.Value.RVARanges);
            }
            foreach (var coffGroupContribution in compiland.COFFGroupContributions)
            {
                var thisCOFFGroupContrib = GetOrCreateCOFFGroupContribution(coffGroupContribution.Key);
                thisCOFFGroupContrib.AddRVARanges(coffGroupContribution.Value.RVARanges);
            }
        }

        foreach (var sectionContribution in this._sectionContributions.Values)
        {
            sectionContribution.MarkFullyConstructed();
        }

        foreach (var coffGroupContribution in this._coffGroupContributions.Values)
        {
            coffGroupContribution.MarkFullyConstructed();
        }

        this._fullyConstructed = true;
    }

    internal bool IsVeryLikelyTheSameAs(Library otherLib)
        => PathHeuristicComparer.PathNamesAreVerySimilar(this.Name, otherLib.Name);

    public override bool Equals(object? obj) => base.Equals(obj as Library);

    public bool Equals(Library? other)
    {
        if (other is null) { return false; }

        if (ReferenceEquals(this, other)) { return true; }

        return this.Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() => this.Name.GetHashCode(StringComparison.OrdinalIgnoreCase);
}
