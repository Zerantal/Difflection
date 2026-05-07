using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Difflection.Models;
using Difflection.Storage;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [AvaloniaFact]
    public async Task Difference_status_updates_when_both_images_are_loaded()
    {
        var viewModel = new MainWindowViewModel();

        await viewModel.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left-reference.png", 8, 8));

        Assert.Equal("Load two images to compare", viewModel.DifferenceStatusText);

        await viewModel.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("candidate.png", 8, 8));

        Assert.StartsWith("Difference 100.0%", viewModel.DifferenceStatusText);
        Assert.Contains("RMS error", viewModel.DifferenceStatusText);
        Assert.Contains("Compared 8x8", viewModel.DifferenceStatusText);
    }

    [Fact]
    public async Task LoadProjectsAsync_populates_projects_and_selects_first_project_and_comparison()
    {
        var project = new Project { Name = "Project A" };
        var comparison = new ComparisonSet { Name = "Comparison A" };
        project.Comparisons.Add(comparison);
        var storage = new FakeProjectStorage(project);
        var viewModel = new MainWindowViewModel(storage);

        await viewModel.LoadProjectsAsync(TestContext.Current.CancellationToken);

        Assert.Same(project, Assert.Single(viewModel.Projects));
        Assert.Same(project, viewModel.SelectedProject);
        Assert.Same(comparison, viewModel.SelectedComparison);
    }

    [Fact]
    public async Task AddProjectAsync_adds_selects_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        var project = await viewModel.AddProjectAsync("  Visual Checks  ", TestContext.Current.CancellationToken);

        Assert.Equal("Visual Checks", project.Name);
        Assert.Same(project, Assert.Single(viewModel.Projects));
        Assert.Same(project, viewModel.SelectedProject);
        Assert.Null(viewModel.SelectedComparison);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task DeleteSelectedProjectAsync_deletes_project_and_selects_next_available_project()
    {
        var first = new Project { Name = "First" };
        var second = new Project { Name = "Second" };
        var storage = new FakeProjectStorage(first, second);
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.LoadProjectsAsync(TestContext.Current.CancellationToken);

        var deleted = await viewModel.DeleteSelectedProjectAsync(TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.DoesNotContain(first, viewModel.Projects);
        Assert.Same(second, viewModel.SelectedProject);
        Assert.Contains(first.Id, storage.DeletedProjectIds);
    }

    [Fact]
    public async Task AddComparisonAsync_adds_selects_and_saves_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var comparison = await viewModel.AddComparisonAsync("  Header States  ", TestContext.Current.CancellationToken);

        Assert.Equal("Header States", comparison.Name);
        Assert.Same(comparison, Assert.Single(project.Comparisons));
        Assert.Same(comparison, viewModel.SelectedComparison);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task DeleteSelectedComparisonAsync_deletes_comparison_and_selects_next_available_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var first = await viewModel.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        var second = await viewModel.AddComparisonAsync("Second", TestContext.Current.CancellationToken);
        viewModel.SelectedComparison = first;
        storage.SavedProjects.Clear();

        var deleted = await viewModel.DeleteSelectedComparisonAsync(TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.DoesNotContain(first, project.Comparisons);
        Assert.Same(second, viewModel.SelectedComparison);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task AddComparisonAsync_requires_selected_project()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.AddComparisonAsync("No Project", TestContext.Current.CancellationToken));

        Assert.Contains("project must be selected", exception.Message);
    }

    [Fact]
    public async Task AddImageAsync_adds_image_to_selected_comparison_and_saves_content()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var image = await viewModel.AddImageAsync(
            "  reference.png  ",
            new MemoryStream([1, 2, 3]),
            mediaType: "image/png",
            label: "  Baseline  ",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Baseline", image.Label);
        Assert.Equal("reference.png", image.SourceName);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal("stored/reference.png", image.StorageKey);
        Assert.Same(image, Assert.Single(comparison.Images));
        Assert.Equal(image.Id, comparison.ReferenceImageId);
        Assert.Null(comparison.CandidateImageId);
        Assert.Equal([1, 2, 3], storage.SavedImageContents[image.Id]);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task AddImageAsync_renames_default_comparison_from_first_image_label()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync(cancellationToken: TestContext.Current.CancellationToken);

        await viewModel.AddImageAsync(
            "first-reference.png",
            new MemoryStream([1]),
            label: "  Homepage Reference  ",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Homepage Reference", comparison.Name);
        Assert.Equal("Project / Homepage Reference", viewModel.WorkspaceContextTitle);
    }

    [Fact]
    public async Task AddImageAsync_keeps_user_named_comparison_name()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Header States", TestContext.Current.CancellationToken);

        await viewModel.AddImageAsync(
            "first-reference.png",
            new MemoryStream([1]),
            label: "Homepage Reference",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Header States", comparison.Name);
    }

    [Fact]
    public async Task Workspace_context_summarizes_selected_project_comparison_and_image_count()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        Assert.Equal("No project selected", viewModel.WorkspaceContextTitle);
        Assert.Equal("Create or select a project", viewModel.WorkspaceContextDetail);

        await viewModel.AddProjectAsync("Project A", TestContext.Current.CancellationToken);

        Assert.Equal("Project A", viewModel.WorkspaceContextTitle);
        Assert.Equal("No comparison selected", viewModel.WorkspaceContextDetail);

        await viewModel.AddComparisonAsync("Header States", TestContext.Current.CancellationToken);

        Assert.Equal("Project A / Header States", viewModel.WorkspaceContextTitle);
        Assert.Equal("0 images in image set", viewModel.WorkspaceContextDetail);

        await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("1 image", viewModel.SelectedComparisonImageCountText);
        Assert.Equal("1 image in image set", viewModel.WorkspaceContextDetail);

        await viewModel.RenameSelectedProjectAsync("Project B", TestContext.Current.CancellationToken);
        await viewModel.RenameSelectedComparisonAsync("Footer States", TestContext.Current.CancellationToken);

        Assert.Equal("Project B / Footer States", viewModel.WorkspaceContextTitle);
    }

    [Fact]
    public async Task AddImageAsync_requires_selected_comparison()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.AddImageAsync("image.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("comparison must be selected", exception.Message);
    }

    [Fact]
    public async Task DeleteImageAsync_removes_image_repairs_roles_and_deletes_stored_content()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var deleted = await viewModel.DeleteImageAsync(reference, TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.DoesNotContain(reference, comparison.Images);
        Assert.Equal(candidate.Id, comparison.ReferenceImageId);
        Assert.Null(comparison.CandidateImageId);
        Assert.Contains(reference.Id, storage.DeletedImageIds);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task DeleteImageAsync_returns_false_when_image_is_not_in_selected_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var deleted = await viewModel.DeleteImageAsync(new ImageAsset(), TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Empty(storage.SavedProjects);
        Assert.Empty(storage.DeletedImageIds);
    }

    [Fact]
    public async Task LabelImageAsync_updates_display_label_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.AddImageAsync("candidate.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var labelled = await viewModel.LabelImageAsync(image, "  Current Render  ", TestContext.Current.CancellationToken);

        Assert.True(labelled);
        Assert.Equal("Current Render", image.Label);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task LabelImageAsync_returns_false_when_image_is_not_in_selected_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var labelled = await viewModel.LabelImageAsync(new ImageAsset(), "Missing", TestContext.Current.CancellationToken);

        Assert.False(labelled);
        Assert.Empty(storage.SavedProjects);
    }

    [Fact]
    public async Task SetReferenceImageAsync_sets_reference_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        var alternate = await viewModel.AddImageAsync("alternate.png", new MemoryStream([3]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var changed = await viewModel.SetReferenceImageAsync(alternate, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(alternate.Id, comparison.ReferenceImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.NotEqual(reference.Id, comparison.ReferenceImageId);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task SetCandidateImageAsync_sets_candidate_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        var alternate = await viewModel.AddImageAsync("alternate.png", new MemoryStream([3]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var changed = await viewModel.SetCandidateImageAsync(alternate, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(reference.Id, comparison.ReferenceImageId);
        Assert.Equal(alternate.Id, comparison.CandidateImageId);
        Assert.NotEqual(candidate.Id, comparison.CandidateImageId);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task SetReferenceImageAsync_swaps_roles_when_image_is_current_candidate()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);

        var changed = await viewModel.SetReferenceImageAsync(candidate, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(candidate.Id, comparison.ReferenceImageId);
        Assert.Equal(reference.Id, comparison.CandidateImageId);
    }

    [Fact]
    public async Task SetCandidateImageAsync_swaps_roles_when_image_is_current_reference()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);

        var changed = await viewModel.SetCandidateImageAsync(reference, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(candidate.Id, comparison.ReferenceImageId);
        Assert.Equal(reference.Id, comparison.CandidateImageId);
    }

    [Fact]
    public async Task SetCandidateImageAsync_requires_at_least_two_images()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.SetCandidateImageAsync(image, TestContext.Current.CancellationToken));

        Assert.Contains("at least two images", exception.Message);
    }

    [Fact]
    public async Task Role_reassignment_returns_false_when_image_is_not_in_selected_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var missing = new ImageAsset();
        storage.SavedProjects.Clear();

        var referenceChanged = await viewModel.SetReferenceImageAsync(missing, TestContext.Current.CancellationToken);
        var candidateChanged = await viewModel.SetCandidateImageAsync(missing, TestContext.Current.CancellationToken);

        Assert.False(referenceChanged);
        Assert.False(candidateChanged);
        Assert.Empty(storage.SavedProjects);
    }

    [Fact]
    public async Task Role_reassignment_guard_methods_reflect_selected_comparison_state()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        var missing = new ImageAsset();

        Assert.True(viewModel.CanSetReferenceImage(reference));
        Assert.True(viewModel.CanSetCandidateImage(candidate));
        Assert.False(viewModel.CanSetReferenceImage(missing));
        Assert.False(viewModel.CanSetCandidateImage(missing));
        Assert.False(viewModel.CanSetReferenceImage(null));
        Assert.False(viewModel.CanSetCandidateImage(null));
    }

    [AvaloniaFact]
    public async Task CaptureMonitoredImageChangeAsync_adds_new_reference_version()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var file = TestUiSupport.CreateStorageFile("monitored-reference.png");
        var image = await viewModel.AddImageAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(image, ImageMonitoringRole.Reference, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        WriteFixtureImage(file.Path.LocalPath, SKColors.DarkGreen);

        var version = await viewModel.CaptureMonitoredImageChangeAsync(project, comparison, image, TestContext.Current.CancellationToken);

        Assert.NotNull(version);
        Assert.Equal(image.Id, version.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.None, image.MonitoringRole);
        Assert.Equal(ImageMonitoringRole.Reference, version.MonitoringRole);
        Assert.Equal(version.Id, comparison.ReferenceImageId);
        Assert.Equal(2, comparison.Images.Count);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [AvaloniaFact]
    public async Task CaptureMonitoredImageChangeAsync_adds_new_candidate_version()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.AddImageAsync(TestUiSupport.CreateStorageFile("reference-source.png"), cancellationToken: TestContext.Current.CancellationToken);
        var candidateFile = TestUiSupport.CreateStorageFile("candidate-source.png");
        var candidate = await viewModel.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(candidate, ImageMonitoringRole.Candidate, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        WriteFixtureImage(candidateFile.Path.LocalPath, SKColors.Purple);

        var version = await viewModel.CaptureMonitoredImageChangeAsync(project, comparison, candidate, TestContext.Current.CancellationToken);

        Assert.NotNull(version);
        Assert.Equal(reference.Id, comparison.ReferenceImageId);
        Assert.Equal(version.Id, comparison.CandidateImageId);
        Assert.Equal(candidate.Id, version.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.None, candidate.MonitoringRole);
        Assert.Equal(ImageMonitoringRole.Candidate, version.MonitoringRole);
        Assert.Equal(3, comparison.Images.Count);
    }

    [Fact]
    public async Task CaptureMonitoredImageChangeAsync_ignores_unchanged_hash()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.AddImageAsync(TestUiSupport.CreateStorageFile("unchanged-source.png"), cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(image, ImageMonitoringRole.Reference, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var version = await viewModel.CaptureMonitoredImageChangeAsync(project, comparison, image, TestContext.Current.CancellationToken);

        Assert.Null(version);
        Assert.Single(comparison.Images);
        Assert.Empty(storage.SavedProjects);
    }

    private sealed class FakeProjectStorage(params Project[] projects) : IProjectStorage
    {
        private readonly List<Project> _projects = [..projects];

        public List<Project> SavedProjects { get; } = [];

        public List<Guid> DeletedProjectIds { get; } = [];

        public Dictionary<Guid, byte[]> SavedImageContents { get; } = [];

        public List<Guid> DeletedImageIds { get; } = [];

        public Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(_projects.ToArray());
        }

        public Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_projects.FirstOrDefault(project => project.Id == projectId));
        }

        public Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default)
        {
            SavedProjects.Add(project);

            if (!_projects.Contains(project))
            {
                _projects.Add(project);
            }

            return Task.CompletedTask;
        }

        public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            DeletedProjectIds.Add(projectId);
            _projects.RemoveAll(project => project.Id == projectId);
            return Task.CompletedTask;
        }

        public async Task<string> SaveImageAsync(
            Guid projectId,
            Guid comparisonSetId,
            ImageAsset image,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            SavedImageContents[image.Id] = buffer.ToArray();
            image.StorageKey = $"stored/{image.SourceName}";
            return image.StorageKey;
        }

        public Task<Stream> LoadImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(new MemoryStream(SavedImageContents[image.Id]));
        }

        public Task DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
        {
            DeletedImageIds.Add(image.Id);
            SavedImageContents.Remove(image.Id);
            return Task.CompletedTask;
        }
    }

    private static void WriteFixtureImage(string path, SKColor color)
    {
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
