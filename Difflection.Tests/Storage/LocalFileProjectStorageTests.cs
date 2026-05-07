using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;
using Difflection.Storage;
using Xunit;

namespace Difflection.Tests.Storage;

public sealed class LocalFileProjectStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Difflection.Tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileProjectStorage _storage;
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public LocalFileProjectStorageTests()
    {
        _storage = new LocalFileProjectStorage(_rootPath);
    }

    [Fact]
    public async Task SaveProjectAsync_persists_project_metadata()
    {
        var project = new Project { Name = "Website Checks" };
        var comparison = new ComparisonSet { Name = "Landing Page" };
        var reference = CreateImage("reference.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(reference);
        comparison.AddImage(candidate);
        project.Comparisons.Add(comparison);

        await _storage.SaveProjectAsync(project, CancellationToken);

        var loaded = await _storage.LoadProjectAsync(project.Id, CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal("Website Checks", loaded.Name);
        var loadedComparison = Assert.Single(loaded.Comparisons);
        Assert.Equal("Landing Page", loadedComparison.Name);
        Assert.Equal(reference.Id, loadedComparison.ReferenceImageId);
        Assert.Equal(candidate.Id, loadedComparison.CandidateImageId);
    }

    [Fact]
    public async Task LoadProjectsAsync_loads_saved_projects_ordered_by_name()
    {
        await _storage.SaveProjectAsync(new Project { Name = "Zeta" }, CancellationToken);
        await _storage.SaveProjectAsync(new Project { Name = "Alpha" }, CancellationToken);

        var projects = await _storage.LoadProjectsAsync(CancellationToken);

        Assert.Collection(
            projects,
            project => Assert.Equal("Alpha", project.Name),
            project => Assert.Equal("Zeta", project.Name));
    }

    [Fact]
    public async Task SaveImageAsync_persists_image_content_and_updates_storage_key()
    {
        var project = new Project();
        var comparison = new ComparisonSet();
        var image = CreateImage("reference.png");
        var content = Encoding.UTF8.GetBytes("image bytes");

        await _storage.SaveImageAsync(project.Id, comparison.Id, image, new MemoryStream(content), CancellationToken);

        Assert.Contains(project.Id.ToString("N"), image.StorageKey);
        Assert.Contains(comparison.Id.ToString("N"), image.StorageKey);
        Assert.EndsWith(".png", image.StorageKey);

        await using var imageStream = await _storage.LoadImageAsync(image, CancellationToken);
        using var reader = new StreamReader(imageStream, Encoding.UTF8);
        Assert.Equal("image bytes", await reader.ReadToEndAsync(CancellationToken));
    }

    [Fact]
    public async Task DeleteImageAsync_removes_stored_image_file()
    {
        var project = new Project();
        var comparison = new ComparisonSet();
        var image = CreateImage("candidate.jpg");
        await _storage.SaveImageAsync(project.Id, comparison.Id, image, new MemoryStream([1, 2, 3]), CancellationToken);

        await _storage.DeleteImageAsync(image, CancellationToken);

        await Assert.ThrowsAnyAsync<IOException>(() => _storage.LoadImageAsync(image, CancellationToken));
    }

    [Fact]
    public async Task DeleteProjectAsync_removes_project_metadata_and_images()
    {
        var project = new Project { Name = "Disposable" };
        var comparison = new ComparisonSet();
        var image = CreateImage("candidate.jpg");
        project.Comparisons.Add(comparison);

        await _storage.SaveImageAsync(project.Id, comparison.Id, image, new MemoryStream([1, 2, 3]), CancellationToken);
        comparison.AddImage(image);
        await _storage.SaveProjectAsync(project, CancellationToken);

        await _storage.DeleteProjectAsync(project.Id, CancellationToken);

        Assert.Null(await _storage.LoadProjectAsync(project.Id, CancellationToken));
        await Assert.ThrowsAnyAsync<IOException>(() => _storage.LoadImageAsync(image, CancellationToken));
    }

    [Fact]
    public async Task LoadProjectAsync_repairs_stale_role_assignments_without_changing_updated_at()
    {
        var project = new Project();
        var comparison = new ComparisonSet();
        var image = CreateImage("reference.png");
        comparison.AddImage(image);
        comparison.UpdatedAt = DateTimeOffset.UnixEpoch;
        project.Comparisons.Add(comparison);

        await _storage.SaveProjectAsync(project, CancellationToken);

        var projectFilePath = GetProjectFilePath(project.Id);
        var projectJson = JsonNode.Parse(await File.ReadAllTextAsync(projectFilePath, TestContext.Current.CancellationToken))!.AsObject();
        var comparisonJson = projectJson["Comparisons"]!.AsArray()[0]!.AsObject();
        comparisonJson["ReferenceImageId"] = Guid.NewGuid().ToString();
        comparisonJson["CandidateImageId"] = Guid.NewGuid().ToString();
        await File.WriteAllTextAsync(projectFilePath, projectJson.ToJsonString(), TestContext.Current.CancellationToken);

        var loaded = await _storage.LoadProjectAsync(project.Id, CancellationToken);

        var loadedComparison = Assert.Single(loaded!.Comparisons);
        Assert.Equal(image.Id, loadedComparison.ReferenceImageId);
        Assert.Null(loadedComparison.CandidateImageId);
        Assert.Equal(DateTimeOffset.UnixEpoch, loadedComparison.UpdatedAt);
    }

    [Fact]
    public async Task SaveProjectAsync_does_not_serialize_computed_image_properties()
    {
        var project = new Project();
        var comparison = new ComparisonSet();
        comparison.AddImage(CreateImage("reference.png"));
        project.Comparisons.Add(comparison);

        await _storage.SaveProjectAsync(project, CancellationToken);

        var projectJson = await File.ReadAllTextAsync(GetProjectFilePath(project.Id), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("\"ReferenceImage\":", projectJson);
        Assert.DoesNotContain("\"CandidateImage\":", projectJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static ImageAsset CreateImage(string sourceName)
    {
        return new ImageAsset
        {
            Label = sourceName,
            SourceName = sourceName,
            StorageKey = $"source/{sourceName}"
        };
    }

    private string GetProjectFilePath(Guid projectId)
    {
        return Path.Combine(_rootPath, "projects", projectId.ToString("N"), "project.json");
    }
}
