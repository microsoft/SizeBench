using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateBinarySectionsAndCOFFGroupsSessionTask : SessionTask<List<BinarySection>>
{
    public EnumerateBinarySectionsAndCOFFGroupsSessionTask(SessionTaskParameters parameters, CancellationToken token)
        : base(parameters, null, token)
    {
        this.TaskName = "Enumerate Binary Sections and COFF Groups";
    }

    protected override List<BinarySection> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllBinarySections != null)
        {
            logger.Log("Found sections in the cache, re-using them, hooray!");
            return this.DataCache.AllBinarySections;
        }

        var binarySections = EnumerateBinarySections(logger);
        var coffGroups = EnumerateCOFFGroups(logger);

        // Now hook the two things up so the OM has a nice relationship
        foreach (var section in binarySections)
        {
            this.CancellationToken.ThrowIfCancellationRequested();

            var coffGroupsInSection = coffGroups.Where(cg => cg.RVA >= section.RVA && (cg.RVA + cg.RawSize) <= (section.RVA + section.VirtualSize));
            foreach (var cg in coffGroupsInSection)
            {
                cg.Section = section;
                section.AddCOFFGroup(cg);
                cg.MarkFullyConstructed();
            }
        }

        // In some binaries it seems that the COFF Group information in the PE headers is corrupted, but it's possible that the
        // PDB "SECTIONHEADERS" stream has the same corrupted data to help us match sections to COFF Groups.  So, if any COFF
        // Group is still not fully constructed, we'll try that fallback.
        // For example this has been seen for PresentationHost_v0400.dll that ships with .NET Framework 4 WPF.  It has a COFF Group
        // .rsrc$02 which has an (RVA + virtual size) that goes outside of its section, likely a bug in the linker when it was
        // originally linked.
        if (coffGroups.Any(static cg => !cg.IsFullyConstructed))
        {
            var imageSectionHeaders = this.DIAAdapter.FindAllImageSectionHeadersFromPDB(this.CancellationToken);
            foreach (var cg in coffGroups.Where(static cg => !cg.IsFullyConstructed))
            {
                foreach (var header in imageSectionHeaders)
                {
                    if (header.VirtualAddress <= cg.RVA && (header.VirtualAddress + header.VirtualSize) >= (cg.RVA + cg.RawSize))
                    {
                        var containingSection = binarySections.Single(section => section.RVA == header.VirtualAddress);
                        cg.Section = containingSection;
                        containingSection.AddCOFFGroup(cg);
                        cg.MarkFullyConstructed();
                        break;
                    }
                }
            }
        }

        foreach (var section in binarySections)
        {
            section.MarkFullyConstructed();
        }

        logger.Log($"Finished enumerating {binarySections.Count} binary sections, containing {coffGroups.Count} COFF Groups.");

        // Safe to deref AllBinarySections, as we know it's assigned in EnumerateBinarySections
        return this.DataCache.AllBinarySections!;
    }

    private List<BinarySection> EnumerateBinarySections(ILogger parentLogger)
    {
        var binarySections = this.DIAAdapter.FindBinarySections(this.Session.PEFile, parentLogger, this.CancellationToken).ToList();

        this.DataCache.AllBinarySections = binarySections;
        return binarySections;
    }

    private List<COFFGroup> EnumerateCOFFGroups(ILogger parentLogger)
    {
        var coffGroups = this.DIAAdapter.FindCOFFGroups(this.Session.PEFile, parentLogger, this.CancellationToken).ToList();

        this.DataCache.AllCOFFGroups = coffGroups;
        return coffGroups;
    }
}
