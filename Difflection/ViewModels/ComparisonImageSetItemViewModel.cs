using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.ViewModels;

public sealed partial class ComparisonImageSetItemViewModel(ImageAsset image, ComparisonSet comparison) : ViewModelBase, IDisposable
{
    private static readonly IBrush InactiveActionBackground = new SolidColorBrush(Color.Parse("#262626"));
    private static readonly IBrush InactiveActionBorderBrush = new SolidColorBrush(Color.Parse("#444444"));
    private static readonly IBrush DefaultActionForeground = new SolidColorBrush(Color.Parse("#E5E7EB"));
    private static readonly IBrush ActiveBaselineBackground = new SolidColorBrush(Color.Parse("#3A2A1F"));
    private static readonly IBrush ActiveBaselineBorderBrush = new SolidColorBrush(Color.Parse("#F97316"));
    private static readonly IBrush ActiveCandidateBackground = new SolidColorBrush(Color.Parse("#1F2C3A"));
    private static readonly IBrush ActiveCandidateBorderBrush = new SolidColorBrush(Color.Parse("#38BDF8"));

    public ImageAsset Image { get; } = image;

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
        }
    }

    public bool HasDisplayLabel => !IsDefaultLabel(Image.Label);

    public string SourceName => Image.SourceName;

    public string RevisionText => $"r{GetRevisionNumber()}";

    public string AddedAtText => Image.AddedAt.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.CurrentCulture);

    public bool IsReference => comparison.ReferenceImageId == Image.Id;

    public bool IsCandidate => comparison.CandidateImageId == Image.Id;

    public IBrush BaselineButtonBackground => IsReference ? ActiveBaselineBackground : InactiveActionBackground;

    public IBrush BaselineButtonBorderBrush => IsReference ? ActiveBaselineBorderBrush : InactiveActionBorderBrush;

    public IBrush BaselineButtonForeground => DefaultActionForeground;

    public IBrush CandidateButtonBackground => IsCandidate ? ActiveCandidateBackground : InactiveActionBackground;

    public IBrush CandidateButtonBorderBrush => IsCandidate ? ActiveCandidateBorderBrush : InactiveActionBorderBrush;

    public IBrush CandidateButtonForeground => DefaultActionForeground;

    public bool CanSetReference => comparison.Images.Contains(Image);

    public bool CanSetCandidate => comparison.Images.Count >= 2 && comparison.Images.Contains(Image);

    public bool HasMonitoring => Image.MonitoringRole != ImageMonitoringRole.None;

    public string MonitoringText => Image.MonitoringRole switch
    {
        ImageMonitoringRole.Reference => "Monitoring reference",
        ImageMonitoringRole.Candidate => "Monitoring candidate",
        _ => string.Empty
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    [NotifyPropertyChangedFor(nameof(HasNoThumbnail))]
    public partial Bitmap? Thumbnail { get; private set; }

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasNoThumbnail => Thumbnail is null;

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
        OnPropertyChanged(nameof(RevisionText));
        OnPropertyChanged(nameof(AddedAtText));
        OnPropertyChanged(nameof(MonitoringText));
        OnPropertyChanged(nameof(HasMonitoring));
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

    private int GetRevisionNumber()
    {
        var versionNumber = 1;
        var current = Image;
        var visited = new HashSet<Guid> { current.Id };

        while (current.PreviousVersionImageId is { } previousVersionImageId
               && visited.Add(previousVersionImageId))
        {
            versionNumber++;
            var previous = comparison.Images.FirstOrDefault(image => image.Id == previousVersionImageId);
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
        if (oldValue is not null)
        {
            Dispatcher.UIThread.Post(oldValue.Dispose, DispatcherPriority.Background);
        }
    }
}
