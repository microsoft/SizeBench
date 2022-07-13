namespace SizeBench.GUI.Core;

public interface IDialogService
{
    /// <summary>
    /// Opens a dialog modal to the entire app window, which is just for showing progress (and optional cancellation).  No
    /// User interaction possible.
    /// </summary>
    /// <param name="title">The title of the dialog (put in the caption area)</param>
    /// <param name="message">The contents of the dialog</param>
    /// <param name="isCancelable">Can this dialog be closed by the user (is the task it represents logically 'cancelable'?)</param>
    /// <param name="task">Input: cancellation token that will be canceled if the dialog is closed, Output: the awaitable task for the operation that is ongoing and modal</param>
    /// <returns>Task that you can wait on to wait for the dialog to finish closing (either by user clicking 'x' or the task it represents completing/canceling on its own)</returns>
    Task OpenAppWideModalProgressOnlyDialog(string title, string message, bool isCancelable, Func<CancellationToken, Task> task);

    /// <summary>
    /// Opens a dialog modal to the entire app window, which the user can interact with.  For now just an OK button,
    /// but this could be extended in the future to support Cancel button and so on.
    /// </summary>
    /// <param name="title">The title of the dialog (put in the caption area)</param>
    /// <param name="message">The contents of the dialog</param>
    /// <returns>Task that you can wait on to wait for the dialog to finish closing (like the user clicking 'OK')</returns>
    Task OpenAppWideModalMessageDialog(string title, string message);

    /// <summary>
    /// Opens a dialog modal to the entire app window, which shows an error.  Like a message dialog, the user can interact with this,
    /// and it stays open until they click the OK button,
    /// </summary>
    /// <param name="title">The title of the dialog (put in the caption area)</param>
    /// <param name="leadingText">The leading text to show in the error message, before all the details</param>
    /// <param name="ex">The exception that occurred</param>
    /// <returns>Task that you can wait on to wait for the dialog to finish closing (like the user clicking 'OK')</returns>
    Task OpenAppWideModalErrorDialog(string title, string leadingText, Exception ex);
}
