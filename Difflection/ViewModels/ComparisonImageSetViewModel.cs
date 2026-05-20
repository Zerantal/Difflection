using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Difflection.Infrastructure;
using Difflection.Models;
using Difflection.Monitoring;
using Difflection.Storage;

namespace Difflection.ViewModels;

public partial class ComparisonImageSetViewModel : ViewModelBase
{
    private readonly WorkspaceNavigatorViewModel _workspace;
    private readonly ComparisonDisplayViewModel _comparisonDisplay;
    private readonly IProjectStorage? _projectStorage;
    private bool _suppressWorkspaceImageRefresh;
    private CancellationTokenSource? _scheduledImageRowsRefresh;
    private ComparisonSet? _imageRowsComparison;

    public ComparisonImageSetViewModel(
        WorkspaceNavigatorViewModel workspace,
        ComparisonDisplayViewModel comparisonDisplay,
        IProjectStorage? projectStorage)
    {
        _workspace = workspace;
        _comparisonDisplay = comparisonDisplay;
        _projectStorage = projectStorage;

        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        _ = ObservedTask.ReportFailureAsync(
            RefreshImageRowsAsync(),
            "Difflection could not refresh the comparison image set.");
    }

    public event EventHandler? ImageSetChanged;

    public ObservableCollection<ComparisonImageSetItemViewModel> ImageRows { get; } = [];

    public ObservableCollection<ComparisonImageSetItemViewModel> BaselineRevisionRows { get; } = [];

    public ObservableCollection<ComparisonImageSetItemViewModel> CandidateRevisionRows { get; } = [];

    public bool HasBaselineRevisionRows => BaselineRevisionRows.Count > 0;

    public bool HasCandidateRevisionRows => CandidateRevisionRows.Count > 0;

    public bool CanClearNonRoleImages =>
        _workspace.SelectedComparison?.Images.Any(IsNotCurrentRoleImage) == true;

    public bool CanSetBaselineImage(ImageAsset? image)
    {
        return _workspace.SelectedComparison is not null
            && image is not null
            && _workspace.SelectedComparison.BaselineChannel.Contains(image);
    }

    public bool CanSetCandidateImage(ImageAsset? image)
    {
        return _workspace.SelectedComparison is not null
            && image is not null
            && _workspace.SelectedComparison.CandidateChannel.Contains(image);
    }

    public async Task<ImageAsset> AddImageAsync(
        string sourceName,
        Stream content,
        string? mediaType = null,
        string? label = null,
        ImageSourceMetadata? originalFileMetadata = null,
        DateTimeOffset? addedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (_workspace.SelectedProject is null)
        {
            throw new InvalidOperationException("A project must be selected before adding an image.");
        }

        if (_workspace.SelectedComparison is null)
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
            AddedAt = addedAt ?? DateTimeOffset.UtcNow,
            OriginalFileMetadata = originalFileMetadata
        };

        if (_projectStorage is not null)
        {
            await _projectStorage.SaveImageAsync(
                _workspace.SelectedProject.Id,
                _workspace.SelectedComparison.Id,
                image,
                content,
                cancellationToken);
        }

