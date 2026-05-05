using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Difflection.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ComparisonImagesModel _images = new();
    private ComparisonViewMode _selectedViewMode = ComparisonViewMode.SideBySide;
    private string _splitPercentageText = "50 / 50";
    private double _zoomScale = 1.0;
    private string _zoomText = "100%";
    private double _stageWidth = 920;
    private double _stageHeight = 560;

    public Bitmap? LeftImage
    {
        get => _images.LeftImage;
        private set
        {
            if (ReferenceEquals(_images.LeftImage, value))
            {
                return;
            }

            var previous = _images.LeftImage;
            _images.LeftImage = value;
            OnPropertyChanged();
            previous?.Dispose();
            OnPropertyChanged(nameof(HasLeftImage));
            OnPropertyChanged(nameof(HasAnyImage));
            OnImageAvailabilityChanged();
            UpdateStageSize();
        }
    }

    public Bitmap? RightImage
    {
        get => _images.RightImage;
        private set
        {
            if (ReferenceEquals(_images.RightImage, value))
            {
                return;
            }

            var previous = _images.RightImage;
            _images.RightImage = value;
            OnPropertyChanged();
            previous?.Dispose();
            OnPropertyChanged(nameof(HasRightImage));
            OnPropertyChanged(nameof(HasAnyImage));
            OnImageAvailabilityChanged();
            UpdateStageSize();
        }
    }

    public bool HasLeftImage => LeftImage is not null;

    public bool HasRightImage => RightImage is not null;

    public bool HasAnyImage => HasLeftImage || HasRightImage;

    public bool HasBothImages => HasLeftImage && HasRightImage;

    public double LeftImageWidth => LeftImage?.PixelSize.Width ?? StageWidth;

    public double LeftImageHeight => LeftImage?.PixelSize.Height ?? StageHeight;

    public double RightImageWidth => RightImage?.PixelSize.Width ?? StageWidth;

    public double RightImageHeight => RightImage?.PixelSize.Height ?? StageHeight;

    public ComparisonViewMode SelectedViewMode
    {
        get => _selectedViewMode;
        private set
        {
            if (!SetProperty(ref _selectedViewMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSideBySideView));
            OnPropertyChanged(nameof(IsSplitScreenView));
            OnPropertyChanged(nameof(CurrentViewTitle));
        }
    }

    public bool IsSideBySideView => SelectedViewMode == ComparisonViewMode.SideBySide;

    public bool IsSplitScreenView => SelectedViewMode == ComparisonViewMode.SplitScreen;

    public bool CanUseSplitScreen => HasBothImages;

    public string CurrentViewTitle => SelectedViewMode switch
    {
        ComparisonViewMode.SplitScreen => "Split screen",
        _ => "Side-by-side",
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

    public string LeftFileName
    {
        get => _images.LeftFileName;
        private set
        {
            if (_images.LeftFileName == value)
            {
                return;
            }

            _images.LeftFileName = value;
            OnPropertyChanged();
        }
    }

    public string RightFileName
    {
        get => _images.RightFileName;
        private set
        {
            if (_images.RightFileName == value)
            {
                return;
            }

            _images.RightFileName = value;
            OnPropertyChanged();
        }
    }

    public string SplitPercentageText
    {
        get => _splitPercentageText;
        set => SetProperty(ref _splitPercentageText, value);
    }

    public double ZoomScale
    {
        get => _zoomScale;
        private set => SetProperty(ref _zoomScale, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        set => SetProperty(ref _zoomText, value);
    }

    public double StageWidth
    {
        get => _stageWidth;
        private set
        {
            if (SetProperty(ref _stageWidth, value))
            {
                OnPropertyChanged(nameof(SideBySideStageWidth));
            }
        }
    }

    public double StageHeight
    {
        get => _stageHeight;
        private set => SetProperty(ref _stageHeight, value);
    }

    public void SetZoomScale(double zoomScale)
    {
        var clamped = Math.Clamp(zoomScale, 0.05, 64.0);
        ZoomScale = clamped;
        ZoomText = $"{Math.Round(clamped * 100):0}%";
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
        LeftImage?.Dispose();
        RightImage?.Dispose();
        _images.LeftImage = null;
        _images.RightImage = null;
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
        OnPropertyChanged(nameof(LeftImageWidth));
        OnPropertyChanged(nameof(LeftImageHeight));
        OnPropertyChanged(nameof(RightImageWidth));
        OnPropertyChanged(nameof(RightImageHeight));
        OnPropertyChanged(nameof(SideBySideStageWidth));
        OnPropertyChanged(nameof(SideBySideStageHeight));
    }

    private void OnImageAvailabilityChanged()
    {
        OnPropertyChanged(nameof(HasBothImages));
        OnPropertyChanged(nameof(CanUseSplitScreen));
        OnPropertyChanged(nameof(LeftImageWidth));
        OnPropertyChanged(nameof(LeftImageHeight));
        OnPropertyChanged(nameof(RightImageWidth));
        OnPropertyChanged(nameof(RightImageHeight));
        OnPropertyChanged(nameof(SideBySideStageWidth));
        OnPropertyChanged(nameof(SideBySideStageHeight));

        if (!CanUseSplitScreen && IsSplitScreenView)
        {
            SelectSideBySideView();
        }
    }
}

public enum ImageSlot
{
    Left,
    Right,
}

public enum ComparisonViewMode
{
    SideBySide,
    SplitScreen,
}
