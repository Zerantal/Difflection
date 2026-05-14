using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
            var projectsSelector = GetControl<ComboBox>(mainView, "ProjectsSelector");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsSelector.ItemCount == 2);

            Assert.Same(first, viewModel.Workspace.SelectedProject);
            Assert.Same(firstComparison, viewModel.Workspace.SelectedComparison);
            Assert.Equal(0, projectsSelector.SelectedIndex);
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
            var projectsSelector = GetControl<ComboBox>(mainView, "ProjectsSelector");

            Click(GetControl<Button>(mainView, "AddProjectButton"));

            await TestUiSupport.WaitForAsync(() => projectsSelector.ItemCount == 1);

            var project = Assert.Single(viewModel.Workspace.Projects);
            Assert.Equal("Untitled Project", project.Name);
            Assert.Same(project, viewModel.Workspace.SelectedProject);
            Assert.Same(viewModel.Workspace.SelectedProjectRow, projectsSelector.SelectedItem);
            Assert.Same(project, Assert.Single(storage.SavedProjects));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Inline_sidebar_name_editors_rename_created_project_and_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);

            Click(GetControl<Button>(mainView, "AddProjectButton"));
            var projectRow = viewModel.Workspace.SelectedProjectRow!;
            var projectNameTextBox = await WaitForProjectNameTextBoxAsync(mainView, projectRow);
            await TestUiSupport.WaitForAsync(() => projectRow.IsEditing);
            projectNameTextBox.Text = "  Client Work  ";
            LoseFocus(projectNameTextBox);

            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedProject?.Name == "Client Work" && !projectRow.IsEditing);

            Click(GetControl<Button>(mainView, "AddComparisonButton"));
            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedComparison is not null);
            storage.SavedProjects.Clear();

            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");
            var comparisonRow = viewModel.Workspace.SelectedComparisonRow!;
            var comparisonNameTextBox = await WaitForInlineNameTextBoxAsync(comparisonsList, comparisonRow);
            await TestUiSupport.WaitForAsync(() => comparisonRow.IsEditing);
            comparisonNameTextBox.Text = "  Header States  ";
            LoseFocus(comparisonNameTextBox);

            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedComparison?.Name == "Header States" && !comparisonRow.IsEditing);

            Assert.Equal("Client Work", viewModel.Workspace.SelectedProject?.Name);
            Assert.Equal("Header States", viewModel.Workspace.SelectedComparison?.Name);
            Assert.NotEmpty(storage.SavedProjects);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Rename_context_menu_items_begin_inline_rename_for_project_and_comparison()
    {
        var project = new Project { Name = "Project" };
        var comparison = new ComparisonSet { Name = "Comparison" };
        project.Comparisons.Add(comparison);
        var viewModel = new MainWindowViewModel(new FakeProjectStorage(project));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 1);

            Click(GetControl<Button>(mainView, "EditProjectButton"));
            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedProjectRow?.IsEditing == true);
            WorkspaceNavigatorViewModel.CancelProjectRename(viewModel.Workspace.SelectedProjectRow!);

            Click(GetContextMenuItem(comparisonsList, viewModel.Workspace.SelectedComparisonRow!, "Rename"));
            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedComparisonRow?.IsEditing == true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Sidebar_context_menu_items_receive_their_project_or_comparison_row()
    {
        var project = new Project { Name = "Project" };
        var comparison = new ComparisonSet { Name = "Comparison" };
        project.Comparisons.Add(comparison);
        var viewModel = new MainWindowViewModel(new FakeProjectStorage(project));

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 1);

            GetContextMenuItem(comparisonsList, viewModel.Workspace.SelectedComparisonRow!, "Refresh source images");
            GetContextMenuItem(comparisonsList, viewModel.Workspace.SelectedComparisonRow!, "Rename");
            GetContextMenuItem(comparisonsList, viewModel.Workspace.SelectedComparisonRow!, "Delete");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Remove_image_button_removes_image_and_refreshes_current_display()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("left-reference.png"), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("candidate.png"), cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(viewModel.Workspace.SelectedComparison, viewModel.ProjectStorage, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var imagesList = GetControl<ListBox>(mainView, "ComparisonImagesList");

            await TestUiSupport.WaitForAsync(() => imagesList.ItemCount == 2);

            Click(GetImageActionButton(imagesList, reference, "Remove image"));

            await TestUiSupport.WaitForAsync(() => imagesList.ItemCount == 1 && viewModel.LeftFileName == candidate.Label);

            Assert.DoesNotContain(reference, viewModel.Workspace.SelectedComparison!.Images);
            Assert.Equal(candidate.Id, viewModel.Workspace.SelectedComparison.ReferenceImageId);
            Assert.Null(viewModel.Workspace.SelectedComparison.CandidateImageId);
            Assert.Contains(reference.Id, storage.DeletedImageIds);
            Assert.Same(viewModel.Workspace.SelectedProject, Assert.Single(storage.SavedProjects));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Selecting_comparison_refreshes_main_display_images()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var first = await viewModel.Workspace.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        var firstImage = await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("first-left-reference.png"), label: "First Reference", cancellationToken: TestContext.Current.CancellationToken);
        var second = await viewModel.Workspace.AddComparisonAsync("Second", TestContext.Current.CancellationToken);
        var secondImage = await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("second-left-reference.png"), label: "Second Reference", cancellationToken: TestContext.Current.CancellationToken);
        viewModel.Workspace.SelectedComparison = first;
        await viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(viewModel.Workspace.SelectedComparison, viewModel.ProjectStorage, TestContext.Current.CancellationToken);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => viewModel.LeftFileName == firstImage.Label);

            comparisonsList.SelectedItem = viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, second));

            await TestUiSupport.WaitForAsync(() => viewModel.LeftFileName == secondImage.Label);

            Assert.Same(second, viewModel.Workspace.SelectedComparison);
            Assert.Null(viewModel.RightImage);
            Assert.Equal("Candidate image", viewModel.RightFileName);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Add_comparison_selects_new_empty_comparison_and_clears_display()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("first-left-reference.png"), label: "First Reference", cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(viewModel.Workspace.SelectedComparison, viewModel.ProjectStorage, TestContext.Current.CancellationToken);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => viewModel.LeftFileName == "First Reference");

            Click(GetControl<Button>(mainView, "AddComparisonButton"));

            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 2
                && viewModel.Workspace.SelectedComparison?.Images.Count == 0
                && viewModel.LeftImage is null
                && viewModel.RightImage is null);

            Assert.Same(viewModel.Workspace.SelectedComparisonRow, comparisonsList.SelectedItem);
            Assert.Equal("Baseline image", viewModel.LeftFileName);
            Assert.Equal("Candidate image", viewModel.RightFileName);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Comparison_add_button_follows_selected_project_state()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var addComparisonButton = GetControl<Button>(mainView, "AddComparisonButton");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            Assert.False(addComparisonButton.IsEffectivelyEnabled);

            Click(GetControl<Button>(mainView, "AddProjectButton"));
            await TestUiSupport.WaitForAsync(() => addComparisonButton.IsEffectivelyEnabled);

            Click(addComparisonButton);
            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 1);

            var comparison = Assert.Single(viewModel.Workspace.SelectedProject!.Comparisons);
            Assert.Equal("Untitled Comparison", comparison.Name);
            Assert.Same(comparison, viewModel.Workspace.SelectedComparison);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Selecting_project_in_selector_updates_comparison_list_and_selected_comparison()
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
            var projectsSelector = GetControl<ComboBox>(mainView, "ProjectsSelector");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsSelector.ItemCount == 2);

            projectsSelector.SelectedItem = viewModel.Workspace.ProjectRows.Single(row => ReferenceEquals(row.Project, second));

            await TestUiSupport.WaitForAsync(() => ReferenceEquals(viewModel.Workspace.SelectedProject, second)
                && ReferenceEquals(viewModel.Workspace.SelectedComparison, secondComparison)
                && comparisonsList.ItemCount == 2);

            Assert.Same(viewModel.Workspace.SelectedProjectRow, projectsSelector.SelectedItem);
            Assert.Equal(2, comparisonsList.ItemCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Delete_actions_confirm_remove_selected_items_and_repair_selection()
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
            mainView.ConfirmDestructiveActionAsync = (_, _) => Task.FromResult(true);
            var projectsSelector = GetControl<ComboBox>(mainView, "ProjectsSelector");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsSelector.ItemCount == 2 && comparisonsList.ItemCount == 2);

            Click(GetContextMenuItem(comparisonsList, viewModel.Workspace.SelectedComparisonRow!, "Delete"));
            await TestUiSupport.WaitForAsync(() => comparisonsList.ItemCount == 1);

            Assert.DoesNotContain(firstComparison, first.Comparisons);
            Assert.Same(secondComparison, viewModel.Workspace.SelectedComparison);
            Assert.Same(first, Assert.Single(storage.SavedProjects));

            Click(GetControl<Button>(mainView, "DeleteProjectButton"));
            await TestUiSupport.WaitForAsync(() => projectsSelector.ItemCount == 1);

            Assert.DoesNotContain(first, viewModel.Workspace.Projects);
            Assert.Same(second, viewModel.Workspace.SelectedProject);
            Assert.Null(viewModel.Workspace.SelectedComparison);
            Assert.Contains(first.Id, storage.DeletedProjectIds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Delete_context_menu_cancel_leaves_project_and_comparison_unchanged()
    {
        var project = new Project { Name = "Project" };
        var comparison = new ComparisonSet { Name = "Comparison" };
        project.Comparisons.Add(comparison);
        var storage = new FakeProjectStorage(project);
        var viewModel = new MainWindowViewModel(storage);

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            mainView.ConfirmDestructiveActionAsync = (_, _) => Task.FromResult(false);
            var projectsSelector = GetControl<ComboBox>(mainView, "ProjectsSelector");
            var comparisonsList = GetControl<ListBox>(mainView, "ComparisonsList");

            await TestUiSupport.WaitForAsync(() => projectsSelector.ItemCount == 1 && comparisonsList.ItemCount == 1);

            Click(GetContextMenuItem(comparisonsList, viewModel.Workspace.SelectedComparisonRow!, "Delete"));
            Click(GetControl<Button>(mainView, "DeleteProjectButton"));

            Assert.Single(project.Comparisons);
            Assert.Single(viewModel.Workspace.Projects);
            Assert.Empty(storage.SavedProjects);
            Assert.Empty(storage.DeletedProjectIds);
        }
        finally
        {
            window.Close();
        }
    }

    private static T GetControl<T>(Control root, string name)
        where T : Control
    {
        return TestUiSupport.FindNamedControl<T>(root, name);
    }

    private static void Click(Button button)
    {
        if (button.Command is { } command)
        {
            var parameter = button.CommandParameter;
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static void Click(MenuItem menuItem)
    {
        if (menuItem.Command is { } command)
        {
            var parameter = menuItem.CommandParameter;
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }

        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private static void LoseFocus(TextBox textBox)
    {
        textBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
    }

    private static async Task<TextBox> WaitForInlineNameTextBoxAsync(ItemsControl list, object item)
    {
        TextBox? textBox = null;
        await TestUiSupport.WaitForAsync(() =>
        {
            textBox = FindInlineNameTextBox(list, item);
            return textBox is not null;
        });

        return textBox!;
    }

    private static async Task<TextBox> WaitForProjectNameTextBoxAsync(Control root, ProjectListItemViewModel row)
    {
        TextBox? textBox = null;
        await TestUiSupport.WaitForAsync(() =>
        {
            textBox = TestUiSupport.FindNamedControl<TextBox>(root, "ProjectNameTextBox");
            return textBox is { IsVisible: true, DataContext: ProjectListItemViewModel textBoxRow }
                && ReferenceEquals(textBoxRow, row);
        });

        return textBox!;
    }

    private static TextBox? FindInlineNameTextBox(ItemsControl list, object item)
    {
        return list.ContainerFromItem(item)
            ?.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault();
    }

    private static Button GetImageActionButton(ItemsControl list, ImageAsset image, string tooltip)
    {
        var row = list.Items
            .OfType<ComparisonImageSetItemViewModel>()
            .FirstOrDefault(item => ReferenceEquals(item.Image, image))
            ?? throw new InvalidOperationException($"Image row for '{image.SourceName}' not found.");

        return list.ContainerFromItem(row)
            ?.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(ToolTip.GetTip(button)?.ToString(), tooltip, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Image action button '{tooltip}' not found.");
    }

    private static MenuItem GetContextMenuItem(ItemsControl list, object item, string header)
    {
        var border = list.ContainerFromItem(item)
            ?.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.ContextMenu is not null)
            ?? throw new InvalidOperationException("Context menu owner not found.");

        border.RaiseEvent(new ContextRequestedEventArgs
        {
            RoutedEvent = InputElement.ContextRequestedEvent
        });

        var contextMenu = border.ContextMenu
            ?? throw new InvalidOperationException("Context menu not found.");

        var menuItem = contextMenu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(menuItem => string.Equals(menuItem.Header?.ToString(), header, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Context menu item '{header}' not found.");

        Assert.Same(item, menuItem.CommandParameter);
        if (string.Equals(header, "Rename", StringComparison.Ordinal))
        {
            Assert.NotNull(menuItem.Command);
        }

        return menuItem;
    }

    private sealed class FakeProjectStorage(params Project[] projects) : IProjectStorage
    {
        private readonly List<Project> _projects = [.. projects];

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
            image.StorageKey = $"stored/{image.Id:N}";
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
}
