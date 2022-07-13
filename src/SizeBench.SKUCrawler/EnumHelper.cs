using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SizeBench.SKUCrawler;

internal static class EnumHelper<T>
    where T : struct, Enum
{
    public static IList<T> GetValues()
    {
        var enumValues = new List<T>();

        foreach (var fi in typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            enumValues.Add((T)Enum.Parse(typeof(T), fi.Name, false));
        }
        return enumValues;
    }

    public static T Parse(string value) => (T)Enum.Parse(typeof(T), value, true);

    public static IList<string> GetNames() => typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public).Select(fi => fi.Name).ToList();

    public static IList<string> GetDisplayValues() => GetNames().Select(obj => GetDisplayValue(Parse(obj))).ToList();

    public static string GetDisplayValue(T value)
    {
        var fieldInfo = typeof(T).GetField(value.ToString());

        if (fieldInfo!.GetCustomAttributes(typeof(DisplayAttribute), false) is not DisplayAttribute[] displayAttributes ||
            displayAttributes.Length == 0)
        {
            return value.ToString();
        }

        var displayAttribute = displayAttributes[0];
        return displayAttribute.Name ?? value.ToString();
    }
}
