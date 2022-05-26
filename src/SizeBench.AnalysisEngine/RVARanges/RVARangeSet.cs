using System.Collections;

namespace SizeBench.AnalysisEngine;

internal sealed class RVARangeSet : IEnumerable<RVARange>
{
    private HashSet<RVARange> _rvaRanges = new HashSet<RVARange>();

    public int Count => this._rvaRanges.Count;

    public void Add(RVARange range)
    {
        foreach(var r in this._rvaRanges)
        {
            if (r.IsAdjacentTo(range) || r.Contains(range) || range.Contains(r))
            {
                throw new ArgumentException("Don't add an adjacent or overlapping range to the set, instead coalesce them at the caller.  As an example, if you have (100, 200) in the set and you want to add (200, 300) you should instead have inserted (100,300) to begin with.");
            }
        }

        this._rvaRanges.Add(range);
    }

    public bool Contains(uint rva)
    {
        foreach (var range in this._rvaRanges)
        {
            if (range.Contains(rva))
            {
                return true;
            }
        }

        return false;
    }

    public bool FullyContains(uint rva, uint size)
    {
        foreach (var range in this._rvaRanges)
        {
            if (range.Contains(rva, size))
            {
                return true;
            }
        }

        return false;
    }

    public bool FullyContains(RVARange range)
    {
        foreach (var r in this._rvaRanges)
        {
            if (r.Contains(range))
            {
                return true;
            }
        }

        return false;
    }

    public void UnionWith(RVARangeSet rvaRangeSet)
    {
        ArgumentNullException.ThrowIfNull(rvaRangeSet);

        foreach (var range in rvaRangeSet._rvaRanges)
        {
            foreach(var r in this._rvaRanges)
            {
                if (r != range &&
                    (r.IsAdjacentTo(range) ||
                     r.Contains(range) ||
                     range.Contains(r)))
                {
                    throw new ArgumentException("Don't union in something that will add an overlapping or adjacent range.  Coalesce them at the caller or make this code more resilient so coalescing is done in RVARangeSet.");
                }
            }
        }

        var copy = new HashSet<RVARange>(this._rvaRanges);
        copy.UnionWith(rvaRangeSet._rvaRanges);

        this._rvaRanges = copy;
    }

    public bool AtLeastPartiallyOverlapsWith(RVARange incoming)
    {
        // There's three cases here.  Imagine this scenario:
        // RVARangeSet contains: (0, 100) and (200, 300)
        // We need to return 'true' for these three cases:
        // 1) AtLeastPartiallyOverlapsWith(50, 150), via range.Contains(rvaStart)
        // 2) AtLeastPartiallyOverlapsWith(150, 250), via range.Contains(rvaEnd)
        // 3) AtLeastPartiallyOverlapsWith(150, 350), via (150,350).Contains(range) because it'll contain (200,300)

        foreach (var range in this._rvaRanges)
        {
            if (range.Contains(incoming.RVAStart) || range.Contains(incoming.RVAEnd) || incoming.Contains(range))
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerator<RVARange> GetEnumerator() => this._rvaRanges.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static List<RVARange> CoalesceRVARangesFromList(List<RVARange> ranges, uint maxPaddingToMerge = 0)
    {
        var rangesToReturn = new List<RVARange>();

        foreach (var range in ranges.OrderBy(range => range.RVAStart))
        {
            if (rangesToReturn.Count > 0 && rangesToReturn[^1].CanBeCombinedWith(range, maxPaddingToMerge))
            {
                rangesToReturn[^1] = rangesToReturn[^1].CombineWith(range, maxPaddingToMerge);
            }
            else
            {
                rangesToReturn.Add(range);
            }
        }

        return rangesToReturn;
    }

    public static RVARangeSet FromListOfRVARanges(List<RVARange> ranges, uint maxPaddingToMerge)
    {
        var set = new RVARangeSet();
        foreach (var coalescedRange in CoalesceRVARangesFromList(ranges, maxPaddingToMerge))
        {
            set.Add(coalescedRange);
        }

        return set;
    }
}
