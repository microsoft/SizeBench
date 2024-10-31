using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Windows;

namespace SizeBench.GUI.Windows;

[ExcludeFromCodeCoverage]
public partial class AboutBox : Window
{
    public AboutBox(Window owner)
    {
        InitializeComponent();
        this.Owner = owner;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        this.HideMinimizeAndMaximizeFromTitleBar();
        base.OnSourceInitialized(e);
    }

    /// <summary>
    /// Gets the specified property value either from a specific attribute, or from a resource dictionary.
    /// </summary>
    /// <typeparam name="T">Attribute type that we're trying to retrieve.</typeparam>
    /// <param name="propertyName">Property name to use on the attribute.</param>
    /// <returns>The resulting string to use for a property.
    /// Returns null if no data could be retrieved.</returns>
    private static string CalculatePropertyValue<T>(string propertyName)
    {
        var result = String.Empty;
        // first, try to get the property value from an attribute.
        var attributes = Assembly.GetEntryAssembly()!.GetCustomAttributes(typeof(T), false);
        if (attributes.Length > 0)
        {
            var attrib = (T)attributes[0];
            var property = attrib.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                result = (property.GetValue(attributes[0], null) as string) ?? String.Empty;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the title property, which is display in the About dialogs window title.
    /// </summary>
    public static string ProductTitle
    {
        get
        {
            var result = CalculatePropertyValue<AssemblyTitleAttribute>(nameof(AssemblyTitleAttribute.Title));
            if (String.IsNullOrEmpty(result))
            {
                // otherwise, just get the name of the assembly itself.
                result = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location) ?? String.Empty;
            }
            return result;
        }
    }

    /// <summary>
    /// Gets the application's version information to show.
    /// </summary>
    public static string Version
    {
        get
        {
            var result = String.Empty;
            // first, try to get the version string from the assembly.
            var version = Assembly.GetEntryAssembly()!.GetName().Version;
            if (version != null)
            {
                result = version.ToString();
            }
            return result;
        }
    }

    public static string InformationalVersion
    {
        get
        {
            var informationalVersion = CalculatePropertyValue<AssemblyInformationalVersionAttribute>(nameof(AssemblyInformationalVersionAttribute.InformationalVersion)).Split("+");
            if (informationalVersion.Length < 2)
            {
                return String.Empty;
            }
            else
            {
                return $"(git commit: {informationalVersion[1]})";
            }
        }
    }

    public static string VersionAndInformationalVersion
        => $"{Version} {InformationalVersion}";

    /// <summary>
    /// Gets the description about the application.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - this needs to be non-static to data bind to it, and doing it as an x:Static causes a strange blank line to appear in the text box.
    public string Description => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "CONTRIBUTORS"));
#pragma warning restore CA1822 // Mark members as static

    /// <summary>
    ///  Gets the product's full name.
    /// </summary>
    public static string Product => CalculatePropertyValue<AssemblyProductAttribute>(nameof(AssemblyProductAttribute.Product));

    /// <summary>
    /// Gets the copyright information for the product.
    /// </summary>
    public static string Copyright => CalculatePropertyValue<AssemblyCopyrightAttribute>(nameof(AssemblyCopyrightAttribute.Copyright));

    /// <summary>
    /// Gets the product's company name.
    /// </summary>
    public static string Company => CalculatePropertyValue<AssemblyCompanyAttribute>(nameof(AssemblyCompanyAttribute.Company));
}
