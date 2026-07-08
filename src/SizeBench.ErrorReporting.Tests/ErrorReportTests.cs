using System.Text;
using SizeBench.ErrorReporting.ErrorInfoProviders;

namespace SizeBench.ErrorReporting.Tests;

[TestClass]
public class ErrorReportTests
{
    internal sealed class TestErrorInfoProvider : IErrorInfoProvider
    {
        public string Info { get; set; } = String.Empty;

        public void AddErrorInfo(StringBuilder body) => body.Append(this.Info);
    }

    [TestMethod]
    public void NullParametersThrow()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type. This test is intentionally testing null.
        Assert.ThrowsExactly<ArgumentNullException>(() => ErrorReport.GetErrorInfo(null, new List<IErrorInfoProvider>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => ErrorReport.GetErrorInfo(new InvalidOperationException("test"), null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [TestMethod]
    public void InfoProvidersGetAppendedToErrorDetails()
    {
        var testErrorInfoProvider = new TestErrorInfoProvider() { Info = "This is a\ntest error info provider\n\nwith newlines." };
        InvalidOperationException? caughtException;
        try
        {
            throw new InvalidOperationException("Test exception message");
        }
        catch (InvalidOperationException ex)
        {
            caughtException = ex;
        }

        var errorInfo = ErrorReport.GetErrorInfo(caughtException, new List<IErrorInfoProvider>()
            {
                testErrorInfoProvider
            });

        Assert.Contains(testErrorInfoProvider.Info, errorInfo, StringComparison.Ordinal);
        Assert.Contains(caughtException.Hash(), errorInfo, StringComparison.Ordinal);
    }
}
