using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public sealed class MemberToBitfieldSuffixConverter : IValueConverter
{
    public static MemberToBitfieldSuffixConverter Instance { get; } = new MemberToBitfieldSuffixConverter();

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
            if (memberToStringify.NumberOfBits == 1)
            {
                return $"(bit {memberToStringify.BitStartPosition})";
            }
            else
            {
                return $"(bits {memberToStringify.BitStartPosition}-{memberToStringify.BitStartPosition + memberToStringify.NumberOfBits - 1})";
            }
        }
        else
        {
            return String.Empty;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
