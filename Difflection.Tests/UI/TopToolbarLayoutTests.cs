using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests.UI;

public sealed class TopToolbarLayoutTests
{
    [AvaloniaFact]
    public async Task Narrow_side_by_side_toolbar_keeps_controls_inside_toolbar_bounds()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png"));
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png"));

        var window = TestUiSupport.CreateWindow(viewModel, width: 820, height: 700);
        try
        {
            var toolbar = TestUiSupport.FindNamedControl<TopToolbar>(TestUiSupport.GetMainView(window), "TopToolbarHost");

            await TestUiSupport.WaitForAsync(() => toolbar.Bounds.Width > 0 && toolbar.Bounds.Height > 0);

            AssertControlsInsideToolbar(
                toolbar,
                "SideBySideViewButton",
                "SplitScreenViewButton",
                "DifferenceViewButton",
                "ZoomTextBox");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Narrow_difference_toolbar_keeps_controls_inside_toolbar_bounds()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left.png"));
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("right.png"));
        viewModel.ToolState.SelectDifferenceView();

        var window = TestUiSupport.CreateWindow(viewModel, width: 820, height: 700);
        try
        {
            var toolbar = TestUiSupport.FindNamedControl<TopToolbar>(TestUiSupport.GetMainView(window), "TopToolbarHost");

            await TestUiSupport.WaitForAsync(() => toolbar.Bounds.Width > 0 && toolbar.Bounds.Height > 0);

            AssertControlsInsideToolbar(
                toolbar,
                "SideBySideViewButton",
                "SplitScreenViewButton",
                "DifferenceViewButton",
                "DifferenceBaseBaselineButton",
                "DifferenceBaseCandidateButton",
                "DifferenceBaseMapButton",
                "DifferenceOpacitySlider",
                "ZoomTextBox");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertControlsInsideToolbar(TopToolbar toolbar, params string[] controlNames)
    {
        foreach (var controlName in controlNames)
        {
            var control = toolbar.FindControl<Control>(controlName)
                ?? throw new InvalidOperationException($"{controlName} not found.");
            var position = control.TranslatePoint(new Point(0, 0), toolbar)
                ?? throw new InvalidOperationException($"{controlName} is not positioned in toolbar.");

            Assert.InRange(position.X, 0, toolbar.Bounds.Width - control.Bounds.Width + 1.0);
            Assert.InRange(position.Y, 0, toolbar.Bounds.Height - control.Bounds.Height + 1.0);
        }
    }
}
