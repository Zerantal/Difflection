using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests.ComparisonStage;

public sealed class ComparisonStageRenderingTests
{
    [AvaloniaFact]
    public async Task Difference_view_controls_render_distinct_frames()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(
            ImageSlot.Left,
            TestUiSupport.CreateStorageFile("control-left.png", 512, 384));
        await viewModel.ComparisonDisplay.LoadImageAsync(
            ImageSlot.Right,
            TestUiSupport.CreateStorageFile("control-right.png", 512, 384));

        var window = TestUiSupport.CreateWindow(viewModel, width: 1100, height: 760);
        try
        {
            viewModel.ToolState.SelectDifferenceView();
            Dispatcher.UIThread.RunJobs();

            var stage = TestUiSupport.GetComparisonStage(window);
            var differencePane = stage.FindControl<RuledImagePane>("DifferencePane")
                ?? throw new InvalidOperationException("DifferencePane not found.");
            var toolbar = TestUiSupport.FindNamedControl<TopToolbar>(TestUiSupport.GetMainView(window), "TopToolbarHost");
            var baselineButton = toolbar.FindControl<Button>("DifferenceBaseBaselineButton")
                ?? throw new InvalidOperationException("DifferenceBaseBaselineButton not found.");
            var candidateButton = toolbar.FindControl<Button>("DifferenceBaseCandidateButton")
                ?? throw new InvalidOperationException("DifferenceBaseCandidateButton not found.");
            var mapButton = toolbar.FindControl<Button>("DifferenceBaseMapButton")
                ?? throw new InvalidOperationException("DifferenceBaseMapButton not found.");
            var opacitySlider = toolbar.FindControl<Slider>("DifferenceOpacitySlider")
                ?? throw new InvalidOperationException("DifferenceOpacitySlider not found.");

            await TestUiSupport.WaitForAsync(() =>
                viewModel.ToolState.IsDifferenceView
                && differencePane.IsVisible
                && viewModel.ComparisonDisplay.DifferenceImage is not null
                && opacitySlider.IsVisible);

            var initialFrame = TestUiSupport.CaptureFrameHash(window);

            opacitySlider.Value = 0.25;
            var opacityFrame = TestUiSupport.CaptureFrameHash(window);
            Assert.NotEqual(initialFrame, opacityFrame);

            baselineButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var baselineFrame = TestUiSupport.CaptureFrameHash(window);
            Assert.NotEqual(opacityFrame, baselineFrame);

            candidateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var candidateFrame = TestUiSupport.CaptureFrameHash(window);
            Assert.NotEqual(baselineFrame, candidateFrame);

            mapButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var mapFrame = TestUiSupport.CaptureFrameHash(window);
            Assert.NotEqual(candidateFrame, mapFrame);
        }
        finally
        {
            window.Close();
        }
    }
}
