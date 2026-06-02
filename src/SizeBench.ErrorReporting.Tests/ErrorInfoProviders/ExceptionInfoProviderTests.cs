using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders.Tests;

[TestClass]
public class ExceptionInfoProviderTests
{
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    [TestMethod]
    public void NullBodyThrows()
    {
        var provider = new ExceptionInfoProvider(new InvalidOperationException("test"));
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.  This test is intentionally testing null.
        provider.AddErrorInfo(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [TestMethod]
    public void EntriesContainUsefulExceptionData()
    {
        var innerExceptionMessage = "inner exception for ExceptionInfoProvider";
        var outerExceptionMessage = "This is a test exception for ExceptionInfoProvider";

        Exception testException;
        try
        {
            try
            {
                throw new ArithmeticException(innerExceptionMessage);
            }
            catch (Exception innerException)
            {
                throw new InvalidOperationException(outerExceptionMessage, innerException);
            }
        }
        catch (Exception ex)
        {
            testException = ex;
        }

        var provider = new ExceptionInfoProvider(testException);
        var bodySB = new StringBuilder(1000);
        provider.AddErrorInfo(bodySB);
        var body = bodySB.ToString();

        StringAssert.Contains(body, $"\t{typeof(ArithmeticException).FullName}: {innerExceptionMessage}", StringComparison.Ordinal);
        StringAssert.Contains(body, $"{typeof(InvalidOperationException).FullName}: {outerExceptionMessage}", StringComparison.Ordinal);
    }
}
