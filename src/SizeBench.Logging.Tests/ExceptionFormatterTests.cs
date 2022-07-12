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

        StringAssert.Contains(text, "Callsite message", StringComparison.Ordinal);
        StringAssert.Contains(text, "Important Message", StringComparison.Ordinal);

        var stackTrace = new StackTrace(caughtException, fNeedFileInfo: true);

        StringAssert.Contains(text, stackTrace.ToString(), StringComparison.Ordinal);
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

        StringAssert.Contains(text, "Callsite message", StringComparison.Ordinal);
        StringAssert.Contains(text, "Important Outer Message", StringComparison.Ordinal);
        StringAssert.Contains(text, caughtException.GetType().FullName, StringComparison.Ordinal);

        Assert.IsNotNull(caughtException.InnerException);
        StringAssert.Contains(text, "Important Inner Message", StringComparison.Ordinal);
        StringAssert.Contains(text, caughtException.InnerException.GetType().FullName, StringComparison.Ordinal);

        var stackTrace = new StackTrace(caughtException, fNeedFileInfo: true);
        StringAssert.Contains(text, stackTrace.ToString(), StringComparison.Ordinal);

        stackTrace = new StackTrace(caughtException.InnerException, fNeedFileInfo: true);
        StringAssert.Contains(text, stackTrace.ToString(), StringComparison.Ordinal);
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

        StringAssert.Contains(text, "Callsite message", StringComparison.Ordinal);

        StringAssert.Contains(text, "Outer aggregate exception", StringComparison.Ordinal);
        StringAssert.Contains(text, caughtException.GetType().FullName, StringComparison.Ordinal);

        Assert.IsNotNull(innerException1);
        StringAssert.Contains(text, "Important Inner Message", StringComparison.Ordinal);
        StringAssert.Contains(text, innerException1.GetType().FullName, StringComparison.Ordinal);

        Assert.IsNotNull(innerException2);
        StringAssert.Contains(text, "Some argument was out of range", StringComparison.Ordinal);
        StringAssert.Contains(text, innerException2.GetType().FullName, StringComparison.Ordinal);

        var stackTrace = new StackTrace(caughtException, fNeedFileInfo: true);
        StringAssert.Contains(text, stackTrace.ToString(), StringComparison.Ordinal);

        stackTrace = new StackTrace(innerException1, fNeedFileInfo: true);
        StringAssert.Contains(text, stackTrace.ToString(), StringComparison.Ordinal);

        stackTrace = new StackTrace(innerException2, fNeedFileInfo: true);
        StringAssert.Contains(text, stackTrace.ToString(), StringComparison.Ordinal);
    }
}
