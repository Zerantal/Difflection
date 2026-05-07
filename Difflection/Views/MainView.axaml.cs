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
using Avalonia.VisualTree;
using Difflection.Infrastructure;
using Difflection.Models;
using Difflection.Monitoring;
using Difflection.ViewModels;

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
            var project = await _viewModel.AddProjectAsync();
            await RefreshStageForSelectedComparisonAsync();
            BeginEditProjectName(project);
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
            var comparison = await _viewModel.AddComparisonAsync();
            await RefreshStageForSelectedComparisonAsync();
            BeginEditComparisonName(comparison);
        }
    }

    private async void DeleteComparisonButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.DeleteSelectedComparisonAsync();
        }
    }

    private async void ProjectListNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: Project project } textBox && !textBox.IsReadOnly)
        {
            await _viewModel.RenameProjectAsync(project, textBox.Text);
            EndInlineRename(textBox);
        }
    }

    private async void ProjectListNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: Project project } textBox || textBox.IsReadOnly)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel is not null)
        {
            await _viewModel.RenameProjectAsync(project, textBox.Text);
            EndInlineRename(textBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            textBox.Text = project.Name;
            EndInlineRename(textBox);
            e.Handled = true;
        }
    }

    private async void ComparisonListNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ComparisonSet comparison } textBox && !textBox.IsReadOnly)
        {
            await _viewModel.RenameComparisonAsync(comparison, textBox.Text);
            EndInlineRename(textBox);
        }
    }

    private async void ComparisonListNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ComparisonSet comparison } textBox || textBox.IsReadOnly)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel is not null)
        {
            await _viewModel.RenameComparisonAsync(comparison, textBox.Text);
            EndInlineRename(textBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            textBox.Text = comparison.Name;
            EndInlineRename(textBox);
            e.Handled = true;
        }
    }

    private void ProjectRenameMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Project project })
        {
            BeginEditProjectName(project);
        }
    }

    private void ComparisonRenameMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: ComparisonSet comparison })
        {
            BeginEditComparisonName(comparison);
        }
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

    private void MainEmptyStateOverlay_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = GetDroppedFiles(e.DataTransfer).Any();
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MainEmptyStateOverlay_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        _ = MainEmptyStateOverlay_OnDropAsync(e);
    }

    private async Task MainEmptyStateOverlay_OnDropAsync(DragEventArgs e)
    {
        var files = GetDroppedFiles(e.DataTransfer).Take(2).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        await ComparisonStage.LoadDroppedFilesAsync(null, files);
        await RefreshStageForSelectedComparisonAsync();
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
        DisposeImageChangeMonitor();
        _imageChangeMonitor = CreateImageChangeMonitor(_viewModel);
        _projectsLoaded = false;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateViewControls();
        SyncSidebarSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedProject)
            or nameof(MainWindowViewModel.SelectedComparison))
        {
            _ = RefreshStageForSelectedComparisonAsync();
        }

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedProjectComparisons)
            or nameof(MainWindowViewModel.SelectedComparisonImages))
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

    private void BeginEditProjectName(Project project)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SyncSidebarSelection();
            if (FindInlineNameTextBox(ProjectsList, project) is { } textBox)
            {
                BeginInlineRename(textBox);
            }
        });
    }

    private void BeginEditComparisonName(ComparisonSet comparison)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SyncSidebarSelection();
            if (FindInlineNameTextBox(ComparisonsList, comparison) is { } textBox)
            {
                BeginInlineRename(textBox);
            }
        });
    }

    private static TextBox? FindInlineNameTextBox(ItemsControl list, object item)
    {
        if (list.ContainerFromItem(item) is not { } container)
        {
            return null;
        }

        return container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
    }

    private static void BeginInlineRename(TextBox textBox)
    {
        textBox.IsReadOnly = false;
        textBox.Focusable = true;
        textBox.IsHitTestVisible = true;
        textBox.Classes.Add("editing");
        textBox.Focus();
        textBox.SelectAll();
    }

    private static void EndInlineRename(TextBox textBox)
    {
        textBox.IsReadOnly = true;
        textBox.Focusable = false;
        textBox.IsHitTestVisible = false;
        textBox.Classes.Remove("editing");
        textBox.SelectionStart = 0;
        textBox.SelectionEnd = 0;
    }

    private static IEnumerable<IStorageFile> GetDroppedFiles(IDataTransfer dataTransfer)
    {
        return dataTransfer.Items
            .Select(item => item.TryGetRaw(DataFormat.File))
            .OfType<IStorageFile>();
    }

    private async Task RefreshStageForSelectedComparisonAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.RefreshCurrentComparisonImagesAsync();
        UpdateViewControls();
        ComparisonStage.FitZoomToStage();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        BrowserInterop.DetachBrowserBridge?.Invoke(this);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        DisposeImageChangeMonitor();
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
                SyncSidebarSelection();
                Dispatcher.UIThread.Post(SyncSidebarSelection);
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
            _imageChangeMonitor?.Start(_viewModel.Projects);
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

        if (ReferenceEquals(e.Project, _viewModel.SelectedProject)
            && ReferenceEquals(e.Comparison, _viewModel.SelectedComparison))
        {
            await _viewModel.RefreshCurrentComparisonImagesAsync();
        }
    }
}
