namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public class RVARangeSetTests
{
    [TestMethod]
    public void AtLeastPartiallyOverlapsWithWorks()
    {
        var set = new RVARangeSet
            {
                new RVARange(100, 200),
                new RVARange(202, 300),
                new RVARange(400, 500)
            };

        // Assert that subsets of a given range count at partially overlapping
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(100, 150)));
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(200, 250)));

        // Assert that just having the 'end' inside counts as partially overlapping
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(350, 400)));
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(350, 450)));

        // Assert that just having the 'start' inside counts as partially overlapping
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(250, 301)));
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(250, 350)));

        // Assert that we can cross 'gaps'
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(250, 450)));

        // Assert that we can cover the case where the range is 'around' something in the set
        Assert.IsTrue(set.AtLeastPartiallyOverlapsWith(new RVARange(350, 550)));
    }

    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void AdjacentRVARangesBeingAddedThrows()
    {
        // The AtLeastPartiallyOverlapsWith code really needs us to not allow adjacent ranges in - the caller
        // should collapse them before putting them in the set.
        _ = new RVARangeSet
            {
                new RVARange(100, 200),
                new RVARange(400, 500),
                new RVARange(200, 300) // Should throw since it's adjacent to (100, 200)
            };
    }

    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void OverlappingRVARangesBeingAddedThrows()
    {
        // The AtLeastPartiallyOverlapsWith code really needs us to not allow overlapping ranges in - the caller
        // should collapse them before putting them in the set.
        _ = new RVARangeSet
            {
                new RVARange(100, 200),
                new RVARange(400, 500),
                new RVARange(100, 150) // Should throw since it's overlapping with (100, 200)
            };
    }

    [TestMethod]
    public void UnionWithWorks()
    {
        var set = new RVARangeSet
            {
                new RVARange(100, 200),
                new RVARange(400, 500)
            };

        var set2 = new RVARangeSet
            {
                new RVARange(202, 300),
                new RVARange(0, 50)
            };

        set.UnionWith(set2);

        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(49));
        Assert.IsTrue(set.Contains(100));
        Assert.IsTrue(set.Contains(200));
        Assert.IsFalse(set.Contains(201));
        Assert.IsTrue(set.Contains(202));
        Assert.IsTrue(set.Contains(300));
        Assert.IsFalse(set.Contains(301));
        Assert.IsFalse(set.Contains(399));
        Assert.IsTrue(set.Contains(400));
        Assert.IsTrue(set.Contains(500));
    }

    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void AdjacentRVARangesBeingUnionedThrows()
    {
        // If you call UnionWith() with something that would insert an adjacent range, just throw.
        // We could do the work to merge, but nobody needs it now, so better to stay self-consistent in
        // our assumption that nobody ever gets adjacent ranges into the set and throw for now.
        var set = new RVARangeSet
            {
                new RVARange(100, 200),
                new RVARange(400, 500)
            };

        var set2 = new RVARangeSet
            {
                new RVARange(200, 300)
            };

        set.UnionWith(set2); // Should throw because of the adjacent set
    }

    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void OverlappingRVARangesBeingUnionedThrows()
    {
        // If you call UnionWith() with something that would insert an overlapping range, just throw.
        // We could do the work to merge, but nobody needs it now, so better to stay self-consistent in
        // our assumption that nobody ever gets adjacent ranges into the set and throw for now.
        var set = new RVARangeSet
            {
                new RVARange(100, 200),
                new RVARange(400, 500)
            };

        var set2 = new RVARangeSet
            {
                new RVARange(100, 150)
            };

        set.UnionWith(set2); // Should throw because of the overlapping set
    }

    [TestMethod]
    public void CoalescingRangesWorksWithSignificantPaddingAllowed()
    {
        // This is a regression test for a complicated set of ranges seen in a real binary when parsing import data,
        // where many are close to each other and yet not quite adjacent - we should still merge them when allowing
        // up to 16 bytes of padding like we do when coalescing PE symbol ranges for import symbols.
        var ranges = new List<RVARange>()
        {
            new RVARange(0xe4948, 0xe5a8c),
            new RVARange(0xe5a8e, 0xe5ace),
            new RVARange(0xe5ad0, 0xe5ad8),
            new RVARange(0xe5ada, 0xe5ae2),
            new RVARange(0xe5ae4, 0xe5af6),
            new RVARange(0xe5af8, 0xe5b20),
            new RVARange(0xf3000, 0xf598b)
        };

        var set = RVARangeSet.FromListOfRVARanges(ranges, maxPaddingToMerge: 16);

        // We should have collapsed all those adjacent (within padding) ranges except that last one which is not even close
        Assert.AreEqual(2, set.Count);

        Assert.AreEqual(new RVARange(0xe4948, 0xe5b20), set.First());
        Assert.AreEqual(new RVARange(0xf3000, 0xf598b), set.Skip(1).First());
    }

}
