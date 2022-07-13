using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

// IValueConverter doesn't support Nullable Reference Types in WPF yet, so need to disable for this file - it's valid
// to return null, but the return type of Convert is "object" instead of "object?"
#nullable disable

namespace SizeBench.GUI.Converters;

public sealed class BinarySectionToDescriptionConverter : IValueConverter
{
    private static readonly Dictionary<string, string> sectionDescriptions = new Dictionary<string, string>()
        {
            { ".text", "Code" },
            { ".textbss", "Edit and Continue scratch space" },
            { ".rdata", "Read-only data" },
            { ".data", "Read/write data" },
            { ".didat", "Delay-loaded Import Address Table (IAT)" },
            { ".pdata", "Procedure data" },
            { ".reloc", "Relocation data" },
            { ".rsrc", "Win32 resources" }
        };

    public static BinarySectionToDescriptionConverter Instance { get; } = new BinarySectionToDescriptionConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return String.Empty;
        }

        if (value is not BinarySection and not BinarySectionDiff)
        {
            throw new ArgumentException("must be BinarySection or BinarySectionDiff", nameof(value));
        }

        var sectionName = String.Empty;
        if (value is BinarySection binSection)
        {
            sectionName = binSection.Name;
        }
        else if (value is BinarySectionDiff binSectionDiff)
        {
            sectionName = binSectionDiff.Name;
        }

        if (sectionDescriptions.TryGetValue(sectionName, out var description))
        {
            return description;
        }
        else
        {
            return String.Empty;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
