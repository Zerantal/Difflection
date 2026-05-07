using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Difflection.Models;
using Difflection.Storage;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Xunit;

namespace Difflection.Tests.UI;

public sealed class SidebarNavigationTests
{
    [AvaloniaFact]
    public async Task Sidebar_loads_projects_from_storage_and_selects_first_project_and_comparison()
    {
        var first = new Project { Name = "First Project" };
        var firstComparison = new ComparisonSet { Name = "First Comparison" };
        first.Comparisons.Add(firstComparison);
        var second = new Project { Name = "Second Project" };
        var viewModel = new MainWindowViewModel(new FakeProjectStorage(first, second));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var projectsList = GetControl<ListBox>(mainView, "ProjectsList");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsList.ItemCount == 2);

            Assert.Same(first, viewModel.SelectedProject);
            Assert.Same(firstComparison, viewModel.SelectedComparison);
            Assert.Equal(0, projectsList.SelectedIndex);
            Assert.Equal(0, comparisonsList.SelectedIndex);
            Assert.Equal(1, comparisonsList.ItemCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Add_project_button_creates_selects_and_persists_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var projectsList = GetControl<ListBox>(mainView, "ProjectsList");

            Click(GetControl<Button>(mainView, "AddProjectButton"));

            await TestUiSupport.WaitForAsync(() => projectsList.ItemCount == 1);

            var project = Assert.Single(viewModel.Projects);
            Assert.Equal("Untitled Project", project.Name);
            Assert.Same(project, viewModel.SelectedProject);
            Assert.Same(project, projectsList.SelectedItem);
            Assert.Same(project, Assert.Single(storage.SavedProjects));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Comparison_buttons_follow_selected_project_and_selected_comparison_state()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var addComparisonButton = GetControl<Button>(mainView, "AddComparisonButton");
            var deleteComparisonButton = GetControl<Button>(mainView, "DeleteComparisonButton");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            Assert.False(addComparisonButton.IsEffectivelyEnabled);
            Assert.False(deleteComparisonButton.IsEffectivelyEnabled);

            Click(GetControl<Button>(mainView, "AddProjectButton"));
            await TestUiSupport.WaitForAsync(() => addComparisonButton.IsEffectivelyEnabled);

            Click(addComparisonButton);
            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 1);

            var comparison = Assert.Single(viewModel.SelectedProject!.Comparisons);
            Assert.Equal("Untitled Comparison", comparison.Name);
            Assert.Same(comparison, viewModel.SelectedComparison);
            Assert.True(deleteComparisonButton.IsEffectivelyEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Selecting_project_in_sidebar_updates_comparison_list_and_selected_comparison()
    {
        var first = new Project { Name = "First" };
        first.Comparisons.Add(new ComparisonSet { Name = "First A" });
        var second = new Project { Name = "Second" };
        var secondComparison = new ComparisonSet { Name = "Second A" };
        second.Comparisons.Add(secondComparison);
        second.Comparisons.Add(new ComparisonSet { Name = "Second B" });
        var viewModel = new MainWindowViewModel(new FakeProjectStorage(first, second));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var projectsList = GetControl<ListBox>(mainView, "ProjectsList");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsList.ItemCount == 2);

            projectsList.SelectedItem = second;

            await TestUiSupport.WaitForAsync(() => ReferenceEquals(viewModel.SelectedProject, second)
                && ReferenceEquals(viewModel.SelectedComparison, secondComparison)
                && comparisonsList.ItemCount == 2);

            Assert.Same(second, projectsList.SelectedItem);
            Assert.Equal(2, comparisonsList.ItemCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Delete_buttons_remove_selected_items_and_repair_selection()
    {
        var first = new Project { Name = "First" };
        var second = new Project { Name = "Second" };
        var firstComparison = new ComparisonSet { Name = "First Comparison" };
        var secondComparison = new ComparisonSet { Name = "Second Comparison" };
        first.Comparisons.Add(firstComparison);
        first.Comparisons.Add(secondComparison);
        var storage = new FakeProjectStorage(first, second);
        var viewModel = new MainWindowViewModel(storage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var projectsList = GetControl<ListBox>(mainView, "ProjectsList");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsList.ItemCount == 2 && comparisonsList.ItemCount == 2);

            Click(GetControl<Button>(mainView, "DeleteComparisonButton"));
            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 1);

            Assert.DoesNotContain(firstComparison, first.Comparisons);
            Assert.Same(secondComparison, viewModel.SelectedComparison);
            Assert.Same(first, Assert.Single(storage.SavedProjects));

            Click(GetControl<Button>(mainView, "DeleteProjectButton"));
            await TestUiSupport.WaitForAsync(() => projectsList.ItemCount == 1);

            Assert.DoesNotContain(first, viewModel.Projects);
            Assert.Same(second, viewModel.SelectedProject);
            Assert.Null(viewModel.SelectedComparison);
            Assert.Contains(first.Id, storage.DeletedProjectIds);
        }
        finally
        {
            window.Close();
        }
    }

    private static T GetControl<T>(Control root, string name)
        where T : Control
    {
        return root.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} not found.");
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private sealed class FakeProjectStorage(params Project[] projects) : IProjectStorage
    {
        private readonly List<Project> projects = [..projects];

        public List<Project> SavedProjects { get; } = [];

        public List<Guid> DeletedProjectIds { get; } = [];

        public Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(projects.ToArray());
        }

        public Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(projects.FirstOrDefault(project => project.Id == projectId));
        }

        public Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default)
        {
            SavedProjects.Add(project);

            if (!projects.Contains(project))
            {
                projects.Add(project);
            }

            return Task.CompletedTask;
        }

        public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            DeletedProjectIds.Add(projectId);
            projects.RemoveAll(project => project.Id == projectId);
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
