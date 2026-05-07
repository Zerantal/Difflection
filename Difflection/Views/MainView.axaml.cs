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
using Difflection.Models;
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
        await OpenFilePickerAndAddImagesAsync();
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

    private async void ProjectNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.RenameSelectedProjectAsync(ProjectNameTextBox.Text);
        }
    }

    private async void ProjectNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
        {
            return;
        }

        await _viewModel.RenameSelectedProjectAsync(ProjectNameTextBox.Text);
        e.Handled = true;
    }

    private async void ComparisonNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.RenameSelectedComparisonAsync(ComparisonNameTextBox.Text);
        }
    }

    private async void ComparisonNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
        {
            return;
        }

        await _viewModel.RenameSelectedComparisonAsync(ComparisonNameTextBox.Text);
        e.Handled = true;
    }

    private async void ImageLabelTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ImageAsset image } textBox)
        {
            await _viewModel.LabelImageAsync(image, textBox.Text);
        }
    }

    private async void ImageLabelTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null || sender is not TextBox { DataContext: ImageAsset image } textBox)
        {
            return;
        }

        await _viewModel.LabelImageAsync(image, textBox.Text);
        e.Handled = true;
    }

    private async void SetReferenceImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button { DataContext: ImageAsset image })
        {
            return;
        }

        if (await _viewModel.SetReferenceImageAsync(image))
        {
            await _viewModel.RefreshCurrentComparisonImagesAsync();
            ComparisonStage.FitZoomToStage();
        }
    }

    private async void SetCandidateImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button { DataContext: ImageAsset image } || !_viewModel.CanSetCandidateImage(image))
        {
            return;
        }

        if (await _viewModel.SetCandidateImageAsync(image))
        {
            await _viewModel.RefreshCurrentComparisonImagesAsync();
            ComparisonStage.FitZoomToStage();
        }
    }

    private async void DeleteImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button { DataContext: ImageAsset image })
        {
            return;
        }

        if (await _viewModel.DeleteImageAsync(image))
        {
            await _viewModel.RefreshCurrentComparisonImagesAsync();
            ComparisonStage.FitZoomToStage();
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

        await AddFilesToCurrentComparisonAsync(files);
    }

    private async Task AddFilesToCurrentComparisonAsync(IEnumerable<IStorageFile> files)
    {
        if (_viewModel is null)
        {
            return;
        }

        var imageFiles = files.ToArray();
        if (imageFiles.Length == 0)
        {
            return;
        }

        await _viewModel.EnsureProjectAndComparisonAsync();

        foreach (var file in imageFiles)
        {
            await _viewModel.AddImageAsync(file);
        }

        await _viewModel.RefreshCurrentComparisonImagesAsync();
        ComparisonStage.FitZoomToStage();
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
        SyncSidebarSelection();
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

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedProject)
            or nameof(MainWindowViewModel.SelectedComparison)
            or nameof(MainWindowViewModel.SelectedProjectComparisons))
        {
            SyncSidebarSelection();
            Dispatcher.UIThread.Post(SyncSidebarSelection);
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

    private void SyncSidebarSelection()
    {
        if (_viewModel is null)
        {
            return;
        }

        var projectIndex = _viewModel.SelectedProject is null
            ? -1
            : _viewModel.Projects.IndexOf(_viewModel.SelectedProject);

        if (ProjectsList.SelectedIndex != projectIndex)
        {
            ProjectsList.SelectedIndex = projectIndex;
        }

        var comparisonIndex = _viewModel.SelectedProject is null || _viewModel.SelectedComparison is null
            ? -1
            : _viewModel.SelectedProject.Comparisons.IndexOf(_viewModel.SelectedComparison);

        if (ComparisonsList.SelectedIndex != comparisonIndex)
        {
            ComparisonsList.SelectedIndex = comparisonIndex;
        }
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
            SyncSidebarSelection();
            Dispatcher.UIThread.Post(SyncSidebarSelection);
        }
    }
}
