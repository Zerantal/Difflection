using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.ViewModels;

public sealed partial class ComparisonImageSetItemViewModel(ImageAsset image, ComparisonSet comparison) : ViewModelBase, IDisposable
{
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
        }
    }

    public string SourceName => Image.SourceName;

    public bool IsReference => comparison.ReferenceImageId == Image.Id;

    public bool IsCandidate => comparison.CandidateImageId == Image.Id;

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

    partial void OnThumbnailChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        if (oldValue is not null)
        {
            Dispatcher.UIThread.Post(oldValue.Dispose, DispatcherPriority.Background);
        }
    }
}
