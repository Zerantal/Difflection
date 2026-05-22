using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Infrastructure;
using Difflection.Models;
using Difflection.Storage;
// ReSharper disable UnusedParameterInPartialMethod

namespace Difflection.ViewModels;

public sealed partial class ComparisonImageSetItemViewModel(ImageAsset image, ComparisonSet comparison, ComparisonChannel channel) : ViewModelBase, IDisposable
{
    private static readonly IBrush DarkThumbnailOverlayBrush = new SolidColorBrush(Color.FromArgb(0xD8, 0x05, 0x05, 0x05));
    private static readonly IBrush DarkThumbnailTagOverlayBrush = new SolidColorBrush(Color.FromArgb(0xE8, 0x10, 0x10, 0x10));
    private static readonly IBrush DarkThumbnailOverlayForegroundBrush = new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB));
    private static readonly IBrush DarkThumbnailOverlayBorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush LightThumbnailOverlayBrush = new SolidColorBrush(Color.FromArgb(0xE8, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush LightThumbnailTagOverlayBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush LightThumbnailOverlayForegroundBrush = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
    private static readonly IBrush LightThumbnailOverlayBorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x11, 0x18, 0x27));
    private double _thumbnailAverageLuminance;

    public ComparisonImageSetItemViewModel(ImageAsset image, ComparisonSet comparison)
        : this(image, comparison, comparison.GetChannelForImage(image) ?? comparison.BaselineChannel)
    {
    }

    public ImageAsset Image { get; } = image;

    public ComparisonChannel Channel { get; } = channel;

    public Guid Id => Image.Id;

    public string Label
    {
        get => Image.Label;
        set
        {
            if (string.Equals(Image.Label, value, StringComparison.Ordinal))
            {
                return;
            }

            Image.Label = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(HasDisplayLabel));
            OnMetadataChanged();
        }
    }

    public string DisplayLabel
    {
        get => HasDisplayLabel ? Image.Label : string.Empty;
        set
        {
            if (string.Equals(DisplayLabel, value, StringComparison.Ordinal))
            {
                return;
            }

            Image.Label = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(HasDisplayLabel));
            OnMetadataChanged();
        }
    }

    public bool HasDisplayLabel => !IsDefaultLabel(Image.Label);

    // ReSharper disable once MemberCanBePrivate.Global
    public string SourceName => Image.SourceName;

    public string RevisionText => $"r{GetRevisionNumber()}";

    public string AddedAtText => Image.AddedAt.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.CurrentCulture);

    public string FileLocationText => GetFileLocationText();

    public bool IsActive => Channel.ActiveImageId == Image.Id;

    public bool IsBaseline => ReferenceEquals(Channel, comparison.BaselineChannel);

    public bool IsCandidate => ReferenceEquals(Channel, comparison.CandidateChannel);

    public bool CanSetBaseline => comparison.BaselineChannel.Contains(Image);

    public bool CanSetCandidate => comparison.CandidateChannel.Contains(Image);

    public bool HasMonitoring => Image.MonitoringRole != ImageMonitoringRole.None;

    public string MonitoringText => Image.MonitoringRole switch
    {
        ImageMonitoringRole.Baseline => "Monitoring baseline",
        ImageMonitoringRole.Candidate => "Monitoring candidate",
        _ => string.Empty
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    [NotifyPropertyChangedFor(nameof(HasNoThumbnail))]
    public partial Bitmap? Thumbnail { get; private set; }

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasNoThumbnail => Thumbnail is null;

    public IBrush ThumbnailOverlayBrush => UsesDarkThumbnailOverlay
        ? DarkThumbnailOverlayBrush
        : LightThumbnailOverlayBrush;

    public IBrush ThumbnailTagOverlayBrush => UsesDarkThumbnailOverlay
        ? DarkThumbnailTagOverlayBrush
        : LightThumbnailTagOverlayBrush;

    public IBrush ThumbnailOverlayForegroundBrush => UsesDarkThumbnailOverlay
        ? DarkThumbnailOverlayForegroundBrush
        : LightThumbnailOverlayForegroundBrush;

    public IBrush ThumbnailOverlayBorderBrush => UsesDarkThumbnailOverlay
        ? DarkThumbnailOverlayBorderBrush
        : LightThumbnailOverlayBorderBrush;

    private bool UsesDarkThumbnailOverlay => Thumbnail is null || _thumbnailAverageLuminance >= 0.5;

    public async Task LoadThumbnailAsync(IProjectStorage projectStorage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectStorage);

        try
        {
            await using var stream = await projectStorage.LoadImageAsync(Image, cancellationToken);
            Thumbnail = await CreateBitmapAsync(stream);
        }
        catch
        {
            Thumbnail = null;
        }
    }

    public void Dispose()
    {
        var thumbnail = Thumbnail;
        Thumbnail = null;
        thumbnail?.Dispose();
    }

    public void RefreshDisplayMetadata()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasDisplayLabel));
        OnMetadataChanged();
        OnPropertyChanged(nameof(RevisionText));
        OnPropertyChanged(nameof(AddedAtText));
        OnPropertyChanged(nameof(MonitoringText));
        OnPropertyChanged(nameof(HasMonitoring));
        OnPropertyChanged(nameof(IsActive));
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

    private bool IsDefaultLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return true;
        }

        var trimmedLabel = label.Trim();
        return string.Equals(trimmedLabel, "Image", StringComparison.Ordinal)
            || string.Equals(trimmedLabel, SourceName, StringComparison.Ordinal)
            || string.Equals(trimmedLabel, GetDefaultLabelFromSourceName(SourceName), StringComparison.Ordinal);
    }

    private static string GetDefaultLabelFromSourceName(string? sourceName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceName?.Trim());
        if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return fileNameWithoutExtension;
        }

        var fileName = Path.GetFileName(sourceName?.Trim());
        return string.IsNullOrWhiteSpace(fileName) ? "Image" : fileName;
    }

    private string GetFileLocationText()
    {
        var path = Image.OriginalFileMetadata?.Path;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var directory = Path.GetDirectoryName(path);
            return string.IsNullOrWhiteSpace(directory) ? "Not available" : directory;
        }

        if (string.IsNullOrWhiteSpace(Image.StorageKey))
        {
            return "Not available";
        }

        var storageDirectory = Path.GetDirectoryName(Image.StorageKey);
        return string.IsNullOrWhiteSpace(storageDirectory) ? "Not available" : storageDirectory;
    }

    private void OnMetadataChanged()
    {
        OnPropertyChanged(nameof(FileLocationText));
    }

    private int GetRevisionNumber()
    {
        var versionNumber = 1;
        var current = Image;
        var visited = new HashSet<Guid> { current.Id };

        while (current.PreviousVersionImageId is { } previousVersionImageId
               && visited.Add(previousVersionImageId))
        {
            versionNumber++;
            var previous = Channel.Images.FirstOrDefault(image => image.Id == previousVersionImageId);
            if (previous is null)
            {
                break;
            }

            current = previous;
        }

        return versionNumber;
    }

    partial void OnThumbnailChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        _thumbnailAverageLuminance = newValue is null ? 0.0 : BitmapLuminanceAnalyzer.GetAverageLuminance(newValue);
        OnPropertyChanged(nameof(ThumbnailOverlayBrush));
        OnPropertyChanged(nameof(ThumbnailTagOverlayBrush));
        OnPropertyChanged(nameof(ThumbnailOverlayForegroundBrush));
        OnPropertyChanged(nameof(ThumbnailOverlayBorderBrush));

        if (oldValue is not null)
        {
            Dispatcher.UIThread.Post(oldValue.Dispose, DispatcherPriority.Background);
        }
    }
}
