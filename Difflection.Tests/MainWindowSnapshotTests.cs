using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Difflection.ViewModels;
using Difflection.Views;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests;

public sealed class MainWindowSnapshotTests
{
    private const double SnapshotWidth = 1100;
    private const double SnapshotHeight = 700;
    private const double SnapshotRenderScale = 1.0;

    [AvaloniaFact]
    public void Default_side_by_side_shell_matches_snapshot()
    {
        var window = CreateWindow(new MainWindowViewModel());
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
        viewModel.SelectSplitScreenView();

        var window = CreateWindow(viewModel);
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
        viewModel.TrySetZoomText("50%");

        var window = CreateWindow(viewModel);
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
    public async Task Ctrl_wheel_zoom_does_not_push_toolbar_actions_offscreen()
    {
        var viewModel = new MainWindowViewModel();
        await LoadFixtureImagesAsync(viewModel);

        var window = CreateWindow(viewModel);
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

    private static MainWindow CreateWindow(MainWindowViewModel viewModel)
    {
        var window = new MainWindow
        {
            Width = SnapshotWidth,
            Height = SnapshotHeight,
            DataContext = viewModel,
        };

        window.SetRenderScaling(SnapshotRenderScale);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void AssertSnapshot(TopLevel topLevel, string snapshotName)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        var frame = topLevel.CaptureRenderedFrame();
        Assert.NotNull(frame);
        SnapshotAssert.Matches(snapshotName, frame);
    }

    private static async Task LoadFixtureImagesAsync(MainWindowViewModel viewModel)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Difflection.Tests");
        Directory.CreateDirectory(directory);

        var referencePath = Path.Combine(directory, "reference.png");
        var candidatePath = Path.Combine(directory, "candidate.png");

        WriteFixtureImage(referencePath, new SKColor(34, 89, 165), new SKColor(249, 115, 22));
        WriteFixtureImage(candidatePath, new SKColor(92, 42, 145), new SKColor(14, 165, 233));

        await viewModel.LoadImageAsync(ImageSlot.Left, referencePath);
        await viewModel.LoadImageAsync(ImageSlot.Right, candidatePath);
    }

    private static void WriteFixtureImage(string path, SKColor background, SKColor accent)
    {
        using var bitmap = new SKBitmap(256, 160);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);

        using var fillPaint = new SKPaint
        {
            Color = accent,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        using var strokePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(210),
            IsAntialias = true,
            StrokeWidth = 8,
            Style = SKPaintStyle.Stroke,
        };

        canvas.DrawRoundRect(new SKRoundRect(new SKRect(28, 24, 228, 136), 12, 12), fillPaint);
        canvas.DrawLine(44, 116, 212, 44, strokePaint);
        canvas.DrawCircle(72, 64, 18, strokePaint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
