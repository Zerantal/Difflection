using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;
using Difflection.Storage;
using Xunit;

namespace Difflection.Tests.Storage;

public sealed class LocalFileApplicationSettingsStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Difflection.Tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileApplicationSettingsStorage _storage;
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public LocalFileApplicationSettingsStorageTests()
    {
        _storage = new LocalFileApplicationSettingsStorage(_rootPath);
    }

    [Fact]
    public async Task SaveApplicationSettingsAsync_persists_global_settings()
    {
        var settings = new ApplicationSettings
        {
            ThemePreference = AppThemePreference.Light,
            MonitorSourceFilesForChanges = true
        };

        await _storage.SaveApplicationSettingsAsync(settings, CancellationToken);

        var loaded = await _storage.LoadApplicationSettingsAsync(CancellationToken);

        Assert.Equal(AppThemePreference.Light, loaded.ThemePreference);
        Assert.True(loaded.MonitorSourceFilesForChanges);
    }

    [Fact]
    public async Task LoadApplicationSettingsAsync_returns_defaults_when_settings_file_is_missing()
    {
        var settings = await _storage.LoadApplicationSettingsAsync(CancellationToken);

        Assert.Equal(AppThemePreference.SyncWithOs, settings.ThemePreference);
        Assert.False(settings.MonitorSourceFilesForChanges);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
