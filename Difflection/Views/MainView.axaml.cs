using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Difflection.Infrastructure;
using Difflection.Monitoring;
using Difflection.ViewModels;
using JetBrains.Annotations;

namespace Difflection.Views;

public partial class MainView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private ProjectImageChangeMonitor? _imageChangeMonitor;
    private bool _projectsLoaded;

    public MainView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        UpdateViewControls();

    }

    public Func<string, string, Task<bool>> ConfirmDestructiveActionAsync { get; set; } = ConfirmationDialogService.ShowAsync;

    private void SidebarItem_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { ContextMenu: { Items: { } items } } control || control.DataContext is null)
        {
            return;
        }

        foreach (var menuItem in items.OfType<MenuItem>())
        {
            menuItem.CommandParameter = control.DataContext;

            if (string.Equals(menuItem.Header?.ToString(), "Rename", StringComparison.Ordinal))
            {
                menuItem.Command = control.DataContext switch
                {
                    ProjectListItemViewModel => _viewModel?.Workspace.BeginRenameProjectCommand,
                    ComparisonListItemViewModel => _viewModel?.Workspace.BeginRenameComparisonCommand,
                    _ => menuItem.Command
                };
            }
        }
    }

    private void SideBySideViewTab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.ToolState.SelectSideBySideView();
        UpdateViewControls();
        ComparisonStage.FitZoomToStage();
        e.Handled = true;
    }

    private void SplitScreenViewTab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.ToolState.SelectSplitScreenView();
        UpdateViewControls();
        ComparisonStage.FitZoomToStage();
        e.Handled = true;
    }

    private async void AddMediaButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await OpenFilePickerAndAddImagesAsync();
    }

    private async void ProjectListNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ProjectListItemViewModel row } && row.IsEditing)
        {
            await _viewModel.Workspace.CommitProjectRenameAsync(row);
        }
    }

    private async void ProjectListNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ProjectListItemViewModel row } || !row.IsEditing)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel is not null)
        {
            await _viewModel.Workspace.CommitProjectRenameAsync(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            WorkspaceNavigatorViewModel.CancelProjectRename(row);
            e.Handled = true;
        }
    }

    private async void ComparisonListNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ComparisonListItemViewModel row } && row.IsEditing)
        {
            await _viewModel.Workspace.CommitComparisonRenameAsync(row);
        }
    }

    private async void ComparisonListNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ComparisonListItemViewModel row } || !row.IsEditing)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel is not null)
        {
            await _viewModel.Workspace.CommitComparisonRenameAsync(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            WorkspaceNavigatorViewModel.CancelComparisonRename(row);
            e.Handled = true;
        }
    }

    private async void RefreshProjectSourcesMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem { CommandParameter: ProjectListItemViewModel row })
        {
            return;
        }

        await _viewModel.RefreshProjectSourceImagesAsync(row.Project);
        RestartImageChangeMonitor();
    }

    private async void RefreshSelectedProjectSourcesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Workspace.SelectedProject is null)
        {
            return;
        }

        await _viewModel.RefreshProjectSourceImagesAsync(_viewModel.Workspace.SelectedProject);
        RestartImageChangeMonitor();
    }

    private async void RefreshComparisonSourcesMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem { CommandParameter: ComparisonListItemViewModel row })
        {
            return;
        }

        await _viewModel.RefreshComparisonSourceImagesAsync(row.Comparison);
        RestartImageChangeMonitor();
    }

    private async void DeleteProjectMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem { CommandParameter: ProjectListItemViewModel row })
        {
            return;
        }

        var confirmed = await ConfirmDestructiveActionAsync(
            "Delete project?",
            $"Delete project \"{row.Name}\" and all of its comparisons and images?");

        if (!confirmed)
        {
            return;
        }

        await _viewModel.Workspace.DeleteProjectAsync(row.Project);
        RestartImageChangeMonitor();
    }

    private async void DeleteSelectedProjectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Workspace.SelectedProjectRow is not { } row)
        {
            return;
        }

        var confirmed = await ConfirmDestructiveActionAsync(
            "Delete project?",
            $"Delete project \"{row.Name}\" and all of its comparisons and images?");

        if (!confirmed)
        {
            return;
        }

        await _viewModel.Workspace.DeleteProjectAsync(row.Project);
        RestartImageChangeMonitor();
    }

    private async void DeleteComparisonMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem { CommandParameter: ComparisonListItemViewModel row })
        {
            return;
        }

        var confirmed = await ConfirmDestructiveActionAsync(
            "Delete comparison?",
            $"Delete comparison \"{row.Name}\" and all images in its image set?");

        if (!confirmed)
        {
            return;
        }

        await _viewModel.Workspace.DeleteComparisonAsync(row.Comparison);
        RestartImageChangeMonitor();
    }

    private void InlineNameTextBox_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        FocusInlineNameEditor(sender as TextBox);
    }

    private void InlineNameTextBox_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
        {
            FocusInlineNameEditor(sender as TextBox);
        }
    }

    private static void FocusInlineNameEditor(TextBox? textBox)
    {
        if (textBox is not null
            && textBox.IsVisible
            && (textBox.DataContext is ProjectListItemViewModel { IsEditing: true }
                || textBox.DataContext is ComparisonListItemViewModel { IsEditing: true }))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!textBox.IsVisible)
                {
                    return;
                }

                textBox.Focus();
                textBox.SelectAll();
            });
        }
    }

    private async void ImageLabelTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ComparisonImageSetItemViewModel row } textBox)
        {
            await _viewModel.ImageSet.LabelImageAsync(row.Image, textBox.Text);
        }
    }

    private async void ImageLabelTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null || sender is not TextBox { DataContext: ComparisonImageSetItemViewModel row } textBox)
        {
            return;
        }

        await _viewModel.ImageSet.LabelImageAsync(row.Image, textBox.Text);
        e.Handled = true;
    }

    public async Task LoadBrowserDroppedFilesAsync(IReadOnlyList<string> fileNames, IReadOnlyList<byte[]> fileContents)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.ImageSet.AddBrowserFilesToCurrentComparisonAsync(fileNames, fileContents, maxFiles: 2);
        ComparisonStage.FitZoomToStage();
    }

    [UsedImplicitly]
    private void MainEmptyStateOverlay_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = GetDroppedFiles(e.DataTransfer).Any();
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    [UsedImplicitly]
    private void MainEmptyStateOverlay_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        _ = MainEmptyStateOverlay_OnDropAsync(e);
    }

    private async Task MainEmptyStateOverlay_OnDropAsync(DragEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var files = GetDroppedFiles(e.DataTransfer).Take(2).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        await _viewModel.ImageSet.AddFilesToCurrentComparisonAfterCommittingRenamesAsync(files, maxFiles: 2);
        ComparisonStage.FitZoomToStage();
    }

    private void ZoomTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ToolState.TrySetZoomText(ZoomTextBox.Text);
    }

    private void ZoomTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        _viewModel?.ToolState.TrySetZoomText(ZoomTextBox.Text);
        e.Handled = true;
    }

    private async Task OpenFilePickerAndAddImagesAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Add images to comparison",
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.tif", "*.tiff"],
                    MimeTypes = ["image/*"]
                }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        await _viewModel.ImageSet.AddFilesToCurrentComparisonAsync(files);
        ComparisonStage.FitZoomToStage();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeViewModelEvents();

        _viewModel = DataContext as MainWindowViewModel;
        DisposeImageChangeMonitor();
        _imageChangeMonitor = CreateImageChangeMonitor(_viewModel);
        _projectsLoaded = false;

        SubscribeViewModelEvents();

        UpdateViewControls();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ComparisonToolStateViewModel.SelectedViewMode) or nameof(ComparisonToolStateViewModel.CanUseSplitScreen))
        {
            UpdateViewControls();
        }

        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedProject)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparison))
        {
            _ = RefreshCurrentComparisonAndFitStageAsync();
        }

        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedProjectComparisons)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparisonImages))
        {
            RestartImageChangeMonitor();
        }
    }

    private void UpdateViewControls()
    {
        if (_viewModel is null)
        {
            return;
        }

        SideBySideViewTabText.Foreground = Brush.Parse(_viewModel.ToolState.IsSideBySideView ? "#F97316" : "#A8AFB8");
        SplitScreenViewTabText.Foreground = Brush.Parse(_viewModel.ToolState.IsSplitScreenView ? "#F97316" : "#A8AFB8");
        SplitScreenViewTab.Opacity = _viewModel.ToolState.CanUseSplitScreen ? 1.0 : 0.58;
        SideBySideViewTabUnderline.IsVisible = _viewModel.ToolState.IsSideBySideView;
        SplitScreenViewTabUnderline.IsVisible = _viewModel.ToolState.IsSplitScreenView;
    }

    private static IEnumerable<IStorageFile> GetDroppedFiles(IDataTransfer dataTransfer)
    {
        return dataTransfer.Items
            .Select(item => item.TryGetRaw(DataFormat.File))
            .OfType<IStorageFile>();
    }

    private async Task RefreshCurrentComparisonAndFitStageAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(_viewModel.Workspace.SelectedComparison, _viewModel.ProjectStorage);
        UpdateViewControls();
        ComparisonStage.FitZoomToStage();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        BrowserInterop.DetachBrowserBridge?.Invoke(this);

        UnsubscribeViewModelEvents();

        DisposeImageChangeMonitor();
    }

    private void SubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToolState.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Workspace.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnsubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToolState.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Workspace.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        BrowserInterop.AttachBrowserBridge?.Invoke(this);

        if (_viewModel is not null && !_projectsLoaded)
        {
            try
            {
                _projectsLoaded = true;
                await _viewModel.LoadProjectsAsync();
                RestartImageChangeMonitor();
            }
            catch (Exception exception)
            {
                ApplicationErrorReporter.Report(exception, "Difflection could not load saved projects.");
            }
        }
    }

    private void RestartImageChangeMonitor()
    {
        if (_viewModel is not null)
        {
            _imageChangeMonitor?.Start(_viewModel.Workspace.Projects);
        }
    }

    private ProjectImageChangeMonitor? CreateImageChangeMonitor(MainWindowViewModel? viewModel)
    {
        if (viewModel?.ProjectStorage is not { } projectStorage || OperatingSystem.IsBrowser())
        {
            return null;
        }

        var monitor = new ProjectImageChangeMonitor(
            new DesktopImageSourceChangeWatcher(),
            new MonitoredImageVersionCapture(projectStorage));

        monitor.VersionCaptured += ImageChangeMonitor_OnVersionCaptured;
        return monitor;
    }

    private void DisposeImageChangeMonitor()
    {
        if (_imageChangeMonitor is not null)
        {
            _imageChangeMonitor.VersionCaptured -= ImageChangeMonitor_OnVersionCaptured;
            _imageChangeMonitor.Dispose();
            _imageChangeMonitor = null;
        }
    }

    private async void ImageChangeMonitor_OnVersionCaptured(object? sender, MonitoredImageVersionCapturedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (ReferenceEquals(e.Project, _viewModel.Workspace.SelectedProject)
            && ReferenceEquals(e.Comparison, _viewModel.Workspace.SelectedComparison))
        {
            await RefreshCurrentComparisonAndFitStageAsync();
        }
    }

}
