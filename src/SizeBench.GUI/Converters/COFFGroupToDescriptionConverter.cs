using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

// IValueConverter doesn't support Nullable Reference Types in WPF yet, so need to disable for this file - it's valid
// to return null, but the return type of Convert is "object" instead of "object?"
#nullable disable

namespace SizeBench.GUI.Converters;

public sealed class COFFGroupToDescriptionConverter : IValueConverter
{
    private static readonly Dictionary<string, string> coffGroupDescriptions = new Dictionary<string, string>()
        {
            { ".bss$zz", "Zero-initialized read/write data that is 'dead' to PGO" },

            { ".CRT$XCA", "First C++ Initializer" },
            { ".CRT$XCAA", "Startup C++ Initializer" },
            { ".CRT$XCU", "Debug code masquerading as CRT code" },
            { ".CRT$XCZ", "Last C++ Initializer" },

            { ".CRT$XDA", "First Dynamic TLS Initializer" },
            { ".CRT$XDZ", "Last Dynamic TLS Initializer" },

            { ".CRT$XIA", "First C Initializer" },
            { ".CRT$XIAA", "Startup C Initializer" },
            { ".CRT$XIC", "CRT C Initializers" },
            { ".CRT$XIZ", "Last C Initializer" },

            { ".CRT$XLA", "First Loader TLS Callback" },
            { ".CRT$XLC", "CRT TLS Constructor" },
            { ".CRT$XLD", "CRT TLS Terminator" },
            { ".CRT$XLZ", "Last Loader TLS Callback" },

            { ".CRT$XPA", "First Pre-Terminator" },
            { ".CRT$XPB", "CRT ConcRT (Concurrency Runime) Pre-Terminator" },
            { ".CRT$XPX", "CRT Pre-Terminators" },
            { ".CRT$XPXA", "CRT stdio Pre-Terminator" },
            { ".CRT$XPZ", "Last Pre-Terminator" },

            { ".CRT$XTA", "First Terminator" },
            { ".CRT$XTZ", "Last Terminator" },

            { ".CRTMA$XCA", "First Managed C++ Initializer" },
            { ".CRTMA$XCZ", "Last Managed C++ Initializer" },

            { ".CRTVT$XCA", "First Managed C++ VTable Initializer" },
            { ".CRTVT$XCZ", "Last Manager C++ VTable Initializer" },

            { ".data", "Other read/write data" },
            { ".data$00", "Read/write data in phase 00 of pri7" },
            { ".data$01", "Read/write data in phase 01 of pri7" },
            { ".data$d", "RTTI read/write data" },
            { ".data$pr", "PGO read - global vars PGO is sure are only read from in training" },
            { ".data$r", "RTTI for writable data" },

            { ".idata$2", "Import descriptors" },
            { ".idata$3", "Import descriptor null temrinator" },
            { ".idata$4", "Import Name Table (INT)" },
            { ".idata$5", "Import Address Table (IAT)" },
            { ".idata$6", "Import data strings" },

            { ".gfids", "CFG (Control Flow Guard) data" },

            { ".pdata", "Procedure data" },

            { ".rdata", "Other read-only data" },
            { ".rdata$00", "Read-only data in phase 00 of pri7" },
            { ".rdata$01", "Read-only data in phase 01 of pri7" },
            { ".rdata$brc", "Base reloc clustering" },
            { ".rdata$r", "RTTI read-only data" },
            { ".rdata$wbrd", "Read-only data for warbird obfuscation" },
            { ".rdata$zETW0", "Fixed-length header when using TraceLoggingWrite" },
            { ".rdata$zETW1", "One entry per TraceLoggingWrite" },
            { ".rdata$zETW2", "One entry per TRACELOGGING_DEFINE_PROVIDER" },
            { ".rdata$zETW9", "Fixed-length footer when using TraceLoggingWrite" },

            { ".rtc$IAA", "First RTC (Run-Time Checks) Initializer" },
            { ".rtc$IZZ", "Last RTC (Run-Time Checks) Initializer" },
            { ".rtc$TAA", "First RTC (Run-Time Checks) Terminator" },
            { ".rtc$TZZ", "Last RTC (Run-Time Checks) Terminator" },

            { ".text$di", "Dynamic Initializers" },
            { ".text$mn", "'Main' code - likely not using PGO" },
            { ".text$np", "Code with optimizations turned off via pragma, or assembly code - PGO cannot optimize this" },
            { ".text$x", "Exception unwinding funclets (ex: __finally blocks)" },
            { ".text$yd", "Dynamic atexit destructors" },
            { ".text$yz", "Code blocks within a function which are cold to PGO" },
            { ".text$zs", "Code that is not dead, but is rarely called" },
            { ".text$zy", "Code blocks within a function which are dead to PGO" },
            { ".text$zz", "Whole functions that are dead to PGO" },

            { ".xdata", "Exception unwinding data" },
            { ".xdata$x", "Exception unwinding data" },
        };

    private static readonly Dictionary<Regex, string> coffGroupRegexDescriptions = new Dictionary<Regex, string>()
        {
            { new Regex(@"\.bss.*"), "Zero-initialized read/write data" },

            { new Regex(@"\.data\$dk.*"), "PGO 'don't know' (global vars with only reads but not proven const)" },
            { new Regex(@"\.data\$zz.*"), "Read-write data 'dead' from PGO training" },

            { new Regex(@"\.didat.*"), "Delay-loaded Import Address Table (IAT)" },

            { new Regex(@"\.idata.*"), "Import data" },

            { new Regex(@"\.edata.*"), "Export function strings" },

            { new Regex(@"\.rdata\$zz.*"), "Read-only data 'dead' from PGO training" },

            { new Regex(@"\.rsrc.*"), "Win32 Resources" },

            { new Regex(@"\.text\$.*_pri7"), "Code executed during Pri7 PGO training" },
            { new Regex(@"\.text\$.*_clientonly"), "Code shared between client and server PGO training" },
            { new Regex(@"\.text\$.*_serveronly"), "Code executed only on server in PGO training" },
            { new Regex(@"\.text\$.*_coldboot"), "Code executed during cold boot PGO training" },
            { new Regex(@"\.text\$.*_hybridboot"), "Code executed during cold or hybrid boot PGO training" },
            { new Regex(@"\.text\$lp00.*"), "Code in \"loader phase 0\" from PGO, the hottest code from PGO training" },
            { new Regex(@"\.text\$lp01.*"), "Code in \"loader phase 1\" from PGO, the second-hottest code after phase 0 from PGO training" },
            { new Regex(@"\.text\$lp.*"), "Code in a \"loader phase\" from PGO, this is warm-to-hot depending on the phase number" },
        };

    public static COFFGroupToDescriptionConverter Instance { get; } = new COFFGroupToDescriptionConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return String.Empty;
        }

        if (value is not COFFGroup and not COFFGroupDiff)
        {
            throw new ArgumentException("must be COFFGroup or COFFGroupDiff", nameof(value));
        }

        string coffGroupName = null;
        if (value is COFFGroup)
        {
            coffGroupName = (value as COFFGroup).Name;
        }
        else if (value is COFFGroupDiff)
        {
            coffGroupName = (value as COFFGroupDiff).Name;
        }

        if (coffGroupDescriptions.TryGetValue(coffGroupName, out var description))
        {
            return description;
        }

        foreach (var regexDescriptionPair in coffGroupRegexDescriptions)
        {
            if (regexDescriptionPair.Key.IsMatch(coffGroupName))
            {
                return regexDescriptionPair.Value;
            }
        }

        return String.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
