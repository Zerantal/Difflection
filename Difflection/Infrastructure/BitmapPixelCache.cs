using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Difflection.Infrastructure;

public static class BitmapPixelCache
{
    private const int BytesPerPixel = 4;
    private static readonly ConditionalWeakTable<Bitmap, CachedBitmapPixels> PixelCache = new();

    public static CachedBitmapPixels GetPixels(Bitmap bitmap)
    {
        return PixelCache.GetValue(bitmap, static key => CopyPixels(key));
    }

    private static CachedBitmapPixels CopyPixels(Bitmap bitmap)
    {
        var pixelSize = bitmap.PixelSize;
        var stride = pixelSize.Width * BytesPerPixel;
        var pixels = new byte[stride * pixelSize.Height];
        using var framebuffer = new ManagedFramebuffer(pixels, pixelSize, stride);
        bitmap.CopyPixels(framebuffer);
        return new CachedBitmapPixels(pixels, pixelSize, stride);
    }
}

public sealed class CachedBitmapPixels(byte[] pixels, PixelSize pixelSize, int stride)
{
    public byte[] Pixels { get; } = pixels;

    public PixelSize PixelSize { get; } = pixelSize;

    public int Stride { get; } = stride;
}
