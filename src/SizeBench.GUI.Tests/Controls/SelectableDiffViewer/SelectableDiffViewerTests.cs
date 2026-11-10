using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace SizeBench.GUI.Controls.Tests;

[STATestClass]
public sealed class SelectableDiffViewerTests
{
    [TestMethod]
    public void PresenterZoomBindingRetainsConvertedContent()
    {
        var source = new FrameworkElement { Tag = 100 };
        var presenter = new ContentPresenter();
        var style = new Style(typeof(SelectableDiffViewer));
        style.Setters.Add(new Setter(SelectableDiffViewer.ZoomPercentProperty,
            new Binding("DataContext.Tag") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContentPresenter), 1) }));
        presenter.Resources.Add(typeof(SelectableDiffViewer), style);
        var contentBinding = new MultiBinding { Converter = Converters.TwoStringsToDiffViewerUIConverter.Instance };
        contentBinding.Bindings.Add(new Binding { Source = "before" });
        contentBinding.Bindings.Add(new Binding { Source = "after" });
        presenter.SetBinding(ContentPresenter.ContentProperty, contentBinding);
        var window = new Window { Content = presenter, DataContext = source, Width = 400, Height = 300, ShowInTaskbar = false, ShowActivated = false };

        try
        {
            window.Show();
            FlushLayout(window);
            var viewer = (SelectableDiffViewer)presenter.Content;
            Assert.AreEqual(100, viewer.ZoomPercent);
            foreach (var zoomPercent in new[] { 20, 200, 40, 100 })
            {
                source.Tag = zoomPercent;
                FlushLayout(window);
                Assert.AreSame(viewer, presenter.Content);
                Assert.AreEqual(zoomPercent, viewer.ZoomPercent);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    [DataRow(20)]
    [DataRow(40)]
    [DataRow(60)]
    [DataRow(80)]
    [DataRow(100)]
    [DataRow(120)]
    [DataRow(140)]
    [DataRow(160)]
    [DataRow(180)]
    [DataRow(200)]
    public void ZoomScalesContentWithoutRebuildingDocumentOrScalingScrollBars(int zoomPercent)
    {
        var text = String.Join("\n", Enumerable.Range(1, 100).Select(i => $"{i}: {new string('x', 400)}"));
        var viewer = new SelectableDiffViewer(text, text);
        var grid = (Grid)viewer.Content;
        var gutter = (ScrollViewer)grid.Children[0];
        var gutterPanel = (StackPanel)gutter.Content;
        var textViewer = (FlowDocumentScrollViewer)grid.Children[1];
        var document = textViewer.Document;
        var window = new Window { Content = viewer, Width = 650, Height = 350, ShowInTaskbar = false, ShowActivated = false };

        try
        {
            window.Show();
            FlushLayout(window);
            var innerScrollViewer = (ScrollViewer)textViewer.Template.FindName("PART_ContentHost", textViewer);
            var scrollBars = VisualDescendants<ScrollBar>(textViewer).ToList();
            Assert.AreEqual(2, scrollBars.Count);
            var originalSizes = scrollBars.Select(bar => bar.TransformToAncestor(viewer).TransformBounds(new Rect(bar.RenderSize)).Size).ToList();
            textViewer.Selection.Select(document.ContentStart, document.ContentEnd);
            var selectedText = textViewer.Selection.Text;

            viewer.ZoomPercent = zoomPercent;
            FlushLayout(window);

            Assert.AreSame(document, textViewer.Document);
            Assert.AreEqual(selectedText, textViewer.Selection.Text);
            Assert.AreEqual(16.0, document.FontSize);
            Assert.AreEqual(zoomPercent, textViewer.Zoom);
            var documentScale = (ScaleTransform)((FrameworkElement)innerScrollViewer.Content).LayoutTransform;
            var gutterScale = (ScaleTransform)gutterPanel.LayoutTransform;
            Assert.AreEqual(zoomPercent / 100.0, documentScale.ScaleX);
            Assert.AreEqual(documentScale.ScaleX, gutterScale.ScaleX);
            Assert.AreEqual(documentScale.ScaleY, gutterScale.ScaleY);
            Assert.IsTrue(innerScrollViewer.ExtentWidth > innerScrollViewer.ViewportWidth);
            Assert.AreEqual(1.0, gutter.BorderThickness.Right);
            for (var i = 0; i < scrollBars.Count; i++)
            {
                var bar = scrollBars[i];
                var size = bar.TransformToAncestor(viewer).TransformBounds(new Rect(bar.RenderSize)).Size;
                if (bar.Orientation == Orientation.Vertical)
                {
                    Assert.AreEqual(originalSizes[i].Width, size.Width, 0.01);
                }
                else
                {
                    Assert.AreEqual(originalSizes[i].Height, size.Height, 0.01);
                }
            }

            innerScrollViewer.ScrollToVerticalOffset(100);
            innerScrollViewer.ScrollToHorizontalOffset(100);
            FlushLayout(window);
            Assert.IsTrue(innerScrollViewer.HorizontalOffset > 0);
            Assert.AreEqual(innerScrollViewer.VerticalOffset * zoomPercent / 100.0, gutter.VerticalOffset, 0.01,
                $"Text extent/viewport: {innerScrollViewer.ExtentHeight}/{innerScrollViewer.ViewportHeight}; gutter extent/viewport: {gutter.ExtentHeight}/{gutter.ViewportHeight}");
            Assert.AreEqual(innerScrollViewer.ViewportHeight * zoomPercent / 100.0, gutter.ViewportHeight, 0.1);

            var paragraphs = document.Blocks.Cast<Paragraph>().ToList();
            var textRowSpacing = paragraphs[10].ContentStart.GetCharacterRect(LogicalDirection.Forward).Top -
                                 paragraphs[0].ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
            var gutterRowSpacing = gutterPanel.Children[10].TransformToAncestor(viewer).Transform(new Point()).Y -
                                   gutterPanel.Children[0].TransformToAncestor(viewer).Transform(new Point()).Y;
            Assert.AreEqual(textRowSpacing, gutterRowSpacing, 0.1);

            innerScrollViewer.ScrollToEnd();
            FlushLayout(window);
            Assert.AreEqual(innerScrollViewer.VerticalOffset * zoomPercent / 100.0, gutter.VerticalOffset, 0.1);

            viewer.ZoomPercent = 100;
            FlushLayout(window);
            Assert.AreSame(document, textViewer.Document);
            Assert.AreEqual(innerScrollViewer.VerticalOffset, gutter.VerticalOffset, 0.1);
        }
        finally
        {
            window.Close();
        }
    }

    private static void FlushLayout(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in VisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
