using System.Globalization;
using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders;

public abstract class KeyValueInfoProvider : IErrorInfoProvider
{
    private readonly string _title;

    internal KeyValueInfoProvider(string title)
    {
        this._title = title;
    }

    public void AddErrorInfo(StringBuilder body)
    {
        ArgumentNullException.ThrowIfNull(body);

        body.Append(this._title);
        body.Append(Environment.NewLine);

        foreach (var entry in GetEntries())
        {
            body.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}", entry.Key, entry.Value);
            body.Append(Environment.NewLine);
        }
    }

    public abstract Dictionary<string, string> GetEntries();
}
