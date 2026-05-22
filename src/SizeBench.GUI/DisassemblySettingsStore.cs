using System.IO;
using System.Text.Json;
using SizeBench.Logging;

namespace SizeBench.GUI;

internal interface IDisassemblySettings
{
    int TemplateFoldabilityDisassemblyZoomPercent { get; set; }
}

internal sealed class DisassemblySettingsStore : IDisassemblySettings
{
    internal const int DefaultZoomPercent = 100;

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
    {
        WriteIndented = true
    };

    private readonly IApplicationLogger _applicationLogger;
    private readonly string _storagePath;
    private readonly PersistedDisassemblySettings _settings;

    public int TemplateFoldabilityDisassemblyZoomPercent
    {
        get => this._settings.TemplateFoldabilityDisassemblyZoomPercent;
        set
        {
            var normalizedValue = NormalizeZoomPercent(value);
            if (this._settings.TemplateFoldabilityDisassemblyZoomPercent != normalizedValue)
            {
                this._settings.TemplateFoldabilityDisassemblyZoomPercent = normalizedValue;
                PersistSettings();
            }
        }
    }

    public DisassemblySettingsStore(IApplicationLogger applicationLogger)
        : this(applicationLogger, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SizeBench", "DisassemblySettings.json"))
    {
    }

    internal DisassemblySettingsStore(IApplicationLogger applicationLogger, string storagePath)
    {
        this._applicationLogger = applicationLogger ?? throw new ArgumentNullException(nameof(applicationLogger));
        this._storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        this._settings = LoadSettings();
    }

    private PersistedDisassemblySettings LoadSettings()
    {
        if (File.Exists(this._storagePath) == false)
        {
            return new PersistedDisassemblySettings();
        }

        try
        {
            using var stream = File.OpenRead(this._storagePath);
            var settings = JsonSerializer.Deserialize<PersistedDisassemblySettings>(stream, SerializerOptions);
            if (settings is null)
            {
                return new PersistedDisassemblySettings();
            }

            settings.TemplateFoldabilityDisassemblyZoomPercent = NormalizeZoomPercent(settings.TemplateFoldabilityDisassemblyZoomPercent);
            return settings;
        }
        catch (JsonException ex)
        {
            this._applicationLogger.LogException("Unable to deserialize disassembly settings.", ex);
            return new PersistedDisassemblySettings();
        }
        catch (IOException ex)
        {
            this._applicationLogger.LogException("Unable to read disassembly settings.", ex);
            return new PersistedDisassemblySettings();
        }
        catch (UnauthorizedAccessException ex)
        {
            this._applicationLogger.LogException("Unable to read disassembly settings.", ex);
            return new PersistedDisassemblySettings();
        }
    }

    private void PersistSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(this._storagePath);
            if (String.IsNullOrEmpty(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Create(this._storagePath);
            JsonSerializer.Serialize(stream, this._settings, SerializerOptions);
        }
        catch (IOException ex)
        {
            this._applicationLogger.LogException("Unable to persist disassembly settings.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            this._applicationLogger.LogException("Unable to persist disassembly settings.", ex);
        }
    }

    private static int NormalizeZoomPercent(int zoomPercent)
    {
        var clamped = Math.Clamp(zoomPercent, 0, 200);
        return clamped % 20 == 0 ? clamped : DefaultZoomPercent;
    }

    private sealed class PersistedDisassemblySettings
    {
        public int TemplateFoldabilityDisassemblyZoomPercent { get; set; } = DefaultZoomPercent;
    }
}
