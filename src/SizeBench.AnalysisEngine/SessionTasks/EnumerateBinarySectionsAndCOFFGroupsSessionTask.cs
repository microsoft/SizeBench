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
