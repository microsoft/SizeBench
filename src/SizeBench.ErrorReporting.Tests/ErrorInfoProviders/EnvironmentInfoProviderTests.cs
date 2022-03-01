using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders.Tests;

[TestClass]
public sealed class EnvironmentInfoProviderTests
{
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    [TestMethod]
    public void NullBodyThrows()
    {
        var provider = new EnvironmentInfoProvider();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type. This test is intentionally testing null.
        provider.AddErrorInfo(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [TestMethod]
    public void ImportantEnvironmentInformationIsPresent()
    {
        var provider = new EnvironmentInfoProvider();
        var body = new StringBuilder();
        provider.AddErrorInfo(body);
        var output = body.ToString();

        StringAssert.Contains(output, RuntimeInformation.FrameworkDescription, StringComparison.Ordinal);
        StringAssert.Contains(output, RuntimeInformation.OSDescription, StringComparison.Ordinal);
        StringAssert.Contains(output, $"OS Architecture: {RuntimeInformation.OSArchitecture}", StringComparison.Ordinal);
        StringAssert.Contains(output, $"Process Architecture: {RuntimeInformation.ProcessArchitecture}", StringComparison.Ordinal);
        StringAssert.Contains(output, $"Locale: {CultureInfo.CurrentCulture}", StringComparison.Ordinal);
        StringAssert.Contains(output, $"UI Locale: {CultureInfo.CurrentUICulture}", StringComparison.Ordinal);
    }
}
