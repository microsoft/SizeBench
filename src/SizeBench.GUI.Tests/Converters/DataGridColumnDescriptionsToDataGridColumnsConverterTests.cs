using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public class DataGridColumnDescriptionsToDataGridColumnsConverterTests
{
    [TestMethod]
    public void TextColumnsCreatedFromDescriptions()
    {
        var desc1 = new DataGridColumnDescription(
            header: "First header",
            propertyPath: "COFFGroupContributionsByName[.text]");
        var desc2 = new DataGridColumnDescription(
            header: "Second header",
            propertyPath: "COFFGroupContributionsByName[.rdata]");

        var descriptions = new ObservableCollection<DataGridColumnDescription>
            {
                desc1,
                desc2
            };

        var columns = DataGridColumnDescriptionsToDataGridColumnsConverter.Instance.Convert(descriptions, typeof(object), null, CultureInfo.CurrentCulture) as ObservableCollection<DataGridColumn>;

        Assert.HasCount(2, columns);
        Assert.AreEqual(desc1.Header, columns[0].Header);
        Assert.IsTrue(columns[0] is DataGridTextColumn);
        Assert.AreEqual(desc1.PropertyPath, ((columns[0] as DataGridTextColumn).Binding as Binding).Path.Path);
        Assert.AreEqual(desc2.Header, columns[1].Header);
        Assert.IsTrue(columns[1] is DataGridTextColumn);
        Assert.AreEqual(desc2.PropertyPath, ((columns[1] as DataGridTextColumn).Binding as Binding).Path.Path);
    }

    [TestMethod]
    public void TextColumnsChangedWhenSourceCollectionChanges()
    {
        var desc1 = new DataGridColumnDescription(
            header: "First header",
            propertyPath: "COFFGroupContributionsByName[.text]");
        var desc2 = new DataGridColumnDescription(
            header: "Second header",
            propertyPath: "COFFGroupContributionsByName[.rdata]");

        var descriptions = new ObservableCollection<DataGridColumnDescription>
            {
                desc1,
                desc2
            };

        var columns = DataGridColumnDescriptionsToDataGridColumnsConverter.Instance.Convert(descriptions, typeof(object), null, CultureInfo.CurrentCulture) as ObservableCollection<DataGridColumn>;

        Assert.HasCount(2, columns);
        Assert.AreEqual(desc1.Header, columns[0].Header);
        Assert.IsTrue(columns[0] is DataGridTextColumn);
        Assert.AreEqual(desc1.PropertyPath, ((columns[0] as DataGridTextColumn).Binding as Binding).Path.Path);
        Assert.AreEqual(desc2.Header, columns[1].Header);
        Assert.IsTrue(columns[1] is DataGridTextColumn);
        Assert.AreEqual(desc2.PropertyPath, ((columns[1] as DataGridTextColumn).Binding as Binding).Path.Path);

        var inccChangesSeen = 0;

        columns.CollectionChanged += (sender, args) => inccChangesSeen++;

        var desc3 = new DataGridColumnDescription(
            header: "Third header",
            propertyPath: "COFFGroupContributionsByName[.data]");
        descriptions.Add(desc3);

        Assert.IsGreaterThan(0, inccChangesSeen);
        Assert.HasCount(3, columns);
        Assert.AreEqual(desc1.Header, columns[0].Header);
        Assert.IsTrue(columns[0] is DataGridTextColumn);
        Assert.AreEqual(desc1.PropertyPath, ((columns[0] as DataGridTextColumn).Binding as Binding).Path.Path);
        Assert.AreEqual(desc2.Header, columns[1].Header);
        Assert.IsTrue(columns[1] is DataGridTextColumn);
        Assert.AreEqual(desc2.PropertyPath, ((columns[1] as DataGridTextColumn).Binding as Binding).Path.Path);
        Assert.AreEqual(desc3.Header, columns[2].Header);
        Assert.IsTrue(columns[2] is DataGridTextColumn);
        Assert.AreEqual(desc3.PropertyPath, ((columns[2] as DataGridTextColumn).Binding as Binding).Path.Path);

        inccChangesSeen = 0;

        descriptions.Remove(desc2);

        Assert.IsGreaterThan(0, inccChangesSeen);
        Assert.HasCount(2, columns);
        Assert.AreEqual(desc1.Header, columns[0].Header);
        Assert.IsTrue(columns[0] is DataGridTextColumn);
        Assert.AreEqual(desc1.PropertyPath, ((columns[0] as DataGridTextColumn).Binding as Binding).Path.Path);
        Assert.AreEqual(desc3.Header, columns[1].Header);
        Assert.IsTrue(columns[1] is DataGridTextColumn);
        Assert.AreEqual(desc3.PropertyPath, ((columns[1] as DataGridTextColumn).Binding as Binding).Path.Path);
    }

    [TestMethod]
    public void WrongInputTypeThrows()
        => Assert.ThrowsExactly<ArgumentException>(() => DataGridColumnDescriptionsToDataGridColumnsConverter.Instance.Convert(new DataGridColumnDescription(header: String.Empty, propertyPath: String.Empty), typeof(object), null, CultureInfo.CurrentCulture));

    [TestMethod]
    public void ConvertBackShouldThrow()
        => Assert.ThrowsExactly<NotImplementedException>(() => DataGridColumnDescriptionsToDataGridColumnsConverter.Instance.ConvertBack(null, typeof(string), null, CultureInfo.CurrentCulture));
}
