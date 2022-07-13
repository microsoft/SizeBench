using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using SizeBench.Logging;

namespace SizeBench.GUI.ViewModels;

public sealed class LogEntryToLogWindowTreeViewDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogEntry entry)
        {
            throw new NotSupportedException();
        }

        var displayedText = $"{entry.CallingMember}: {entry.Message}";

        if (entry is TaskLogEntry taskEntry)
        {
            if (taskEntry.Stopwatch.IsRunning)
            {
                displayedText += " (still running)";
            }
            else
            {
                displayedText += $" (elapsed: {taskEntry.Stopwatch.Elapsed.ToString(@"mm\:ss\:fff", CultureInfo.InvariantCulture)})";
            }
        }

        return displayedText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

internal class LogWindowViewModel
{
    private readonly IApplicationLogger _applicationLogger;

    public ICollectionView LogScopes { get; }

    public ObservableCollection<ILogger> LogScopesSourceCollection { get; } = new ObservableCollection<ILogger>();

    public LogWindowViewModel(IApplicationLogger applicationLogger)
    {
        this._applicationLogger = applicationLogger;
        this.LogScopes = CollectionViewSource.GetDefaultView(this.LogScopesSourceCollection);
        CreateLogScopes();

        this.LogScopes.MoveCurrentToFirst();

        if (this._applicationLogger.SessionLogs is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += sessionLogs_CollectionChanged;
        }
    }

    private void CreateLogScopes()
    {
        this.LogScopesSourceCollection.Clear();
        this.LogScopesSourceCollection.Add(this._applicationLogger);
        foreach (var sessionLogger in this._applicationLogger.SessionLogs)
        {
            this.LogScopesSourceCollection.Add(sessionLogger);
        }
    }

    private void sessionLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Need to maintain currency, so store the currently selected item before mucking about
        // with the collection.
        var currentSelection = (ILogger)this.LogScopes.CurrentItem;
        CreateLogScopes();
        if (this.LogScopesSourceCollection.Contains(currentSelection))
        {
            this.LogScopes.MoveCurrentTo(currentSelection);
        }
    }
}
