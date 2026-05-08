using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Difflection.Models;
using Difflection.Storage;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests.UI;

public sealed class MainWindowSnapshotTests
{
    [AvaloniaFact]
    public void Default_side_by_side_shell_matches_snapshot()
    {
        var window = TestUiSupport.CreateWindow(new MainWindowViewModel());
        try
        {
            AssertSnapshot(window, "main-window-default-side-by-side");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Split_screen_with_two_images_matches_snapshot()
    {
        var viewModel = new MainWindowViewModel();
        await LoadFixtureImagesAsync(viewModel);
        viewModel.ToolState.SelectSplitScreenView();

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            AssertSnapshot(window, "main-window-split-screen-with-images");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Side_by_side_with_two_images_matches_snapshot()
    {
        var viewModel = new MainWindowViewModel();
        await LoadFixtureImagesAsync(viewModel);
        viewModel.ToolState.TrySetZoomText("50%");

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            AssertSnapshot(window, "main-window-side-by-side-with-images");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Workspace_with_project_and_no_comparisons_matches_snapshot()
    {
        var project = new Project { Name = "Client Redesign" };
        var viewModel = new MainWindowViewModel(new SnapshotProjectStorage(project));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedProject is not null);

            AssertSnapshot(window, "main-window-workspace-no-comparisons");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Workspace_with_project_and_empty_comparison_matches_snapshot()
    {
        var project = new Project { Name = "Client Redesign" };
        project.Comparisons.Add(new ComparisonSet { Name = "Homepage Header" });
        project.Comparisons.Add(new ComparisonSet { Name = "Checkout Empty State" });
        var viewModel = new MainWindowViewModel(new SnapshotProjectStorage(project));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedComparison is not null);

            AssertSnapshot(window, "main-window-workspace-empty-comparison");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Workspace_with_one_image_matches_snapshot()
    {
        var storage = new SnapshotProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Client Redesign");
        await viewModel.Workspace.AddComparisonAsync("Homepage Header");
        await AddFixtureImageAsync(viewModel, "reference.png", "Approved Header", new SKColor(34, 89, 165), new SKColor(249, 115, 22));
        await viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(viewModel.Workspace.SelectedComparison, viewModel.ProjectStorage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            AssertSnapshot(window, "main-window-workspace-one-image");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Workspace_side_by_side_with_project_image_set_matches_snapshot()
    {
        var storage = new SnapshotProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Client Redesign");
        await viewModel.Workspace.AddComparisonAsync("Homepage Header");
        await AddFixtureImageAsync(viewModel, "reference.png", "Approved Header", new SKColor(34, 89, 165), new SKColor(249, 115, 22));
        await AddFixtureImageAsync(viewModel, "candidate.png", "Current Header", new SKColor(92, 42, 145), new SKColor(14, 165, 233));
        await viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(viewModel.Workspace.SelectedComparison, viewModel.ProjectStorage);
        viewModel.ToolState.TrySetZoomText("50%");

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            AssertSnapshot(window, "main-window-workspace-side-by-side-image-set");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Narrow_workspace_with_project_image_set_matches_snapshot()
    {
        var storage = new SnapshotProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Mobile Review");
        await viewModel.Workspace.AddComparisonAsync("Receipt Screen");
        await AddFixtureImageAsync(viewModel, "reference.png", "Reference Receipt", new SKColor(34, 89, 165), new SKColor(249, 115, 22));
        await AddFixtureImageAsync(viewModel, "candidate.png", "Candidate Receipt", new SKColor(92, 42, 145), new SKColor(14, 165, 233));
        await viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(viewModel.Workspace.SelectedComparison, viewModel.ProjectStorage);

        var window = TestUiSupport.CreateWindow(viewModel, width: 820, height: 700);
        try
        {
            AssertSnapshot(window, "main-window-workspace-narrow-image-set");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Ctrl_wheel_zoom_does_not_push_toolbar_actions_offscreen()
    {
        var viewModel = new MainWindowViewModel();
        await LoadFixtureImagesAsync(viewModel);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            for (var i = 0; i < 14; i++)
            {
                window.MouseWheel(new Point(500, 350), new Vector(0, 1), RawInputModifiers.Control);
                Dispatcher.UIThread.RunJobs();
            }

            AssertSnapshot(window, "main-window-zoomed-side-by-side");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertSnapshot(TopLevel topLevel, string snapshotName)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        var frame = topLevel.CaptureRenderedFrame();
        Assert.NotNull(frame);
        SnapshotAssert.Matches(snapshotName, frame);
    }

    private static async Task AddFixtureImageAsync(
        MainWindowViewModel viewModel,
        string sourceName,
        string label,
        SKColor background,
        SKColor accent)
    {
        await using var stream = new MemoryStream(CreateFixtureImageBytes(background, accent));
        await viewModel.ImageSet.AddImageAsync(sourceName, stream, mediaType: "image/png", label: label);
    }

    private static async Task LoadFixtureImagesAsync(MainWindowViewModel viewModel)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Difflection.Tests");
        Directory.CreateDirectory(directory);

        var referencePath = Path.Combine(directory, "reference.png");
        var candidatePath = Path.Combine(directory, "candidate.png");

        WriteFixtureImage(referencePath, new SKColor(34, 89, 165), new SKColor(249, 115, 22));
        WriteFixtureImage(candidatePath, new SKColor(92, 42, 145), new SKColor(14, 165, 233));

        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, referencePath);
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, candidatePath);
    }

    private static void WriteFixtureImage(string path, SKColor background, SKColor accent)
    {
        File.WriteAllBytes(path, CreateFixtureImageBytes(background, accent));
    }

    private static byte[] CreateFixtureImageBytes(SKColor background, SKColor accent)
    {
        using var bitmap = new SKBitmap(256, 160);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);

        using var fillPaint = new SKPaint();
        fillPaint.Color = accent;
        fillPaint.IsAntialias = true;
        fillPaint.Style = SKPaintStyle.Fill;

        using var strokePaint = new SKPaint();
        strokePaint.Color = SKColors.White.WithAlpha(210);
        strokePaint.IsAntialias = true;
        strokePaint.StrokeWidth = 8;
        strokePaint.Style = SKPaintStyle.Stroke;

        canvas.DrawRoundRect(new SKRoundRect(new SKRect(28, 24, 228, 136), 12, 12), fillPaint);
        canvas.DrawLine(44, 116, 212, 44, strokePaint);
        canvas.DrawCircle(72, 64, 18, strokePaint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed class SnapshotProjectStorage(params Project[] projects) : IProjectStorage
    {
        private readonly List<Project> _projects = [..projects];
        private readonly Dictionary<Guid, byte[]> _imageContents = [];

        public Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(_projects.ToArray());
        }

        public Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_projects.FirstOrDefault(project => project.Id == projectId));
        }

        public Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default)
        {
            if (!_projects.Contains(project))
            {
                _projects.Add(project);
            }

            return Task.CompletedTask;
        }

        public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            _projects.RemoveAll(project => project.Id == projectId);
            return Task.CompletedTask;
        }

        public async Task<string> SaveImageAsync(
            Guid projectId,
            Guid comparisonSetId,
            ImageAsset image,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _imageContents[image.Id] = buffer.ToArray();
            image.StorageKey = $"snapshot/{image.Id:N}.png";
            return image.StorageKey;
        }

        public Task<Stream> LoadImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
        {
            if (!_imageContents.TryGetValue(image.Id, out var content))
            {
                throw new FileNotFoundException("Snapshot image content was not saved.", image.StorageKey);
            }

            return Task.FromResult<Stream>(new MemoryStream(content));
        }

        public Task DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
        {
            _imageContents.Remove(image.Id);
            return Task.CompletedTask;
        }
    }
}
