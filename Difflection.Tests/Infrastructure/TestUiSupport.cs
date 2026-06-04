using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Difflection.Infrastructure;
using Difflection.Models;
using Difflection.ViewModels;
using Difflection.Views;
using SkiaSharp;

namespace Difflection.Tests.Infrastructure;

internal static class TestUiSupport
{
    internal static MainWindow CreateWindow(
        MainWindowViewModel viewModel,
        double width = 1100,
        double height = 700,
        double renderScale = 1.0,
        ThemeVariant? themeVariant = null)
    {
        themeVariant ??= ThemeVariant.Dark;
        viewModel.SetThemePreference(themeVariant == ThemeVariant.Light
            ? AppThemePreference.Light
            : AppThemePreference.Dark);
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = themeVariant;
        }

        var window = new MainWindow
        {
            RequestedThemeVariant = themeVariant,
            Width = width,
            Height = height,
            DataContext = viewModel
        };

        window.SetRenderScaling(renderScale);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    internal static MainView GetMainView(Window window) =>
        window.Content as MainView ?? throw new InvalidOperationException("MainView not found.");

    internal static Difflection.Views.ComparisonStage GetComparisonStage(Window window) =>
        FindNamedControl<Difflection.Views.ComparisonStage>(GetMainView(window), "ComparisonStage");

    internal static T FindNamedControl<T>(Control root, string name)
        where T : Control
    {
        if (root is T typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{name} not found.");
    }

    internal static RuledImagePane GetSideBySideLeftPane(Difflection.Views.ComparisonStage stage) =>
        stage.FindControl<RuledImagePane>("SideBySideLeftPane") ?? throw new InvalidOperationException("SideBySideLeftPane not found.");

    internal static RuledImagePane GetSideBySideRightPane(Difflection.Views.ComparisonStage stage) =>
        stage.FindControl<RuledImagePane>("SideBySideRightPane") ?? throw new InvalidOperationException("SideBySideRightPane not found.");

    internal static RuledSplitImagePane GetSplitPane(Difflection.Views.ComparisonStage stage) =>
        stage.FindControl<RuledSplitImagePane>("SplitPane") ?? throw new InvalidOperationException("SplitPane not found.");

    internal static DataTransfer CreateTransfer(params IStorageFile[] files)
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

    internal static IStorageFile CreateStorageFile(string fileName, int width = 48, int height = 48)
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

    internal static byte[] CreatePngBytes(int width = 48, int height = 48, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color ?? SKColors.OrangeRed);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    internal static bool RulerZeroIsAlignedWithOrigin(Control originControl, PixelRuler topRuler, PixelRuler leftRuler, Visual relativeTo)
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

    internal static string CaptureFrameHash(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            throw new InvalidOperationException("Rendered frame was not captured.");
        }

        return HashFrame(frame);
    }

    internal static string HashFrame(Avalonia.Media.Imaging.Bitmap frame)
    {
        var pixelSize = frame.PixelSize;
        var stride = pixelSize.Width * 4;
        var pixels = new byte[stride * pixelSize.Height];

        using var framebuffer = new ManagedFramebuffer(pixels, pixelSize, stride);
        frame.CopyPixels(framebuffer);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }

    internal static async Task WaitForAsync(Func<bool> predicate)
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

    private static void WriteFixtureImage(string path, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private class StorageFileProxy : DispatchProxy
    {
        public string Name { get; set; } = string.Empty;

        public Uri Path { get; set; } = new("file:///tmp/placeholder.png");

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
                _ => throw new NotSupportedException($"Unexpected call to {targetMethod?.Name}.")
            };
        }
    }
}
