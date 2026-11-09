using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders.Tests;

[TestClass]
public sealed class EnvironmentInfoProviderTests
{
    [TestMethod]
    public void NullBodyThrows()
    {
        var provider = new EnvironmentInfoProvider();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type. This test is intentionally testing null.
        Assert.ThrowsExactly<ArgumentNullException>(() => provider.AddErrorInfo(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [TestMethod]
    public void ImportantEnvironmentInformationIsPresent()
    {
        var provider = new EnvironmentInfoProvider();
        var body = new StringBuilder();
        provider.AddErrorInfo(body);
        var output = body.ToString();

        Assert.Contains(RuntimeInformation.FrameworkDescription, output, StringComparison.Ordinal);
        Assert.Contains(RuntimeInformation.OSDescription, output, StringComparison.Ordinal);
        Assert.Contains($"OS Architecture: {RuntimeInformation.OSArchitecture}", output, StringComparison.Ordinal);
        Assert.Contains($"Process Architecture: {RuntimeInformation.ProcessArchitecture}", output, StringComparison.Ordinal);
        Assert.Contains($"Locale: {CultureInfo.CurrentCulture}", output, StringComparison.Ordinal);
        Assert.Contains($"UI Locale: {CultureInfo.CurrentUICulture}", output, StringComparison.Ordinal);
    }
}
