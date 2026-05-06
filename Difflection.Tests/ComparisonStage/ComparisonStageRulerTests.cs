using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Difflection.ViewModels;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests;

public sealed partial class ComparisonStageTests
{
    [AvaloniaFact]
    public async Task Side_by_side_ruler_zero_stays_aligned_with_image_origin_after_zoom()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("reference.png", 1600, 1200));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var transform = leftPane.FindControl<LayoutTransformControl>("Transform") ?? throw new InvalidOperationException("Transform not found.");
            var topRuler = leftPane.FindControl<PixelRuler>("TopRuler") ?? throw new InvalidOperationException("TopRuler not found.");
            var leftRuler = leftPane.FindControl<PixelRuler>("LeftRuler") ?? throw new InvalidOperationException("LeftRuler not found.");

            viewModel.TrySetZoomText("50%");
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Split_screen_ruler_zero_stays_aligned_with_image_origin_after_zoom()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png", 1600, 1200));
        await viewModel.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png", 1600, 1200));
        viewModel.SelectSplitScreenView();

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var pane = TestUiSupport.GetSplitPane(stage);
            var transform = pane.FindControl<LayoutTransformControl>("Transform") ?? throw new InvalidOperationException("Transform not found.");
            var topRuler = pane.FindControl<PixelRuler>("TopRuler") ?? throw new InvalidOperationException("TopRuler not found.");
            var leftRuler = pane.FindControl<PixelRuler>("LeftRuler") ?? throw new InvalidOperationException("LeftRuler not found.");

            viewModel.TrySetZoomText("50%");
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(TestUiSupport.RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
        }
        finally
        {
            window.Close();
        }
    }
}
