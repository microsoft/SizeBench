using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace SizeBench.GUI.Controls;

// Hosts a selectable FlowDocument of diff text next to a non-selectable line-number gutter, keeping
// the two vertically scroll-synchronized.  DiffPlex.Wpf's viewer showed line numbers but didn't
// support text selection; Jon Wiswall's FlowDocument approach gained selection but lost the numbers.
// This control brings the numbers back without making them selectable: because the gutter is a
// separate visual subtree (plain TextBlocks, not part of the FlowDocument), a text selection in the
// diff can never include the numbers.
//
// A fixed, uniform line height plus disabled wrapping makes row i sit at exactly i * lineHeight in
// BOTH panes, so the gutter and text stay aligned even while scrolling.  This is essentially all
// layout/scroll plumbing that can't be meaningfully unit tested, so it's excluded from coverage; the
// only piece with real logic - the line numbering - lives in the separately-tested DiffRowBuilder.
[ExcludeFromCodeCoverage]
public sealed class SelectableDiffViewer : UserControl
{
    private static readonly Brush InsertedBackground = CreateFrozenBrush(Color.FromRgb(255, 255, 187));
    private static readonly Brush DeletedBackground = CreateFrozenBrush(Color.FromRgb(255, 168, 168));
    private static readonly Brush ImaginaryBackground = CreateFrozenBrush(Color.FromRgb(230, 230, 230));
    private static readonly Brush LineNumberForeground = CreateFrozenBrush(Color.FromRgb(64, 128, 160));
    private static readonly Brush GutterSeparatorBrush = CreateFrozenBrush(Color.FromRgb(200, 200, 200));

    private const double DiffFontSize = 16;

    private readonly FlowDocumentScrollViewer _textViewer;
    private readonly ScrollViewer _gutterScrollViewer;
    private readonly string _longestLine;
    private ScrollViewer? _textInnerScrollViewer;
    private bool _pageWidthSet;

