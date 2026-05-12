using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
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

    public ComparisonImageSetViewModel(
        WorkspaceNavigatorViewModel workspace,
        ComparisonDisplayViewModel comparisonDisplay,
        IProjectStorage? projectStorage)
    {
        _workspace = workspace;
        _comparisonDisplay = comparisonDisplay;
        _projectStorage = projectStorage;

        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        _ = RefreshImageRowsAsync();
    }

    public event EventHandler? ImageSetChanged;

    public ObservableCollection<ComparisonImageSetItemViewModel> ImageRows { get; } = [];

    public bool CanSetReferenceImage(ImageAsset? image)
    {
        return _workspace.SelectedComparison is not null
            && image is not null
            && _workspace.SelectedComparison.Images.Contains(image);
    }

    public bool CanSetCandidateImage(ImageAsset? image)
    {
        return _workspace.SelectedComparison is not null
            && image is not null
            && _workspace.SelectedComparison.Images.Count >= 2
            && _workspace.SelectedComparison.Images.Contains(image);
    }

    public async Task<ImageAsset> AddImageAsync(
        string sourceName,
        Stream content,
        string? mediaType = null,
        string? label = null,
        ImageSourceMetadata? originalFileMetadata = null,
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

            if (_workspace.SelectedComparison?.ReferenceImageId == image.Id)
            {
                await _comparisonDisplay.LoadImageAsync(ImageSlot.Left, file);
            }
            else if (_workspace.SelectedComparison?.CandidateImageId == image.Id)
            {
                await _comparisonDisplay.LoadImageAsync(ImageSlot.Right, file);
            }
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

            if (_workspace.SelectedComparison?.ReferenceImageId == image.Id)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await _comparisonDisplay.LoadImageAsync(ImageSlot.Left, image.Label, displayStream);
            }
            else if (_workspace.SelectedComparison?.CandidateImageId == image.Id)
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

        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null || !_workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        image.Label = NormalizeName(label, image.SourceName);
        _workspace.SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);

        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);
        return true;
    }

    public async Task<bool> SetReferenceImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null || !_workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        _workspace.SelectedComparison.SetReferenceImage(image.Id);
        _workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await NotifySelectedComparisonImagesChangedAsync(cancellationToken);

        await SaveProjectAsync(_workspace.SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetReferenceImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetReferenceImageAsync(image, cancellationToken))
        {
            return false;
        }

        await _comparisonDisplay.RefreshCurrentComparisonImagesAsync(_workspace.SelectedComparison, _projectStorage, cancellationToken);
        return true;
    }

    public async Task<bool> SetCandidateImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_workspace.SelectedProject is null || _workspace.SelectedComparison is null || !_workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        if (_workspace.SelectedComparison.Images.Count < 2)
        {
            throw new InvalidOperationException("Setting a candidate image requires at least two images in the comparison.");
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

    public Task RefreshImageRowsAsync(CancellationToken cancellationToken = default)
    {
        ClearImageRows();

        if (_workspace.SelectedComparison is not { } comparison)
        {
            return Task.CompletedTask;
        }

        foreach (var image in comparison.Images
                     .OrderByDescending(image => image.AddedAt)
                     .ThenByDescending(image => image.SourceName, StringComparer.Ordinal))
        {
            var row = new ComparisonImageSetItemViewModel(image, comparison);
            ImageRows.Add(row);
        }

        OnPropertyChanged(nameof(ImageRows));
        return Task.CompletedTask;
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

        await RefreshImageRowsAsync(cancellationToken);
        ImageSetChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedComparison)
            || e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedComparisonImages) && !_suppressWorkspaceImageRefresh)
        {
            _ = RefreshImageRowsAsync();
        }
    }

    private void ClearImageRows()
    {
        foreach (var row in ImageRows)
        {
            row.Dispose();
        }

        ImageRows.Clear();
    }
}
