using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace SizeBench.GUI.Controls.Tests;

[TestClass]
public sealed class DiffRowBuilderTests
{
    [TestMethod]
    public void NullDiffThrows()
        => Assert.ThrowsExactly<ArgumentNullException>(() => DiffRowBuilder.BuildRows(null!));

    [TestMethod]
    public void AllUnchangedLinesAreNumberedSequentially()
    {
        var rows = DiffRowBuilder.BuildRows(InlineDiffBuilder.Diff("a\nb\nc", "a\nb\nc"));

        Assert.HasCount(3, rows);
        AssertRow(rows[0], "1", "  ", "a", ChangeType.Unchanged);
        AssertRow(rows[1], "2", "  ", "b", ChangeType.Unchanged);
        AssertRow(rows[2], "3", "  ", "c", ChangeType.Unchanged);
    }

    [TestMethod]
    public void ChangedLineShowsDeletedThenInsertedAndIncrementsNumberOnce()
    {
        var rows = DiffRowBuilder.BuildRows(InlineDiffBuilder.Diff("a\nb\nc", "a\nX\nc"));

        Assert.HasCount(4, rows);
        AssertRow(rows[0], "1", "  ", "a", ChangeType.Unchanged);
        // The deleted old line has no new-file position, so its gutter is blank...
        AssertRow(rows[1], "", "- ", "b", ChangeType.Deleted);
        // ...and the number only advances on the inserted new line, so the '-'/'+' pair increments once.
        AssertRow(rows[2], "2", "+ ", "X", ChangeType.Inserted);
        AssertRow(rows[3], "3", "  ", "c", ChangeType.Unchanged);
    }

    [TestMethod]
    public void PureInsertionIsNumbered()
    {
        var rows = DiffRowBuilder.BuildRows(InlineDiffBuilder.Diff("a\nc", "a\nb\nc"));

        Assert.HasCount(3, rows);
        AssertRow(rows[0], "1", "  ", "a", ChangeType.Unchanged);
        AssertRow(rows[1], "2", "+ ", "b", ChangeType.Inserted);
        AssertRow(rows[2], "3", "  ", "c", ChangeType.Unchanged);
    }

    [TestMethod]
    public void PureDeletionLeavesGutterBlankAndDoesNotIncrement()
    {
        var rows = DiffRowBuilder.BuildRows(InlineDiffBuilder.Diff("a\nb\nc", "a\nc"));

        Assert.HasCount(3, rows);
        AssertRow(rows[0], "1", "  ", "a", ChangeType.Unchanged);
        AssertRow(rows[1], "", "- ", "b", ChangeType.Deleted);
        // "c" is the second line of the new file, so it is numbered 2 - the deleted line did not count.
        AssertRow(rows[2], "2", "  ", "c", ChangeType.Unchanged);
    }

    private static void AssertRow(DiffRowInfo row, string expectedLineNumber, string expectedPrefix, string expectedText, ChangeType expectedChangeType)
    {
        Assert.AreEqual(expectedLineNumber, row.LineNumber);
        Assert.AreEqual(expectedPrefix, row.Prefix);
        Assert.AreEqual(expectedText, row.Text);
        Assert.AreEqual(expectedChangeType, row.ChangeType);
    }
}
