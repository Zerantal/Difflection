using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Difflection.Views;

public sealed class AdaptiveInterpolationImage : Image
{
    public static readonly StyledProperty<double> SourcePixelThresholdProperty =
        AvaloniaProperty.Register<AdaptiveInterpolationImage, double>(
            nameof(SourcePixelThreshold),
            2.0);

    public static readonly StyledProperty<double> ViewScaleProperty =
        AvaloniaProperty.Register<AdaptiveInterpolationImage, double>(
            nameof(ViewScale),
            1.0);

    public double SourcePixelThreshold
    {
        get => GetValue(SourcePixelThresholdProperty);
        set => SetValue(SourcePixelThresholdProperty, value);
    }

    public double ViewScale
    {
        get => GetValue(ViewScaleProperty);
        set => SetValue(ViewScaleProperty, value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arrangedSize = base.ArrangeOverride(finalSize);
        UpdateBitmapInterpolationMode();
        return arrangedSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty
            || change.Property == SourceProperty
            || change.Property == StretchProperty
            || change.Property == SourcePixelThresholdProperty
            || change.Property == ViewScaleProperty)
        {
            UpdateBitmapInterpolationMode();
        }
    }

    private void UpdateBitmapInterpolationMode()
    {
        var sourcePixelScale = GetSourcePixelScale(Bounds.Size, Source?.Size ?? default, Stretch) * Math.Max(0, ViewScale);
        var interpolationMode = sourcePixelScale >= SourcePixelThreshold
            ? BitmapInterpolationMode.None
            : BitmapInterpolationMode.HighQuality;

        if (RenderOptions.GetBitmapInterpolationMode(this) != interpolationMode)
        {
            RenderOptions.SetBitmapInterpolationMode(this, interpolationMode);
        }
    }

    private static double GetSourcePixelScale(Size targetSize, Size sourceSize, Stretch stretch)
    {
        if (targetSize.Width <= 0
            || targetSize.Height <= 0
            || sourceSize.Width <= 0
            || sourceSize.Height <= 0)
        {
            return 0;
        }

        var xScale = targetSize.Width / sourceSize.Width;
        var yScale = targetSize.Height / sourceSize.Height;

        return stretch switch
        {
            Stretch.None => 1.0,
            Stretch.Fill => Math.Min(xScale, yScale),
            Stretch.UniformToFill => Math.Max(xScale, yScale),
            _ => Math.Min(xScale, yScale)
        };
    }
}
