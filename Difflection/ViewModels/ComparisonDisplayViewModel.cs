using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.ViewModels;

public partial class ComparisonDisplayViewModel : ViewModelBase
{
    private const double MinimumStageWidth = 920;
    private const double MinimumStageHeight = 560;

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
        CancellationToken cancellationToken = default)
    {
        if (selectedComparison?.ReferenceImage is { } reference)
        {
            await LoadImageAssetAsync(ImageSlot.Left, reference, projectStorage, cancellationToken);
        }
        else
        {
            LeftImage = null;
            LeftFileName = "Reference image";
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
        LeftFileName = "Reference image";
        RightFileName = "Candidate image";
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
        DifferenceStatusText = ImageDifferenceMetric.Compare(LeftImage, RightImage)?.ToStatusText()
            ?? "Load two images to compare";
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnLeftImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        UpdateStageSize();
        UpdateDifferenceStatus();
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnRightImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        UpdateStageSize();
        UpdateDifferenceStatus();
    }
}
