using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.Logging.Tests;

[TestClass]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This class is all about testing thrown exceptions, so catching them generically is necessary for the tests.")]
public class ExceptionFormatterTests
{
    [TestMethod]
    public void SimpleExceptionLogsUsefulBits()
    {
        Exception caughtException;

        try
        {
            throw new Exception("Important Message");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        var text = ExceptionFormatter.GetFormattedTextForLogging(caughtException, "Callsite message", Environment.NewLine);

        Assert.Contains("Callsite message", text, StringComparison.Ordinal);
        Assert.Contains("Important Message", text, StringComparison.Ordinal);

        var stackTrace = new StackTrace(caughtException, fNeedFileInfo: true);

        Assert.Contains(stackTrace.ToString(), text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void InnerExceptionLogsUsefulBits()
    {
        Exception caughtException;

        try
        {
            try
            {
                throw new InvalidOperationException("Important Inner Message");
            }
            catch (Exception ex)
            {
                throw new Exception("Important Outer Message", ex);
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        var text = ExceptionFormatter.GetFormattedTextForLogging(caughtException, "Callsite message", Environment.NewLine);

        Assert.Contains("Callsite message", text, StringComparison.Ordinal);
        Assert.Contains("Important Outer Message", text, StringComparison.Ordinal);
        Assert.Contains(caughtException.GetType().FullName!, text, StringComparison.Ordinal);

        Assert.IsNotNull(caughtException.InnerException);
        Assert.Contains("Important Inner Message", text, StringComparison.Ordinal);
        Assert.Contains(caughtException.InnerException.GetType().FullName!, text, StringComparison.Ordinal);

        var stackTrace = new StackTrace(caughtException, fNeedFileInfo: true);
        Assert.Contains(stackTrace.ToString(), text, StringComparison.Ordinal);

        stackTrace = new StackTrace(caughtException.InnerException, fNeedFileInfo: true);
        Assert.Contains(stackTrace.ToString(), text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AggregateExceptionLogsAllInnerExceptions()
    {
        Exception caughtException;
        Exception? innerException1 = null, innerException2 = null;

        try
        {
            try
            {
                throw new InvalidOperationException("Important Inner Message");
            }
            catch (Exception ex)
            {
                innerException1 = ex;
            }

            try
            {
                throw new ArgumentOutOfRangeException("arg1", "Some argument was out of range");
            }
            catch (Exception ex)
            {
                innerException2 = ex;
            }

            throw new AggregateException("Outer aggregate exception", innerException1, innerException2);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        var text = ExceptionFormatter.GetFormattedTextForLogging(caughtException, "Callsite message", Environment.NewLine);

        Assert.Contains("Callsite message", text, StringComparison.Ordinal);

        Assert.Contains("Outer aggregate exception", text, StringComparison.Ordinal);
        Assert.Contains(caughtException.GetType().FullName!, text, StringComparison.Ordinal);

        Assert.IsNotNull(innerException1);
        Assert.Contains("Important Inner Message", text, StringComparison.Ordinal);
        Assert.Contains(innerException1.GetType().FullName!, text, StringComparison.Ordinal);

        Assert.IsNotNull(innerException2);
        Assert.Contains("Some argument was out of range", text, StringComparison.Ordinal);
        Assert.Contains(innerException2.GetType().FullName!, text, StringComparison.Ordinal);

        var stackTrace = new StackTrace(caughtException, fNeedFileInfo: true);
        Assert.Contains(stackTrace.ToString(), text, StringComparison.Ordinal);

        stackTrace = new StackTrace(innerException1, fNeedFileInfo: true);
        Assert.Contains(stackTrace.ToString(), text, StringComparison.Ordinal);

        stackTrace = new StackTrace(innerException2, fNeedFileInfo: true);
        Assert.Contains(stackTrace.ToString(), text, StringComparison.Ordinal);
    }
}
