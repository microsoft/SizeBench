using System.Globalization;
using System.IO;
using System.Text;

namespace SizeBench.Logging;

public static class ExceptionFormatter
{
    public static string GetFormattedTextForLogging(this Exception ex, string message, string lineSeparator, int initialIndentLevel = 0)
    {
        var formattedMessage = new StringBuilder(5000);

        formattedMessage.Append(message);
        formattedMessage.Append(lineSeparator);

        AddExceptionAndInnerExceptionsToBuilder(ex, lineSeparator, formattedMessage, initialIndentLevel);

        return formattedMessage.ToString();
    }

    private static void AddExceptionAndInnerExceptionsToBuilder(Exception ex, string lineSeparator, StringBuilder formattedMessage, int indentLevel)
    {
        while (ex != null)
        {
            AddExceptionToBuilder(ex, lineSeparator, formattedMessage, indentLevel);

            if (ex is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    AddExceptionAndInnerExceptionsToBuilder(innerException, lineSeparator, formattedMessage, indentLevel + 1);
                }
                return;
            }
            else if (ex.InnerException != null)
            {
                ex = ex.InnerException;
                indentLevel++;
            }
            else
            {
                return;
            }
        }
    }

    private static void AddExceptionToBuilder(Exception ex, string lineSeparator, StringBuilder formattedMessage, int indentLevel)
    {
        var indentString = new string('\t', indentLevel);

        formattedMessage.Append(indentString);
        formattedMessage.Append(ex.GetType().FullName);
        formattedMessage.Append(": ");
        formattedMessage.Append(ex.Message);
        formattedMessage.Append(lineSeparator);

        formattedMessage.Append(indentString);
        formattedMessage.Append("HRESULT: 0x");
        formattedMessage.Append(ex.HResult.ToString("X", CultureInfo.InvariantCulture.NumberFormat));
        formattedMessage.Append(lineSeparator);

        if (ex.StackTrace != null)
        {
            using var reader = new StringReader(ex.StackTrace);
            var line = reader.ReadLine();
            while (line != null)
            {
                formattedMessage.Append(indentString);
                formattedMessage.Append(line);
                formattedMessage.Append(lineSeparator);

                line = reader.ReadLine();
            }
        }
    }
}
