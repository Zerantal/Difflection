using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Difflection.ViewModels;

internal sealed record ImageDifferenceMetric(
    int ComparedWidth,
    int ComparedHeight,
    int DifferentPixels,
    int TotalPixels,
    double DifferentPixelRatio,
    double RmsError)
{
    public string ToStatusText()
    {
        if (TotalPixels <= 0)
        {
            return "Difference unavailable";
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "Difference {0:0.0}% | RMS error {1:0.0} | Compared {2}x{3}",
            DifferentPixelRatio * 100,
            RmsError,
            ComparedWidth,
            ComparedHeight);
    }

    public static ImageDifferenceMetric? Compare(Bitmap? left, Bitmap? right)
    {
        if (left is null || right is null)
        {
            return null;
        }

        var width = Math.Min(left.PixelSize.Width, right.PixelSize.Width);
        var height = Math.Min(left.PixelSize.Height, right.PixelSize.Height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var leftPixels = CopyPixels(left);
        var rightPixels = CopyPixels(right);
        var leftStride = left.PixelSize.Width * BytesPerPixel;
        var rightStride = right.PixelSize.Width * BytesPerPixel;
        var differentPixels = 0;
        long totalSquaredChannelDelta = 0;

        for (var y = 0; y < height; y++)
        {
            var leftRow = y * leftStride;
            var rightRow = y * rightStride;

            for (var x = 0; x < width; x++)
            {
                var leftIndex = leftRow + (x * BytesPerPixel);
                var rightIndex = rightRow + (x * BytesPerPixel);
                var blueDelta = Math.Abs(leftPixels[leftIndex] - rightPixels[rightIndex]);
                var greenDelta = Math.Abs(leftPixels[leftIndex + 1] - rightPixels[rightIndex + 1]);
                var redDelta = Math.Abs(leftPixels[leftIndex + 2] - rightPixels[rightIndex + 2]);

                totalSquaredChannelDelta += (redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta);
                if (redDelta != 0 || greenDelta != 0 || blueDelta != 0)
                {
                    differentPixels++;
                }
            }
        }

        var totalPixels = width * height;
        var differentPixelRatio = (double)differentPixels / totalPixels;
        var rmsError = Math.Sqrt((double)totalSquaredChannelDelta / (totalPixels * 3));
        return new ImageDifferenceMetric(width, height, differentPixels, totalPixels, differentPixelRatio, rmsError);
    }

    private const int BytesPerPixel = 4;

    private static byte[] CopyPixels(Bitmap bitmap)
    {
        var stride = bitmap.PixelSize.Width * BytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelSize.Height];
        using var framebuffer = new ManagedFramebuffer(pixels, bitmap.PixelSize, stride);
        bitmap.CopyPixels(framebuffer);
        return pixels;
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
            if (_handle.IsAllocated)
            {
                _handle.Free();
                _handle = default;
            }
        }
    }
}
