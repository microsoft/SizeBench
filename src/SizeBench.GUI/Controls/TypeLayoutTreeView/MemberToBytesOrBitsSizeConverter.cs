using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public sealed class MemberToBitsOrBytesSuffixConverter : IValueConverter
{
    public static MemberToBitsOrBytesSuffixConverter Instance { get; } = new MemberToBitsOrBytesSuffixConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TypeLayoutItemViewModel.MemberViewModel and not TypeLayoutItemDiffViewModel.MemberDiffViewModel)
        {
            throw new ArgumentException("value is not of the right type");
        }

        if (targetType != typeof(string))
        {
            throw new ArgumentException("targetType must be string");
        }

        TypeLayoutItemMember? memberToStringify = null;
        if (value is TypeLayoutItemViewModel.MemberViewModel memVM)
        {
            memberToStringify = memVM.Member;
        }
        else if (value is TypeLayoutItemDiffViewModel.MemberDiffViewModel memDiffVM)
        {
            var memberDiff = memDiffVM.Member;
            memberToStringify = memberDiff.AfterMember ?? memberDiff.BeforeMember;
        }

        if (memberToStringify is null)
        {
            return String.Empty;
        }

        if (memberToStringify.IsBitField)
        {
            if (memberToStringify.NumberOfBits % 8 == 0)
            {
                if (memberToStringify.Size == 1)
                {
                    return $"{memberToStringify.Size.ToString("F0", CultureInfo.InvariantCulture)} byte";
                }
                else
                {
                    return $"{memberToStringify.Size.ToString("F0", CultureInfo.InvariantCulture)} bytes";
                }
            }
            else
            {
                if (memberToStringify.NumberOfBits == 1)
                {
                    return $"{memberToStringify.NumberOfBits} bit";
                }
                else
                {
                    return $"{memberToStringify.NumberOfBits} bits";
                }
            }
        }
        else
        {
            if (memberToStringify.Size == 1)
            {
                return $"{memberToStringify.Size.ToString("F0", CultureInfo.InvariantCulture)} byte";
            }
            else
            {
                return $"{memberToStringify.Size.ToString("F0", CultureInfo.InvariantCulture)} bytes";
            }
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
