using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Difflection.Views;

public sealed class MiddleEllipsisTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> FullTextProperty =
        AvaloniaProperty.Register<MiddleEllipsisTextBlock, string?>(nameof(FullText));

    private bool _isUpdatingText;

    public string? FullText
    {
        get => GetValue(FullTextProperty);
        set => SetValue(FullTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FullTextProperty ||
            change.Property == FontFamilyProperty ||
            change.Property == FontSizeProperty ||
            change.Property == FontStyleProperty ||
            change.Property == FontWeightProperty ||
            change.Property == FontStretchProperty ||
            change.Property == ForegroundProperty)
        {
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateDisplayedText(GetAvailableWidth(availableSize.Width));
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateDisplayedText(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    private double GetAvailableWidth(double layoutWidth)
    {
        if (double.IsFinite(layoutWidth))
        {
            return layoutWidth;
        }

        return double.IsFinite(MaxWidth) ? MaxWidth : double.PositiveInfinity;
    }

    private void UpdateDisplayedText(double availableWidth)
    {
        if (_isUpdatingText)
        {
            return;
        }

        var fullText = FullText ?? string.Empty;
        var displayText = fullText;

        if (!string.IsNullOrEmpty(fullText) &&
            double.IsFinite(availableWidth) &&
            availableWidth > 0 &&
            MeasureTextWidth(fullText) > availableWidth)
        {
            displayText = CreateMiddleEllipsis(fullText, availableWidth);
        }

        if (Text == displayText)
        {
            return;
        }

        _isUpdatingText = true;
        Text = displayText;
        _isUpdatingText = false;
    }

    private string CreateMiddleEllipsis(string value, double availableWidth)
    {
        const string ellipsis = "…";

        if (MeasureTextWidth(ellipsis) > availableWidth)
        {
            return string.Empty;
        }

        var low = 0;
        var high = value.Length;
        var best = ellipsis;

        while (low <= high)
        {
            var keep = (low + high) / 2;
            var candidate = BuildCandidate(value, keep, ellipsis);

            if (MeasureTextWidth(candidate) <= availableWidth)
            {
                best = candidate;
                low = keep + 1;
            }
            else
            {
                high = keep - 1;
            }
        }

        return best;
    }

    private static string BuildCandidate(string value, int keep, string ellipsis)
    {
        if (keep <= 0)
        {
            return ellipsis;
        }

        var leftLength = keep / 2;
        var rightLength = keep - leftLength;
        return string.Concat(value.AsSpan(0, leftLength), ellipsis, value.AsSpan(value.Length - rightLength, rightLength));
    }

    private double MeasureTextWidth(string value)
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var text = new FormattedText(
            value,
            CultureInfo.CurrentCulture,
            FlowDirection,
            typeface,
            FontSize,
            Foreground ?? Brushes.Black);

        return text.Width;
    }
}
