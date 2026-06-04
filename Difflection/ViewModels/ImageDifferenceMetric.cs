using System;
using System.Globalization;
using Difflection.Infrastructure;
using Avalonia.Media.Imaging;

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

        var leftPixelBuffer = BitmapPixelCache.GetPixels(left);
        var rightPixelBuffer = BitmapPixelCache.GetPixels(right);
        var leftPixelSize = leftPixelBuffer.PixelSize;
        var rightPixelSize = rightPixelBuffer.PixelSize;
        var width = Math.Min(leftPixelSize.Width, rightPixelSize.Width);
        var height = Math.Min(leftPixelSize.Height, rightPixelSize.Height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var leftPixels = leftPixelBuffer.Pixels;
        var rightPixels = rightPixelBuffer.Pixels;
        var leftStride = leftPixelBuffer.Stride;
        var rightStride = rightPixelBuffer.Stride;
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

    private const int BytesPerPixel = 4;
}
