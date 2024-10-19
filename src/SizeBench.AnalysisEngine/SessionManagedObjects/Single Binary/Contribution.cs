namespace SizeBench.AnalysisEngine;

public abstract class Contribution
{
    private bool _fullyConstructed;

    internal Contribution(string name)
    {
        this.Name = name;
    }

    public string Name { get; }

    // Be careful using this!  It's available to use before a Contribution is marked as fully constructed, so someone may add ranges later - but
    // some situations (like attribution of PDATA symbols during construction of Compilands or Source Files) need access to the RVA Ranges before construction is
    // completed so this has to exist.
    // VERY VERY little code should use this.  Almost all code should only operate on fully constructed objects.
    internal List<RVARange>? _rvaRangesUnsafe_AvailableBeforeFullyConstructed = new List<RVARange>();

    private readonly List<RVARange> _rvaRanges = new List<RVARange>();

    public IReadOnlyList<RVARange> RVARanges
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._rvaRanges;
        }
    }

    internal List<RVARange> RVARangesRegardlessOfFinalConstructionState
        => this._fullyConstructed ? this._rvaRanges : this._rvaRangesUnsafe_AvailableBeforeFullyConstructed!;

    public uint Size
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            //PERF: If the memory usage of that across all the Contributions is reasonable, to not need to calculate it repeatedly -
            //      we could calculate this in MarkFullyConstructed and store it.
            uint sum = 0;
            for (var i = 0; i < this._rvaRanges.Count; i++)
            {
                sum += this._rvaRanges[i].Size;
            }

            return sum;
        }
    }

    public uint VirtualSize
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            uint sum = 0;
            for (var i = 0; i < this._rvaRanges.Count; i++)
            {
                sum += this._rvaRanges[i].VirtualSize;
            }

            return sum;
        }
    }

    public bool Contains(uint rva, uint size)
    {
        for (var i = 0; i < this._rvaRanges.Count; i++)
        {
            if (this._rvaRanges[i].Contains(rva, size))
            {
                return true;
            }
        }

        return false;
    }

    internal void CompressRVARanges()
    {
        if (this._fullyConstructed || this._rvaRangesUnsafe_AvailableBeforeFullyConstructed is null)
        {
            return;
        }

        this._rvaRangesUnsafe_AvailableBeforeFullyConstructed = RVARangeSet.CoalesceRVARangesFromList(this._rvaRangesUnsafe_AvailableBeforeFullyConstructed, 1);
    }

    internal void MarkFullyConstructed()
    {
        // Time to coalesce all the RVA ranges to ensure that if they 'abut' each other, we just collapse them.
        // Less RVA Ranges is good for two reasons:
        // 1) It's less RVA ranges to enumerate separately when finding symbols in a contribution, and less memory
        //    usage in the app to keep just one range that represents the superset.
        // 2) It ensure that symbols that 'straddle' an RVA Range can be found if both ranges get coalesced into
        //    one larger range.

        if (this._fullyConstructed)
        {
            return;
        }

        if (this._rvaRangesUnsafe_AvailableBeforeFullyConstructed!.Count > 0)
        {
            this._rvaRanges.AddRange(RVARangeSet.CoalesceRVARangesFromList(this._rvaRangesUnsafe_AvailableBeforeFullyConstructed));
        }

        this._rvaRangesUnsafe_AvailableBeforeFullyConstructed = null;
        this._fullyConstructed = true;
    }

    internal void AddRVARange(RVARange rvaRange)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        this._rvaRangesUnsafe_AvailableBeforeFullyConstructed!.Add(rvaRange);
    }


    internal void AddRVARanges(IEnumerable<RVARange> rvaRanges)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        this._rvaRangesUnsafe_AvailableBeforeFullyConstructed!.AddRange(rvaRanges);
    }
}
