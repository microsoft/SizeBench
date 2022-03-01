using System.Diagnostics;
using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders.Tests;

[TestClass]
public sealed class ProcessInfoProviderTests
{
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    [TestMethod]
    public void NullBodyThrows()
    {
        var provider = new ProcessInfoProvider();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.  This test is intentionally testing null
        provider.AddErrorInfo(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [TestMethod]
    public void ImportantProcessInformationIsPresent()
    {
        var provider = new ProcessInfoProvider();
        var body = new StringBuilder();
        provider.AddErrorInfo(body);
        var output = body.ToString();

        StringAssert.Contains(output, Environment.CommandLine, StringComparison.Ordinal);
        StringAssert.Contains(output, Process.GetCurrentProcess().ProcessName, StringComparison.Ordinal);
        StringAssert.Contains(output, $"64-bit Process: {Environment.Is64BitProcess}", StringComparison.Ordinal);
    }
}
