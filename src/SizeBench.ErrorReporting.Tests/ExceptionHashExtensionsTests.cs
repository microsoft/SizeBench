namespace SizeBench.ErrorReporting.Tests;

[TestClass]
public class ExceptionHashExtensionsTests
{
#nullable disable // Intentionally testing passing null to test this case
    [TestMethod]
    public void NullExceptionThrows()
    {
        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => ExceptionHashExtensions.Hash(null));
        Assert.AreEqual("ex", ex.ParamName);
    }
#nullable enable

    [TestMethod]
    public void ExceptionsFromDifferentCallsitesGenerateDifferentHashes()
    {
        var innerException = new Exception("innerException");
        string hash1;
        try
        {
            throw new Exception("Exception", innerException);
        }
        catch (Exception e)
        {
            hash1 = e.Hash();
        }

        string hash2;
        try
        {
            throw new Exception("Exception", innerException);
        }
        catch (Exception e)
        {
            hash2 = e.Hash();
        }

        Assert.AreNotEqual(hash1, hash2);
    }
}
