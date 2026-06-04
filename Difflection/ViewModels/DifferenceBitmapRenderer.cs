using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Difflection.Infrastructure;

namespace Difflection.ViewModels;

internal sealed class DifferenceBitmapRenderer
{
    private const int BytesPerPixel = 4;
    private Bitmap? _left;
    private Bitmap? _right;
    private CachedBitmapPixels? _leftPixels;
    private CachedBitmapPixels? _rightPixels;
    private byte[]? _maxDeltas;
    private int _width;
    private int _height;
    private WriteableBitmap? _outputBitmap;

    public Bitmap? Render(
        Bitmap? left,
        Bitmap? right,
        DifferenceBaseImage baseImage,
        double overlayOpacity)
    {
        if (left is null || right is null)
        {
            Reset();
            return null;
        }

        EnsureInputs(left, right);
        if (_width <= 0 || _height <= 0 || _leftPixels is null || _rightPixels is null || _maxDeltas is null)
        {
            return null;
        }

        _outputBitmap ??= new WriteableBitmap(
            new PixelSize(_width, _height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        RenderIntoOutput(baseImage, overlayOpacity);
        return _outputBitmap;
    }

    private void EnsureInputs(Bitmap left, Bitmap right)
    {
        if (ReferenceEquals(_left, left) && ReferenceEquals(_right, right))
        {
            return;
        }

        _left = left;
        _right = right;
        _leftPixels = BitmapPixelCache.GetPixels(left);
        _rightPixels = BitmapPixelCache.GetPixels(right);
        _width = Math.Min(_leftPixels.PixelSize.Width, _rightPixels.PixelSize.Width);
        _height = Math.Min(_leftPixels.PixelSize.Height, _rightPixels.PixelSize.Height);
        _maxDeltas = _width <= 0 || _height <= 0
            ? null
            : new byte[_width * _height];
        _outputBitmap = _outputBitmap?.PixelSize == new PixelSize(_width, _height)
            ? _outputBitmap
            : null;

        if (_maxDeltas is not null)
        {
            BuildMaxDeltas();
        }
    }

    private void BuildMaxDeltas()
    {
        var leftPixels = _leftPixels!.Pixels;
        var rightPixels = _rightPixels!.Pixels;
        var leftStride = _leftPixels.Stride;
        var rightStride = _rightPixels.Stride;
        var maxDeltas = _maxDeltas!;

        unsafe
        {
            fixed (byte* leftBase = leftPixels)
            fixed (byte* rightBase = rightPixels)
            fixed (byte* maxDeltaBase = maxDeltas)
            {
                for (var y = 0; y < _height; y++)
                {
                    var leftPixel = leftBase + y * leftStride;
                    var rightPixel = rightBase + y * rightStride;
                    var maxDelta = maxDeltaBase + y * _width;

                    for (var x = 0; x < _width; x++)
                    {
                        var blueDelta = leftPixel[0] - rightPixel[0];
                        if (blueDelta < 0) blueDelta = -blueDelta;
                        var greenDelta = leftPixel[1] - rightPixel[1];
                        if (greenDelta < 0) greenDelta = -greenDelta;
                        var redDelta = leftPixel[2] - rightPixel[2];
                        if (redDelta < 0) redDelta = -redDelta;

                        var delta = redDelta > greenDelta ? redDelta : greenDelta;
                        if (blueDelta > delta) delta = blueDelta;
                        *maxDelta = (byte)delta;

                        leftPixel += BytesPerPixel;
                        rightPixel += BytesPerPixel;
                        maxDelta++;
                    }
                }
            }
        }
    }

    private void RenderIntoOutput(DifferenceBaseImage baseImage, double overlayOpacity)
    {
        var overlayOpacity255 = overlayOpacity <= 0.0 || double.IsNaN(overlayOpacity)
            ? 0
            : overlayOpacity >= 1.0
                ? 255
                : (int)(overlayOpacity * 255.0 + 0.5);
        var leftPixels = _leftPixels!.Pixels;
        var rightPixels = _rightPixels!.Pixels;
        var leftStride = _leftPixels.Stride;
        var rightStride = _rightPixels.Stride;
        var maxDeltas = _maxDeltas!;

        using var framebuffer = _outputBitmap!.Lock();
        unsafe
        {
            fixed (byte* leftBase = leftPixels)
            fixed (byte* rightBase = rightPixels)
            fixed (byte* maxDeltaBase = maxDeltas)
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

                for (var y = 0; y < _height; y++)
                {
                    var basePixel = baseBase + y * baseStride;
                    var maxDelta = maxDeltaBase + y * _width;
                    var outputPixel = outputBase + y * outputStride;

                    for (var x = 0; x < _width; x++)
                    {
                        var delta = *maxDelta;
                        var alpha = delta == 0
                            ? 0
                            : (overlayOpacity255 * ((35 * 255) + (65 * delta)) + 12_750) / 25_500;
                        var inverseAlpha = 255 - alpha;

                        outputPixel[0] = (byte)((basePixel[0] * inverseAlpha + overlayBlue * alpha + 127) / 255);
                        outputPixel[1] = (byte)((basePixel[1] * inverseAlpha + overlayGreen * alpha + 127) / 255);
                        outputPixel[2] = (byte)((basePixel[2] * inverseAlpha + overlayRed * alpha + 127) / 255);
                        outputPixel[3] = 255;

                        basePixel += basePixelStep;
                        outputPixel += BytesPerPixel;
                        maxDelta++;
                    }
                }
            }
        }
    }

    private void Reset()
    {
        _left = null;
        _right = null;
        _leftPixels = null;
        _rightPixels = null;
        _maxDeltas = null;
        _width = 0;
        _height = 0;
        _outputBitmap = null;
    }
}
