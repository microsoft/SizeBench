namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public class RVARangeTests
{
    [TestMethod]
    public void FromRVAAndSizeWorks()
    {
        var range = RVARange.FromRVAAndSize(100, 15);

        Assert.AreEqual(100u, range.RVAStart);
        Assert.AreEqual(114u, range.RVAEnd);
        Assert.AreEqual(15u, range.Size);
        Assert.AreEqual(false, range.IsVirtualSize);
    }

    [TestMethod]
    public void ContainsRVAWorks()
    {
        var range = new RVARange(100, 200);

        Assert.IsTrue(range.Contains(100));
        Assert.IsTrue(range.Contains(200));
        Assert.IsTrue(range.Contains(150));

        Assert.IsFalse(range.Contains(0));
        Assert.IsFalse(range.Contains(50));
        Assert.IsFalse(range.Contains(99));
        Assert.IsFalse(range.Contains(201));
        Assert.IsFalse(range.Contains(250));
        Assert.IsFalse(range.Contains(UInt32.MaxValue));
    }

    [TestMethod]
    public void ContainsRVAAndSizeWorks()
    {
        var range = new RVARange(100, 200);

        Assert.IsTrue(range.Contains(100, 1));
        Assert.IsTrue(range.Contains(100, 50));
        Assert.IsTrue(range.Contains(100, 100));
        Assert.IsTrue(range.Contains(150, 30));
        Assert.IsTrue(range.Contains(150, 50));
        Assert.IsTrue(range.Contains(200, 1)); // This is true because the range we set above is [100 to 200] and this is testing for a range starting at 200 and of size 1.

        Assert.IsFalse(range.Contains(99, 1));
        Assert.IsFalse(range.Contains(0, 100));
        Assert.IsFalse(range.Contains(0, 150));
        Assert.IsFalse(range.Contains(0, 200));
        Assert.IsFalse(range.Contains(50, 250));
    }

    [TestMethod]
    public void ContainsRVARangeWorks()
    {
        var range = new RVARange(100, 200);

        Assert.IsTrue(range.Contains(RVARange.FromRVAAndSize(100, 1)));
        Assert.IsTrue(range.Contains(RVARange.FromRVAAndSize(100, 50)));
        Assert.IsTrue(range.Contains(RVARange.FromRVAAndSize(100, 100)));
        Assert.IsTrue(range.Contains(RVARange.FromRVAAndSize(150, 30)));
        Assert.IsTrue(range.Contains(RVARange.FromRVAAndSize(150, 50)));

        Assert.IsFalse(range.Contains(RVARange.FromRVAAndSize(201, 1)));
        Assert.IsFalse(range.Contains(RVARange.FromRVAAndSize(98, 1)));
        Assert.IsFalse(range.Contains(RVARange.FromRVAAndSize(0, 100)));
        Assert.IsFalse(range.Contains(RVARange.FromRVAAndSize(0, 150)));
        Assert.IsFalse(range.Contains(RVARange.FromRVAAndSize(0, 200)));
        Assert.IsFalse(range.Contains(RVARange.FromRVAAndSize(50, 250)));
    }

    [TestMethod]
    public void IsAdjacentToWorks()
    {
        var range = new RVARange(100, 200);

        Assert.IsTrue(range.IsAdjacentTo(new RVARange(50, 99)));
        Assert.IsTrue(new RVARange(50, 99).IsAdjacentTo(range));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(50, 100)));
        Assert.IsTrue(new RVARange(50, 100).IsAdjacentTo(range));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(200, 300)));
        Assert.IsTrue(new RVARange(200, 300).IsAdjacentTo(range));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(201, 300)));
        Assert.IsTrue(new RVARange(201, 300).IsAdjacentTo(range));

        Assert.IsFalse(range.IsAdjacentTo(new RVARange(150, 250)));
        Assert.IsFalse(new RVARange(150, 250).IsAdjacentTo(range));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(199, 300)));
        Assert.IsFalse(new RVARange(199, 300).IsAdjacentTo(range));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 101)));
        Assert.IsFalse(new RVARange(50, 101).IsAdjacentTo(range));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 80)));
        Assert.IsFalse(new RVARange(50, 80).IsAdjacentTo(range));

        Assert.IsFalse(range.IsAdjacentTo(range));

        // Same ranges, but with a larger max padding allowed - should result in the same answers as they're already adjacent to within 1 byte, or wildly far apart
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(50, 99), maxPaddingAllowed: 8));
        Assert.IsTrue(new RVARange(50, 99).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(50, 100), maxPaddingAllowed: 8));
        Assert.IsTrue(new RVARange(50, 100).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(200, 300), maxPaddingAllowed: 8));
        Assert.IsTrue(new RVARange(200, 300).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(201, 300), maxPaddingAllowed: 8));
        Assert.IsTrue(new RVARange(201, 300).IsAdjacentTo(range, maxPaddingAllowed: 8));

        Assert.IsFalse(range.IsAdjacentTo(new RVARange(150, 250), maxPaddingAllowed: 8));
        Assert.IsFalse(new RVARange(150, 250).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(199, 300), maxPaddingAllowed: 8));
        Assert.IsFalse(new RVARange(199, 300).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 101), maxPaddingAllowed: 8));
        Assert.IsFalse(new RVARange(50, 101).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 80), maxPaddingAllowed: 8));
        Assert.IsFalse(new RVARange(50, 80).IsAdjacentTo(range, maxPaddingAllowed: 8));

        // Some ranges that are a bit further apart, but to see if maxPadding still allows adjacency
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 98)));
        Assert.IsFalse(new RVARange(50, 98).IsAdjacentTo(range));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(202, 300)));
        Assert.IsFalse(new RVARange(202, 300).IsAdjacentTo(range));

        Assert.IsTrue(range.IsAdjacentTo(new RVARange(50, 98), maxPaddingAllowed: 2));
        Assert.IsTrue(new RVARange(50, 98).IsAdjacentTo(range, maxPaddingAllowed: 2));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(202, 300), maxPaddingAllowed: 2));
        Assert.IsTrue(new RVARange(202, 300).IsAdjacentTo(range, maxPaddingAllowed: 2));

        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 97), maxPaddingAllowed: 2));
        Assert.IsFalse(new RVARange(50, 97).IsAdjacentTo(range, maxPaddingAllowed: 2));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(203, 300), maxPaddingAllowed: 2));
        Assert.IsFalse(new RVARange(203, 300).IsAdjacentTo(range, maxPaddingAllowed: 2));

        // And now some even further apart, at max padding amounts actually used in the analysis engine today
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 92)));
        Assert.IsFalse(new RVARange(50, 92).IsAdjacentTo(range));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(208, 300)));
        Assert.IsFalse(new RVARange(208, 300).IsAdjacentTo(range));

        Assert.IsTrue(range.IsAdjacentTo(new RVARange(50, 92), maxPaddingAllowed: 8));
        Assert.IsTrue(new RVARange(50, 92).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsTrue(range.IsAdjacentTo(new RVARange(208, 300), maxPaddingAllowed: 8));
        Assert.IsTrue(new RVARange(208, 300).IsAdjacentTo(range, maxPaddingAllowed: 8));

        Assert.IsFalse(range.IsAdjacentTo(new RVARange(50, 91), maxPaddingAllowed: 8));
        Assert.IsFalse(new RVARange(50, 91).IsAdjacentTo(range, maxPaddingAllowed: 8));
        Assert.IsFalse(range.IsAdjacentTo(new RVARange(209, 300), maxPaddingAllowed: 8));
        Assert.IsFalse(new RVARange(209, 300).IsAdjacentTo(range, maxPaddingAllowed: 8));

        Assert.IsFalse(range.IsAdjacentTo(range, maxPaddingAllowed: 150));
    }

    [TestMethod]
    public void CanBeCombinedWithWorks()
    {
        var range = new RVARange(100, 200);

        Assert.IsTrue(range.CanBeCombinedWith(new RVARange(101, 150)));
        Assert.IsTrue(new RVARange(101, 150).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(101, 150, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(101, 150, isVirtualSize: true).CanBeCombinedWith(range));

        Assert.IsTrue(range.CanBeCombinedWith(new RVARange(101, 300)));
        Assert.IsTrue(new RVARange(101, 300).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(101, 300, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(101, 300, isVirtualSize: true).CanBeCombinedWith(range));

        Assert.IsTrue(range.CanBeCombinedWith(new RVARange(50, 99)));
        Assert.IsTrue(new RVARange(50, 99).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(50, 99, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(50, 99, isVirtualSize: true).CanBeCombinedWith(range));

        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(50, 98)));
        Assert.IsFalse(new RVARange(50, 98).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(50, 98, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(50, 98, isVirtualSize: true).CanBeCombinedWith(range));

        Assert.IsTrue(range.CanBeCombinedWith(new RVARange(201, 300)));
        Assert.IsTrue(new RVARange(201, 300).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(201, 300, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(201, 300, isVirtualSize: true).CanBeCombinedWith(range));

        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(202, 300)));
        Assert.IsFalse(new RVARange(202, 300).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(202, 300, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(202, 300, isVirtualSize: true).CanBeCombinedWith(range));

        Assert.IsTrue(range.CanBeCombinedWith(new RVARange(100, 200)));
        Assert.IsTrue(new RVARange(100, 200).CanBeCombinedWith(range));
        Assert.IsFalse(range.CanBeCombinedWith(new RVARange(100, 200, isVirtualSize: true)));
        Assert.IsFalse(new RVARange(100, 200, isVirtualSize: true).CanBeCombinedWith(range));
    }

    [TestMethod]
    public void CombineWithWorks()
    {
        var result = new RVARange(50, 100).CombineWith(new RVARange(100, 199));

        Assert.AreEqual(50u, result.RVAStart);
        Assert.AreEqual(199u, result.RVAEnd);
        Assert.AreEqual(150u, result.Size);

        result = new RVARange(100, 199).CombineWith(new RVARange(50, 100));

        Assert.AreEqual(50u, result.RVAStart);
        Assert.AreEqual(199u, result.RVAEnd);
        Assert.AreEqual(150u, result.Size);

        result = new RVARange(50, 100).CombineWith(new RVARange(101, 199));

        Assert.AreEqual(50u, result.RVAStart);
        Assert.AreEqual(199u, result.RVAEnd);
        Assert.AreEqual(150u, result.Size);

        result = new RVARange(101, 199).CombineWith(new RVARange(50, 100));

        Assert.AreEqual(50u, result.RVAStart);
        Assert.AreEqual(199u, result.RVAEnd);
        Assert.AreEqual(150u, result.Size);
    }

    [TestMethod]
    public void CombineWithThrowsWhenTryingToMergeVirtualAndNonVirtualRanges()
    {
        var virtualRange = new RVARange(50, 100, isVirtualSize: true);
        var nonVirtualRange = new RVARange(100, 150);

        Assert.ThrowsException<InvalidOperationException>(() => virtualRange.CombineWith(nonVirtualRange));
        Assert.ThrowsException<InvalidOperationException>(() => nonVirtualRange.CombineWith(virtualRange));
    }

    [TestMethod]
    public void AdjacentRangesAddUpToTheSameSizeAsSupersetRange()
    {
        var subset1 = new RVARange(0, 2);
        var subset2 = new RVARange(3, 5);
        var superset = subset1.CombineWith(subset2);

        Assert.AreEqual(superset.Size, subset1.Size + subset2.Size);

        var subset3 = new RVARange(6, 8);
        superset = subset3.CombineWith(superset);

        Assert.AreEqual(superset.Size, subset1.Size + subset2.Size + subset3.Size);
    }

    [TestMethod]
    public void RVARangeHasValueEqualitySemantics()
    {
        // Value equality consists of many parts - GetHashCode, operator==, operator!=, and Equals (two overrides).
        // We'll test them all.

        var virtualRange = new RVARange(50, 100, isVirtualSize: true);
        var virtualRange2Identical = new RVARange(50, 100, isVirtualSize: true);
        var nonVirtualRange = new RVARange(50, 100);
        var nonVirtualRange2 = new RVARange(100, 150);

        Assert.AreEqual(virtualRange.GetHashCode(), virtualRange.GetHashCode());
        Assert.AreEqual(virtualRange.GetHashCode(), virtualRange2Identical.GetHashCode());
        Assert.AreNotEqual(virtualRange.GetHashCode(), nonVirtualRange.GetHashCode());
        Assert.AreNotEqual(nonVirtualRange.GetHashCode(), nonVirtualRange2.GetHashCode());

        Assert.IsTrue(virtualRange == virtualRange2Identical);
        Assert.IsTrue(virtualRange == virtualRange2Identical);
        Assert.IsFalse(virtualRange == nonVirtualRange);
        Assert.IsFalse(nonVirtualRange == nonVirtualRange2);

        Assert.IsFalse(virtualRange != virtualRange2Identical);
        Assert.IsFalse(virtualRange != virtualRange2Identical);
        Assert.IsTrue(virtualRange != nonVirtualRange);
        Assert.IsTrue(nonVirtualRange != nonVirtualRange2);

        Assert.IsTrue(virtualRange.Equals(virtualRange));
        Assert.IsTrue(virtualRange.Equals(virtualRange2Identical));
        Assert.IsTrue(virtualRange.Equals(virtualRange2Identical));
        Assert.IsFalse(virtualRange.Equals(nonVirtualRange));
        Assert.IsFalse(nonVirtualRange.Equals(nonVirtualRange2));

        Assert.IsTrue(virtualRange.Equals((object)virtualRange));
        Assert.IsTrue(virtualRange.Equals((object)virtualRange2Identical));
        Assert.IsTrue(virtualRange.Equals((object)virtualRange2Identical));
        Assert.IsFalse(virtualRange.Equals((object)nonVirtualRange));
        Assert.IsFalse(nonVirtualRange.Equals((object)nonVirtualRange2));

#pragma warning disable CA1508 // Avoid dead conditional code - this is explicitly testing this case
        Assert.IsFalse(virtualRange.Equals(null));
        Assert.IsFalse(virtualRange is null);
        Assert.IsFalse(null == virtualRange);
#pragma warning restore CA1508 // Avoid dead conditional code
    }
}
