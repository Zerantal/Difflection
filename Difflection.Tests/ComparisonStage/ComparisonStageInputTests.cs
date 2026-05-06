using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Difflection.ViewModels;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests;

public sealed partial class ComparisonStageTests
{
    [AvaloniaFact]
    public async Task Side_by_side_wheel_scrolls_vertical_and_shift_scrolls_horizontal()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("reference.png", 1600, 1200));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var scrollViewer = leftPane.ActiveScrollViewer;

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();
            await TestUiSupport.WaitForAsync(() => viewModel.ZoomScale > 1.5);
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
        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png", 1600, 1200));
        await viewModel.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png", 1600, 1200));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var rightPane = TestUiSupport.GetSideBySideRightPane(stage);

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();
            await TestUiSupport.WaitForAsync(() => viewModel.ZoomScale > 1.5);

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
    public async Task Split_screen_divider_can_be_dragged()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png"));
        await viewModel.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png"));
        viewModel.SelectSplitScreenView();

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var pane = TestUiSupport.GetSplitPane(TestUiSupport.GetComparisonStage(window));
            var divider = pane.FindControl<Border>("SplitDivider") ?? throw new InvalidOperationException("SplitDivider not found.");
            var dragSurface = pane.FindControl<Border>("SplitDragSurface") ?? throw new InvalidOperationException("SplitDragSurface not found.");

            await TestUiSupport.WaitForAsync(() => divider.Bounds.Width > 0 && dragSurface.Bounds.Width > 0);

            var before = divider.Bounds.X;
            var start = dragSurface.TranslatePoint(new Point(dragSurface.Bounds.Width / 2, dragSurface.Bounds.Height / 2), window)
                ?? throw new InvalidOperationException("Could not translate drag surface point.");

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X + 120, start.Y));
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => Math.Abs(divider.Bounds.X - before) > 5);

            Assert.True(Math.Abs(divider.Bounds.X - before) > 5);

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
        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("reference.png"));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            _ = TestUiSupport.GetComparisonStage(window);

            var before = viewModel.ZoomScale;
            window.MouseWheel(new Point(500, 350), new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            await TestUiSupport.WaitForAsync(() => viewModel.ZoomScale > before);

            Assert.True(viewModel.ZoomScale > before);
        }
        finally
        {
            window.Close();
        }
    }
}
