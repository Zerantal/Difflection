using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.ViewModels;

public partial class ComparisonDisplayViewModel : ViewModelBase
{
    private const double MinimumStageWidth = 920;
    private const double MinimumStageHeight = 560;
    private readonly DifferenceBitmapRenderer _differenceBitmapRenderer = new();
    private bool _suppressDifferenceStatusUpdates;
    private int _differenceStatusVersion;
    private int _differenceImageUpdateVersion;
    private bool _differenceImageUpdateQueued;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    public partial double StageWidth { get; set; } = MinimumStageWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial double StageHeight { get; set; } = MinimumStageHeight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLeftImage))]
    [NotifyPropertyChangedFor(nameof(HasAnyImage))]
    [NotifyPropertyChangedFor(nameof(HasBothImages))]
    [NotifyPropertyChangedFor(nameof(LeftImageWidth))]
    [NotifyPropertyChangedFor(nameof(LeftImageHeight))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial Bitmap? LeftImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRightImage))]
    [NotifyPropertyChangedFor(nameof(HasAnyImage))]
    [NotifyPropertyChangedFor(nameof(HasBothImages))]
    [NotifyPropertyChangedFor(nameof(RightImageWidth))]
    [NotifyPropertyChangedFor(nameof(RightImageHeight))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial Bitmap? RightImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifferenceImageWidth))]
    [NotifyPropertyChangedFor(nameof(DifferenceImageHeight))]
    public partial Bitmap? DifferenceImage { get; private set; }

    [ObservableProperty]
    public partial int DifferenceImageRevision { get; private set; }

    [ObservableProperty]
    public partial string LeftFileName { get; set; } = "Baseline image";

    [ObservableProperty]
    public partial string RightFileName { get; set; } = "Candidate image";

    [ObservableProperty]
    public partial string DifferenceStatusText { get; set; } = "Load two images to compare";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDifferenceBaseBaseline))]
    [NotifyPropertyChangedFor(nameof(IsDifferenceBaseCandidate))]
    [NotifyPropertyChangedFor(nameof(IsDifferenceBaseMap))]
    public partial DifferenceBaseImage DifferenceBaseImage { get; set; } = DifferenceBaseImage.Candidate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifferenceOverlayOpacityText))]
    public partial double DifferenceOverlayOpacity { get; set; } = 0.75;

    public bool HasLeftImage => LeftImage is not null;

    public bool HasRightImage => RightImage is not null;

    public bool HasAnyImage => HasLeftImage || HasRightImage;

    public bool HasBothImages => HasLeftImage && HasRightImage;

    public double LeftImageWidth => LeftImage?.PixelSize.Width ?? StageWidth;

    public double LeftImageHeight => LeftImage?.PixelSize.Height ?? StageHeight;

    public double RightImageWidth => RightImage?.PixelSize.Width ?? StageWidth;

    public double RightImageHeight => RightImage?.PixelSize.Height ?? StageHeight;

    public double DifferenceImageWidth => DifferenceImage?.PixelSize.Width ?? StageWidth;

    public double DifferenceImageHeight => DifferenceImage?.PixelSize.Height ?? StageHeight;

    public bool IsDifferenceBaseBaseline => DifferenceBaseImage == DifferenceBaseImage.Baseline;

    public bool IsDifferenceBaseCandidate => DifferenceBaseImage == DifferenceBaseImage.Candidate;

    public bool IsDifferenceBaseMap => DifferenceBaseImage == DifferenceBaseImage.Map;

    public string DifferenceOverlayOpacityText => $"{Math.Round(DifferenceOverlayOpacity * 100):0}%";

    public void SelectDifferenceBaseImage(DifferenceBaseImage baseImage)
    {
        DifferenceBaseImage = baseImage;
    }

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

    public async Task<bool> LoadImageAssetAsync(
        ImageSlot slot,
        ImageAsset image,
        IProjectStorage? projectStorage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (projectStorage is null)
        {
            return false;
        }

        await using var stream = await projectStorage.LoadImageAsync(image, cancellationToken);
        await LoadImageAsync(slot, GetDisplayName(image), stream);
        return true;
    }

    public async Task RefreshCurrentComparisonImagesAsync(
        ComparisonSet? selectedComparison,
        IProjectStorage? projectStorage,
        CancellationToken cancellationToken = default,
        bool deferDifferenceStatus = false)
    {
        _suppressDifferenceStatusUpdates = true;
        try
        {
            if (selectedComparison?.BaselineImage is { } baseline)
            {
                await LoadImageAssetAsync(ImageSlot.Left, baseline, projectStorage, cancellationToken);
            }
            else
            {
                LeftImage = null;
                LeftFileName = "Baseline image";
            }

            if (selectedComparison?.CandidateImage is { } candidate)
            {
                await LoadImageAssetAsync(ImageSlot.Right, candidate, projectStorage, cancellationToken);
            }
            else
            {
                RightImage = null;
                RightFileName = "Candidate image";
            }
        }
        finally
        {
            _suppressDifferenceStatusUpdates = false;
        }

        if (deferDifferenceStatus && HasBothImages)
        {
            ScheduleDifferenceStatusUpdate();
        }
        else
        {
            UpdateDifferenceStatus();
        }
    }

    public async Task LoadImageAsync(ImageSlot slot, IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = await file.OpenReadAsync();
        await LoadImageAsync(slot, file.Name, stream);
    }

    public async Task LoadImageAsync(ImageSlot slot, string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        await LoadImageAsync(slot, Path.GetFileName(filePath), stream);
    }

    public async Task LoadImageAsync(ImageSlot slot, string fileName, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

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

    public void Clear()
    {
        LeftImage = null;
        RightImage = null;
        LeftFileName = "Baseline image";
        RightFileName = "Candidate image";
        UpdateDifferenceStatus();
    }

    public void DisposeImages()
    {
        LeftImage = null;
        RightImage = null;
        DifferenceImage = null;
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

    private static string GetDisplayName(ImageAsset image)
    {
        return string.IsNullOrWhiteSpace(image.Label) ? image.SourceName : image.Label;
    }

    private void UpdateStageSize()
    {
        var leftWidth = LeftImage?.PixelSize.Width ?? 0;
        var rightWidth = RightImage?.PixelSize.Width ?? 0;
        var leftHeight = LeftImage?.PixelSize.Height ?? 0;
        var rightHeight = RightImage?.PixelSize.Height ?? 0;

        StageWidth = Math.Max(MinimumStageWidth, Math.Max(leftWidth, rightWidth));
        StageHeight = Math.Max(MinimumStageHeight, Math.Max(leftHeight, rightHeight));
    }

    private void UpdateDifferenceStatus()
    {
        _differenceStatusVersion++;
        DifferenceStatusText = ImageDifferenceMetric.Compare(LeftImage, RightImage)?.ToStatusText()
            ?? "Load two images to compare";
        UpdateDifferenceImage();
    }

    private void ScheduleDifferenceStatusUpdate()
    {
        var version = ++_differenceStatusVersion;
        DifferenceStatusText = "Calculating difference...";
        Dispatcher.UIThread.Post(
            () =>
            {
                if (version != _differenceStatusVersion)
                {
                    return;
                }

                DifferenceStatusText = ImageDifferenceMetric.Compare(LeftImage, RightImage)?.ToStatusText()
                    ?? "Load two images to compare";
                UpdateDifferenceImage();
            },
            DispatcherPriority.Background);
    }

    private void UpdateDifferenceImage()
    {
        SetDifferenceImage(_differenceBitmapRenderer.Render(
            LeftImage,
            RightImage,
            DifferenceBaseImage,
            DifferenceOverlayOpacity));
    }

    private void ScheduleDifferenceImageUpdate()
    {
        if (!OperatingSystem.IsBrowser())
        {
            UpdateDifferenceImage();
            return;
        }

        _differenceImageUpdateVersion++;
        if (_differenceImageUpdateQueued)
        {
            return;
        }

        _differenceImageUpdateQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _differenceImageUpdateQueued = false;
                var version = _differenceImageUpdateVersion;
                UpdateDifferenceImage();
                // if new difference image is requested, schedule another update
                if (version != _differenceImageUpdateVersion)
                {
                    ScheduleDifferenceImageUpdate();
                }
            },
            DispatcherPriority.Render);
    }

    private void SetDifferenceImage(Bitmap? image)
    {
        if (ReferenceEquals(DifferenceImage, image))
        {
            if (image is not null)
            {
                DifferenceImageRevision++;
                OnPropertyChanged(nameof(DifferenceImage));
                OnPropertyChanged(nameof(DifferenceImageWidth));
                OnPropertyChanged(nameof(DifferenceImageHeight));
            }

            return;
        }

        DifferenceImage = image;
        DifferenceImageRevision++;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDifferenceImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        if (oldValue is not null)
        {
            Dispatcher.UIThread.Post(oldValue.Dispose, DispatcherPriority.Background);
        }
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDifferenceBaseImageChanged(DifferenceBaseImage value)
    {
        UpdateDifferenceImage();
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDifferenceOverlayOpacityChanged(double value)
    {
        ScheduleDifferenceImageUpdate();
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnLeftImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        if (oldValue is not null)
        {
            Dispatcher.UIThread.Post(oldValue.Dispose, DispatcherPriority.Background);
        }

        UpdateStageSize();
        if (!_suppressDifferenceStatusUpdates)
        {
            UpdateDifferenceStatus();
        }
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnRightImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        if (oldValue is not null)
        {
            Dispatcher.UIThread.Post(oldValue.Dispose, DispatcherPriority.Background);
        }

        UpdateStageSize();
        if (!_suppressDifferenceStatusUpdates)
        {
            UpdateDifferenceStatus();
        }
    }
}
