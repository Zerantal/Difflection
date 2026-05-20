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

        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left-reference.png", 8, 8));

        Assert.Equal("Load two images to compare", viewModel.DifferenceStatusText);

        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("candidate.png", 8, 8));

        Assert.StartsWith("Difference 100.0%", viewModel.DifferenceStatusText);
        Assert.Contains("RMS error", viewModel.DifferenceStatusText);
        Assert.Contains("Compared 8x8", viewModel.DifferenceStatusText);
    }

    [AvaloniaFact]
    public async Task Difference_image_updates_when_both_images_are_loaded()
    {
        var viewModel = new MainWindowViewModel();

        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left-reference.png", 12, 10));

        Assert.Null(viewModel.ComparisonDisplay.DifferenceImage);

        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("candidate.png", 8, 6));

        Assert.NotNull(viewModel.ComparisonDisplay.DifferenceImage);
        Assert.Equal(8, viewModel.ComparisonDisplay.DifferenceImage.PixelSize.Width);
        Assert.Equal(6, viewModel.ComparisonDisplay.DifferenceImage.PixelSize.Height);
    }

    [AvaloniaFact]
    public async Task Difference_image_updates_when_overlay_options_change()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, TestUiSupport.CreateStorageFile("left-reference.png", 8, 8));
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, TestUiSupport.CreateStorageFile("candidate.png", 8, 8));
        var neutralDifference = viewModel.ComparisonDisplay.DifferenceImage;

        viewModel.ComparisonDisplay.SelectDifferenceBaseImage(DifferenceBaseImage.Baseline);
        var baselineDifference = viewModel.ComparisonDisplay.DifferenceImage;
        viewModel.ComparisonDisplay.DifferenceOverlayOpacity = 0.4;

        Assert.Equal("40%", viewModel.ComparisonDisplay.DifferenceOverlayOpacityText);
        Assert.True(viewModel.ComparisonDisplay.IsDifferenceBaseBaseline);
        Assert.NotNull(viewModel.ComparisonDisplay.DifferenceImage);
        Assert.NotSame(neutralDifference, baselineDifference);
        Assert.NotSame(baselineDifference, viewModel.ComparisonDisplay.DifferenceImage);
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

        Assert.Same(project, Assert.Single(viewModel.Workspace.Projects));
        Assert.Same(project, viewModel.Workspace.SelectedProject);
        Assert.Same(comparison, viewModel.Workspace.SelectedComparison);
        var projectRow = Assert.Single(viewModel.Workspace.ProjectRows);
        Assert.Same(project, projectRow.Project);
        Assert.Same(projectRow, viewModel.Workspace.SelectedProjectRow);
        Assert.Equal("1 comparison", projectRow.DetailText);
        var comparisonRow = Assert.Single(viewModel.Workspace.SelectedProjectComparisonRows);
        Assert.Same(comparison, comparisonRow.Comparison);
        Assert.Same(comparisonRow, viewModel.Workspace.SelectedComparisonRow);
        Assert.Equal("No images", comparisonRow.DetailText);
    }

    [AvaloniaFact]
    public async Task LoadProjectsAsync_refreshes_difference_status_for_selected_comparison()
    {
        var project = new Project { Name = "Project A" };
        var comparison = new ComparisonSet { Name = "Comparison A" };
        project.Comparisons.Add(comparison);

        var reference = new ImageAsset { Label = "Reference", SourceName = "reference.png" };
        var candidate = new ImageAsset { Label = "Candidate", SourceName = "candidate.png" };
        comparison.AddImage(reference);
        comparison.AddImage(candidate);

        var storage = new FakeProjectStorage(project)
        {
            SavedImageContents =
            {
                [reference.Id] = TestUiSupport.CreatePngBytes(8, 8, SKColors.OrangeRed),
                [candidate.Id] = TestUiSupport.CreatePngBytes(8, 8, SKColors.DarkSlateBlue)
            }
        };
        var viewModel = new MainWindowViewModel(storage);

        await viewModel.LoadProjectsAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("Difference 100.0%", viewModel.DifferenceStatusText);
        Assert.Contains("RMS error", viewModel.DifferenceStatusText);
        Assert.Contains("Compared 8x8", viewModel.DifferenceStatusText);
    }

    [Fact]
    public async Task AddProjectAsync_adds_selects_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);

        var project = await viewModel.Workspace.AddProjectAsync("  Visual Checks  ", TestContext.Current.CancellationToken);

        Assert.Equal("Visual Checks", project.Name);
        Assert.Same(project, Assert.Single(viewModel.Workspace.Projects));
        Assert.Same(project, viewModel.Workspace.SelectedProject);
        Assert.Null(viewModel.Workspace.SelectedComparison);
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

        var deleted = await viewModel.Workspace.DeleteSelectedProjectAsync(TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.DoesNotContain(first, viewModel.Workspace.Projects);
        Assert.Same(second, viewModel.Workspace.SelectedProject);
        Assert.Contains(first.Id, storage.DeletedProjectIds);
    }

    [Fact]
    public async Task AddComparisonAsync_adds_selects_and_saves_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var comparison = await viewModel.Workspace.AddComparisonAsync("  Header States  ", TestContext.Current.CancellationToken);

        Assert.Equal("Header States", comparison.Name);
        Assert.Same(comparison, Assert.Single(project.Comparisons));
        Assert.Same(comparison, viewModel.Workspace.SelectedComparison);
        Assert.Same(viewModel.Workspace.SelectedComparisonRow, Assert.Single(viewModel.Workspace.SelectedProjectComparisonRows));
        Assert.Equal("1 comparison", Assert.Single(viewModel.Workspace.ProjectRows).DetailText);
        Assert.Equal("No images", viewModel.Workspace.SelectedComparisonRow?.DetailText);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task DeleteSelectedComparisonAsync_deletes_comparison_and_selects_next_available_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var first = await viewModel.Workspace.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        var second = await viewModel.Workspace.AddComparisonAsync("Second", TestContext.Current.CancellationToken);
        viewModel.Workspace.SelectedComparison = first;
        storage.SavedProjects.Clear();

        var deleted = await viewModel.Workspace.DeleteSelectedComparisonAsync(TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.DoesNotContain(first, project.Comparisons);
        Assert.Same(second, viewModel.Workspace.SelectedComparison);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task SelectedProjectRow_and_SelectedProject_stay_in_sync_without_view_selection_repair()
    {
        var first = new Project { Name = "First" };
        first.Comparisons.Add(new ComparisonSet { Name = "First A" });
        var second = new Project { Name = "Second" };
        var secondComparison = new ComparisonSet { Name = "Second A" };
        second.Comparisons.Add(secondComparison);
        second.Comparisons.Add(new ComparisonSet { Name = "Second B" });
        var viewModel = new MainWindowViewModel(new FakeProjectStorage(first, second));
        await viewModel.LoadProjectsAsync(TestContext.Current.CancellationToken);

        var secondRow = viewModel.Workspace.ProjectRows.Single(row => ReferenceEquals(row.Project, second));
        viewModel.Workspace.SelectedProjectRow = secondRow;

        Assert.Same(second, viewModel.Workspace.SelectedProject);
        Assert.Same(secondRow, viewModel.Workspace.SelectedProjectRow);
        Assert.Same(secondComparison, viewModel.Workspace.SelectedComparison);
        Assert.Same(
            viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, secondComparison)),
            viewModel.Workspace.SelectedComparisonRow);

        viewModel.Workspace.SelectedProject = first;

        Assert.Same(first, viewModel.Workspace.SelectedProject);
        Assert.Same(viewModel.Workspace.ProjectRows.Single(row => ReferenceEquals(row.Project, first)), viewModel.Workspace.SelectedProjectRow);
        Assert.Same(first.Comparisons[0], viewModel.Workspace.SelectedComparison);
        Assert.Same(
            viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, first.Comparisons[0])),
            viewModel.Workspace.SelectedComparisonRow);
    }

    [Fact]
    public async Task SelectedComparisonRow_and_SelectedComparison_stay_in_sync_without_view_selection_repair()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var first = await viewModel.Workspace.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        var second = await viewModel.Workspace.AddComparisonAsync("Second", TestContext.Current.CancellationToken);

        var firstRow = viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, first));
        viewModel.Workspace.SelectedComparisonRow = firstRow;

        Assert.Same(first, viewModel.Workspace.SelectedComparison);
        Assert.Same(firstRow, viewModel.Workspace.SelectedComparisonRow);

        viewModel.Workspace.SelectedComparison = second;

        Assert.Same(second, viewModel.Workspace.SelectedComparison);
        Assert.Same(
            viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, second)),
            viewModel.Workspace.SelectedComparisonRow);
    }

    [Fact]
    public async Task Workspace_rows_preserve_identity_when_projects_and_comparisons_change()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var firstProject = await viewModel.Workspace.AddProjectAsync("First", TestContext.Current.CancellationToken);
        var firstProjectRow = viewModel.Workspace.ProjectRows.Single(row => ReferenceEquals(row.Project, firstProject));

        await viewModel.Workspace.AddProjectAsync("Second", TestContext.Current.CancellationToken);

        Assert.Same(firstProjectRow, viewModel.Workspace.ProjectRows.Single(row => ReferenceEquals(row.Project, firstProject)));

        viewModel.Workspace.SelectedProject = firstProject;
        var firstComparison = await viewModel.Workspace.AddComparisonAsync("First comparison", TestContext.Current.CancellationToken);
        var firstComparisonRow = viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, firstComparison));

        await viewModel.Workspace.AddComparisonAsync("Second comparison", TestContext.Current.CancellationToken);

        Assert.Same(
            firstComparisonRow,
            viewModel.Workspace.SelectedProjectComparisonRows.Single(row => ReferenceEquals(row.Comparison, firstComparison)));
    }

    [Fact]
    public async Task AddComparisonAsync_requires_selected_project()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.Workspace.AddComparisonAsync("No Project", TestContext.Current.CancellationToken));

        Assert.Contains("project must be selected", exception.Message);
    }

    [Fact]
    public async Task AddImageAsync_adds_image_to_selected_comparison_and_saves_content()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var image = await viewModel.ImageSet.AddImageAsync(
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
        Assert.Equal(image.Id, comparison.BaselineImageId);
        Assert.Null(comparison.CandidateImageId);
        Assert.Equal("Needs candidate", viewModel.Workspace.SelectedComparisonRow?.DetailText);
        Assert.Equal([1, 2, 3], storage.SavedImageContents[image.Id]);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [AvaloniaFact]
    public async Task AddFilesToCurrentComparisonAsync_ensures_workspace_adds_files_and_loads_display_images()
    {
        var viewModel = new MainWindowViewModel();
        var referenceFile = TestUiSupport.CreateStorageFile("reference-drop.png");
        var candidateFile = TestUiSupport.CreateStorageFile("candidate-drop.png");

        var addedImages = await viewModel.ImageSet.AddFilesToCurrentComparisonAsync(
            [referenceFile, candidateFile],
            cancellationToken: TestContext.Current.CancellationToken);

        var project = Assert.Single(viewModel.Workspace.Projects);
        var comparison = Assert.Single(project.Comparisons);
        Assert.Equal(2, addedImages.Count);
        Assert.Equal(2, comparison.Images.Count);
        Assert.Equal("reference-drop", comparison.Name);
        Assert.Equal(comparison.Images[0].Id, comparison.BaselineImageId);
        Assert.Equal(comparison.Images[1].Id, comparison.CandidateImageId);
        Assert.Equal("reference-drop.png", viewModel.LeftFileName);
        Assert.Equal("candidate-drop.png", viewModel.RightFileName);
        Assert.True(viewModel.HasBothImages);
    }

    [AvaloniaFact]
    public async Task AddFilesToCurrentComparisonAsync_honors_max_file_count()
    {
        var viewModel = new MainWindowViewModel();

        var addedImages = await viewModel.ImageSet.AddFilesToCurrentComparisonAsync(
            [
                TestUiSupport.CreateStorageFile("first.png"),
                TestUiSupport.CreateStorageFile("second.png")
            ],
            maxFiles: 1,
            cancellationToken: TestContext.Current.CancellationToken);

        var comparison = Assert.Single(Assert.Single(viewModel.Workspace.Projects).Comparisons);
        Assert.Single(addedImages);
        Assert.Single(comparison.Images);
        Assert.Equal("first.png", viewModel.LeftFileName);
        Assert.False(viewModel.HasRightImage);
    }

    [AvaloniaFact]
    public async Task ClearNonRoleImagesAndRefreshAsync_removes_only_images_outside_current_baseline_and_candidate()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync(
            "reference.png",
            new MemoryStream(TestUiSupport.CreatePngBytes(color: SKColors.Red)),
            cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync(
            "candidate.png",
            new MemoryStream(TestUiSupport.CreatePngBytes(color: SKColors.Blue)),
            cancellationToken: TestContext.Current.CancellationToken);
        var extra = await viewModel.ImageSet.AddImageAsync(
            "extra.png",
            new MemoryStream(TestUiSupport.CreatePngBytes(color: SKColors.Green)),
            cancellationToken: TestContext.Current.CancellationToken);

        var removedCount = await viewModel.ImageSet.ClearNonRoleImagesAndRefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, removedCount);
        Assert.Equal([reference, extra], comparison.Images);
        Assert.Same(reference, comparison.BaselineImage);
        Assert.Same(extra, comparison.CandidateImage);
        Assert.DoesNotContain(candidate.Id, storage.SavedImageContents.Keys);
        Assert.Contains(candidate.Id, storage.DeletedImageIds);
        Assert.False(viewModel.ImageSet.CanClearNonRoleImages);
    }

    [AvaloniaFact]
    public async Task AddBrowserFilesToCurrentComparisonAsync_ensures_workspace_adds_files_and_loads_display_images()
    {
        var viewModel = new MainWindowViewModel();

        var addedImages = await viewModel.ImageSet.AddBrowserFilesToCurrentComparisonAsync(
            ["browser-reference.png", "browser-candidate.png"],
            [TestUiSupport.CreatePngBytes(), TestUiSupport.CreatePngBytes()],
            cancellationToken: TestContext.Current.CancellationToken);

        var comparison = Assert.Single(Assert.Single(viewModel.Workspace.Projects).Comparisons);
        Assert.Equal(2, addedImages.Count);
        Assert.Equal(2, comparison.Images.Count);
        Assert.Equal("browser-reference", comparison.Name);
        Assert.Equal("browser-reference", viewModel.LeftFileName);
        Assert.Equal("browser-candidate", viewModel.RightFileName);
        Assert.True(viewModel.HasBothImages);
    }

    [Fact]
    public async Task AddImageAsync_renames_default_comparison_from_first_image_label()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync(cancellationToken: TestContext.Current.CancellationToken);

        await viewModel.ImageSet.AddImageAsync(
            "first-reference.png",
            new MemoryStream([1]),
            label: "  Homepage Reference  ",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Homepage Reference", comparison.Name);
        Assert.Equal("Project / Homepage Reference", viewModel.WorkspaceStatus.WorkspaceContextTitle);
    }

    [Fact]
    public async Task AddImageAsync_keeps_user_named_comparison_name()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Header States", TestContext.Current.CancellationToken);

        await viewModel.ImageSet.AddImageAsync(
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

        Assert.Equal("No project selected", viewModel.WorkspaceStatus.WorkspaceContextTitle);
        Assert.Equal("Create or select a project", viewModel.WorkspaceStatus.WorkspaceContextDetail);
        Assert.True(viewModel.WorkspaceStatus.ShowProjectsEmptyState);
        Assert.True(viewModel.WorkspaceStatus.ShowMainEmptyState);
        Assert.Equal("No projects", viewModel.WorkspaceStatus.MainEmptyStateTitle);

        await viewModel.Workspace.AddProjectAsync("Project A", TestContext.Current.CancellationToken);

        Assert.Equal("Project A", viewModel.WorkspaceStatus.WorkspaceContextTitle);
        Assert.Equal("No comparison selected", viewModel.WorkspaceStatus.WorkspaceContextDetail);
        Assert.False(viewModel.WorkspaceStatus.ShowProjectsEmptyState);
        Assert.True(viewModel.WorkspaceStatus.ShowComparisonsEmptyState);
        Assert.Equal("No comparison selected", viewModel.WorkspaceStatus.MainEmptyStateTitle);

        await viewModel.Workspace.AddComparisonAsync("Header States", TestContext.Current.CancellationToken);

        Assert.Equal("Project A / Header States", viewModel.WorkspaceStatus.WorkspaceContextTitle);
        Assert.Equal("0 images in image set", viewModel.WorkspaceStatus.WorkspaceContextDetail);
        Assert.True(viewModel.WorkspaceStatus.ShowMainEmptyState);
        Assert.Equal("No images in this comparison", viewModel.WorkspaceStatus.MainEmptyStateTitle);
        Assert.Equal("Add or drop a baseline image.", viewModel.WorkspaceStatus.WorkspaceActionHint);

        await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("1 image", viewModel.Workspace.SelectedComparisonImageCountText);
        Assert.Equal("1 image in image set", viewModel.WorkspaceStatus.WorkspaceContextDetail);
        Assert.False(viewModel.WorkspaceStatus.ShowMainEmptyState);
        Assert.Equal("Add or drop a candidate image.", viewModel.WorkspaceStatus.WorkspaceActionHint);

        await viewModel.Workspace.RenameSelectedProjectAsync("Project B", TestContext.Current.CancellationToken);
        await viewModel.Workspace.RenameSelectedComparisonAsync("Footer States", TestContext.Current.CancellationToken);

        Assert.Equal("Project B / Footer States", viewModel.WorkspaceStatus.WorkspaceContextTitle);
    }

    [Fact]
    public async Task AddImageAsync_requires_selected_comparison()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.ImageSet.AddImageAsync("image.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("comparison must be selected", exception.Message);
    }

    [Fact]
    public async Task DeleteImageAsync_removes_image_repairs_roles_and_deletes_stored_content()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var deleted = await viewModel.ImageSet.DeleteImageAsync(reference, TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.DoesNotContain(reference, comparison.Images);
        Assert.Null(comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.Contains(reference.Id, storage.DeletedImageIds);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task DeleteImageAsync_returns_false_when_image_is_not_in_selected_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var deleted = await viewModel.ImageSet.DeleteImageAsync(new ImageAsset(), TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Empty(storage.SavedProjects);
        Assert.Empty(storage.DeletedImageIds);
    }

    [Fact]
    public async Task LabelImageAsync_updates_display_label_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var labelled = await viewModel.ImageSet.LabelImageAsync(image, "  Current Render  ", TestContext.Current.CancellationToken);

        Assert.True(labelled);
        Assert.Equal("Current Render", image.Label);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task LabelImageAsync_empty_label_reverts_to_default_without_rebuilding_rows()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.ImageSet.RefreshImageRowsAsync(TestContext.Current.CancellationToken);
        var originalRow = Assert.Single(viewModel.ImageSet.ImageRows);
        storage.SavedProjects.Clear();

        var labelled = await viewModel.ImageSet.LabelImageAsync(image, string.Empty, TestContext.Current.CancellationToken);

        Assert.True(labelled);
        Assert.Equal("candidate.png", image.Label);
        Assert.Same(originalRow, Assert.Single(viewModel.ImageSet.ImageRows));
        Assert.False(originalRow.HasDisplayLabel);
        Assert.Equal(string.Empty, originalRow.DisplayLabel);
        Assert.Same(viewModel.Workspace.SelectedProject, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task LabelImageAsync_returns_false_when_image_is_not_in_selected_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var labelled = await viewModel.ImageSet.LabelImageAsync(new ImageAsset(), "Missing", TestContext.Current.CancellationToken);

        Assert.False(labelled);
        Assert.Empty(storage.SavedProjects);
    }

    [Fact]
    public async Task SetBaselineImageAsync_sets_reference_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        var alternate = new ImageAsset { Label = "alternate", SourceName = "alternate.png" };
        comparison.AddImageToChannel(comparison.BaselineChannel, alternate);
        storage.SavedProjects.Clear();

        var changed = await viewModel.ImageSet.SetBaselineImageAsync(alternate, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(alternate.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.NotEqual(reference.Id, comparison.BaselineImageId);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task SetCandidateImageAsync_sets_candidate_and_saves_project()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        var alternate = await viewModel.ImageSet.AddImageAsync("alternate.png", new MemoryStream([3]), cancellationToken: TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var changed = await viewModel.ImageSet.SetCandidateImageAsync(alternate, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(reference.Id, comparison.BaselineImageId);
        Assert.Equal(alternate.Id, comparison.CandidateImageId);
        Assert.NotEqual(candidate.Id, comparison.CandidateImageId);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [Fact]
    public async Task SetBaselineImageAsync_returns_false_for_candidate_channel_image()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);

        var changed = await viewModel.ImageSet.SetBaselineImageAsync(candidate, TestContext.Current.CancellationToken);

        Assert.False(changed);
        Assert.Equal(reference.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public async Task SetCandidateImageAsync_returns_false_for_baseline_channel_image()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);

        var changed = await viewModel.ImageSet.SetCandidateImageAsync(reference, TestContext.Current.CancellationToken);

        Assert.False(changed);
        Assert.Equal(reference.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public async Task Selecting_comparison_marks_pending_review_as_reviewed()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var pending = await viewModel.Workspace.AddComparisonAsync("Pending", TestContext.Current.CancellationToken);
        var other = await viewModel.Workspace.AddComparisonAsync("Other", TestContext.Current.CancellationToken);
        pending.RequiresReview = true;
        viewModel.Workspace.NotifyComparisonImagesChanged(pending);

        viewModel.Workspace.SelectedComparison = pending;

        Assert.Same(pending, viewModel.Workspace.SelectedComparison);
        Assert.False(pending.RequiresReview);
        Assert.False(viewModel.Workspace.SelectedComparisonRow?.NeedsReview);
        Assert.NotSame(other, viewModel.Workspace.SelectedComparison);
    }

    [Fact]
    public async Task SetCandidateImageAsync_returns_false_for_baseline_only_image()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);

        var changed = await viewModel.ImageSet.SetCandidateImageAsync(image, TestContext.Current.CancellationToken);

        Assert.False(changed);
    }

    [Fact]
    public async Task Role_reassignment_returns_false_when_image_is_not_in_selected_comparison()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var missing = new ImageAsset();
        storage.SavedProjects.Clear();

        var referenceChanged = await viewModel.ImageSet.SetBaselineImageAsync(missing, TestContext.Current.CancellationToken);
        var candidateChanged = await viewModel.ImageSet.SetCandidateImageAsync(missing, TestContext.Current.CancellationToken);

        Assert.False(referenceChanged);
        Assert.False(candidateChanged);
        Assert.Empty(storage.SavedProjects);
    }

    [Fact]
    public async Task Role_reassignment_guard_methods_reflect_selected_comparison_state()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync("reference.png", new MemoryStream([1]), cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync("candidate.png", new MemoryStream([2]), cancellationToken: TestContext.Current.CancellationToken);
        var missing = new ImageAsset();

        Assert.True(viewModel.ImageSet.CanSetBaselineImage(reference));
        Assert.True(viewModel.ImageSet.CanSetCandidateImage(candidate));
        Assert.False(viewModel.ImageSet.CanSetBaselineImage(missing));
        Assert.False(viewModel.ImageSet.CanSetCandidateImage(missing));
        Assert.False(viewModel.ImageSet.CanSetBaselineImage(null));
        Assert.False(viewModel.ImageSet.CanSetCandidateImage(null));
    }

    [Fact]
    public async Task RefreshImageRowsAsync_sorts_images_by_added_date_descending_and_shows_revision_chain()
    {
        var viewModel = new MainWindowViewModel(new FakeProjectStorage());
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var comparison = viewModel.Workspace.SelectedComparison!;

        var oldest = new ImageAsset
        {
            Label = "Oldest",
            SourceName = "oldest.png",
            AddedAt = DateTimeOffset.UnixEpoch.AddDays(1)
        };
        var middle = new ImageAsset
        {
            Label = "Middle",
            SourceName = "middle.png",
            AddedAt = DateTimeOffset.UnixEpoch.AddDays(2),
            PreviousVersionImageId = oldest.Id
        };
        var newest = new ImageAsset
        {
            Label = "Newest",
            SourceName = "newest.png",
            AddedAt = DateTimeOffset.UnixEpoch.AddDays(3),
            PreviousVersionImageId = middle.Id
        };

        comparison.AddImage(oldest);
        comparison.AddImageToChannel(comparison.BaselineChannel, middle);
        comparison.AddImageToChannel(comparison.BaselineChannel, newest);

        await viewModel.ImageSet.RefreshImageRowsAsync(TestContext.Current.CancellationToken);

        Assert.Equal([newest.Id, middle.Id, oldest.Id], viewModel.ImageSet.ImageRows.Select(row => row.Id));
        Assert.Equal("r3", viewModel.ImageSet.ImageRows[0].RevisionText);
        Assert.Equal("r2", viewModel.ImageSet.ImageRows[1].RevisionText);
        Assert.Equal("r1", viewModel.ImageSet.ImageRows[2].RevisionText);
        Assert.NotEmpty(viewModel.ImageSet.ImageRows[0].AddedAtText);
    }

    [Fact]
    public void ComparisonImageSetItemViewModel_hides_source_derived_label_until_custom_label_is_set()
    {
        var comparison = new ComparisonSet();
        var image = new ImageAsset
        {
            Label = "reference",
            SourceName = "reference.png"
        };

        comparison.AddImage(image);
        var row = new ComparisonImageSetItemViewModel(image, comparison);

        Assert.False(row.HasDisplayLabel);
        Assert.Equal(string.Empty, row.DisplayLabel);

        row.DisplayLabel = "Approved home screen";

        Assert.True(row.HasDisplayLabel);
        Assert.Equal("Approved home screen", row.DisplayLabel);
        Assert.Equal("Approved home screen", image.Label);
    }

    [Fact]
    public void ComparisonImageSetItemViewModel_hides_legacy_default_image_label()
    {
        var comparison = new ComparisonSet();
        var image = new ImageAsset
        {
            Label = "Image",
            SourceName = "reference.png"
        };

        comparison.AddImage(image);
        var row = new ComparisonImageSetItemViewModel(image, comparison);

        Assert.False(row.HasDisplayLabel);
        Assert.Equal(string.Empty, row.DisplayLabel);
    }

    [AvaloniaFact]
    public async Task CaptureMonitoredImageChangeAsync_adds_new_reference_version()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var file = TestUiSupport.CreateStorageFile("monitored-reference.png");
        var image = await viewModel.ImageSet.AddImageAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(image, ImageMonitoringRole.Baseline, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        WriteFixtureImage(file.Path.LocalPath, SKColors.DarkGreen);

        var version = await viewModel.CaptureMonitoredImageChangeAsync(project, comparison, image, TestContext.Current.CancellationToken);

        Assert.NotNull(version);
        Assert.Equal(image.Id, version.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.None, image.MonitoringRole);
        Assert.Equal(ImageMonitoringRole.Baseline, version.MonitoringRole);
        Assert.Equal(version.Id, comparison.BaselineImageId);
        Assert.Equal(2, comparison.Images.Count);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
    }

    [AvaloniaFact]
    public async Task CaptureMonitoredImageChangeAsync_adds_new_candidate_version()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("reference-source.png"), cancellationToken: TestContext.Current.CancellationToken);
        var candidateFile = TestUiSupport.CreateStorageFile("candidate-source.png");
        var candidate = await viewModel.ImageSet.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(candidate, ImageMonitoringRole.Candidate, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        WriteFixtureImage(candidateFile.Path.LocalPath, SKColors.Purple);

        var version = await viewModel.CaptureMonitoredImageChangeAsync(project, comparison, candidate, TestContext.Current.CancellationToken);

        Assert.NotNull(version);
        Assert.Equal(reference.Id, comparison.BaselineImageId);
        Assert.Equal(version.Id, comparison.CandidateImageId);
        Assert.Equal(candidate.Id, version.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.None, candidate.MonitoringRole);
        Assert.Equal(ImageMonitoringRole.Candidate, version.MonitoringRole);
        Assert.True(comparison.RequiresReview);
        Assert.True(viewModel.Workspace.SelectedComparisonRow?.NeedsReview);
        Assert.Equal(3, comparison.Images.Count);
    }

    [Fact]
    public async Task CaptureMonitoredImageChangeAsync_ignores_unchanged_hash()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var image = await viewModel.ImageSet.AddImageAsync(TestUiSupport.CreateStorageFile("unchanged-source.png"), cancellationToken: TestContext.Current.CancellationToken);
        await viewModel.SetImageMonitoringAsync(image, ImageMonitoringRole.Baseline, TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        var version = await viewModel.CaptureMonitoredImageChangeAsync(project, comparison, image, TestContext.Current.CancellationToken);

        Assert.Null(version);
        Assert.Single(comparison.Images);
        Assert.Empty(storage.SavedProjects);
    }

    [AvaloniaFact]
    public async Task RefreshComparisonSourceImagesAsync_adds_new_versions_for_changed_current_roles()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var referenceFile = TestUiSupport.CreateStorageFile("refresh-reference.png");
        var candidateFile = TestUiSupport.CreateStorageFile("refresh-candidate.png");
        var reference = await viewModel.ImageSet.AddImageAsync(referenceFile, cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        var previousReferenceModifiedAt = reference.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;
        var previousCandidateModifiedAt = candidate.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;
        storage.SavedProjects.Clear();

        WriteFixtureImage(referenceFile.Path.LocalPath, SKColors.DarkGreen);
        File.SetLastWriteTimeUtc(referenceFile.Path.LocalPath, previousReferenceModifiedAt.UtcDateTime.AddSeconds(5));
        WriteFixtureImage(candidateFile.Path.LocalPath, SKColors.Purple);
        File.SetLastWriteTimeUtc(candidateFile.Path.LocalPath, previousCandidateModifiedAt.UtcDateTime.AddSeconds(5));

        var capturedCount = await viewModel.RefreshComparisonSourceImagesAsync(comparison, TestContext.Current.CancellationToken);

        Assert.Equal(2, capturedCount);
        Assert.Equal(4, comparison.Images.Count);
        Assert.NotEqual(reference.Id, comparison.BaselineImageId);
        Assert.NotEqual(candidate.Id, comparison.CandidateImageId);
        Assert.Equal(reference.Id, comparison.BaselineImage?.PreviousVersionImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImage?.PreviousVersionImageId);
        Assert.Equal(ImageMonitoringRole.None, comparison.BaselineImage?.MonitoringRole);
        Assert.Equal(ImageMonitoringRole.None, comparison.CandidateImage?.MonitoringRole);
        Assert.True(comparison.RequiresReview);
        Assert.True(viewModel.Workspace.SelectedComparisonRow?.NeedsReview);
        Assert.NotEmpty(storage.SavedProjects);
        Assert.All(storage.SavedProjects, savedProject => Assert.Same(viewModel.Workspace.SelectedProject, savedProject));
    }

    [AvaloniaFact]
    public async Task RefreshComparisonSourceImagesAsync_ignores_non_current_and_not_newer_sources()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var referenceFile = TestUiSupport.CreateStorageFile("refresh-not-newer-reference.png");
        var candidateFile = TestUiSupport.CreateStorageFile("refresh-not-newer-candidate.png");
        var alternateFile = TestUiSupport.CreateStorageFile("refresh-alternate.png");
        var reference = await viewModel.ImageSet.AddImageAsync(referenceFile, cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        var alternate = await viewModel.ImageSet.AddImageAsync(alternateFile, cancellationToken: TestContext.Current.CancellationToken);
        comparison.SetCandidateImage(candidate.Id);
        var referenceModifiedAt = reference.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;
        var alternateModifiedAt = alternate.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;
        storage.SavedProjects.Clear();

        WriteFixtureImage(referenceFile.Path.LocalPath, SKColors.DarkGreen);
        File.SetLastWriteTimeUtc(referenceFile.Path.LocalPath, referenceModifiedAt.UtcDateTime);
        WriteFixtureImage(alternateFile.Path.LocalPath, SKColors.Purple);
        File.SetLastWriteTimeUtc(alternateFile.Path.LocalPath, alternateModifiedAt.UtcDateTime.AddSeconds(5));

        var capturedCount = await viewModel.RefreshComparisonSourceImagesAsync(comparison, TestContext.Current.CancellationToken);

        Assert.Equal(0, capturedCount);
        Assert.Equal(3, comparison.Images.Count);
        Assert.Equal(reference.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.Empty(storage.SavedProjects);
    }

    [AvaloniaFact]
    public async Task RefreshComparisonSourceImagesAsync_does_not_mark_matching_candidate_for_review()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var referenceFile = TestUiSupport.CreateStorageFile("refresh-matching-reference.png");
        var candidateFile = TestUiSupport.CreateStorageFile("refresh-matching-candidate.png");
        var reference = await viewModel.ImageSet.AddImageAsync(referenceFile, cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync(candidateFile, cancellationToken: TestContext.Current.CancellationToken);
        var previousReferenceModifiedAt = reference.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;
        var previousCandidateModifiedAt = candidate.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;

        WriteFixtureImage(referenceFile.Path.LocalPath, SKColors.DarkGreen);
        File.SetLastWriteTimeUtc(referenceFile.Path.LocalPath, previousReferenceModifiedAt.UtcDateTime.AddSeconds(5));
        WriteFixtureImage(candidateFile.Path.LocalPath, SKColors.DarkGreen);
        File.SetLastWriteTimeUtc(candidateFile.Path.LocalPath, previousCandidateModifiedAt.UtcDateTime.AddSeconds(5));

        var capturedCount = await viewModel.RefreshComparisonSourceImagesAsync(comparison, TestContext.Current.CancellationToken);

        Assert.Equal(2, capturedCount);
        Assert.False(comparison.RequiresReview);
        Assert.False(viewModel.Workspace.SelectedComparisonRow?.NeedsReview);
    }

    [AvaloniaFact]
    public async Task RefreshProjectSourceImagesAsync_refreshes_current_roles_across_project_comparisons()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var first = await viewModel.Workspace.AddComparisonAsync("First", TestContext.Current.CancellationToken);
        var firstFile = TestUiSupport.CreateStorageFile("refresh-project-first.png");
        var firstReference = await viewModel.ImageSet.AddImageAsync(firstFile, cancellationToken: TestContext.Current.CancellationToken);
        var firstModifiedAt = firstReference.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;
        var second = await viewModel.Workspace.AddComparisonAsync("Second", TestContext.Current.CancellationToken);
        var secondFile = TestUiSupport.CreateStorageFile("refresh-project-second.png");
        var secondReference = await viewModel.ImageSet.AddImageAsync(secondFile, cancellationToken: TestContext.Current.CancellationToken);
        var secondModifiedAt = secondReference.OriginalFileMetadata?.LastModifiedAt ?? DateTimeOffset.UtcNow;

        WriteFixtureImage(firstFile.Path.LocalPath, SKColors.DarkGreen);
        File.SetLastWriteTimeUtc(firstFile.Path.LocalPath, firstModifiedAt.UtcDateTime.AddSeconds(5));
        WriteFixtureImage(secondFile.Path.LocalPath, SKColors.Purple);
        File.SetLastWriteTimeUtc(secondFile.Path.LocalPath, secondModifiedAt.UtcDateTime.AddSeconds(5));

        var capturedCount = await viewModel.RefreshProjectSourceImagesAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(2, capturedCount);
        Assert.Equal(firstReference.Id, first.BaselineImage?.PreviousVersionImageId);
        Assert.Equal(secondReference.Id, second.BaselineImage?.PreviousVersionImageId);
    }

    [AvaloniaFact]
    public async Task ApplyCapturedImageVersionAsync_refreshes_selected_comparison_image_rows()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        var comparison = await viewModel.Workspace.AddComparisonAsync("Comparison", TestContext.Current.CancellationToken);
        var reference = await viewModel.ImageSet.AddImageAsync(
            "reference.png",
            new MemoryStream(TestUiSupport.CreatePngBytes(color: SKColors.Red)),
            cancellationToken: TestContext.Current.CancellationToken);
        var candidate = await viewModel.ImageSet.AddImageAsync(
            "candidate.png",
            new MemoryStream(TestUiSupport.CreatePngBytes(color: SKColors.Blue)),
            cancellationToken: TestContext.Current.CancellationToken);
        var version = new ImageAsset
        {
            Label = "Image",
            SourceName = "candidate.png",
            PreviousVersionImageId = candidate.Id
        };
        await storage.SaveImageAsync(
            project.Id,
            comparison.Id,
            version,
            new MemoryStream(TestUiSupport.CreatePngBytes(color: SKColors.Green)),
            TestContext.Current.CancellationToken);
        comparison.AddImage(version);
        comparison.SetCandidateImage(version.Id);
        Assert.DoesNotContain(viewModel.ImageSet.ImageRows, row => row.Id == version.Id);

        await viewModel.ApplyCapturedImageVersionAsync(project, comparison, version, TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.ImageSet.ImageRows, row => row.Id == version.Id && row.IsCandidate);
        Assert.Same(reference, comparison.BaselineImage);
        Assert.Same(version, comparison.CandidateImage);
    }

    [Fact]
    public async Task SetSelectedProjectSourceFileMonitoringAsync_updates_project_setting_and_saves()
    {
        var storage = new FakeProjectStorage();
        var viewModel = new MainWindowViewModel(storage);
        var project = await viewModel.Workspace.AddProjectAsync("Project", TestContext.Current.CancellationToken);
        storage.SavedProjects.Clear();

        await viewModel.SetSelectedProjectSourceFileMonitoringAsync(true, TestContext.Current.CancellationToken);

        Assert.True(project.Settings.MonitorSourceFilesForChanges);
        Assert.True(viewModel.IsSelectedProjectSourceFileMonitoringEnabled);
        Assert.Same(project, Assert.Single(storage.SavedProjects));
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
