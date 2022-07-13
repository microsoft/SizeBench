using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.GUI.Converters;

public sealed class FunctionToFormattedNameConverter : IValueConverter
{
    public static FunctionToFormattedNameConverter Instance { get; } = new FunctionToFormattedNameConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IFunctionCodeSymbol functionCodeSymbol)
        {
            throw new ArgumentException($"value must be an {nameof(IFunctionCodeSymbol)}");
        }

        if (parameter is not FunctionCodeNameFormatting flags)
        {
            throw new ArgumentException($"ConverterParameter must be a {nameof(FunctionCodeNameFormatting)}");
        }

        return functionCodeSymbol.FormattedName.GetFormattedName(flags);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
