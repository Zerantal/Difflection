using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Difflection.Infrastructure;

public static class BitmapLuminanceAnalyzer
{
    private const int BytesPerPixel = 4;

    public static double GetAverageLuminance(params IImage?[] images)
    {
        var totalLuminance = 0.0;
        var imageCount = 0;

        foreach (var image in images.OfType<Bitmap>())
        {
            totalLuminance += GetAverageLuminance(image);
            imageCount++;
        }

        return imageCount == 0 ? 0.0 : totalLuminance / imageCount;
    }

    public static double GetAverageLuminance(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
        {
            return 0.0;
        }

        var stride = bitmap.PixelSize.Width * BytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelSize.Height];
        using var framebuffer = new ManagedFramebuffer(pixels, bitmap.PixelSize, stride);
        bitmap.CopyPixels(framebuffer);

        var stepX = Math.Max(1, bitmap.PixelSize.Width / 64);
        var stepY = Math.Max(1, bitmap.PixelSize.Height / 64);
        double total = 0;
        var count = 0;

        for (var y = 0; y < bitmap.PixelSize.Height; y += stepY)
        {
            var row = y * stride;
            for (var x = 0; x < bitmap.PixelSize.Width; x += stepX)
            {
                var index = row + x * BytesPerPixel;
                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                total += ((0.2126 * red) + (0.7152 * green) + (0.0722 * blue)) / 255.0;
                count++;
            }
        }

        return count == 0 ? 0.0 : total / count;
    }

    private sealed class ManagedFramebuffer : ILockedFramebuffer
    {
        private GCHandle _handle;

        public ManagedFramebuffer(byte[] pixels, PixelSize size, int rowBytes)
        {
            _handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Address = _handle.AddrOfPinnedObject();
            Size = size;
            RowBytes = rowBytes;
        }

        public IntPtr Address { get; }

        public PixelSize Size { get; }

        public int RowBytes { get; }

        public Vector Dpi { get; } = new(96, 96);

        public PixelFormat Format => PixelFormats.Bgra8888;

        public AlphaFormat AlphaFormat => AlphaFormat.Premul;

        public void Dispose()
        {
            if (!_handle.IsAllocated) return;
            _handle.Free();
            _handle = default;
        }
    }
}
