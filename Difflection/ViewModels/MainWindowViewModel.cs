using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Difflection.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    public partial double StageWidth { get; set; } = 920;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial double StageHeight { get; set; } = 560;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLeftImage))]
    [NotifyPropertyChangedFor(nameof(HasAnyImage))]
    [NotifyPropertyChangedFor(nameof(HasBothImages))]
    [NotifyPropertyChangedFor(nameof(CanUseSplitScreen))]
    [NotifyPropertyChangedFor(nameof(LeftImageWidth))]
    [NotifyPropertyChangedFor(nameof(LeftImageHeight))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial Bitmap? LeftImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRightImage))]
    [NotifyPropertyChangedFor(nameof(HasAnyImage))]
    [NotifyPropertyChangedFor(nameof(HasBothImages))]
    [NotifyPropertyChangedFor(nameof(CanUseSplitScreen))]
    [NotifyPropertyChangedFor(nameof(RightImageWidth))]
    [NotifyPropertyChangedFor(nameof(RightImageHeight))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial Bitmap? RightImage { get; set; }

    [ObservableProperty]
    public partial string LeftFileName { get; set; } = "Reference image";

    [ObservableProperty]
    public partial string RightFileName { get; set; } = "Candidate image";

    [ObservableProperty]
    public partial string DifferenceStatusText { get; set; } = "Load two images to compare";

    public bool HasLeftImage => LeftImage is not null;

    public bool HasRightImage => RightImage is not null;

    public bool HasAnyImage => HasLeftImage || HasRightImage;

    public bool HasBothImages => HasLeftImage && HasRightImage;

    public double LeftImageWidth => LeftImage?.PixelSize.Width ?? StageWidth;

    public double LeftImageHeight => LeftImage?.PixelSize.Height ?? StageHeight;

    public double RightImageWidth => RightImage?.PixelSize.Width ?? StageWidth;

    public double RightImageHeight => RightImage?.PixelSize.Height ?? StageHeight;

    public bool IsSideBySideView => SelectedViewMode == ComparisonViewMode.SideBySide;

    public bool IsSplitScreenView => SelectedViewMode == ComparisonViewMode.SplitScreen;

    public bool CanUseSplitScreen => HasBothImages;

    public string CurrentViewTitle => SelectedViewMode switch
    {
        ComparisonViewMode.SplitScreen => "Split screen",
        _ => "Side-by-side"
    };

    public double SideBySideStageWidth => HasBothImages
        ? LeftImageWidth + 16 + RightImageWidth
        : HasLeftImage
            ? LeftImageWidth
            : HasRightImage
                ? RightImageWidth
                : StageWidth;

    public double SideBySideStageHeight => HasBothImages
        ? Math.Max(LeftImageHeight, RightImageHeight)
        : HasLeftImage
            ? LeftImageHeight
            : HasRightImage
                ? RightImageHeight
                : StageHeight;

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

    public async Task LoadImageAsync(ImageSlot slot, IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = file.Name;
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = file.Name;
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public async Task LoadImageAsync(ImageSlot slot, string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = Path.GetFileName(filePath);
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = Path.GetFileName(filePath);
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public async Task LoadImageAsync(ImageSlot slot, string fileName, Stream stream)
    {
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = Path.GetFileName(fileName);
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = Path.GetFileName(fileName);
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public void DisposeImages()
    {
        LeftImage = null;
        RightImage = null;
    }

    private static async Task<Bitmap> CreateBitmapAsync(Stream stream)
    {
        if (OperatingSystem.IsBrowser() || !stream.CanSeek)
        {
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            return new Bitmap(buffer);
        }

        return new Bitmap(stream);
    }

    private void UpdateStageSize()
    {
        var leftWidth = LeftImage?.PixelSize.Width ?? 0;
        var rightWidth = RightImage?.PixelSize.Width ?? 0;
        var leftHeight = LeftImage?.PixelSize.Height ?? 0;
        var rightHeight = RightImage?.PixelSize.Height ?? 0;

        StageWidth = Math.Max(920, Math.Max(leftWidth, rightWidth));
        StageHeight = Math.Max(560, Math.Max(leftHeight, rightHeight));
    }

    private void UpdateDifferenceStatus()
    {
        DifferenceStatusText = ImageDifferenceMetric.Compare(LeftImage, RightImage)?.ToStatusText()
            ?? "Load two images to compare";
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnLeftImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        UpdateStageSize();
        UpdateDifferenceStatus();

        if (!CanUseSplitScreen && IsSplitScreenView)
        {
            SelectSideBySideView();
        }
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnRightImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        UpdateStageSize();
        UpdateDifferenceStatus();

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

public enum ImageSlot
{
    Left,
    Right
}

public enum ComparisonViewMode
{
    SideBySide,
    SplitScreen
}
