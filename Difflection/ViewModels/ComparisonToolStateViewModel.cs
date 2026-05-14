using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Difflection.ViewModels;

public partial class ComparisonToolStateViewModel(Func<bool> canUseSplitScreenProvider) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSideBySideView))]
    [NotifyPropertyChangedFor(nameof(IsSplitScreenView))]
    [NotifyPropertyChangedFor(nameof(CurrentViewTitle))]
    public partial ComparisonViewMode SelectedViewMode { get; set; } = ComparisonViewMode.SideBySide;

    [ObservableProperty]
    public partial string SplitPercentageText { get; set; } = "50 / 50";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    public partial double ZoomScale { get; set; } = 1.0;

    [ObservableProperty]
    public partial string ZoomText { get; set; } = "100%";

    public bool IsSideBySideView => SelectedViewMode == ComparisonViewMode.SideBySide;

    public bool IsSplitScreenView => SelectedViewMode == ComparisonViewMode.SplitScreen;

    public bool CanUseSplitScreen => canUseSplitScreenProvider();

    public string CurrentViewTitle => SelectedViewMode switch
    {
        ComparisonViewMode.SplitScreen => "Split screen",
        _ => "Side-by-side"
    };

    public void SetZoomScale(double zoomScale)
    {
        ZoomScale = Math.Clamp(zoomScale, 0.05, 64.0);
    }

    public bool TrySetZoomText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ZoomText = $"{Math.Round(ZoomScale * 100):0}%";
            return false;
        }

        var trimmed = text.Trim();
        var percentText = trimmed.EndsWith('%') ? trimmed[..^1] : trimmed;

        if (!double.TryParse(percentText, out var percent) || percent <= 0)
        {
            ZoomText = $"{Math.Round(ZoomScale * 100):0}%";
            return false;
        }

        SetZoomScale(percent / 100.0);
        return true;
    }

    public void SelectSideBySideView()
    {
        SelectedViewMode = ComparisonViewMode.SideBySide;
    }

    public void SelectSplitScreenView()
    {
        if (CanUseSplitScreen)
        {
            SelectedViewMode = ComparisonViewMode.SplitScreen;
        }
    }

    public void NotifyCanUseSplitScreenChanged()
    {
        OnPropertyChanged(nameof(CanUseSplitScreen));

        if (!CanUseSplitScreen && IsSplitScreenView)
        {
            SelectSideBySideView();
        }
    }

    partial void OnZoomScaleChanged(double value)
    {
        ZoomText = $"{Math.Round(value * 100):0}%";
    }
}
