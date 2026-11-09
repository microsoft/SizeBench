using System.Globalization;
using DiffPlex.DiffBuilder.Model;

namespace SizeBench.GUI.Controls;

// One row of the diff as shown by SelectableDiffViewer: the (possibly empty) gutter line number,
// the "+ "/"- "/"  " prefix, the line's text, and the kind of change (used for row coloring).
internal readonly record struct DiffRowInfo(string LineNumber, string Prefix, string Text, ChangeType ChangeType);

// The pure, WPF-free logic for turning a DiffPlex diff model into the rows shown by
// SelectableDiffViewer.  It is deliberately kept out of the (UI-only, coverage-excluded) control
// so that the line-numbering behavior can be unit tested without an STA thread or a visual tree.
internal static class DiffRowBuilder
{
    public static IReadOnlyList<DiffRowInfo> BuildRows(DiffPaneModel diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var rows = new List<DiffRowInfo>(diff.Lines.Count);
        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                ChangeType.Modified => "~ ",
                _ => "  " // Unchanged and Imaginary
            };

            // DiffPlex's InlineDiffBuilder sets Position to the new-file line number for unchanged and
            // inserted lines, and leaves it null for deleted lines.  Using it directly gives exactly the
            // behavior we want: a deleted/inserted pair only advances the number once (on the inserted
            // '+' row), and deleted '-' rows show a blank gutter.
            var lineNumber = line.Position?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            rows.Add(new DiffRowInfo(lineNumber, prefix, line.Text ?? string.Empty, line.Type));
        }

        return rows;
    }
}
