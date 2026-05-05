using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Difflection.ViewModels;
using Difflection.Views;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests;

public sealed class ComparisonStageDragDropUiTests
{
    [AvaloniaFact]
    public async Task Side_by_side_drop_raises_drag_over_and_loads_left_image()
    {
        var viewModel = new MainWindowViewModel();
        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var surface = stage.FindControl<Border>("SideBySideDropSurface") ?? throw new InvalidOperationException("SideBySideDropSurface not found.");
            var file = CreateStorageFile("reference.png");
            var transfer = CreateTransfer(file);

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await WaitForAsync(() => viewModel.HasLeftImage);

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
        var leftFile = CreateStorageFile("left.png");
        var rightFile = CreateStorageFile("right.png");
        await viewModel.LoadImageAsync(ImageSlot.Left, leftFile);
        await viewModel.LoadImageAsync(ImageSlot.Right, rightFile);
        viewModel.SelectSplitScreenView();

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var surface = stage.FindControl<Border>("StageDropSurface") ?? throw new InvalidOperationException("StageDropSurface not found.");
            var transfer = CreateTransfer(CreateStorageFile("swap-left.png"), CreateStorageFile("swap-right.png"));

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await WaitForAsync(() => viewModel.LeftFileName == "swap-left.png" && viewModel.RightFileName == "swap-right.png");

            Assert.Equal("swap-left.png", viewModel.LeftFileName);
            Assert.Equal("swap-right.png", viewModel.RightFileName);
            Assert.True(viewModel.HasBothImages);
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
            Width = 1100,
            Height = 700,
            DataContext = viewModel,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static DataTransfer CreateTransfer(params IStorageFile[] files)
    {
        var transfer = new DataTransfer();
        foreach (var file in files)
        {
            var item = new DataTransferItem();
            item.SetFile(file);
            transfer.Add(item);
        }

        return transfer;
    }

    private static IStorageFile CreateStorageFile(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "Difflection.Tests", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteFixtureImage(path, fileName.Contains("left", StringComparison.OrdinalIgnoreCase) ? SKColors.DarkSlateBlue : SKColors.OrangeRed);

        var proxy = DispatchProxy.Create<IStorageFile, StorageFileProxy>();
        var typed = (StorageFileProxy)(object)proxy;
        typed.Name = fileName;
        typed.Path = new Uri(path);
        typed.FilePath = path;
        return proxy;
    }

    private static void WriteFixtureImage(string path, SKColor color)
    {
        using var bitmap = new SKBitmap(48, 48);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Timed out waiting for UI update.");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    private class StorageFileProxy : DispatchProxy
    {
        public string Name { get; set; } = string.Empty;

        public Uri Path { get; set; } = new Uri("file:///tmp/placeholder.png");

        public string FilePath { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Name" => Name,
                "get_Path" => Path,
                "get_CanBookmark" => false,
                "GetBasicPropertiesAsync" => Task.FromResult(new StorageItemProperties()),
                "SaveBookmarkAsync" => Task.FromResult<string?>(null),
                "GetParentAsync" => Task.FromResult<IStorageFolder?>(null),
                "DeleteAsync" => Task.CompletedTask,
                "MoveAsync" => Task.FromResult<IStorageItem?>(null),
                "OpenReadAsync" => Task.FromResult<Stream>(File.OpenRead(FilePath)),
                "OpenWriteAsync" => Task.FromException<Stream>(new NotSupportedException()),
                "Dispose" => null,
                _ => throw new NotSupportedException($"Unexpected call to {targetMethod?.Name}."),
            };
        }
    }
}
