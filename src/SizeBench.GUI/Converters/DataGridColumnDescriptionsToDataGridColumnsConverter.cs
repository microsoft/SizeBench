using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

// For now this'll just create text columns - this could be expanded to have view-agnostic ways of describing other types of columns if that's
// ever needed.  But for now it's not.
public sealed class DataGridColumnDescription
{
    public object Header { get; }
    public string PropertyPath { get; }
    public IValueConverter? ValueConverter { get; }
    public bool IsRightAligned { get; }

    public DataGridColumnDescription(object header, string propertyPath, IValueConverter? valueConverter = null, bool isRightAligned = false)
    {
        this.Header = header;
        this.PropertyPath = propertyPath;
        this.ValueConverter = valueConverter;
        this.IsRightAligned = isRightAligned;
    }
}

public sealed class DataGridColumnDescriptionsToDataGridColumnsConverter : IValueConverter
{
    public static DataGridColumnDescriptionsToDataGridColumnsConverter Instance { get; } = new DataGridColumnDescriptionsToDataGridColumnsConverter();

    private sealed class DataGridColumnObservableCollectionWrapper : ObservableCollection<DataGridColumn>
    {
        private readonly ObservableCollection<DataGridColumnDescription> _descriptions;

        public DataGridColumnObservableCollectionWrapper(ObservableCollection<DataGridColumnDescription> descriptions)
        {
            this._descriptions = descriptions;
            this._descriptions.CollectionChanged += _descriptions_CollectionChanged;
            CreateDataGridColumns();
        }

        private void _descriptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => CreateDataGridColumns();

        private void CreateDataGridColumns()
        {
            // Calling Clear is hard to suport in DataGridExtensions because then I have to keep track of which collection is firing INCC with an Action of "Reset" - so by
            // doing this, I make the "e.RemovedItems" easy to track in the INCC handler over there.
            while (this.Count > 0)
            {
                RemoveAt(0);
            }

            foreach (var description in this._descriptions)
            {
                Add(new DataGridTextColumn()
                {
                    Header = description.Header,
                    Binding = new Binding(description.PropertyPath)
                    {
                        Converter = description.ValueConverter
                    },
                    ElementStyle = description.IsRightAligned ? Application.Current.FindResource("RightAlignedTextStyle") as Style : null
                });
            }
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ObservableCollection<DataGridColumnDescription>)
        {
            throw new ArgumentException("must be ObservableCollection<DataGridColumnDescription>", nameof(value));
        }

        return new DataGridColumnObservableCollectionWrapper((ObservableCollection<DataGridColumnDescription>)value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
