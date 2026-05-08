using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Xunit;

namespace Difflection.Tests.ComparisonStage;

public sealed partial class ComparisonStageTests
{
    [AvaloniaFact]
    public async Task Side_by_side_wheel_scrolls_vertical_and_shift_scrolls_horizontal()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("reference.png", 1600, 1200));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var scrollViewer = leftPane.ActiveScrollViewer;

            viewModel.ToolState.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();
            await TestUiSupport.WaitForAsync(() => viewModel.ToolState.ZoomScale > 1.5);
            await TestUiSupport.WaitForAsync(() => scrollViewer.Extent.Width > scrollViewer.Bounds.Width || scrollViewer.Extent.Height > scrollViewer.Bounds.Height);

            var before = scrollViewer.Offset;
            window.MouseWheel(new Point(500, 350), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            await TestUiSupport.WaitForAsync(() => scrollViewer.Offset.Y > before.Y || scrollViewer.Offset.X > before.X);

            var afterVertical = scrollViewer.Offset;
            Assert.True(afterVertical.Y > before.Y || afterVertical.X > before.X);

            window.MouseWheel(new Point(500, 350), new Vector(0, -1), RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            await TestUiSupport.WaitForAsync(() => scrollViewer.Offset.X > afterVertical.X);

            Assert.True(scrollViewer.Offset.X > afterVertical.X);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Side_by_side_ctrl_zoom_keeps_both_panes_anchored()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png", 1600, 1200));
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png", 1600, 1200));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var rightPane = TestUiSupport.GetSideBySideRightPane(stage);

            viewModel.ToolState.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();
            await TestUiSupport.WaitForAsync(() => viewModel.ToolState.ZoomScale > 1.5);

            var leftBefore = leftPane.ActiveScrollViewer.Offset;
            var rightBefore = rightPane.ActiveScrollViewer.Offset;

            window.MouseWheel(new Point(500, 350), new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() =>
                leftPane.ActiveScrollViewer.Offset != leftBefore ||
                rightPane.ActiveScrollViewer.Offset != rightBefore);

            var leftAfter = leftPane.ActiveScrollViewer.Offset;
            var rightAfter = rightPane.ActiveScrollViewer.Offset;

            Assert.True(leftAfter.X > leftBefore.X || leftAfter.Y > leftBefore.Y);
            Assert.True(rightAfter.X > rightBefore.X || rightAfter.Y > rightBefore.Y);
            Assert.InRange(Math.Abs(leftAfter.X - rightAfter.X), 0, 1.0);
            Assert.InRange(Math.Abs(leftAfter.Y - rightAfter.Y), 0, 1.0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Side_by_side_fit_zoom_uses_the_larger_loaded_image_dimensions()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png", 1600, 500));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var scrollViewer = leftPane.ActiveScrollViewer;

            await TestUiSupport.WaitForAsync(() => scrollViewer.Bounds is { Width: > 0, Height: > 0 } && viewModel.ToolState.ZoomScale > 0);

            var leftOnlyZoom = viewModel.ToolState.ZoomScale;

            await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png", 800, 1200));
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() =>
            {
                var targetWidth = Math.Max(viewModel.ComparisonDisplay.LeftImageWidth, viewModel.ComparisonDisplay.RightImageWidth);
                var targetHeight = Math.Max(viewModel.ComparisonDisplay.LeftImageHeight, viewModel.ComparisonDisplay.RightImageHeight);
                var expectedZoom = Math.Min(
                    scrollViewer.Bounds.Width / Math.Max(1, targetWidth),
                    scrollViewer.Bounds.Height / Math.Max(1, targetHeight));
                return Math.Abs(viewModel.ToolState.ZoomScale - expectedZoom) < 0.01;
            });

            var targetWidth = Math.Max(viewModel.ComparisonDisplay.LeftImageWidth, viewModel.ComparisonDisplay.RightImageWidth);
            var targetHeight = Math.Max(viewModel.ComparisonDisplay.LeftImageHeight, viewModel.ComparisonDisplay.RightImageHeight);
            var expectedZoom = Math.Min(
                scrollViewer.Bounds.Width / Math.Max(1, targetWidth),
                scrollViewer.Bounds.Height / Math.Max(1, targetHeight));

            Assert.InRange(Math.Abs(viewModel.ToolState.ZoomScale - expectedZoom), 0, 0.01);
            Assert.NotEqual(leftOnlyZoom, viewModel.ToolState.ZoomScale);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Split_screen_divider_can_be_dragged()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png"));
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png"));
        viewModel.ToolState.SelectSplitScreenView();

        var window = TestUiSupport.CreateWindow(viewModel, height: 900);
        try
        {
            var pane = TestUiSupport.GetSplitPane(TestUiSupport.GetComparisonStage(window));
            var divider = pane.FindControl<Border>("SplitDivider") ?? throw new InvalidOperationException("SplitDivider not found.");
            var dragSurface = pane.FindControl<Border>("SplitDragSurface") ?? throw new InvalidOperationException("SplitDragSurface not found.");
            var initialSplitText = viewModel.ToolState.SplitPercentageText;

            await TestUiSupport.WaitForAsync(() => divider.Bounds.Width > 0
                && dragSurface.Bounds.Width > 0
                && dragSurface.Bounds.Height > 0);

            var before = divider.Bounds.X;
            var start = dragSurface.TranslatePoint(new Point(dragSurface.Bounds.Width / 2, dragSurface.Bounds.Height / 2), window)
                ?? throw new InvalidOperationException("Could not translate drag surface point.");

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X + 120, start.Y));
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => Math.Abs(divider.Bounds.X - before) > 5);
            await TestUiSupport.WaitForAsync(() => viewModel.ToolState.SplitPercentageText != initialSplitText);

            Assert.True(Math.Abs(divider.Bounds.X - before) > 5);
            Assert.NotEqual(initialSplitText, viewModel.ToolState.SplitPercentageText);

            var parts = viewModel.ToolState.SplitPercentageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, parts.Length);
            Assert.True(int.TryParse(parts[0], out var leftPercent));
            Assert.True(int.TryParse(parts[2], out var rightPercent));
            Assert.Equal(100, leftPercent + rightPercent);

            window.MouseUp(new Point(start.X + 120, start.Y), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Ctrl_wheel_zoom_stays_applied()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("reference.png"));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            _ = TestUiSupport.GetComparisonStage(window);

            var before = viewModel.ToolState.ZoomScale;
            window.MouseWheel(new Point(500, 350), new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => viewModel.ToolState.ZoomScale > before);

            Assert.True(viewModel.ToolState.ZoomScale > before);
        }
        finally
        {
            window.Close();
        }
    }
}
