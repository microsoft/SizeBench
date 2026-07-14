using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SizeBench.GUI.Settings;

internal sealed class AppSettings : IAppSettings
{
    private const string DefaultPublicMsftSymbolServer = "srv*https://msdl.microsoft.com/download/symbols";

    private readonly string _filePath;
    private readonly PersistedShape _data;

    public AppSettings() : this(DefaultSettingsFilePath()) { }

    internal AppSettings(string filePath)
    {
        this._filePath = filePath;
        this._data = Load(filePath);
    }

    public bool UseSymbolServer
    {
        get => this._data.UseSymbolServer;
        set
        {
            if (this._data.UseSymbolServer != value)
            {
                this._data.UseSymbolServer = value;
                Save();
            }
        }
    }

    public IList<string> SymbolServerPaths => this._data.SymbolServerPaths;

    public void SetSymbolServerPaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        this._data.SymbolServerPaths = paths.Where(p => !String.IsNullOrWhiteSpace(p))
                                            .Select(p => p.Trim())
                                            .ToList();
        Save();
    }

    public string BuildSymbolSearchPath()
        => String.Join(";", this._data.SymbolServerPaths.Where(p => !String.IsNullOrWhiteSpace(p)));

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(this._filePath);
            if (!String.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(this._data, SerializerOptions);
            File.WriteAllText(this._filePath, json);
        }
#pragma warning disable CA1031 // Do not catch general exception types - settings persistence failures should not crash the app
        catch
        {
            // Best-effort persistence: if we can't save settings we still want the app to keep working.
        }
#pragma warning restore CA1031
    }

    internal static string DefaultSettingsFilePath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SizeBench",
                        "settings.json");

    private static PersistedShape Load(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<PersistedShape>(json, SerializerOptions);
                if (loaded != null)
                {
                    loaded.SymbolServerPaths ??= new List<string>();
                    return loaded;
                }
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types - corrupt settings should fall back to defaults
        catch
        {
            // Fall through to defaults on any read/parse failure
        }
#pragma warning restore CA1031

        return new PersistedShape
        {
            UseSymbolServer = false,
            SymbolServerPaths = new List<string> { DefaultPublicMsftSymbolServer },
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal sealed class PersistedShape
    {
        public bool UseSymbolServer { get; set; }
        public List<string> SymbolServerPaths { get; set; } = new();
    }
}
