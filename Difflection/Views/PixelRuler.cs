using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Difflection.Views;

public enum PixelRulerOrientation
{
    Horizontal,
    Vertical,
}

public enum PixelRulerMode
{
    Continuous,
    ResetPerImage,
}

public sealed class PixelRuler : Control
{
    private static readonly SolidColorBrush DefaultBackgroundBrush = new(Color.Parse("#1B1B1B"));
    private static readonly SolidColorBrush DefaultBorderBrush = new(Color.Parse("#343434"));
    private static readonly SolidColorBrush DefaultMajorTickBrush = new(Color.Parse("#AEB4BC"));
    private static readonly SolidColorBrush DefaultMinorTickBrush = new(Color.Parse("#6A707A"));
    private static readonly SolidColorBrush DefaultLabelBrush = new(Color.Parse("#D7DADF"));

    public static readonly StyledProperty<PixelRulerOrientation> OrientationProperty =
        AvaloniaProperty.Register<PixelRuler, PixelRulerOrientation>(nameof(Orientation), PixelRulerOrientation.Horizontal);

    public static readonly StyledProperty<PixelRulerMode> ModeProperty =
        AvaloniaProperty.Register<PixelRuler, PixelRulerMode>(nameof(Mode), PixelRulerMode.Continuous);

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<PixelRuler, double>(nameof(ZoomScale), 1.0);

    public static readonly StyledProperty<double> ContentOriginXProperty =
        AvaloniaProperty.Register<PixelRuler, double>(nameof(ContentOriginX), 0.0);

    public static readonly StyledProperty<double> ContentOriginYProperty =
        AvaloniaProperty.Register<PixelRuler, double>(nameof(ContentOriginY), 0.0);

    public static readonly StyledProperty<double> PrimarySegmentLengthProperty =
        AvaloniaProperty.Register<PixelRuler, double>(nameof(PrimarySegmentLength), 0.0);

    public static readonly StyledProperty<double> SecondarySegmentStartProperty =
        AvaloniaProperty.Register<PixelRuler, double>(nameof(SecondarySegmentStart), 0.0);

    public static readonly StyledProperty<double> SecondarySegmentLengthProperty =
        AvaloniaProperty.Register<PixelRuler, double>(nameof(SecondarySegmentLength), 0.0);

