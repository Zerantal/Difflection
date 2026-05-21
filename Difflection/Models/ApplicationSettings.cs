namespace Difflection.Models;

public enum AppThemePreference
{
    SyncWithOs,
    Light,
    Dark
}

public sealed class ApplicationSettings
{
    public AppThemePreference ThemePreference { get; set; } = AppThemePreference.SyncWithOs;

    public bool MonitorSourceFilesForChanges { get; set; }
}
