using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace SizeBench.AnalysisEngine.Symbols;

[TypeConverter(typeof(EnumConverter))] // This allows WPF xaml to specify flags in a comma-delimited fashion
[Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32 - using a uint32 lets us have up to 32 flags in the future, which seems desirable
public enum FunctionCodeNameFormatting : uint
#pragma warning restore CA1028 // Enum Storage should be Int32
{
    // Just the name, the barest/simplest form
    None = 0x00000000,

    // If this is a member function, include "ParentType::" in the name
    IncludeParentType = 0x00000001,

    // If this is a const/volatile function, include "const" and/or "volatile" suffixes
    IncludeCVQualifiers = 0x00000002,

    // If this function has arguments, include their types
    IncludeArgumentTypes = 0x00000004,

    // If this function has arguments, include their names (you must also specify IncludeArgumentTypes)
    IncludeArgumentNames = 0x00000008,

    // If this function is static, inculde "static" prefix.
    IncludeStatic = 0x00000010,

    // If this function is virtual or a virtual override include "virtual"/"override" prefix/suffix
    IncludeVirtualOverride = 0x00000020,

    // If this function is sealed/final, include "final" suffix
    IncludeSealed = 0x00000040,

    IncludeReturnType = 0x00000080,

    // Include everything that can lead to a unique function signature and uniquely identify overloads, except for prefixes like "static" which
    // can often make sorting by names a mess (e.g. in UI or name canonicalization)
    IncludeUniqueSignatureWithNoPrefixes = IncludeParentType | IncludeCVQualifiers | IncludeArgumentTypes,

    // Include everything that can lead to a unique function signature and uniquely identify overloads, etc.
    IncludeUniqueSignature = IncludeUniqueSignatureWithNoPrefixes | IncludeStatic,

    // Include everything possible.
    All = IncludeParentType | IncludeCVQualifiers | IncludeArgumentTypes | IncludeArgumentNames | IncludeStatic | IncludeVirtualOverride | IncludeSealed | IncludeReturnType,
}

public sealed class FunctionCodeFormattedName
{
    // We cache some of the commonly used names to avoid recalculating them.  For more rarely-used combinations of name flags,
    // just calculate it each time and cache if it shows up hot in profiles.

    private string? _includeParentType;
    public string IncludeParentType => GetCachedFormattedName(FunctionCodeNameFormatting.IncludeParentType, ref this._includeParentType);

    // This is the most complex name for a function possible, including all prefixes/suffixes ("static", "virtual", "override", "const", etc.) as well
    // as the type name if it's a member function, all parameter types, and parameter names if available.  Everything.
    private string? _all;
    public string All => GetCachedFormattedName(FunctionCodeNameFormatting.All, ref this._all);

    private string? _uniqueSignature;
    public string UniqueSignature => GetCachedFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignature, ref this._uniqueSignature);

    private string? _uniqueSignatureWithNoPrefixes;
    public string UniqueSignatureWithNoPrefixes => GetCachedFormattedName(FunctionCodeNameFormatting.IncludeUniqueSignatureWithNoPrefixes, ref this._uniqueSignatureWithNoPrefixes);

    public string GetFormattedName(FunctionCodeNameFormatting flags) =>
        GetFormattedName(flags,
                         this._functionCode.IsStatic,
                         this._functionCode.IsIntroVirtual,
                         this._functionCode.FunctionType,
                         this._functionCode.ParentType,
                         this._functionCode.FunctionName,
                         this._functionCode.ArgumentNames,
                         this._functionCode.IsVirtual,
                         this._functionCode.IsSealed);

    internal static string GetFormattedName(FunctionCodeNameFormatting flags,
                                            bool isStatic,
                                            bool isIntroVirtual,
                                            FunctionTypeSymbol? functionType,
                                            TypeSymbol? parentType,
                                            string functionName,
                                            IReadOnlyList<ParameterDataSymbol>? argumentNames,
                                            bool isVirtual,
                                            bool isSealed)
    {
        var sb = new StringBuilder(capacity: 100);

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeStatic) && isStatic)
        {
            sb.Append("static ");
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeVirtualOverride) && isIntroVirtual)
        {
            sb.Append("virtual ");
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeReturnType) && functionType != null)
        {
            sb.Append(functionType.ReturnValueType.Name);
            sb.Append(' ');
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeParentType) && parentType != null)
        {
            sb.Append(parentType.Name);
            sb.Append("::");
        }

        sb.Append(functionName);

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeArgumentTypes))
        {
            sb.Append('(');

            if (functionType?.ArgumentTypes != null)
            {
                for (var argumentIndex = 0; argumentIndex < functionType.ArgumentTypes.Count; argumentIndex++)
                {
                    // Separate arguments with a comma and a space
                    if (argumentIndex > 0)
                    {
                        sb.Append(", ");
                    }

                    var argumentType = functionType.ArgumentTypes[argumentIndex];

                    sb.Append(argumentType.Name);

                    if (flags.HasFlag(FunctionCodeNameFormatting.IncludeArgumentNames) &&
                        argumentNames != null &&
                        argumentIndex < argumentNames.Count &&
                        argumentNames[argumentIndex].Type == argumentType)
                    {
                        sb.Append(CultureInfo.InvariantCulture, $" {argumentNames[argumentIndex].Name}");
                    }
                }
            }

            sb.Append(')');
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeCVQualifiers) && functionType?.IsConst == true)
        {
            sb.Append(" const");
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeCVQualifiers) && functionType?.IsVolatile == true)
        {
            sb.Append(" volatile");
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeVirtualOverride) && isVirtual && !isIntroVirtual)
        {
            sb.Append(" override");
        }

        if (flags.HasFlag(FunctionCodeNameFormatting.IncludeSealed) && isSealed)
        {
            sb.Append(" final");
        }

        //TODO: should this support "__unaligned" like some other bits that deal with functions do?

        return sb.ToString();
    }

    private string GetCachedFormattedName(FunctionCodeNameFormatting flags, ref string? cache)
    {
        if (cache is null)
        {
            cache = GetFormattedName(flags);
        }

        return cache;
    }

    private readonly IFunctionCodeSymbol _functionCode;
    internal FunctionCodeFormattedName(IFunctionCodeSymbol functionCode)
    {
        this._functionCode = functionCode;
    }
}
