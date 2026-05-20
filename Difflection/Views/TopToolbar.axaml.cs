using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Difflection.ViewModels;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace Difflection.Views;

public partial class TopToolbar : UserControl
{
    private MainWindowViewModel? _viewModel;

    public TopToolbar()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        UpdateViewControls();
    }

    public Action? FitZoomToStage { get; set; }

    public Action? RequestImageChangeMonitorRestart { get; set; }

    private void SideBySideViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ToolState.SelectSideBySideView();
        UpdateViewControls();
        FitZoomToStage?.Invoke();
    }

    private void SplitScreenViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ToolState.SelectSplitScreenView();
        UpdateViewControls();
        FitZoomToStage?.Invoke();
    }

    private void DifferenceViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ToolState.SelectDifferenceView();
        UpdateViewControls();
        FitZoomToStage?.Invoke();
    }

    private async void AddMediaButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await OpenFilePickerAndAddImagesAsync();
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
        FitZoomToStage?.Invoke();
    }

    private async void RefreshSelectedComparisonSourcesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Workspace.SelectedComparison is null)
        {
            return;
        }

        await _viewModel.RefreshComparisonSourceImagesAsync(_viewModel.Workspace.SelectedComparison);
        RequestImageChangeMonitorRestart?.Invoke();
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
        FitZoomToStage?.Invoke();
    }

    private void ActualSizeZoomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ToolState.SetZoomScale(1.0);
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
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeViewModelEvents();
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
