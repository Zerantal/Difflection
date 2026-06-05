using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;

namespace Difflection.Storage;

public sealed class InMemoryProjectStorage(params Project[] projects) : IProjectStorage
{
    private readonly List<Project> _projects = [..projects];
    private readonly Dictionary<string, byte[]> _imageContents = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Project>>(
            _projects
                .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(project => project.CreatedAt)
                .ToArray());
    }

    public Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_projects.FirstOrDefault(project => project.Id == projectId));
    }

    public Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var index = _projects.FindIndex(existing => existing.Id == project.Id);
        if (index >= 0)
        {
            _projects[index] = project;
        }
        else
        {
            _projects.Add(project);
        }

        return Task.CompletedTask;
    }

    public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _projects.RemoveAll(project => project.Id == projectId);

        foreach (var storageKey in _imageContents.Keys.Where(key => key.Contains(projectId.ToString("N"), StringComparison.Ordinal)).ToArray())
        {
            _imageContents.Remove(storageKey);
        }

        return Task.CompletedTask;
    }

    public async Task<string> SaveImageAsync(
        Guid projectId,
        Guid comparisonSetId,
        ImageAsset image,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(content);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var storageKey = CreateImageStorageKey(projectId, comparisonSetId, image);
        _imageContents[storageKey] = buffer.ToArray();
        image.StorageKey = storageKey;
        return storageKey;
    }

    public Task<Stream> LoadImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(image.StorageKey))
        {
            throw new InvalidOperationException("The image does not have a storage key.");
        }

        if (!_imageContents.TryGetValue(image.StorageKey, out var content))
        {
            throw new FileNotFoundException("The image content was not found in memory.", image.StorageKey);
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(image.StorageKey))
        {
            _imageContents.Remove(image.StorageKey);
        }

        return Task.CompletedTask;
    }

    private static string CreateImageStorageKey(Guid projectId, Guid comparisonSetId, ImageAsset image)
    {
        var extension = Path.GetExtension(image.SourceName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GetExtensionFromMediaType(image.MediaType);
        }

        return Path.Combine(
            "memory",
            projectId.ToString("N"),
            comparisonSetId.ToString("N"),
            $"{image.Id:N}{extension}");
    }

    private static string GetExtensionFromMediaType(string? mediaType)
    {
        return mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            _ => ".img"
        };
    }
}
