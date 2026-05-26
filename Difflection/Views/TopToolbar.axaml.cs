using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Difflection.Infrastructure;
using Difflection.ViewModels;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace Difflection.Views;

public partial class TopToolbar : UserControl
{
    private static readonly string[] _shortcutIds =
    [
        "view.side-by-side",
        "view.split-screen",
        "view.difference",
        "zoom.fit",
        "zoom.actual",
        "files.open",
        "sources.refresh"
    ];

    private MainWindowViewModel? _viewModel;
    private KeyboardShortcutRegistry? _registeredOn;

    public TopToolbar()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        UpdateViewControls();
    }

    public Action? FitZoomToStage { get; set; }

    public Action? RequestImageChangeMonitorRestart { get; set; }

    private void RegisterShortcuts(KeyboardShortcutRegistry registry)
    {
        if (ReferenceEquals(_registeredOn, registry))
        {
            return;
        }

        UnregisterShortcuts();

        registry
            .Add("view.side-by-side", "D1",      "Side-by-side view",  SwitchToSideBySideView)
            .Add("view.split-screen", "D2",      "Split-screen view",  SwitchToSplitScreenView)
            .Add("view.difference",   "D3",      "Difference view",    SwitchToDifferenceView)
            .Add("zoom.fit",          "Ctrl+D0", "Fit to window",      FitZoom)
            .Add("zoom.actual",       "Ctrl+D1", "Actual size (100%)", SetActualSize)
            .Add("files.open",        "Ctrl+O",  "Open files",         InvokeOpenFilePicker)
            .Add("sources.refresh",   "F5",      "Refresh sources",    InvokeRefreshSelectedComparisonSources);

        _registeredOn = registry;
    }

    private void UnregisterShortcuts()
    {
        if (_registeredOn is null)
        {
            return;
        }

        foreach (var id in _shortcutIds)
        {
            _registeredOn.Remove(id);
        }

        _registeredOn = null;
    }

    private void InvokeOpenFilePicker()
    {
        _ = ObservedTask.ReportFailureAsync(
            OpenFilePickerAndAddImagesCoreAsync(),
            "Difflection could not open the file picker.");
    }

    private void InvokeRefreshSelectedComparisonSources()
    {
        _ = ObservedTask.ReportFailureAsync(
            RefreshSelectedComparisonSourcesCoreAsync(),
            "Difflection could not refresh source images.");
    }

    private void SwitchToSideBySideView()
    {
        _viewModel?.ToolState.SelectSideBySideView();
        UpdateViewControls();
        FitZoomToStage?.Invoke();
    }

    private void SwitchToSplitScreenView()
    {
        _viewModel?.ToolState.SelectSplitScreenView();
        UpdateViewControls();
        FitZoomToStage?.Invoke();
    }

    private void SwitchToDifferenceView()
    {
        _viewModel?.ToolState.SelectDifferenceView();
        UpdateViewControls();
        FitZoomToStage?.Invoke();
    }

    private void FitZoom()
    {
        FitZoomToStage?.Invoke();
    }

    private void SetActualSize()
    {
        _viewModel?.ToolState.SetZoomScale(1.0);
    }

    private async Task RefreshSelectedComparisonSourcesCoreAsync()
    {
        if (_viewModel?.Workspace.SelectedComparison is null)
        {
            return;
        }

        await _viewModel.RefreshComparisonSourceImagesAsync(_viewModel.Workspace.SelectedComparison);
        RequestImageChangeMonitorRestart?.Invoke();
    }

    private void SideBySideViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SwitchToSideBySideView();
    }

    private void SplitScreenViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SwitchToSplitScreenView();
    }

    private void DifferenceViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SwitchToDifferenceView();
    }

    private async void AddMediaButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await OpenFilePickerAndAddImagesCoreAsync();
    }

    private async Task OpenFilePickerAndAddImagesCoreAsync()
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
        FitZoomToStage?.Invoke();
    }

    private async void RefreshSelectedComparisonSourcesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RefreshSelectedComparisonSourcesCoreAsync();
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

    private void FitZoomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FitZoom();
    }

    private void ActualSizeZoomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetActualSize();
    }

    private void DifferenceBaseBaselineButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ComparisonDisplay.SelectDifferenceBaseImage(DifferenceBaseImage.Baseline);
        UpdateViewControls();
    }

    private void DifferenceBaseCandidateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ComparisonDisplay.SelectDifferenceBaseImage(DifferenceBaseImage.Candidate);
        UpdateViewControls();
    }

    private void DifferenceBaseMapButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ComparisonDisplay.SelectDifferenceBaseImage(DifferenceBaseImage.Map);
        UpdateViewControls();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeViewModelEvents();
        _viewModel = DataContext as MainWindowViewModel;
        SubscribeViewModelEvents();
        UpdateViewControls();

        if (_viewModel is not null)
        {
            RegisterShortcuts(_viewModel.Shortcuts);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeViewModelEvents();
        UnregisterShortcuts();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ComparisonToolStateViewModel.SelectedViewMode)
            or nameof(ComparisonToolStateViewModel.CanUseSplitScreen)
            or nameof(ComparisonToolStateViewModel.CanUseDifferenceView))
        {
            UpdateViewControls();
        }

        if (e.PropertyName is nameof(ComparisonDisplayViewModel.DifferenceBaseImage))
        {
            UpdateViewControls();
        }
    }

    private void UpdateViewControls()
    {
        if (_viewModel is null)
        {
            return;
        }

        ApplyViewModeButtonState(SideBySideViewButton, _viewModel.ToolState.IsSideBySideView);
        ApplyViewModeButtonState(SplitScreenViewButton, _viewModel.ToolState.IsSplitScreenView);
        ApplyViewModeButtonState(DifferenceViewButton, _viewModel.ToolState.IsDifferenceView);
    }

    private static void ApplyViewModeButtonState(Button button, bool isActive)
    {
        button.Classes.Set("active", isActive);
    }

    private void SubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToolState.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ComparisonDisplay.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnsubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToolState.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ComparisonDisplay.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
