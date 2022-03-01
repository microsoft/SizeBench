using System.Globalization;
using System.Text;
using SizeBench.ErrorReporting.ErrorInfoProviders;

namespace SizeBench.ErrorReporting;

public static class ErrorReport
{
    public static string GetErrorInfo(Exception ex, IEnumerable<IErrorInfoProvider> errorInfoProviders)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(errorInfoProviders);

        var exceptionHash = ex.Hash();
        var body = new StringBuilder(10000);
        ProcessInfoProviders(body, errorInfoProviders);
        body.AppendFormat(CultureInfo.InvariantCulture, "\n\n\nExtended Case #{0}", exceptionHash);

        return body.ToString();
    }

    private static void ProcessInfoProviders(StringBuilder body, IEnumerable<IErrorInfoProvider> errorInfoProviders)
    {
        foreach (var infoProvider in errorInfoProviders)
        {
            infoProvider.AddErrorInfo(body);

            // Separate the providers from each other
            body.Append(Environment.NewLine);
            body.Append(Environment.NewLine);
        }
    }
}
