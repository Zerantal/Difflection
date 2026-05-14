using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;

namespace Difflection.Monitoring;

public sealed class ProjectImageChangeMonitor : IDisposable
{
    private readonly IImageSourceChangeWatcher _watcher;
    private readonly MonitoredImageVersionCapture _versionCapture;
    private readonly Dictionary<string, List<MonitoredImage>> _monitoredImagesBySourceId = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _captureLock = new(1, 1);

    public ProjectImageChangeMonitor(
        IImageSourceChangeWatcher watcher,
        MonitoredImageVersionCapture versionCapture)
    {
        this._watcher = watcher;
        this._versionCapture = versionCapture;
        this._watcher.SourceChanged += Watcher_OnSourceChanged;
    }

    public event EventHandler<MonitoredImageVersionCapturedEventArgs>? VersionCaptured;

    public void Start(IEnumerable<Project> projects)
    {
        _watcher.Stop();
        _monitoredImagesBySourceId.Clear();

        foreach (var project in projects)
        {
            foreach (var comparison in project.Comparisons)
            {
                foreach (var image in comparison.Images.Where(IsMonitorable))
                {
                    var sourceId = CreateSourceId(image);

                    if (!_monitoredImagesBySourceId.TryGetValue(sourceId, out var images))
                    {
                        images = [];
                        _monitoredImagesBySourceId[sourceId] = images;
                    }

                    images.Add(new MonitoredImage(project, comparison, image));
                }
            }
        }

        _watcher.Watch(_monitoredImagesBySourceId
            .Select(pair => new ImageSourceWatch(pair.Key, pair.Value[0].Image.OriginalFileMetadata?.Path)));
    }

    public void Stop()
    {
        _watcher.Stop();
        _monitoredImagesBySourceId.Clear();
    }

    public void Dispose()
    {
        _watcher.SourceChanged -= Watcher_OnSourceChanged;
        Stop();
        _watcher.Dispose();
        _captureLock.Dispose();
    }

    public async Task CaptureSourceChangeAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (!_monitoredImagesBySourceId.TryGetValue(sourceId, out var monitoredImages))
        {
            return;
        }

        await _captureLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var monitoredImage in monitoredImages.ToArray())
            {
                var version = await _versionCapture.CaptureAsync(
                    monitoredImage.Project,
                    monitoredImage.Comparison,
                    monitoredImage.Image,
                    cancellationToken);

                if (version is not null)
                {
                    VersionCaptured?.Invoke(
                        this,
                        new MonitoredImageVersionCapturedEventArgs(
                            monitoredImage.Project,
                            monitoredImage.Comparison,
                            monitoredImage.Image,
                            version));

                    ReplaceMonitoredImage(sourceId, monitoredImage, version);
                }
            }
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public Task CapturePathChangeAsync(string path, CancellationToken cancellationToken = default)
    {
        return CaptureSourceChangeAsync(CreateSourceId(path), cancellationToken);
    }

    private static bool IsMonitorable(ImageAsset image)
    {
        return image.MonitoringRole != ImageMonitoringRole.None
            && !string.IsNullOrWhiteSpace(image.OriginalFileMetadata?.Path)
            && File.Exists(image.OriginalFileMetadata.Path);
    }

    private static string CreateSourceId(ImageAsset image)
    {
        return CreateSourceId(image.OriginalFileMetadata?.Path ?? string.Empty);
    }

    private static string CreateSourceId(string path)
    {
        return Path.GetFullPath(path);
    }

    private void ReplaceMonitoredImage(string sourceId, MonitoredImage previous, ImageAsset current)
    {
        if (!_monitoredImagesBySourceId.TryGetValue(sourceId, out var monitoredImages))
        {
            return;
        }

        var index = monitoredImages.IndexOf(previous);

        if (index < 0)
        {
            return;
        }

        if (IsMonitorable(current))
        {
            monitoredImages[index] = previous with { Image = current };
        }
        else
        {
            monitoredImages.RemoveAt(index);
        }
    }

    private void Watcher_OnSourceChanged(object? sender, ImageSourceChangedEventArgs e)
    {
        _ = CaptureSourceChangeAsync(e.SourceId);
    }

    private sealed record MonitoredImage(Project Project, ComparisonSet Comparison, ImageAsset Image);
}

public sealed record MonitoredImageVersionCapturedEventArgs(
    Project Project,
    ComparisonSet Comparison,
    ImageAsset PreviousVersion,
    ImageAsset CurrentVersion);
