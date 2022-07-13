using System.Globalization;
using System.Windows.Data;

namespace SizeBench.GUI.Converters;

public sealed class SymbolNameToFriendlyNameConverter : IValueConverter
{
    public static SymbolNameToFriendlyNameConverter Instance { get; } = new SymbolNameToFriendlyNameConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string symbolName)
        {
            throw new ArgumentException("value must be a string");
        }

        return symbolName.Replace("public: ", String.Empty, StringComparison.Ordinal)
                         .Replace("private: ", String.Empty, StringComparison.Ordinal)
                         .Replace("protected: ", String.Empty, StringComparison.Ordinal)
                         .Replace("__cdecl", String.Empty, StringComparison.Ordinal)
                         .Replace(" __ptr64", String.Empty, StringComparison.Ordinal)
                         .Replace("__ptr64", String.Empty, StringComparison.Ordinal)
                         .Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
