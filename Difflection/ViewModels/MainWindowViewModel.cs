using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectStorage? _projectStorage;

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(IProjectStorage projectStorage)
    {
        _projectStorage = projectStorage;
    }

    public ObservableCollection<Project> Projects { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSideBySideView))]
    [NotifyPropertyChangedFor(nameof(IsSplitScreenView))]
    [NotifyPropertyChangedFor(nameof(CurrentViewTitle))]
    public partial ComparisonViewMode SelectedViewMode { get; set; } = ComparisonViewMode.SideBySide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedProject))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectComparisons))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanAddComparison))]
    public partial Project? SelectedProject { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedComparison))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedComparison))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonImages))]
    public partial ComparisonSet? SelectedComparison { get; set; }

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

    public bool HasSelectedProject => SelectedProject is not null;

    public bool HasSelectedComparison => SelectedComparison is not null;

    public bool CanDeleteSelectedProject => HasSelectedProject;

    public bool CanAddComparison => HasSelectedProject;

    public bool CanDeleteSelectedComparison => HasSelectedComparison;

    public IReadOnlyList<ComparisonSet> SelectedProjectComparisons => SelectedProject?.Comparisons ?? [];

    public IReadOnlyList<ImageAsset> SelectedComparisonImages => SelectedComparison?.Images ?? [];

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

    public async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        if (_projectStorage is null)
        {
            return;
        }

        var projects = await _projectStorage.LoadProjectsAsync(cancellationToken);

        Projects.Clear();

        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        SelectedProject = Projects.FirstOrDefault();
        SelectedComparison = SelectedProject?.Comparisons.FirstOrDefault();
    }

    public async Task<Project> AddProjectAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Name = NormalizeName(name, "Untitled Project")
        };

        Projects.Add(project);
        SelectedProject = project;
        SelectedComparison = null;

        await SaveProjectAsync(project, cancellationToken);
        return project;
    }

    public async Task<bool> DeleteProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var removed = Projects.Remove(project);

        if (!removed)
        {
            return false;
        }

        if (SelectedProject == project)
        {
            SelectedProject = Projects.FirstOrDefault();
            SelectedComparison = SelectedProject?.Comparisons.FirstOrDefault();
        }

        if (_projectStorage is not null)
        {
            await _projectStorage.DeleteProjectAsync(project.Id, cancellationToken);
        }

        return true;
    }

    public Task<bool> DeleteSelectedProjectAsync(CancellationToken cancellationToken = default)
    {
        return SelectedProject is null
            ? Task.FromResult(false)
            : DeleteProjectAsync(SelectedProject, cancellationToken);
    }

    public async Task<ComparisonSet> AddComparisonAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null)
        {
            throw new InvalidOperationException("A project must be selected before adding a comparison.");
        }

        var comparison = new ComparisonSet
        {
            Name = NormalizeName(name, "Untitled Comparison")
        };

        SelectedProject.Comparisons.Add(comparison);
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        SelectedComparison = comparison;
        OnPropertyChanged(nameof(SelectedProjectComparisons));

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return comparison;
    }

    public async Task<bool> DeleteComparisonAsync(ComparisonSet comparison, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (SelectedProject is null)
        {
            return false;
        }

        var removed = SelectedProject.Comparisons.Remove(comparison);

        if (!removed)
        {
            return false;
        }

        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;

        if (SelectedComparison == comparison)
        {
            SelectedComparison = SelectedProject.Comparisons.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedProjectComparisons));
        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    public Task<bool> DeleteSelectedComparisonAsync(CancellationToken cancellationToken = default)
    {
        return SelectedComparison is null
            ? Task.FromResult(false)
            : DeleteComparisonAsync(SelectedComparison, cancellationToken);
    }

    public async Task<ImageAsset> AddImageAsync(
        string sourceName,
        Stream content,
        string? mediaType = null,
        string? label = null,
        ImageSourceMetadata? originalFileMetadata = null,
        CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null)
        {
            throw new InvalidOperationException("A project must be selected before adding an image.");
        }

        if (SelectedComparison is null)
        {
            throw new InvalidOperationException("A comparison must be selected before adding an image.");
        }

        ArgumentNullException.ThrowIfNull(content);

        var normalizedSourceName = NormalizeSourceName(sourceName);
        var fallbackLabel = NormalizeName(Path.GetFileNameWithoutExtension(normalizedSourceName), normalizedSourceName);
        var image = new ImageAsset
        {
            Label = NormalizeName(label, fallbackLabel),
            SourceName = normalizedSourceName,
            MediaType = mediaType,
            OriginalFileMetadata = originalFileMetadata
        };

        if (_projectStorage is not null)
        {
            await _projectStorage.SaveImageAsync(
                SelectedProject.Id,
                SelectedComparison.Id,
                image,
                content,
                cancellationToken);
        }

        SelectedComparison.AddImage(image);
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(SelectedComparisonImages));

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return image;
    }

    public async Task<ImageAsset> AddImageAsync(
        IStorageFile file,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = await file.OpenReadAsync();
        return await AddImageAsync(file.Name, stream, label: label, cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null)
        {
            return false;
        }

        var removed = SelectedComparison.RemoveImage(image.Id);

        if (!removed)
        {
            return false;
        }

        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(SelectedComparisonImages));

        await SaveProjectAsync(SelectedProject, cancellationToken);

        if (_projectStorage is not null)
        {
            await _projectStorage.DeleteImageAsync(image, cancellationToken);
        }

        return true;
    }

    public async Task<bool> LabelImageAsync(ImageAsset image, string? label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null || !SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        image.Label = NormalizeName(label, image.SourceName);
        SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(SelectedComparisonImages));

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

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

    private static string NormalizeName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private static string NormalizeSourceName(string? sourceName)
    {
        var fileName = Path.GetFileName(sourceName?.Trim());
        return string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;
    }

    private Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        return _projectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
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

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (value is null || SelectedComparison is null || !value.Comparisons.Contains(SelectedComparison))
        {
            SelectedComparison = value?.Comparisons.FirstOrDefault();
        }
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
