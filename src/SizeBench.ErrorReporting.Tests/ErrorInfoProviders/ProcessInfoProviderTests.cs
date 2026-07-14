using System.Diagnostics;
using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders.Tests;

[TestClass]
public sealed class ProcessInfoProviderTests
{
    [TestMethod]
    public void NullBodyThrows()
    {
        var provider = new ProcessInfoProvider();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.  This test is intentionally testing null
        Assert.ThrowsExactly<ArgumentNullException>(() => provider.AddErrorInfo(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [TestMethod]
    public void ImportantProcessInformationIsPresent()
    {
        var provider = new ProcessInfoProvider();
        var body = new StringBuilder();
        provider.AddErrorInfo(body);
        var output = body.ToString();

        Assert.Contains(Environment.CommandLine, output, StringComparison.Ordinal);
        Assert.Contains(Process.GetCurrentProcess().ProcessName, output, StringComparison.Ordinal);
        Assert.Contains($"64-bit Process: {Environment.Is64BitProcess}", output, StringComparison.Ordinal);
    }
}
