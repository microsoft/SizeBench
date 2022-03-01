using System.ComponentModel;

namespace SizeBench.Logging;

public sealed class LogEntryForProgress : LogEntry, INotifyPropertyChanged
{
    public LogEntryForProgress(string callingMember, string message, LogLevel logLevel)
        : base(callingMember, message, logLevel)
    {
    }

    public void UpdateProgress(string newProgress)
    {
        this.Message = newProgress;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Message"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
