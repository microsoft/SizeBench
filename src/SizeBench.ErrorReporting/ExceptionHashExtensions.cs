using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SizeBench.ErrorReporting;

public static class ExceptionHashExtensions
{
    public static string Hash(this Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var stringBuilder = new StringBuilder(100);
        stringBuilder.AppendLine(ex.GetType().FullName);
        for (var innerException = ex.InnerException; innerException != null; innerException = innerException.InnerException)
        {
            stringBuilder.AppendLine(innerException.GetType().FullName);
        }
        if (ex.TargetSite != null)
        {
            stringBuilder.AppendLine(ex.TargetSite.Name);
        }

        stringBuilder.AppendLine(ex.Source);
        stringBuilder.AppendLine(ex.HelpLink);
        stringBuilder.AppendLine(ex.StackTrace);

        return CalculateMD5(stringBuilder.ToString());
    }

    private static string CalculateMD5(string textToHash)
    {
        var stringBuilder = new StringBuilder(32);
        var bytes = Encoding.ASCII.GetBytes(textToHash);
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms - this isn't used for any security purpose, just to bucket errors for diagnostics
        var array = MD5.HashData(bytes);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        for (var i = 0; i < array.Length; i++)
        {
            stringBuilder.Append(array[i].ToString("X2", CultureInfo.InvariantCulture.NumberFormat));
        }
        return stringBuilder.ToString();
    }
}
