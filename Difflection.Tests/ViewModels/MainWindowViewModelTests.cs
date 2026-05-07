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

    private sealed class FakeProjectStorage(params Project[] projects) : IProjectStorage
    {
        private readonly List<Project> _projects = [..projects];

        public List<Project> SavedProjects { get; } = [];

        public List<Guid> DeletedProjectIds { get; } = [];

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

        public Task<string> SaveImageAsync(
            Guid projectId,
            Guid comparisonSetId,
            ImageAsset image,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> LoadImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
