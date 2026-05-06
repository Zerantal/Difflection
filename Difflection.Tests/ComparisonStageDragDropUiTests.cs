using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Difflection.ViewModels;
using Difflection.Views;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests;

public sealed class ComparisonStageDragDropUiTests
{
    [AvaloniaFact]
    public async Task Split_screen_divider_starts_centered()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("left.png"));
        await viewModel.LoadImageAsync(ImageSlot.Right, CreateStorageFile("right.png"));
        viewModel.SelectSplitScreenView();

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var pane = mainView.GetVisualDescendants().OfType<RuledSplitImagePane>().FirstOrDefault()
                ?? throw new InvalidOperationException("SplitPane not found.");
            var divider = pane.FindControl<Border>("SplitDivider") ?? throw new InvalidOperationException("SplitDivider not found.");
            var surface = pane.FindControl<Grid>("Surface") ?? throw new InvalidOperationException("Surface not found.");

            await WaitForAsync(() => divider.Bounds.Width > 0 && surface.Bounds.Width > 0);

            Assert.InRange(
                Math.Abs(divider.Bounds.X - (surface.Bounds.Width * 0.5)),
                0,
                4.0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Side_by_side_wheel_scrolls_vertical_and_shift_scrolls_horizontal()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("reference.png", 1600, 1200));

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var leftPane = stage.FindControl<RuledImagePane>("SideBySideLeftPane") ?? throw new InvalidOperationException("SideBySideLeftPane not found.");
            var scrollViewer = leftPane.ActiveScrollViewer;

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();
            await WaitForAsync(() => viewModel.ZoomScale > 1.5);
            await WaitForAsync(() => scrollViewer.Extent.Width > scrollViewer.Bounds.Width || scrollViewer.Extent.Height > scrollViewer.Bounds.Height);

            var before = scrollViewer.Offset;
            window.MouseWheel(new Point(500, 350), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            await WaitForAsync(() => scrollViewer.Offset.Y > before.Y || scrollViewer.Offset.X > before.X);

            var afterVertical = scrollViewer.Offset;
            Assert.True(afterVertical.Y > before.Y || afterVertical.X > before.X);

            window.MouseWheel(new Point(500, 350), new Vector(0, -1), RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            await WaitForAsync(() => scrollViewer.Offset.X > afterVertical.X);

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
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("left.png", 1600, 1200));
        await viewModel.LoadImageAsync(ImageSlot.Right, CreateStorageFile("right.png", 1600, 1200));

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var leftPane = stage.FindControl<RuledImagePane>("SideBySideLeftPane") ?? throw new InvalidOperationException("SideBySideLeftPane not found.");
            var rightPane = stage.FindControl<RuledImagePane>("SideBySideRightPane") ?? throw new InvalidOperationException("SideBySideRightPane not found.");

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();
            await WaitForAsync(() => viewModel.ZoomScale > 1.5);

            var leftBefore = leftPane.ActiveScrollViewer.Offset;
            var rightBefore = rightPane.ActiveScrollViewer.Offset;

            window.MouseWheel(new Point(500, 350), new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() =>
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
    public async Task Side_by_side_ruler_zero_stays_aligned_with_image_origin_after_zoom()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("reference.png", 1600, 1200));

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var leftPane = stage.FindControl<RuledImagePane>("SideBySideLeftPane") ?? throw new InvalidOperationException("SideBySideLeftPane not found.");
            var transform = leftPane.FindControl<LayoutTransformControl>("Transform") ?? throw new InvalidOperationException("Transform not found.");
            var topRuler = leftPane.FindControl<PixelRuler>("TopRuler") ?? throw new InvalidOperationException("TopRuler not found.");
            var leftRuler = leftPane.FindControl<PixelRuler>("LeftRuler") ?? throw new InvalidOperationException("LeftRuler not found.");

            viewModel.TrySetZoomText("50%");
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() => RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() => RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
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
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("left.png"));
        await viewModel.LoadImageAsync(ImageSlot.Right, CreateStorageFile("right.png"));
        viewModel.SelectSplitScreenView();

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var pane = mainView.GetVisualDescendants().OfType<RuledSplitImagePane>().FirstOrDefault()
                ?? throw new InvalidOperationException("SplitPane not found.");
            var divider = pane.FindControl<Border>("SplitDivider") ?? throw new InvalidOperationException("SplitDivider not found.");
            var dragSurface = pane.FindControl<Border>("SplitDragSurface") ?? throw new InvalidOperationException("SplitDragSurface not found.");

            await WaitForAsync(() => divider.Bounds.Width > 0 && dragSurface.Bounds.Width > 0);

            var before = divider.Bounds.X;
            var start = dragSurface.TranslatePoint(new Point(dragSurface.Bounds.Width / 2, dragSurface.Bounds.Height / 2), window)
                ?? throw new InvalidOperationException("Could not translate drag surface point.");

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X + 120, start.Y));
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() => Math.Abs(divider.Bounds.X - before) > 5);

            Assert.True(Math.Abs(divider.Bounds.X - before) > 5);

            window.MouseUp(new Point(start.X + 120, start.Y), MouseButton.Left);
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
        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var leftPane = stage.FindControl<RuledImagePane>("SideBySideLeftPane") ?? throw new InvalidOperationException("SideBySideLeftPane not found.");
            var rightPane = stage.FindControl<RuledImagePane>("SideBySideRightPane");
            var layout = stage.FindControl<Grid>("SideBySideSurface") ?? throw new InvalidOperationException("SideBySideSurface not found.");
            var surface = leftPane.FindControl<Grid>("Surface") ?? throw new InvalidOperationException("Surface not found.");
            var ruler = leftPane.FindControl<PixelRuler>("TopRuler") ?? throw new InvalidOperationException("TopRuler not found.");

            await WaitForAsync(() => ruler.Bounds.Width > 0 && surface.Bounds.Width > 0 && layout.Bounds.Width > 0);

            Assert.True(leftPane.IsVisible);
            Assert.True(surface.Bounds.Width > 0);
            Assert.InRange(Math.Abs(leftPane.Bounds.Width - layout.Bounds.Width), 0, 2.1);
            Assert.True(ruler.Bounds.Width > surface.Bounds.Width);
            Assert.True(rightPane is null || !rightPane.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Side_by_side_drop_raises_drag_over_and_loads_left_image()
    {
        var viewModel = new MainWindowViewModel();
        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var surface = stage.FindControl<Border>("SideBySideDropOverlay") ?? throw new InvalidOperationException("SideBySideDropOverlay not found.");
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
            var pane = mainView.GetVisualDescendants().OfType<RuledSplitImagePane>().FirstOrDefault()
                ?? throw new InvalidOperationException("SplitPane not found.");
            var surface = pane.Content as Grid ?? throw new InvalidOperationException("Split pane root not found.");
            var transfer = CreateTransfer(CreateStorageFile("swap-left.png"), CreateStorageFile("swap-right.png"));

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await WaitForAsync(() => viewModel is { LeftFileName: "swap-left.png", RightFileName: "swap-right.png" });

            Assert.Equal("swap-left.png", viewModel.LeftFileName);
            Assert.Equal("swap-right.png", viewModel.RightFileName);
            Assert.True(viewModel.HasBothImages);
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
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("reference.png"));

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            _ = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");

            var before = viewModel.ZoomScale;
            window.MouseWheel(new Point(500, 350), new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() => viewModel.ZoomScale > before);

            Assert.True(viewModel.ZoomScale > before);
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
        await viewModel.LoadImageAsync(ImageSlot.Left, CreateStorageFile("left.png", 1600, 1200));
        await viewModel.LoadImageAsync(ImageSlot.Right, CreateStorageFile("right.png", 1600, 1200));
        viewModel.SelectSplitScreenView();

        var window = CreateWindow(viewModel);
        try
        {
            var mainView = window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");
            var stage = mainView.FindControl<ComparisonStage>("ComparisonStage") ?? throw new InvalidOperationException("ComparisonStage not found.");
            var pane = stage.FindControl<RuledSplitImagePane>("SplitPane") ?? throw new InvalidOperationException("SplitPane not found.");
            var transform = pane.FindControl<LayoutTransformControl>("Transform") ?? throw new InvalidOperationException("Transform not found.");
            var topRuler = pane.FindControl<PixelRuler>("TopRuler") ?? throw new InvalidOperationException("TopRuler not found.");
            var leftRuler = pane.FindControl<PixelRuler>("LeftRuler") ?? throw new InvalidOperationException("LeftRuler not found.");

            viewModel.TrySetZoomText("50%");
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() => RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));

            viewModel.TrySetZoomText("200%");
            Dispatcher.UIThread.RunJobs();

            await WaitForAsync(() => RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
            Assert.True(RulerZeroIsAlignedWithOrigin(transform, topRuler, leftRuler, window));
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

    private static IStorageFile CreateStorageFile(string fileName, int width = 48, int height = 48)
    {
        var path = Path.Combine(Path.GetTempPath(), "Difflection.Tests", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteFixtureImage(
            path,
            width,
            height,
            fileName.Contains("left", StringComparison.OrdinalIgnoreCase) ? SKColors.DarkSlateBlue : SKColors.OrangeRed);

        var proxy = DispatchProxy.Create<IStorageFile, StorageFileProxy>();
        // ReSharper disable once SuspiciousTypeConversion.Global
        var typed = (StorageFileProxy)proxy;
        typed.Name = fileName;
        typed.Path = new Uri(path);
        typed.FilePath = path;
        return proxy;
    }

    private static bool RulerZeroIsAlignedWithOrigin(Control originControl, PixelRuler topRuler, PixelRuler leftRuler, Visual relativeTo)
    {
        var origin = originControl.TranslatePoint(new Point(0, 0), relativeTo);
        var topZero = topRuler.TranslatePoint(new Point(topRuler.ContentOriginX, 0), relativeTo);
        var leftZero = leftRuler.TranslatePoint(new Point(0, leftRuler.ContentOriginY), relativeTo);
        if (origin is null || topZero is null || leftZero is null)
        {
            return false;
        }

        return Math.Abs(origin.Value.X - topZero.Value.X) <= 1.0 &&
            Math.Abs(origin.Value.Y - leftZero.Value.Y) <= 1.0;
    }

    private static void WriteFixtureImage(string path, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
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
