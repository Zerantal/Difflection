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

        var leftPixelSize = left.PixelSize;
        var rightPixelSize = right.PixelSize;
        var width = Math.Min(leftPixelSize.Width, rightPixelSize.Width);
        var height = Math.Min(leftPixelSize.Height, rightPixelSize.Height);
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
                var leftIndex = leftRow + x * BytesPerPixel;
                var rightIndex = rightRow + x * BytesPerPixel;
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

    public static Bitmap? CreateDifferenceBitmap(
        Bitmap? left,
        Bitmap? right,
        DifferenceBaseImage baseImage = DifferenceBaseImage.Candidate,
        double overlayOpacity = 0.75)
    {
        if (left is null || right is null)
        {
            return null;
        }

        var leftPixelSize = left.PixelSize;
        var rightPixelSize = right.PixelSize;
        var width = Math.Min(leftPixelSize.Width, rightPixelSize.Width);
        var height = Math.Min(leftPixelSize.Height, rightPixelSize.Height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var overlayOpacity255 = overlayOpacity <= 0.0 || double.IsNaN(overlayOpacity)
            ? 0
            : overlayOpacity >= 1.0
                ? 255
                : (int)(overlayOpacity * 255.0 + 0.5);
        var leftPixels = CopyPixels(left);
        var rightPixels = CopyPixels(right);
        var leftStride = leftPixelSize.Width * BytesPerPixel;
        var rightStride = rightPixelSize.Width * BytesPerPixel;

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using var framebuffer = bitmap.Lock();
        unsafe
        {
            fixed (byte* leftBase = leftPixels)
            fixed (byte* rightBase = rightPixels)
            {
                var outputBase = (byte*)framebuffer.Address;
                var outputStride = framebuffer.RowBytes;
                const int overlayBlue = 32;
                const int overlayGreen = 28;
                const int overlayRed = 255;

                byte* baseBase;
                int baseStride;
                int basePixelStep;
                var mapBase = stackalloc byte[4];
                if (baseImage == DifferenceBaseImage.Map)
                {
                    mapBase[0] = 12;
                    mapBase[1] = 12;
                    mapBase[2] = 12;
                    mapBase[3] = 255;
                    baseBase = mapBase;
                    baseStride = 0;
                    basePixelStep = 0;
                }
                else
                {
                    baseBase = baseImage == DifferenceBaseImage.Baseline ? leftBase : rightBase;
                    baseStride = baseImage == DifferenceBaseImage.Baseline ? leftStride : rightStride;
                    basePixelStep = BytesPerPixel;
                }

                for (var y = 0; y < height; y++)
                {
                    var leftPixel = leftBase + y * leftStride;
                    var rightPixel = rightBase + y * rightStride;
                    var basePixel = baseBase + y * baseStride;
                    var outputPixel = outputBase + y * outputStride;

                    for (var x = 0; x < width; x++)
                    {
                        var blueDelta = leftPixel[0] - rightPixel[0];
                        if (blueDelta < 0) blueDelta = -blueDelta;
                        var greenDelta = leftPixel[1] - rightPixel[1];
                        if (greenDelta < 0) greenDelta = -greenDelta;
                        var redDelta = leftPixel[2] - rightPixel[2];
                        if (redDelta < 0) redDelta = -redDelta;

                        var maxDelta = redDelta > greenDelta ? redDelta : greenDelta;
                        if (blueDelta > maxDelta) maxDelta = blueDelta;

                        var alpha = maxDelta == 0
                            ? 0
                            : (overlayOpacity255 * ((35 * 255) + (65 * maxDelta)) + 12_750) / 25_500;
                        var inverseAlpha = 255 - alpha;

                        outputPixel[0] = (byte)((basePixel[0] * inverseAlpha + overlayBlue * alpha + 127) / 255);
                        outputPixel[1] = (byte)((basePixel[1] * inverseAlpha + overlayGreen * alpha + 127) / 255);
                        outputPixel[2] = (byte)((basePixel[2] * inverseAlpha + overlayRed * alpha + 127) / 255);
                        outputPixel[3] = 255;

                        leftPixel += BytesPerPixel;
                        rightPixel += BytesPerPixel;
                        basePixel += basePixelStep;
                        outputPixel += BytesPerPixel;
                    }
                }
            }
        }

        return bitmap;
    }

    private const int BytesPerPixel = 4;

    private static byte[] CopyPixels(Bitmap bitmap)
    {
        var pixelSize = bitmap.PixelSize;
        var stride = pixelSize.Width * BytesPerPixel;
        var pixels = new byte[stride * pixelSize.Height];
        using var framebuffer = new ManagedFramebuffer(pixels, pixelSize, stride);
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
            if (!_handle.IsAllocated) return;
            _handle.Free();
            _handle = default;
        }
    }
}
