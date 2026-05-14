using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
        if (e.PropertyName is nameof(ComparisonToolStateViewModel.SelectedViewMode) or nameof(ComparisonToolStateViewModel.CanUseSplitScreen))
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
        SplitScreenViewButton.Opacity = _viewModel.ToolState.CanUseSplitScreen ? 1.0 : 0.58;
    }

    private static void ApplyViewModeButtonState(Button button, bool isActive)
    {
        button.Background = Brush.Parse(isActive ? "#3A2A1F" : "#262626");
        button.BorderBrush = Brush.Parse(isActive ? "#F97316" : "#444444");
        button.Foreground = Brush.Parse(isActive ? "#F97316" : "#A8AFB8");
    }

    private void SubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToolState.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnsubscribeViewModelEvents()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToolState.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
