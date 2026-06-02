using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public sealed class TypeSymbolToDisplayTypeNameConverter : IValueConverter
{
    public static TypeSymbolToDisplayTypeNameConverter Instance { get; } = new TypeSymbolToDisplayTypeNameConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return String.Empty;
        }

        if (value is not TypeSymbol)
        {
            throw new ArgumentException("value must be a TypeSymbol");
        }

        if (targetType != typeof(string))
        {
            throw new ArgumentException("targetType must be string");
        }

        var typeSymbol = (TypeSymbol)value;

        // Functions already have their name as good as it's going to get for display purposes.
        if (typeSymbol is FunctionTypeSymbol)
        {
            return typeSymbol.Name;
        }

        var displayName = typeSymbol.Name;

        if (typeSymbol.Name.Contains("::", StringComparison.Ordinal))
        {
            // For templated types, you care about the type to the left of the first '<'
            // typeSymbol.Name.Substring(typeSymbol.Name.LastIndexOf("::",typeSymbol.Name.IndexOf("<"))+2)
            if (typeSymbol.Name.Contains('<', StringComparison.Ordinal))
            {
                var templateStartIndex = typeSymbol.Name.IndexOf('<', StringComparison.Ordinal);
                var namespaceStartIndex = typeSymbol.Name.IndexOf("::", StringComparison.Ordinal);
                if (namespaceStartIndex < templateStartIndex)
                {
                    displayName = typeSymbol.Name[(typeSymbol.Name.LastIndexOf("::", typeSymbol.Name.IndexOf('<', StringComparison.Ordinal), StringComparison.Ordinal) + "::".Length)..];
                }
            }
            else
            {
                displayName = typeSymbol.Name[(typeSymbol.Name.LastIndexOf("::", StringComparison.Ordinal) + "::".Length)..];
            }
        }

        if (displayName.Length > 45)
        {
            return String.Concat(displayName.AsSpan(0, 42), "...");
        }
        else
        {
            return displayName;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
