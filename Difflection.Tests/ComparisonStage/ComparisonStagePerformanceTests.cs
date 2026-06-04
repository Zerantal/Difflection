using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Difflection.Infrastructure;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests.ComparisonStage;

public sealed class ComparisonStagePerformanceTests
{
    private static readonly TimeSpan MaximumOpacitySweepDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumOpacityStepDuration = TimeSpan.FromMilliseconds(100);
    private const int OpacityAdjustmentCount = 100;
    private const int MinimumDistinctDiffFrames = 90;

    [AvaloniaFact]
    [Trait("Category", "Performance")]
    public async Task Difference_view_opacity_sweep_completes_with_expected_rendered_frames()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(
            ImageSlot.Left,
            TestUiSupport.CreateStorageFile("perf-left.png", 512, 384));
        await viewModel.ComparisonDisplay.LoadImageAsync(
            ImageSlot.Right,
            TestUiSupport.CreateStorageFile("perf-right.png", 512, 384));

        var window = TestUiSupport.CreateWindow(viewModel, width: 1100, height: 760);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var differencePane = stage.FindControl<RuledImagePane>("DifferencePane")
                ?? throw new InvalidOperationException("DifferencePane not found.");
            var toolbar = TestUiSupport.FindNamedControl<TopToolbar>(TestUiSupport.GetMainView(window), "TopToolbarHost");
            var opacitySlider = toolbar.FindControl<Slider>("DifferenceOpacitySlider")
                ?? throw new InvalidOperationException("DifferenceOpacitySlider not found.");

            viewModel.ToolState.SelectDifferenceView();
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() =>
                viewModel.ToolState.IsDifferenceView
                && differencePane.IsVisible
                && viewModel.ComparisonDisplay.DifferenceImage is not null
                && opacitySlider.IsVisible);

            var distinctFrameHashes = new HashSet<string>(StringComparer.Ordinal);
            var stopwatch = Stopwatch.StartNew();
            var maximumStepDuration = TimeSpan.Zero;

            foreach (var opacity in CreateOpacitySweep(OpacityAdjustmentCount))
            {
                var stepStopwatch = Stopwatch.StartNew();

                opacitySlider.Value = opacity;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                distinctFrameHashes.Add(HashFrame(frame));

                stepStopwatch.Stop();
                maximumStepDuration = TimeSpan.FromTicks(Math.Max(maximumStepDuration.Ticks, stepStopwatch.Elapsed.Ticks));
            }

            stopwatch.Stop();

            Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, MaximumOpacitySweepDuration);
            Assert.InRange(maximumStepDuration, TimeSpan.Zero, MaximumOpacityStepDuration);
            Assert.True(
                distinctFrameHashes.Count >= MinimumDistinctDiffFrames,
                $"Expected at least {MinimumDistinctDiffFrames} distinct rendered diff frames, but captured {distinctFrameHashes.Count}.");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<double> CreateOpacitySweep(int count)
    {
        const double minimumOpacity = 0.05;
        const double maximumOpacity = 0.95;

        for (var index = 0; index < count; index++)
        {
            yield return minimumOpacity + (maximumOpacity - minimumOpacity) * index / Math.Max(1, count - 1);
        }
    }

    private static string HashFrame(Avalonia.Media.Imaging.Bitmap frame)
    {
        var pixelSize = frame.PixelSize;
        var stride = pixelSize.Width * 4;
        var pixels = new byte[stride * pixelSize.Height];

        using var framebuffer = new ManagedFramebuffer(pixels, pixelSize, stride);
        frame.CopyPixels(framebuffer);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
