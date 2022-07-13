using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Controls.Navigation;

// This type is excluded from code coverage because it's basically a big copy/paste from the Silverlight SDK and in Silverlight
// this was extensively tested.  See details at the top of the file for SizeBenchFrame.
[ExcludeFromCodeCoverage]
internal class SizeBenchJournal
{
    #region Fields

    private readonly object _syncLock = new object();

    #endregion Fields

    #region Constructors & Destructor

    internal SizeBenchJournal()
    {
    }

    #endregion

    #region Events

    internal event EventHandler<JournalEventArgs>? Navigated;

    #endregion Events

    #region Properties

    /// <summary>
    /// Gets a value indicating whether or not the Journal instance
    /// can navigate backward.
    /// </summary>
    internal bool CanGoBack => this.BackStack.Count > 0;

    /// <summary>
    /// Gets a value indicating whether or not the Journal instance
    /// can navigate forward.
    /// </summary>
    internal bool CanGoForward => (this.ForwardStack.Count > 0);

    /// <summary>
    /// Gets the current JournalEntry or null if no history items exist.
    /// </summary>
    internal SizeBenchJournalEntry? CurrentEntry
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets a stack of back entries in this journal
    /// </summary>
    internal Stack<SizeBenchJournalEntry> BackStack { get; } = new Stack<SizeBenchJournalEntry>();

    /// <summary>
    /// Gets a stack of forward entries in this journal
    /// </summary>
    internal Stack<SizeBenchJournalEntry> ForwardStack { get; } = new Stack<SizeBenchJournalEntry>();

    #endregion Properties

    #region Methods

    /// <summary>
    /// Adds a new JournalEntry to the history stack.
    /// </summary>
    /// <param name="journalEntry">A new JournalEntry to add to the history stack.</param>
    /// <remarks>
    /// Any JournalEntry items existing on the ForwardStack will be removed.
    /// </remarks>
    internal void AddHistoryPoint(SizeBenchJournalEntry journalEntry)
    {
        ArgumentNullException.ThrowIfNull(journalEntry);

        lock (this._syncLock)
        {
            this.ForwardStack.Clear();

            if (this.CurrentEntry != null)
            {
                this.BackStack.Push(this.CurrentEntry);
            }

            this.CurrentEntry = journalEntry;
        }

        UpdateObservables(journalEntry, NavigationMode.New);
    }

    internal void GoBack()
    {
        if (this.CanGoBack == false)
        {
            throw new InvalidOperationException("Cannot go back when CanGoBack is false.");
        }

        lock (this._syncLock)
        {
            this.ForwardStack.Push(this.CurrentEntry!);
            this.CurrentEntry = this.BackStack.Pop();
        }

        UpdateObservables(this.CurrentEntry, NavigationMode.Back);
    }

    internal void GoForward()
    {
        if (this.CanGoForward == false)
        {
            throw new InvalidOperationException("Cannot go forward when CanGoForward is false.");
        }

        lock (this._syncLock)
        {
            this.BackStack.Push(this.CurrentEntry!);
            this.CurrentEntry = this.ForwardStack.Pop();
        }

        UpdateObservables(this.CurrentEntry, NavigationMode.Forward);
    }

    protected void OnNavigated(string name, Uri uri, NavigationMode mode)
    {
        var args = new JournalEventArgs(name, uri, mode);

        Navigated?.Invoke(this, args);
    }

    private void UpdateObservables(SizeBenchJournalEntry currentEntry, NavigationMode mode)
        => OnNavigated(currentEntry.Name, currentEntry.Source, mode);

    #endregion Methods
}
