using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Difflection.Models;
using Difflection.Monitoring;
using Difflection.Storage;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests.Monitoring;

public sealed class ProjectImageChangeMonitorTests : IDisposable
{
    private readonly string storageRootPath = Path.Combine(Path.GetTempPath(), "Difflection.Tests", Guid.NewGuid().ToString("N"));

    [AvaloniaFact]
    public async Task CapturePathChangeAsync_captures_changed_monitored_image()
    {
        var storage = new LocalFileProjectStorage(storageRootPath);
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var sourceFile = TestUiSupport.CreateStorageFile("monitor-capture.png");
        var image = await viewModel.AddImageAsync(sourceFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(image, ImageMonitoringRole.Reference, TestContext.Current.CancellationToken);
        using var watcher = new FakeImageSourceChangeWatcher();
        using var monitor = new ProjectImageChangeMonitor(watcher, new MonitoredImageVersionCapture(storage));
        MonitoredImageVersionCapturedEventArgs? captured = null;
        monitor.VersionCaptured += (_, e) => captured = e;
        monitor.Start(viewModel.Projects);

        WriteFixtureImage(sourceFile.Path.LocalPath, SKColors.CornflowerBlue);

        watcher.RaiseChanged(Path.GetFullPath(sourceFile.Path.LocalPath));

        await TestUiSupport.WaitForAsync(() => captured is not null);
        Assert.Equal(2, comparison.Images.Count);
        Assert.NotEqual(image.Id, comparison.ReferenceImageId);
        Assert.Equal(image.Id, comparison.ReferenceImage?.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.Reference, comparison.ReferenceImage?.MonitoringRole);
        Assert.Same(project, captured!.Project);
        Assert.Same(comparison, captured.Comparison);
        Assert.Same(image, captured.PreviousVersion);
        Assert.Same(project, viewModel.SelectedProject);

        var firstVersion = comparison.ReferenceImage;
        Assert.NotNull(firstVersion);
        captured = null;

        WriteFixtureImage(sourceFile.Path.LocalPath, SKColors.Firebrick);

        watcher.RaiseChanged(Path.GetFullPath(sourceFile.Path.LocalPath));

        await TestUiSupport.WaitForAsync(() => captured is not null);
        Assert.Equal(3, comparison.Images.Count);
        Assert.Equal(firstVersion!.Id, comparison.ReferenceImage?.PreviousVersionImageId);
        Assert.Same(firstVersion, captured!.PreviousVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(storageRootPath))
        {
            Directory.Delete(storageRootPath, recursive: true);
        }
    }

    private static void WriteFixtureImage(string path, SKColor color)
    {
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private sealed class FakeImageSourceChangeWatcher : IImageSourceChangeWatcher
    {
        private readonly HashSet<string> sourceIds = [];

        public event EventHandler<ImageSourceChangedEventArgs>? SourceChanged;

        public void Watch(IEnumerable<ImageSourceWatch> sources)
        {
            sourceIds.Clear();

            foreach (var source in sources)
            {
                sourceIds.Add(source.SourceId);
            }
        }

        public void Stop()
        {
            sourceIds.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        public void RaiseChanged(string sourceId)
        {
            if (sourceIds.Contains(sourceId))
            {
                SourceChanged?.Invoke(this, new ImageSourceChangedEventArgs(sourceId));
            }
        }
    }
}
