using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Xunit;

namespace Difflection.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [AvaloniaFact]
    public async Task Difference_status_updates_when_both_images_are_loaded()
    {
        var viewModel = new MainWindowViewModel();

        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left-reference.png", 8, 8));

        Assert.Equal("Load two images to compare", viewModel.DifferenceStatusText);

        await viewModel.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("candidate.png", 8, 8));

        Assert.StartsWith("Difference 100.0%", viewModel.DifferenceStatusText);
        Assert.Contains("RMS error", viewModel.DifferenceStatusText);
        Assert.Contains("Compared 8x8", viewModel.DifferenceStatusText);
    }
}