    public static readonly StyledProperty<IBrush> BackgroundBrushProperty =
        AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(BackgroundBrush), DefaultBackgroundBrush);

    public static readonly StyledProperty<IBrush> BorderBrushProperty =
        AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(BorderBrush), DefaultBorderBrush);

    public static readonly StyledProperty<IBrush> MajorTickBrushProperty =
        AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(MajorTickBrush), DefaultMajorTickBrush);

    public static readonly StyledProperty<IBrush> MinorTickBrushProperty =
        AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(MinorTickBrush), DefaultMinorTickBrush);

    public static readonly StyledProperty<IBrush> LabelBrushProperty =
        AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(LabelBrush), DefaultLabelBrush);

    static PixelRuler()
    {
        AffectsRender<PixelRuler>(
            OrientationProperty,
            ModeProperty,
            ZoomScaleProperty,
            ContentOriginXProperty,
            ContentOriginYProperty,
            PrimarySegmentLengthProperty,
            SecondarySegmentStartProperty,
            SecondarySegmentLengthProperty,
            BackgroundBrushProperty,
            BorderBrushProperty,
            MajorTickBrushProperty,
            MinorTickBrushProperty,
            LabelBrushProperty);
    }

    public PixelRulerOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public PixelRulerMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public double ContentOriginX
    {
        get => GetValue(ContentOriginXProperty);
        set => SetValue(ContentOriginXProperty, value);
    }

    public double ContentOriginY
    {
        get => GetValue(ContentOriginYProperty);
        set => SetValue(ContentOriginYProperty, value);
    }

    public double PrimarySegmentLength
    {
        get => GetValue(PrimarySegmentLengthProperty);
        set => SetValue(PrimarySegmentLengthProperty, value);
    }

    public double SecondarySegmentStart
    {
        get => GetValue(SecondarySegmentStartProperty);
        set => SetValue(SecondarySegmentStartProperty, value);
    }

    public double SecondarySegmentLength
    {
        get => GetValue(SecondarySegmentLengthProperty);
        set => SetValue(SecondarySegmentLengthProperty, value);
    }

    public IBrush BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public IBrush BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public IBrush MajorTickBrush
    {
        get => GetValue(MajorTickBrushProperty);
        set => SetValue(MajorTickBrushProperty, value);
    }

    public IBrush MinorTickBrush
    {
        get => GetValue(MinorTickBrushProperty);
        set => SetValue(MinorTickBrushProperty, value);
    }

    public IBrush LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var typeface = new Typeface("Inter");

        context.FillRectangle(BackgroundBrush, bounds);

        if (Orientation == PixelRulerOrientation.Horizontal)
        {
            DrawHorizontal(context, bounds, typeface);
            context.DrawLine(new Pen(BorderBrush, 1), new Point(0, bounds.Height - 0.5), new Point(bounds.Width, bounds.Height - 0.5));
        }
        else
        {
            DrawVertical(context, bounds, typeface);
            context.DrawLine(new Pen(BorderBrush, 1), new Point(bounds.Width - 0.5, 0), new Point(bounds.Width - 0.5, bounds.Height));
        }
    }

    private void DrawHorizontal(
        DrawingContext context,
        Rect bounds,
        Typeface typeface)
    {
        DrawSegmentHorizontal(context, bounds, typeface, 0, PrimarySegmentLength);

        if (Mode == PixelRulerMode.ResetPerImage && SecondarySegmentLength > 0)
        {
            DrawSegmentHorizontal(context, bounds, typeface, SecondarySegmentStart, SecondarySegmentLength);
        }
    }

    private void DrawVertical(
        DrawingContext context,
        Rect bounds,
        Typeface typeface)
    {
        DrawSegmentVertical(context, bounds, typeface, 0, PrimarySegmentLength);
    }

    private void DrawSegmentHorizontal(
        DrawingContext context,
        Rect bounds,
        Typeface typeface,
        double segmentStart,
        double segmentLength)
    {
        if (segmentLength <= 0 || ZoomScale <= 0)
        {
            return;
        }

        var tickStep = ChooseTickStep(ZoomScale);
        var mediumStep = tickStep * 5;
        var labelStep = tickStep * 10;
        var segmentScreenStart = ContentOriginX + (segmentStart * ZoomScale);
        var visibleLocalStart = Math.Max(0, (-segmentScreenStart) / ZoomScale);
        var visibleLocalEnd = Math.Min(segmentLength, (bounds.Width - segmentScreenStart) / ZoomScale);
        if (visibleLocalEnd < visibleLocalStart)
        {
            return;
        }

        var firstTick = Math.Ceiling(visibleLocalStart / tickStep) * tickStep;

        for (var local = firstTick; local <= visibleLocalEnd + 0.001; local += tickStep)
        {
            var screenX = Math.Round(segmentScreenStart + (local * ZoomScale));
            if (screenX < -24 || screenX > bounds.Width + 24)
            {
                continue;
            }

            var isLabel = IsMultiple(local, labelStep);
            var isMajor = isLabel || IsMultiple(local, mediumStep);
            var tickHeight = isLabel ? 14 : isMajor ? 11 : 7;
            var y1 = bounds.Height - 0.5;
            var y0 = Math.Max(0, y1 - tickHeight);
            context.FillRectangle(isMajor ? MajorTickBrush : MinorTickBrush, new Rect(screenX, y0, 1, tickHeight));

            if (!isLabel)
            {
                continue;
            }

            var labelValue = Math.Round(local);
            var text = labelValue.ToString("0", CultureInfo.InvariantCulture);
            var layout = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                LabelBrush);

            context.DrawText(layout, new Point(screenX + 3, 2));
        }
    }

    private void DrawSegmentVertical(
        DrawingContext context,
        Rect bounds,
        Typeface typeface,
        double segmentStart,
        double segmentLength)
    {
        if (segmentLength <= 0 || ZoomScale <= 0)
        {
            return;
        }

        var tickStep = ChooseTickStep(ZoomScale);
        var mediumStep = tickStep * 5;
        var labelStep = tickStep * 10;
        var segmentScreenStart = ContentOriginY + (segmentStart * ZoomScale);
        var visibleLocalStart = Math.Max(0, (-segmentScreenStart) / ZoomScale);
        var visibleLocalEnd = Math.Min(segmentLength, (bounds.Height - segmentScreenStart) / ZoomScale);
        if (visibleLocalEnd < visibleLocalStart)
        {
            return;
        }

        var firstTick = Math.Ceiling(visibleLocalStart / tickStep) * tickStep;

        for (var local = firstTick; local <= visibleLocalEnd + 0.001; local += tickStep)
        {
            var screenY = Math.Round(segmentScreenStart + (local * ZoomScale));
            if (screenY < -24 || screenY > bounds.Height + 24)
            {
                continue;
            }

            var isLabel = IsMultiple(local, labelStep);
            var isMajor = isLabel || IsMultiple(local, mediumStep);
            var tickWidth = isLabel ? 14 : isMajor ? 11 : 7;
            var x1 = bounds.Width - 0.5;
            var x0 = Math.Max(0, x1 - tickWidth);
            context.FillRectangle(isMajor ? MajorTickBrush : MinorTickBrush, new Rect(x0, screenY, tickWidth, 1));

            if (!isLabel)
            {
                continue;
            }

            var labelValue = Math.Round(local);
            var text = labelValue.ToString("0", CultureInfo.InvariantCulture);
            var layout = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                LabelBrush);

            context.DrawText(layout, new Point(3, screenY - 6));
        }
    }

    private static double ChooseTickStep(double zoomScale)
    {
        var targetScreenSpacing = 10.0;
        var desiredContentSpacing = targetScreenSpacing / Math.Max(zoomScale, 0.0001);
        var exponent = Math.Floor(Math.Log10(desiredContentSpacing));
        var scale = Math.Pow(10, exponent);
        var fraction = desiredContentSpacing / scale;

        var niceFraction = fraction <= 1 ? 1
            : fraction <= 2 ? 2
            : fraction <= 5 ? 5
            : 10;

        return niceFraction * scale;
    }

    private static bool IsMultiple(double value, double step)
    {
        if (step <= 0)
        {
            return false;
        }

        var quotient = value / step;
        return Math.Abs(quotient - Math.Round(quotient)) < 0.0001;
    }
}
