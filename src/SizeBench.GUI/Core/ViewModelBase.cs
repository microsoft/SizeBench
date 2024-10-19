using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SizeBench.GUI.Core;

internal abstract class ViewModelBase : INotifyPropertyChanged
{
    #region Query String and Fragment

    protected IDictionary<string, string> QueryString { get; private set; } = new Dictionary<string, string>();

    // Intended to only be called by the navigation framework
    internal void SetQueryString(IDictionary<string, string> queryString)
        => this.QueryString = queryString;

    protected string CurrentFragment { get; private set; } = String.Empty;

    // Intended to only be called by the navigation framework
    internal Task SetCurrentFragment(string? newFragment)
    {
        newFragment ??= String.Empty;

        if (this.CurrentFragment != newFragment)
        {
            this.CurrentFragment = newFragment;
            return OnCurrentFragmentChanged();
        }

        return Task.CompletedTask;
    }

    protected virtual Task OnCurrentFragmentChanged() => Task.CompletedTask;

    #endregion Query String and Fragment

    #region Navigation Framework support

    public event EventHandler<object>? RequestNavigateToModel;

    protected void OnRequestNavigateToModel(object model)
        => RequestNavigateToModel?.Invoke(this, model);

    public event EventHandler<string>? RequestFragmentNavigation;

    protected void OnRequestFragmentNavigation(string newFragment)
    {
        if (this.CurrentFragment != newFragment)
        {
            RequestFragmentNavigation?.Invoke(this, newFragment);
        }
    }

    #endregion Navigation Framework support

    #region Lifetime

    protected internal virtual Task InitializeAsync() => Task.CompletedTask;

    protected internal virtual void Deactivate()
    {
    }

    #endregion Lifetime

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion INPC
}
