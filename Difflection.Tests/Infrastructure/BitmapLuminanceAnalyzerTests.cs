using System.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Difflection.Infrastructure;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests.Infrastructure;

public sealed class BitmapLuminanceAnalyzerTests
{
    [AvaloniaFact]
    public void GetAverageLuminance_returns_low_value_for_dark_bitmap()
    {
        using var bitmap = CreateBitmap(SKColors.Black);

        var luminance = BitmapLuminanceAnalyzer.GetAverageLuminance(bitmap);

        Assert.InRange(luminance, 0.0, 0.01);
    }

    [AvaloniaFact]
    public void GetAverageLuminance_returns_high_value_for_light_bitmap()
    {
        using var bitmap = CreateBitmap(SKColors.White);

        var luminance = BitmapLuminanceAnalyzer.GetAverageLuminance(bitmap);

        Assert.InRange(luminance, 0.99, 1.0);
    }

    private static Bitmap CreateBitmap(SKColor color)
    {
        using var skBitmap = new SKBitmap(16, 16);
        using var canvas = new SKCanvas(skBitmap);
        canvas.Clear(color);

        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new Bitmap(new MemoryStream(data.ToArray()));
    }
}
