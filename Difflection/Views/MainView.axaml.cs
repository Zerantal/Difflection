using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    private readonly WorkspaceSidebar? _workspaceSidebar;
    private Func<string, string, Task<bool>> _confirmDestructiveActionAsync = ConfirmationDialogService.ShowAsync;

    public MainView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        _workspaceSidebar = this.FindControl<WorkspaceSidebar>("WorkspaceSidebarHost");
        if (_workspaceSidebar is not null)
        {
            _workspaceSidebar.ConfirmDestructiveActionAsync = ConfirmDestructiveActionAsync;
            _workspaceSidebar.RequestImageChangeMonitorRestart = RestartImageChangeMonitor;
        }

        var topToolbar = this.FindControl<TopToolbar>("TopToolbarHost");
        if (topToolbar is not null)
        {
            topToolbar.FitZoomToStage = () => ComparisonStage.FitZoomToStage();
            topToolbar.RequestImageChangeMonitorRestart = RestartImageChangeMonitor;
        }
    }

    public Func<string, string, Task<bool>> ConfirmDestructiveActionAsync
    {
        get => _confirmDestructiveActionAsync;
        set
        {
            _confirmDestructiveActionAsync = value;
            if (_workspaceSidebar is not null)
            {
                _workspaceSidebar.ConfirmDestructiveActionAsync = value;
            }
        }
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

    // ReSharper disable once AsyncVoidEventHandlerMethod
    private async void RefreshSelectedProjectSourcesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Workspace.SelectedProject is null)
        {
            return;
        }

        await _viewModel.RefreshProjectSourceImagesAsync(_viewModel.Workspace.SelectedProject);
        RestartImageChangeMonitor();
    }

    // ReSharper disable once AsyncVoidEventHandlerMethod
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

        await _viewModel.Workspace.DeleteProjectAsync(row);
        RestartImageChangeMonitor();
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
        _ = ObservedTask.ReportFailureAsync(
            MainEmptyStateOverlay_OnDropAsync(e),
            "Difflection could not load the dropped images.");
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

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeViewModelEvents();

        _viewModel = DataContext as MainWindowViewModel;
        DisposeImageChangeMonitor();
        _imageChangeMonitor = CreateImageChangeMonitor(_viewModel);
        _projectsLoaded = false;

        SubscribeViewModelEvents();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedProject)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparison))
        {
            _ = ObservedTask.ReportFailureAsync(
                RefreshCurrentComparisonAndFitStageAsync(),
                "Difflection could not refresh the selected comparison.");
        }

        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedProjectComparisons)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparisonImages))
        {
            RestartImageChangeMonitor();
        }
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

        await _viewModel.ComparisonDisplay.RefreshCurrentComparisonImagesAsync(
            _viewModel.Workspace.SelectedComparison,
            _viewModel.ProjectStorage,
            deferDifferenceStatus: true);
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

        _viewModel.Workspace.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnsubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Workspace.PropertyChanged -= OnViewModelPropertyChanged;
    }

    // ReSharper disable once AsyncVoidEventHandlerMethod
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

    private void ImageChangeMonitor_OnVersionCaptured(object? sender, MonitoredImageVersionCapturedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (ReferenceEquals(e.Project, _viewModel.Workspace.SelectedProject)
            && ReferenceEquals(e.Comparison, _viewModel.Workspace.SelectedComparison))
        {
            _ = ObservedTask.ReportFailureAsync(
                RefreshCurrentComparisonAndFitStageAsync(),
                "Difflection could not display the captured source image change.");
        }
    }
}
