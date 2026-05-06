using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Xunit;

namespace Difflection.Tests.ComparisonStage;

public sealed partial class ComparisonStageTests
{
    [AvaloniaFact]
    public async Task Side_by_side_drop_raises_drag_over_and_loads_left_image()
    {
        var viewModel = new MainWindowViewModel();
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var surface = ControlExtensions.FindControl<Border>(stage, "SideBySideDropOverlay") ?? throw new InvalidOperationException("SideBySideDropOverlay not found.");
            var file = TestUiSupport.CreateStorageFile("reference.png");
            var transfer = TestUiSupport.CreateTransfer(file);

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel.HasLeftImage);

            Assert.True(viewModel.HasLeftImage);
            Assert.Equal("reference.png", viewModel.LeftFileName);
            Assert.False(viewModel.HasRightImage);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Split_screen_drop_raises_drag_over_and_loads_both_images()
    {
        var viewModel = new MainWindowViewModel();
        var leftFile = TestUiSupport.CreateStorageFile("left.png");
        var rightFile = TestUiSupport.CreateStorageFile("right.png");
        await viewModel.LoadImageAsync(ImageSlot.Left, leftFile);
        await viewModel.LoadImageAsync(ImageSlot.Right, rightFile);
        viewModel.SelectSplitScreenView();

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var pane = TestUiSupport.GetSplitPane(stage);
            var surface = pane.Content as Grid ?? throw new InvalidOperationException("Split pane root not found.");
            var transfer = TestUiSupport.CreateTransfer(TestUiSupport.CreateStorageFile("swap-left.png"), TestUiSupport.CreateStorageFile("swap-right.png"));

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel is { LeftFileName: "swap-left.png", RightFileName: "swap-right.png" });

            Assert.Equal("swap-left.png", viewModel.LeftFileName);
            Assert.Equal("swap-right.png", viewModel.RightFileName);
            Assert.True(viewModel.HasBothImages);
        }
        finally
        {
            window.Close();
        }
    }
}