        _workspace.RenameDefaultComparisonFromFirstImage(_workspace.SelectedComparison, image.Label);
        _workspace.SelectedComparison.AddImage(image);
        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);

        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);
        return image;
    }

    public async Task<ImageAsset> AddImageAsync(
        IStorageFile file,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = await file.OpenReadAsync();
        var metadata = await ImageSourceMetadataReader.ReadAsync(file, stream, cancellationToken);
        return await AddImageAsync(file.Name, stream, label: label, originalFileMetadata: metadata, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var imageFiles = maxFiles is null
            ? files.ToArray()
            : files.Take(maxFiles.Value).ToArray();

        if (imageFiles.Length == 0)
        {
            return [];
        }

        await _workspace.EnsureProjectAndComparisonAsync(cancellationToken);

        var addedImages = new List<ImageAsset>(imageFiles.Length);
        foreach (var file in imageFiles)
        {
            var image = await AddImageAsync(file, cancellationToken: cancellationToken);
            addedImages.Add(image);
            await LoadAddedImageIntoActiveSlotAsync(image, file, cancellationToken);
        }

        return addedImages;
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAfterCommittingRenamesAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        await _workspace.CommitActiveInlineRenamesAsync(cancellationToken);
        return await AddFilesToCurrentComparisonAsync(files, maxFiles, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddBrowserFilesToCurrentComparisonAsync(
        IReadOnlyList<string> fileNames,
        IReadOnlyList<byte[]> fileContents,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentNullException.ThrowIfNull(fileContents);

        if (fileNames.Count != fileContents.Count)
        {
            return [];
        }

        var files = fileNames
            .Zip(fileContents, (name, bytes) => (Name: name, Bytes: bytes))
            .Take(maxFiles ?? int.MaxValue)
            .ToArray();

        if (files.Length == 0)
        {
            return [];
        }

        await _workspace.EnsureProjectAndComparisonAsync(cancellationToken);

        var addedImages = new List<ImageAsset>(files.Length);
        foreach (var file in files)
        {
            using var addStream = new MemoryStream(file.Bytes, writable: false);
            var image = await AddImageAsync(file.Name, addStream, cancellationToken: cancellationToken);
            addedImages.Add(image);

            if (_workspace.SelectedComparison?.BaselineChannel.Contains(image) == true)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await _comparisonDisplay.LoadImageAsync(ImageSlot.Left, image.Label, displayStream);
            }
            else if (_workspace.SelectedComparison?.CandidateChannel.Contains(image) == true)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await _comparisonDisplay.LoadImageAsync(ImageSlot.Right, image.Label, displayStream);
            }
        }

        return addedImages;
    }

    public async Task<bool> DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null)
        {
            return false;
        }

        var removed = _workspace.SelectedComparison.RemoveImage(image.Id);

        if (!removed)
        {
            return false;
        }

        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);

        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);

        if (_projectStorage is not null)
        {
            await _projectStorage.DeleteImageAsync(image, cancellationToken);
        }

        return true;
    }

    [RelayCommand]
    public async Task<int> ClearNonRoleImagesAndRefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null)
        {
            return 0;
        }

        var removedImages = _workspace.SelectedComparison.Images
            .Where(IsNotCurrentRoleImage)
            .ToArray();

        if (removedImages.Length == 0)
        {
            return 0;
        }

        foreach (var image in removedImages)
        {
            _workspace.SelectedComparison.RemoveImage(image.Id);
        }

        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);
        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);

        if (_projectStorage is not null)
        {
            foreach (var image in removedImages)
            {
                await _projectStorage.DeleteImageAsync(image, cancellationToken);
            }
        }

        await _comparisonDisplay.RefreshCurrentComparisonImagesAsync(_workspace.SelectedComparison, _projectStorage, cancellationToken);
        OnPropertyChanged(nameof(CanClearNonRoleImages));
        return removedImages.Length;
    }

    [RelayCommand]
    public async Task<bool> DeleteImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await DeleteImageAsync(image, cancellationToken))
        {
            return false;
        }

        await _comparisonDisplay.RefreshCurrentComparisonImagesAsync(_workspace.SelectedComparison, _projectStorage, cancellationToken);
        return true;
    }

    public async Task<bool> LabelImageAsync(ImageAsset image, string? label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_workspace.SelectedProject is null ||
            _workspace.SelectedComparison is null ||
            !_workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        image.Label = NormalizeName(label, image.SourceName);
        ImageRows.FirstOrDefault(row => row.Id == image.Id)?.RefreshDisplayMetadata();
        _workspace.SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);
        return true;
    }

    public async Task<bool> SetBaselineImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null || !_workspace.SelectedComparison.BaselineChannel.Contains(image))
        {
            return false;
        }

        _workspace.SelectedComparison.SetBaselineImage(image.Id);
        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);

        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetBaselineImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetBaselineImageAsync(image, cancellationToken))
        {
            return false;
        }

        await _comparisonDisplay.RefreshCurrentComparisonImagesAsync(_workspace.SelectedComparison, _projectStorage, cancellationToken);
        return true;
    }

    public async Task<bool> SetCandidateImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null || !_workspace.SelectedComparison.CandidateChannel.Contains(image))
        {
            return false;
        }

        _workspace.SelectedComparison.SetCandidateImage(image.Id);
        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);

        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetCandidateImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetCandidateImageAsync(image, cancellationToken))
        {
            return false;
        }

        await _comparisonDisplay.RefreshCurrentComparisonImagesAsync(_workspace.SelectedComparison, _projectStorage, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetActiveImageAndRefreshAsync(ComparisonImageSetItemViewModel row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var changed = row.IsBaseline
            ? await SetBaselineImageAsync(row.Image, cancellationToken)
            : await SetCandidateImageAsync(row.Image, cancellationToken);

        if (!changed)
        {
            return false;
        }

        await _comparisonDisplay.RefreshCurrentComparisonImagesAsync(_workspace.SelectedComparison, _projectStorage, cancellationToken);
        return true;
    }

    public async Task RefreshImageRowsAsync(CancellationToken cancellationToken = default, bool force = true)
    {
        var comparison = _workspace.SelectedComparison;
        if (!force && ReferenceEquals(comparison, _imageRowsComparison))
        {
            return;
        }

        ClearImageRows();
        _imageRowsComparison = comparison;

        if (comparison is null)
        {
            return;
        }

        await AddRevisionRowsAsync(BaselineRevisionRows, comparison.BaselineChannel, comparison, cancellationToken);
        await AddRevisionRowsAsync(CandidateRevisionRows, comparison.CandidateChannel, comparison, cancellationToken);

        OnPropertyChanged(nameof(ImageRows));
        OnPropertyChanged(nameof(BaselineRevisionRows));
        OnPropertyChanged(nameof(CandidateRevisionRows));
        OnPropertyChanged(nameof(HasBaselineRevisionRows));
        OnPropertyChanged(nameof(HasCandidateRevisionRows));
    }

    private async Task LoadAddedImageIntoActiveSlotAsync(
        ImageAsset image,
        IStorageFile file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_workspace.SelectedComparison?.BaselineChannel.Contains(image) == true)
        {
            await _comparisonDisplay.LoadImageAsync(ImageSlot.Left, file);
        }
        else if (_workspace.SelectedComparison?.CandidateChannel.Contains(image) == true)
        {
            await _comparisonDisplay.LoadImageAsync(ImageSlot.Right, file);
        }
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

    private async Task NotifySelectedComparisonImagesChangedAsync(CancellationToken cancellationToken)
    {
        try
        {
            _suppressWorkspaceImageRefresh = true;
            _workspace.NotifySelectedComparisonImagesChanged();
        }
        finally
        {
            _suppressWorkspaceImageRefresh = false;
        }

        await RefreshImageRowsAsync(cancellationToken, force: true);
        ImageSetChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanClearNonRoleImages));
    }

    private bool IsNotCurrentRoleImage(ImageAsset image)
    {
        return _workspace.SelectedComparison is { } comparison
            && comparison.BaselineImageId != image.Id
            && comparison.CandidateImageId != image.Id;
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedComparison))
        {
            OnPropertyChanged(nameof(CanClearNonRoleImages));
            ScheduleImageRowsRefresh();
        }
        else if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedComparisonImages) && !_suppressWorkspaceImageRefresh)
        {
            _ = ObservedTask.ReportFailureAsync(
                RefreshImageRowsAsync(force: true),
                "Difflection could not refresh the comparison image set.");
        }
    }

    private void ScheduleImageRowsRefresh()
    {
        _scheduledImageRowsRefresh?.Cancel();
        var refresh = new CancellationTokenSource();
        _scheduledImageRowsRefresh = refresh;

        Dispatcher.UIThread.Post(
            () => _ = ObservedTask.ReportFailureAsync(
                RefreshImageRowsAfterSelectionSettlesAsync(refresh),
                "Difflection could not refresh the comparison image set."),
            DispatcherPriority.Background);
    }

    private async Task RefreshImageRowsAfterSelectionSettlesAsync(CancellationTokenSource refresh)
    {
        if (refresh.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await RefreshImageRowsAsync(refresh.Token, force: false);
        }
        catch (OperationCanceledException)
        {
            // A newer selection superseded this refresh.
        }
        finally
        {
            if (ReferenceEquals(_scheduledImageRowsRefresh, refresh))
            {
                _scheduledImageRowsRefresh = null;
            }

            refresh.Dispose();
        }
    }

    private void ClearImageRows()
    {
        foreach (var row in ImageRows)
        {
            row.Dispose();
        }

        ImageRows.Clear();
        BaselineRevisionRows.Clear();
        CandidateRevisionRows.Clear();
        OnPropertyChanged(nameof(HasBaselineRevisionRows));
        OnPropertyChanged(nameof(HasCandidateRevisionRows));
    }

    private async Task AddRevisionRowsAsync(
        ObservableCollection<ComparisonImageSetItemViewModel> rows,
        ComparisonChannel channel,
        ComparisonSet comparison,
        CancellationToken cancellationToken)
    {
        foreach (var image in channel.Images
                     .OrderByDescending(image => image.AddedAt)
                     .ThenByDescending(image => image.SourceName, StringComparer.Ordinal))
        {
            var row = new ComparisonImageSetItemViewModel(image, comparison, channel);
            ImageRows.Add(row);
            rows.Add(row);

            if (_projectStorage is not null)
            {
                await row.LoadThumbnailAsync(_projectStorage, cancellationToken);
            }
        }
    }
}
