using System;
using System.Collections.Generic;
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

public partial class ComparisonImageSetViewModel(
    WorkspaceNavigatorViewModel workspace,
    ComparisonDisplayViewModel comparisonDisplay,
    IProjectStorage? projectStorage) : ViewModelBase
{
    public event EventHandler? ImageSetChanged;

    public bool CanSetReferenceImage(ImageAsset? image)
    {
        return workspace.SelectedComparison is not null
            && image is not null
            && workspace.SelectedComparison.Images.Contains(image);
    }

    public bool CanSetCandidateImage(ImageAsset? image)
    {
        return workspace.SelectedComparison is not null
            && image is not null
            && workspace.SelectedComparison.Images.Count >= 2
            && workspace.SelectedComparison.Images.Contains(image);
    }

    public async Task<ImageAsset> AddImageAsync(
        string sourceName,
        Stream content,
        string? mediaType = null,
        string? label = null,
        ImageSourceMetadata? originalFileMetadata = null,
        CancellationToken cancellationToken = default)
    {
        if (workspace.SelectedProject is null)
        {
            throw new InvalidOperationException("A project must be selected before adding an image.");
        }

        if (workspace.SelectedComparison is null)
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

        if (projectStorage is not null)
        {
            await projectStorage.SaveImageAsync(
                workspace.SelectedProject.Id,
                workspace.SelectedComparison.Id,
                image,
                content,
                cancellationToken);
        }

        workspace.RenameDefaultComparisonFromFirstImage(workspace.SelectedComparison, image.Label);
        workspace.SelectedComparison.AddImage(image);
        workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(workspace.SelectedProject, cancellationToken);
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

        await workspace.EnsureProjectAndComparisonAsync(cancellationToken);

        var addedImages = new List<ImageAsset>(imageFiles.Length);
        foreach (var file in imageFiles)
        {
            var image = await AddImageAsync(file, cancellationToken: cancellationToken);
            addedImages.Add(image);

            if (workspace.SelectedComparison?.ReferenceImageId == image.Id)
            {
                await comparisonDisplay.LoadImageAsync(ImageSlot.Left, file);
            }
            else if (workspace.SelectedComparison?.CandidateImageId == image.Id)
            {
                await comparisonDisplay.LoadImageAsync(ImageSlot.Right, file);
            }
        }

        return addedImages;
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAfterCommittingRenamesAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        await workspace.CommitActiveInlineRenamesAsync(cancellationToken);
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

        await workspace.EnsureProjectAndComparisonAsync(cancellationToken);

        var addedImages = new List<ImageAsset>(files.Length);
        foreach (var file in files)
        {
            using var addStream = new MemoryStream(file.Bytes, writable: false);
            var image = await AddImageAsync(file.Name, addStream, cancellationToken: cancellationToken);
            addedImages.Add(image);

            if (workspace.SelectedComparison?.ReferenceImageId == image.Id)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await comparisonDisplay.LoadImageAsync(ImageSlot.Left, image.Label, displayStream);
            }
            else if (workspace.SelectedComparison?.CandidateImageId == image.Id)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await comparisonDisplay.LoadImageAsync(ImageSlot.Right, image.Label, displayStream);
            }
        }

        return addedImages;
    }

    public async Task<bool> DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (workspace.SelectedProject is null || workspace.SelectedComparison is null)
        {
            return false;
        }

        var removed = workspace.SelectedComparison.RemoveImage(image.Id);

        if (!removed)
        {
            return false;
        }

        workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(workspace.SelectedProject, cancellationToken);

        if (projectStorage is not null)
        {
            await projectStorage.DeleteImageAsync(image, cancellationToken);
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

        await comparisonDisplay.RefreshCurrentComparisonImagesAsync(workspace.SelectedComparison, projectStorage, cancellationToken);
        return true;
    }

    public async Task<bool> LabelImageAsync(ImageAsset image, string? label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (workspace.SelectedProject is null || workspace.SelectedComparison is null || !workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        image.Label = NormalizeName(label, image.SourceName);
        workspace.SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(workspace.SelectedProject, cancellationToken);
        return true;
    }

    public async Task<bool> SetReferenceImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (workspace.SelectedProject is null || workspace.SelectedComparison is null || !workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        workspace.SelectedComparison.SetReferenceImage(image.Id);
        workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(workspace.SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetReferenceImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetReferenceImageAsync(image, cancellationToken))
        {
            return false;
        }

        await comparisonDisplay.RefreshCurrentComparisonImagesAsync(workspace.SelectedComparison, projectStorage, cancellationToken);
        return true;
    }

    public async Task<bool> SetCandidateImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (workspace.SelectedProject is null || workspace.SelectedComparison is null || !workspace.SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        if (workspace.SelectedComparison.Images.Count < 2)
        {
            throw new InvalidOperationException("Setting a candidate image requires at least two images in the comparison.");
        }

        workspace.SelectedComparison.SetCandidateImage(image.Id);
        workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(workspace.SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetCandidateImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetCandidateImageAsync(image, cancellationToken))
        {
            return false;
        }

        await comparisonDisplay.RefreshCurrentComparisonImagesAsync(workspace.SelectedComparison, projectStorage, cancellationToken);
        return true;
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
        return projectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
    }

    private void NotifySelectedComparisonImagesChanged()
    {
        workspace.NotifySelectedComparisonImagesChanged();
        ImageSetChanged?.Invoke(this, EventArgs.Empty);
    }
}
