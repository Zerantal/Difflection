using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests.ComparisonStage;

public sealed partial class ComparisonStageTests
{
    [AvaloniaFact]
    public async Task Split_screen_divider_starts_centered()
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
            var surface = pane.FindControl<Grid>("Surface") ?? throw new InvalidOperationException("Surface not found.");

            await TestUiSupport.WaitForAsync(() => divider.Bounds.Width > 0 && surface.Bounds.Width > 0);

            Assert.InRange(
                Math.Abs(divider.Bounds.X - surface.Bounds.Width * 0.5),
                0,
                4.0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Side_by_side_starts_with_a_single_left_pane()
    {
        var viewModel = new MainWindowViewModel();
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var leftPane = TestUiSupport.GetSideBySideLeftPane(stage);
            var rightPane = stage.FindControl<RuledImagePane>("SideBySideRightPane");
            var layout = stage.FindControl<Grid>("SideBySideSurface") ?? throw new InvalidOperationException("SideBySideSurface not found.");
            var surface = leftPane.FindControl<Grid>("Surface") ?? throw new InvalidOperationException("Surface not found.");
            var ruler = leftPane.FindControl<PixelRuler>("TopRuler") ?? throw new InvalidOperationException("TopRuler not found.");

            await TestUiSupport.WaitForAsync(() => ruler.Bounds.Width > 0 && surface.Bounds.Width > 0 && layout.Bounds.Width > 0);

            Assert.True(leftPane.IsVisible);
            Assert.True(surface.Bounds.Width > 0);
            Assert.InRange(Math.Abs(leftPane.Bounds.Width - layout.Bounds.Width), 0, 2.1);
            Assert.True(rightPane is null || !rightPane.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
