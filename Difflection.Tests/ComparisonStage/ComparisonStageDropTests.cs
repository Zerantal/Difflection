using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Difflection.Tests.Infrastructure;
using Difflection.ViewModels;
using Xunit;

namespace Difflection.Tests.ComparisonStage;

public sealed partial class ComparisonStageTests
{
    [AvaloniaFact]
    public async Task Side_by_side_drop_raises_drag_over_and_loads_left_image()
    {
        var viewModel = new MainWindowViewModel();
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var surface = stage.FindControl<Border>("SideBySideDropOverlay") ?? throw new InvalidOperationException("SideBySideDropOverlay not found.");
            var file = TestUiSupport.CreateStorageFile("reference.png");
            var transfer = TestUiSupport.CreateTransfer(file);

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel.HasLeftImage);

            Assert.True(viewModel.HasLeftImage);
            Assert.Equal("reference.png", viewModel.LeftFileName);
            Assert.False(viewModel.HasRightImage);

            var project = Assert.Single(viewModel.Workspace.Projects);
            var comparison = Assert.Single(project.Comparisons);
            var image = Assert.Single(comparison.Images);
            Assert.Equal("reference", image.Label);
            Assert.Equal(image.Id, comparison.ReferenceImageId);
            Assert.Null(comparison.CandidateImageId);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Split_screen_drop_raises_drag_over_and_loads_both_images()
    {
        var viewModel = new MainWindowViewModel();
        var leftFile = TestUiSupport.CreateStorageFile("left.png");
        var rightFile = TestUiSupport.CreateStorageFile("right.png");
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Left, leftFile);
        await viewModel.ComparisonDisplay.LoadImageAsync(ImageSlot.Right, rightFile);
        viewModel.ToolState.SelectSplitScreenView();

        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var stage = TestUiSupport.GetComparisonStage(window);
            var pane = TestUiSupport.GetSplitPane(stage);
            var surface = pane.Content as Grid ?? throw new InvalidOperationException("Split pane root not found.");
            var transfer = TestUiSupport.CreateTransfer(TestUiSupport.CreateStorageFile("swap-left.png"), TestUiSupport.CreateStorageFile("swap-right.png"));

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, surface, new Point(10, 10), KeyModifiers.None);
            surface.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel is { LeftFileName: "swap-left.png", RightFileName: "swap-right.png" });

            Assert.Equal("swap-left.png", viewModel.LeftFileName);
            Assert.Equal("swap-right.png", viewModel.RightFileName);
            Assert.True(viewModel.HasBothImages);

            var project = Assert.Single(viewModel.Workspace.Projects);
            var comparison = Assert.Single(project.Comparisons);
            Assert.Equal("swap-left", comparison.Name);
            Assert.Equal("Untitled Project / swap-left", viewModel.WorkspaceStatus.WorkspaceContextTitle);
            Assert.Equal(2, comparison.Images.Count);
            Assert.Equal(comparison.Images[0].Id, comparison.ReferenceImageId);
            Assert.Equal(comparison.Images[1].Id, comparison.CandidateImageId);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_state_overlay_drop_raises_drag_over_and_loads_images()
    {
        var viewModel = new MainWindowViewModel();
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var overlay = mainView.FindControl<Border>("MainEmptyStateOverlay")
                ?? throw new InvalidOperationException("MainEmptyStateOverlay not found.");
            await TestUiSupport.WaitForAsync(() => overlay.IsVisible);

            var transfer = TestUiSupport.CreateTransfer(
                TestUiSupport.CreateStorageFile("empty-reference.png"),
                TestUiSupport.CreateStorageFile("empty-candidate.png"));

            var dragOver = new DragEventArgs(DragDrop.DragOverEvent, transfer, overlay, new Point(10, 10), KeyModifiers.None);
            overlay.RaiseEvent(dragOver);

            Assert.Equal(DragDropEffects.Copy, dragOver.DragEffects);

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, overlay, new Point(10, 10), KeyModifiers.None);
            overlay.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel.HasBothImages && !overlay.IsVisible);

            Assert.Equal("empty-reference.png", viewModel.LeftFileName);
            Assert.Equal("empty-candidate.png", viewModel.RightFileName);
            Assert.False(viewModel.WorkspaceStatus.ShowMainEmptyState);

            var project = Assert.Single(viewModel.Workspace.Projects);
            var comparison = Assert.Single(project.Comparisons);
            Assert.Equal(2, comparison.Images.Count);
            Assert.Equal(comparison.Images[0].Id, comparison.ReferenceImageId);
            Assert.Equal(comparison.Images[1].Id, comparison.CandidateImageId);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_state_overlay_single_file_drop_sets_default_comparison_name()
    {
        var viewModel = new MainWindowViewModel();
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var overlay = mainView.FindControl<Border>("MainEmptyStateOverlay")
                ?? throw new InvalidOperationException("MainEmptyStateOverlay not found.");
            await TestUiSupport.WaitForAsync(() => overlay.IsVisible);

            var transfer = TestUiSupport.CreateTransfer(TestUiSupport.CreateStorageFile("single-reference.png"));

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, overlay, new Point(10, 10), KeyModifiers.None);
            overlay.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel.HasLeftImage && !overlay.IsVisible);

            var project = Assert.Single(viewModel.Workspace.Projects);
            var comparison = Assert.Single(project.Comparisons);
            Assert.Equal("single-reference", comparison.Name);
            Assert.Equal("Untitled Project / single-reference", viewModel.WorkspaceStatus.WorkspaceContextTitle);
            Assert.Equal("single-reference.png", viewModel.LeftFileName);
            Assert.False(viewModel.HasRightImage);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Browser_drop_into_empty_state_sets_default_comparison_name()
    {
        var viewModel = new MainWindowViewModel();
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var file = TestUiSupport.CreateStorageFile("browser-reference.png");
            await using var stream = await file.OpenReadAsync();
            using var buffer = new System.IO.MemoryStream();
            await stream.CopyToAsync(buffer);

            await mainView.LoadBrowserDroppedFilesAsync(
                ["browser-reference.png"],
                [buffer.ToArray()]);

            await TestUiSupport.WaitForAsync(() => viewModel.HasLeftImage);

            var project = Assert.Single(viewModel.Workspace.Projects);
            var comparison = Assert.Single(project.Comparisons);
            Assert.Equal("browser-reference", comparison.Name);
            Assert.Equal("Untitled Project / browser-reference", viewModel.WorkspaceStatus.WorkspaceContextTitle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_state_overlay_drop_into_project_without_comparison_sets_default_comparison_name()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.Workspace.AddProjectAsync("Existing Project");
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var overlay = mainView.FindControl<Border>("MainEmptyStateOverlay")
                ?? throw new InvalidOperationException("MainEmptyStateOverlay not found.");
            await TestUiSupport.WaitForAsync(() => overlay.IsVisible);

            var transfer = TestUiSupport.CreateTransfer(TestUiSupport.CreateStorageFile("project-reference.png"));

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, overlay, new Point(10, 10), KeyModifiers.None);
            overlay.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel.HasLeftImage && viewModel.Workspace.SelectedComparison is not null);

            var comparison = Assert.Single(viewModel.Workspace.SelectedProject!.Comparisons);
            Assert.Equal("project-reference", comparison.Name);
            Assert.Equal("Existing Project / project-reference", viewModel.WorkspaceStatus.WorkspaceContextTitle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_state_overlay_drop_keeps_first_image_name_when_new_comparison_editor_is_active()
    {
        var viewModel = new MainWindowViewModel();
        await viewModel.Workspace.AddProjectAsync("Existing Project");
        var window = TestUiSupport.CreateWindow(viewModel);
        try
        {
            var mainView = TestUiSupport.GetMainView(window);
            var addComparisonButton = mainView.FindControl<Button>("AddComparisonButton")
                ?? throw new InvalidOperationException("AddComparisonButton not found.");
            addComparisonButton.Command?.Execute(addComparisonButton.CommandParameter);
            addComparisonButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await TestUiSupport.WaitForAsync(() => viewModel.Workspace.SelectedComparisonRow?.IsEditing == true);

            var overlay = mainView.FindControl<Border>("MainEmptyStateOverlay")
                ?? throw new InvalidOperationException("MainEmptyStateOverlay not found.");
            var transfer = TestUiSupport.CreateTransfer(TestUiSupport.CreateStorageFile("active-editor-reference.png"));

            var drop = new DragEventArgs(DragDrop.DropEvent, transfer, overlay, new Point(10, 10), KeyModifiers.None);
            overlay.RaiseEvent(drop);

            await TestUiSupport.WaitForAsync(() => viewModel.HasLeftImage);

            var comparison = Assert.Single(viewModel.Workspace.SelectedProject!.Comparisons);
            Assert.Equal("active-editor-reference", comparison.Name);
            Assert.Equal("Existing Project / active-editor-reference", viewModel.WorkspaceStatus.WorkspaceContextTitle);
            Assert.All(viewModel.Workspace.SelectedProjectComparisonRows, row => Assert.False(row.IsEditing));
        }
        finally
        {
            window.Close();
        }
    }
}