    public SelectableDiffViewer(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var fontFamily = new FontFamily("Consolas");
        var lineHeight = Math.Ceiling(DiffFontSize * fontFamily.LineSpacing);

        var diff = InlineDiffBuilder.Diff(oldText, newText);
        var rows = DiffRowBuilder.BuildRows(diff);

        var document = new FlowDocument
        {
            FontFamily = fontFamily,
            FontSize = DiffFontSize,
            PagePadding = new Thickness(0),
        };

        var gutterPanel = new StackPanel { Orientation = Orientation.Vertical };

        var longestLine = string.Empty;
        foreach (var row in rows)
        {
            var lineText = row.Prefix + row.Text;

            // Consolas is monospace, so the widest line (used below to size the page and disable
            // wrapping) is simply the one with the most characters.
            if (lineText.Length > longestLine.Length)
            {
                longestLine = lineText;
            }

            var background = row.ChangeType switch
            {
                ChangeType.Inserted => InsertedBackground,
                ChangeType.Deleted => DeletedBackground,
                ChangeType.Imaginary => ImaginaryBackground,
                _ => Brushes.Transparent
            };

            document.Blocks.Add(new Paragraph(new Run(lineText))
            {
                Background = background,
                Margin = new Thickness(0),
                Padding = new Thickness(2, 0, 2, 0),
                LineHeight = lineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            });

            gutterPanel.Children.Add(new Border
            {
                Height = lineHeight,
                Background = background,
                Child = new TextBlock
                {
                    Text = row.LineNumber,
                    Foreground = LineNumberForeground,
                    FontFamily = fontFamily,
                    FontSize = DiffFontSize,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(6, 0, 6, 0),
                },
            });
        }

        this._longestLine = longestLine;

        this._textViewer = new FlowDocumentScrollViewer
        {
            Document = document,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsToolBarVisible = false,
        };

        // WPF's built-in rich-text copy path serializes the selection to XAML and then converts it to
        // RTF for the clipboard; that conversion crashes on this content (see OnTextViewerPreviewExecuted),
        // so intercept Copy and provide plain text instead.
        CommandManager.AddPreviewExecutedHandler(this._textViewer, OnTextViewerPreviewExecuted);

        this._gutterScrollViewer = new ScrollViewer
        {
            Content = gutterPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = GutterSeparatorBrush,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(this._gutterScrollViewer, 0);
        Grid.SetColumn(this._textViewer, 1);
        grid.Children.Add(this._gutterScrollViewer);
        grid.Children.Add(this._textViewer);

        this.Content = grid;

        Loaded += OnLoaded;
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OnTextViewerPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        // The built-in copy serializes the FlowDocument selection to XAML and then converts it to RTF for
        // the clipboard.  WPF's XamlToRtfWriter throws "The input string 'Auto' was not in a correct
        // format" while doing that conversion (it tries to parse a layout value that serialized as "Auto"
        // back into a number), which would otherwise surface as an unhandled exception when the user
        // presses Ctrl+C.  Disassembly only needs plain text anyway, so intercept Copy and put the
        // selection's plain text on the clipboard ourselves, bypassing the RTF path entirely.
        if (e.Command != ApplicationCommands.Copy)
        {
            return;
        }

        var selection = this._textViewer.Selection;
        if (selection is not null && !selection.IsEmpty)
        {
            try
            {
                Clipboard.SetText(selection.Text);
            }
            catch (ExternalException)
            {
                // The clipboard is sometimes transiently locked by another process; no-op rather than crash.
            }
        }

        e.Handled = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The FlowDocument would otherwise wrap long disassembly lines at the viewport width.  We want
        // horizontal scrolling instead (matching the old DiffPlex.Wpf viewer), so widen the page to fit
        // the longest line.  Measuring text needs the DPI, which is only known once we're loaded.  This
        // is done once - a subsequent reload (e.g. tab switch) would recompute the same width.
        if (!this._pageWidthSet && this._textViewer.Document is FlowDocument document && this._longestLine.Length > 0)
        {
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formatted = new FormattedText(
                this._longestLine,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(document.FontFamily, document.FontStyle, document.FontWeight, document.FontStretch),
                DiffFontSize,
                Brushes.Black,
                pixelsPerDip);

            // Add the paragraph's horizontal padding (2 + 2) plus a small buffer so measurement rounding
            // never re-introduces wrapping.
            document.PageWidth = Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 4 + 24;
            this._pageWidthSet = true;
        }

        // Only wire the scroll synchronization once.  Loaded can fire again if the control is unloaded
        // and reloaded; re-subscribing would double the mouse-wheel scrolling over the gutter.
        if (this._textInnerScrollViewer is null)
        {
            this._textInnerScrollViewer = FindInnerScrollViewer(this._textViewer);
            if (this._textInnerScrollViewer is not null)
            {
                this._textInnerScrollViewer.ScrollChanged += OnTextScrollChanged;

                // Wheeling over the (independently-scrolled) gutter should move the diff, which then
                // scroll-syncs the gutter back via OnTextScrollChanged.
                this._gutterScrollViewer.PreviewMouseWheel += OnGutterMouseWheel;
            }
        }
    }

    private void OnTextScrollChanged(object sender, ScrollChangedEventArgs e)
        => this._gutterScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);

    private void OnGutterMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || this._textInnerScrollViewer is null)
        {
            return;
        }

        e.Handled = true;
        this._textInnerScrollViewer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = this._textInnerScrollViewer,
        });
    }

    private static ScrollViewer? FindInnerScrollViewer(FlowDocumentScrollViewer viewer)
    {
        viewer.ApplyTemplate();
        return viewer.Template?.FindName("PART_ContentHost", viewer) as ScrollViewer
               ?? FindVisualDescendant<ScrollViewer>(viewer);
    }

    private static T? FindVisualDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
