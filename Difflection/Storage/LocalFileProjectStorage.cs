using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;

namespace Difflection.Storage;

public sealed class LocalFileProjectStorage(string rootPath) : IProjectStorage
{
    private const string ProjectsDirectoryName = "projects";
    private const string ComparisonsDirectoryName = "comparisons";
    private const string ProjectFileName = "project.json";
    private const string ProjectBackupFileName = "project.json.bak";
    private const string ImagesDirectoryName = "images";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string _rootPath = string.IsNullOrWhiteSpace(rootPath)
        ? throw new ArgumentException("A storage root path is required.", nameof(rootPath))
        : rootPath;

    public event EventHandler<ProjectStorageLoadIssueEventArgs>? ProjectLoadIssue;

    public async Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projectsPath = GetProjectsPath();

        if (!Directory.Exists(projectsPath))
        {
            return [];
        }

        var projects = new List<Project>();

        foreach (var projectFilePath in Directory.EnumerateFiles(projectsPath, ProjectFileName, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = await LoadProjectFileWithRecoveryAsync(projectFilePath, cancellationToken);

            if (project is not null)
            {
                RepairProject(project);
                projects.Add(project);
            }
        }

        return projects
            .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(project => project.CreatedAt)
            .ToArray();
    }

    public async Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectFilePath = GetProjectFilePath(projectId);
        var project = await LoadProjectFileWithRecoveryAsync(projectFilePath, cancellationToken);

        if (project is not null)
        {
            RepairProject(project);
        }

        return project;
    }

    public async Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        RepairProject(project);
        var projectPath = GetProjectPath(project.Id);
        Directory.CreateDirectory(projectPath);

        var projectFilePath = GetProjectFilePath(project.Id);
        var temporaryFilePath = Path.Combine(projectPath, $"{ProjectFileName}.{Guid.NewGuid():N}.tmp");
        var backupFilePath = Path.Combine(projectPath, ProjectBackupFileName);

        try
        {
            await using (var stream = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, project, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(projectFilePath))
            {
                File.Replace(temporaryFilePath, projectFilePath, backupFilePath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryFilePath, projectFilePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var projectPath = GetProjectPath(projectId);

        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, recursive: true);
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

        var storageKey = CreateImageStorageKey(projectId, comparisonSetId, image);
        var imagePath = GetPathFromStorageKey(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);

        await using var fileStream = File.Create(imagePath);
        await content.CopyToAsync(fileStream, cancellationToken);

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

        Stream stream = File.OpenRead(GetPathFromStorageKey(image.StorageKey));
        return Task.FromResult(stream);
    }

    public Task DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(image.StorageKey))
        {
            return Task.CompletedTask;
        }

        var imagePath = GetPathFromStorageKey(image.StorageKey);

        if (File.Exists(imagePath))
        {
            File.Delete(imagePath);
        }

        return Task.CompletedTask;
    }

    private static void RepairProject(Project project)
    {
        foreach (var comparison in project.Comparisons)
        {
            comparison.RepairChannelAssignments(updateTimestamp: false);
        }
    }

    private static string CreateImageStorageKey(Guid projectId, Guid comparisonSetId, ImageAsset image)
    {
        var extension = Path.GetExtension(image.SourceName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GetExtensionFromMediaType(image.MediaType);
        }

        return Path.Combine(
            ProjectsDirectoryName,
            projectId.ToString("N"),
            ComparisonsDirectoryName,
            comparisonSetId.ToString("N"),
            ImagesDirectoryName,
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

    private async Task<Project?> LoadProjectFileWithRecoveryAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(projectFilePath))
        {
            return null;
        }

        try
        {
            return await LoadProjectFileAsync(projectFilePath, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverableProjectLoadException(exception))
        {
            var backupFilePath = Path.Combine(Path.GetDirectoryName(projectFilePath)!, ProjectBackupFileName);
            if (File.Exists(backupFilePath))
            {
                try
                {
                    var recoveredProject = await LoadProjectFileAsync(backupFilePath, cancellationToken);
                    ProjectLoadIssue?.Invoke(this, new ProjectStorageLoadIssueEventArgs(
                        projectFilePath,
                        exception,
                        recoveredFromBackup: recoveredProject is not null));
                    return recoveredProject;
                }
                catch (Exception backupException) when (IsRecoverableProjectLoadException(backupException))
                {
                    ProjectLoadIssue?.Invoke(this, new ProjectStorageLoadIssueEventArgs(
                        projectFilePath,
                        new AggregateException(exception, backupException),
                        recoveredFromBackup: false));
                    return null;
                }
            }

            ProjectLoadIssue?.Invoke(this, new ProjectStorageLoadIssueEventArgs(
                projectFilePath,
                exception,
                recoveredFromBackup: false));
            return null;
        }
    }

    private static async Task<Project?> LoadProjectFileAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(projectFilePath);
        return await JsonSerializer.DeserializeAsync<Project>(stream, JsonOptions, cancellationToken);
    }

    private static bool IsRecoverableProjectLoadException(Exception exception)
    {
        return exception is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException;
    }

    private string GetProjectsPath()
    {
        return Path.Combine(_rootPath, ProjectsDirectoryName);
    }

    private string GetProjectPath(Guid projectId)
    {
        return Path.Combine(GetProjectsPath(), projectId.ToString("N"));
    }

    private string GetProjectFilePath(Guid projectId)
    {
        return Path.Combine(GetProjectPath(projectId), ProjectFileName);
    }

    private string GetPathFromStorageKey(string storageKey)
    {
        var normalizedStorageKey = storageKey.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedStorageKey));
        var fullRootPath = Path.GetFullPath(_rootPath);
        var relativePath = Path.GetRelativePath(fullRootPath, fullPath);

        if (relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("The storage key resolves outside the configured storage root.");
        }

        return fullPath;
    }
}
