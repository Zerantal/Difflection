using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;

namespace Difflection.Storage;

public sealed class LocalFileApplicationSettingsStorage(string rootPath) : IApplicationSettingsStorage
{
    private const string SettingsFileName = "settings.json";
    private const string SettingsBackupFileName = "settings.json.bak";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string _rootPath = string.IsNullOrWhiteSpace(rootPath)
        ? throw new ArgumentException("A storage root path is required.", nameof(rootPath))
        : rootPath;

    public async Task<ApplicationSettings> LoadApplicationSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settingsFilePath = GetSettingsFilePath();
        if (!File.Exists(settingsFilePath))
        {
            return new ApplicationSettings();
        }

        await using var stream = File.OpenRead(settingsFilePath);
        return await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, JsonOptions, cancellationToken)
            ?? new ApplicationSettings();
    }

    public async Task SaveApplicationSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_rootPath);

        var settingsFilePath = GetSettingsFilePath();
        var temporaryFilePath = Path.Combine(_rootPath, $"{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        var backupFilePath = Path.Combine(_rootPath, SettingsBackupFileName);

        try
        {
            await using (var stream = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(settingsFilePath))
            {
                File.Replace(temporaryFilePath, settingsFilePath, backupFilePath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryFilePath, settingsFilePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    private string GetSettingsFilePath()
    {
        return Path.Combine(_rootPath, SettingsFileName);
    }
}
