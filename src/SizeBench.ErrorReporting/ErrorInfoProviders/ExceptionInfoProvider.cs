using System.Text;
using SizeBench.Logging;

namespace SizeBench.ErrorReporting.ErrorInfoProviders;

public sealed class ExceptionInfoProvider : IErrorInfoProvider
{
    private readonly Exception _exception;

    public ExceptionInfoProvider(Exception ex)
    {
        this._exception = ex ?? throw new ArgumentNullException(nameof(ex));
    }

    public void AddErrorInfo(StringBuilder body)
    {
        ArgumentNullException.ThrowIfNull(body);

        body.Append(this._exception.GetFormattedTextForLogging("Exception information:", "\n"));
        body.Append(Environment.NewLine);
    }
}
