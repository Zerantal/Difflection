using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Difflection.ViewModels;

namespace Difflection.Views;

public partial class MainView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private bool _projectsLoaded;

    public MainView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        UpdateDropHints();
        UpdateViewControls();

    }

    private void SideBySideViewTab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.SelectSideBySideView();
        UpdateViewControls();
        ComparisonStage.FitZoomToStage();
        e.Handled = true;
    }

    private void SplitScreenViewTab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.SelectSplitScreenView();
        UpdateViewControls();
        ComparisonStage.FitZoomToStage();
        e.Handled = true;
    }

    private async void AddMediaButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ComparisonStage.OpenFilePickerAndLoadAsync();
    }

    private async void AddProjectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.AddProjectAsync();
        }
    }

    private async void DeleteProjectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.DeleteSelectedProjectAsync();
        }
    }

    private async void AddComparisonButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.AddComparisonAsync();
        }
    }

    private async void DeleteComparisonButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.DeleteSelectedComparisonAsync();
        }
    }

    public async Task LoadBrowserDroppedFilesAsync(IReadOnlyList<string> fileNames, IReadOnlyList<byte[]> fileContents)
    {
        await ComparisonStage.LoadBrowserDroppedFilesAsync(fileNames, fileContents);
    }

    private void ZoomTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _viewModel?.TrySetZoomText(ZoomTextBox.Text);
    }

    private void ZoomTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        _viewModel?.TrySetZoomText(ZoomTextBox.Text);
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        _projectsLoaded = false;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateDropHints();
        UpdateViewControls();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.LeftImage)
            or nameof(MainWindowViewModel.RightImage)
            or nameof(MainWindowViewModel.HasAnyImage))
        {
            UpdateDropHints();
        }

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedViewMode) or nameof(MainWindowViewModel.CanUseSplitScreen))
        {
            UpdateViewControls();
        }
    }

    private void UpdateDropHints()
    {
        DropHintBanner.IsVisible = _viewModel?.HasAnyImage != true;
    }

    private void UpdateViewControls()
    {
        if (_viewModel is null)
        {
            return;
        }

        SideBySideViewTabText.Foreground = Brush.Parse(_viewModel.IsSideBySideView ? "#F97316" : "#A8AFB8");
        SplitScreenViewTabText.Foreground = Brush.Parse(_viewModel.IsSplitScreenView ? "#F97316" : "#A8AFB8");
        SplitScreenViewTab.Opacity = _viewModel.CanUseSplitScreen ? 1.0 : 0.58;
        SideBySideViewTabUnderline.IsVisible = _viewModel.IsSideBySideView;
        SplitScreenViewTabUnderline.IsVisible = _viewModel.IsSplitScreenView;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        BrowserInterop.DetachBrowserBridge?.Invoke(this);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        BrowserInterop.AttachBrowserBridge?.Invoke(this);

        if (_viewModel is not null && !_projectsLoaded)
        {
            _projectsLoaded = true;
            await _viewModel.LoadProjectsAsync();
        }
    }
}
