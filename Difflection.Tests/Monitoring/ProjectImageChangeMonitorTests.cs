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
    private readonly string _storageRootPath = Path.Combine(Path.GetTempPath(), "Difflection.Tests", Guid.NewGuid().ToString("N"));

    [AvaloniaFact]
    public async Task CapturePathChangeAsync_captures_changed_monitored_image()
    {
        var storage = new LocalFileProjectStorage(_storageRootPath);
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var sourceFile = TestUiSupport.CreateStorageFile("monitor-capture.png");
        var image = await viewModel.ImageSet.AddImageAsync(sourceFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(image, ImageMonitoringRole.Baseline, TestContext.Current.CancellationToken);
        using var watcher = new FakeImageSourceChangeWatcher();
        using var monitor = new ProjectImageChangeMonitor(watcher, new MonitoredImageVersionCapture(storage));
        MonitoredImageVersionCapturedEventArgs? captured = null;
        monitor.VersionCaptured += (_, e) => captured = e;
        monitor.Start(viewModel.Workspace.Projects);

        WriteFixtureImage(sourceFile.Path.LocalPath, SKColors.CornflowerBlue);

        watcher.RaiseChanged(Path.GetFullPath(sourceFile.Path.LocalPath));

        await TestUiSupport.WaitForAsync(() => captured is not null);
        Assert.Equal(2, comparison.Images.Count);
        Assert.NotEqual(image.Id, comparison.BaselineImageId);
        Assert.Equal(image.Id, comparison.BaselineImage?.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.Baseline, comparison.BaselineImage?.MonitoringRole);
        Assert.Same(project, captured!.Project);
        Assert.Same(comparison, captured.Comparison);
        Assert.Same(image, captured.PreviousVersion);
        Assert.Same(project, viewModel.Workspace.SelectedProject);

        var firstVersion = comparison.BaselineImage;
        Assert.NotNull(firstVersion);
        captured = null;

        WriteFixtureImage(sourceFile.Path.LocalPath, SKColors.Firebrick);

        watcher.RaiseChanged(Path.GetFullPath(sourceFile.Path.LocalPath));

        await TestUiSupport.WaitForAsync(() => captured is not null);
        Assert.Equal(3, comparison.Images.Count);
        Assert.Equal(firstVersion.Id, comparison.BaselineImage?.PreviousVersionImageId);
        Assert.Same(firstVersion, captured!.PreviousVersion);
    }

    [AvaloniaFact]
    public async Task Start_uses_project_source_file_monitoring_for_current_candidate_role()
    {
        var storage = new LocalFileProjectStorage(_storageRootPath);
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync(
            TestUiSupport.CreateStorageFile("project-monitor-reference.png"),
            cancellationToken: TestContext.Current.CancellationToken);
        var candidateFile = TestUiSupport.CreateStorageFile("project-monitor-candidate.png");
        var candidate = await viewModel.ImageSet.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(candidate, ImageMonitoringRole.Candidate, TestContext.Current.CancellationToken);
        await viewModel.SetSelectedProjectSourceFileMonitoringAsync(true, TestContext.Current.CancellationToken);
        using var watcher = new FakeImageSourceChangeWatcher();
        using var monitor = new ProjectImageChangeMonitor(watcher, new MonitoredImageVersionCapture(storage));
        MonitoredImageVersionCapturedEventArgs? captured = null;
        monitor.VersionCaptured += (_, e) => captured = e;

        monitor.Start(viewModel.Workspace.Projects);
        WriteFixtureImage(candidateFile.Path.LocalPath, SKColors.CornflowerBlue);
        watcher.RaiseChanged(Path.GetFullPath(candidateFile.Path.LocalPath));

        await TestUiSupport.WaitForAsync(() => captured is not null);
        Assert.Equal(3, comparison.Images.Count);
        Assert.Same(reference, comparison.BaselineImage);
        Assert.Equal(candidate.Id, comparison.CandidateImage?.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.None, candidate.MonitoringRole);
        Assert.Equal(ImageMonitoringRole.Candidate, comparison.CandidateImage?.MonitoringRole);
        Assert.Same(candidate, captured!.PreviousVersion);
    }

    [AvaloniaFact]
    public async Task Start_uses_project_source_file_monitoring_for_owner_comparison_not_selected_comparison()
    {
        var storage = new LocalFileProjectStorage(_storageRootPath);
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var first = await viewModel.Workspace.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        await viewModel.ImageSet.AddImageAsync(
            TestUiSupport.CreateStorageFile("project-monitor-first.png"),
            cancellationToken: TestContext.Current.CancellationToken);
        var second = await viewModel.Workspace.AddComparisonAsync("Second", TestContext.Current.CancellationToken);
        var secondFile = TestUiSupport.CreateStorageFile("project-monitor-second.png");
        var secondReference = await viewModel.ImageSet.AddImageAsync(secondFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetSelectedProjectSourceFileMonitoringAsync(true, TestContext.Current.CancellationToken);
        viewModel.Workspace.SelectedComparison = first;
        using var watcher = new FakeImageSourceChangeWatcher();
        using var monitor = new ProjectImageChangeMonitor(watcher, new MonitoredImageVersionCapture(storage));
        MonitoredImageVersionCapturedEventArgs? captured = null;
        monitor.VersionCaptured += (_, e) => captured = e;

        monitor.Start(viewModel.Workspace.Projects);
        WriteFixtureImage(secondFile.Path.LocalPath, SKColors.MediumPurple);
        watcher.RaiseChanged(Path.GetFullPath(secondFile.Path.LocalPath));

        await TestUiSupport.WaitForAsync(() => captured is not null);
        Assert.Single(first.Images);
        Assert.Equal(2, second.Images.Count);
        Assert.Equal(secondReference.Id, second.BaselineImage?.PreviousVersionImageId);
        Assert.Same(first, viewModel.Workspace.SelectedComparison);
    }

    [AvaloniaFact]
    public async Task Start_project_source_file_monitoring_watches_files_not_directories()
    {
        var storage = new LocalFileProjectStorage(_storageRootPath);
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var referenceFile = TestUiSupport.CreateStorageFile("project-monitor-watch-reference.png");
        var candidateFile = TestUiSupport.CreateStorageFile("project-monitor-watch-candidate.png");
        await viewModel.ImageSet.AddImageAsync(referenceFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.ImageSet.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetSelectedProjectSourceFileMonitoringAsync(true, TestContext.Current.CancellationToken);
        using var watcher = new FakeImageSourceChangeWatcher();
        using var monitor = new ProjectImageChangeMonitor(watcher, new MonitoredImageVersionCapture(storage));

        monitor.Start(viewModel.Workspace.Projects);

        Assert.Equal(2, watcher.Watches.Count);
        Assert.Contains(watcher.Watches, watch => string.Equals(watch.LocalPath, referenceFile.Path.LocalPath, StringComparison.Ordinal));
        Assert.Contains(watcher.Watches, watch => string.Equals(watch.LocalPath, candidateFile.Path.LocalPath, StringComparison.Ordinal));
        Assert.DoesNotContain(watcher.Watches, watch => Directory.Exists(watch.LocalPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRootPath))
        {
            Directory.Delete(_storageRootPath, recursive: true);
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
        private readonly HashSet<string> _sourceIds = [];

        public event EventHandler<ImageSourceChangedEventArgs>? SourceChanged;

        public List<ImageSourceWatch> Watches { get; } = [];

        public void Watch(IEnumerable<ImageSourceWatch> sources)
        {
            _sourceIds.Clear();
            Watches.Clear();

            foreach (var source in sources)
            {
                Watches.Add(source);
                _sourceIds.Add(source.SourceId);
            }
        }

        public void Stop()
        {
            _sourceIds.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        public void RaiseChanged(string sourceId)
        {
            if (_sourceIds.Contains(sourceId))
            {
                SourceChanged?.Invoke(this, new ImageSourceChangedEventArgs(sourceId));
            }
        }
    }
}
