using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Difflection.ViewModels;

namespace Difflection.Views;

public partial class MainWindow : Window
{
    private const double ZoomStepFactor = 1.15;
    private const double SplitDragSurfaceWidth = 24;
    private const double SplitRatioMin = 0.0;
    private const double SplitRatioMax = 1.0;

    private MainWindowViewModel? _viewModel;
    private bool _isDraggingSplit;
    private double _splitRatio = 0.5;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        PointerMoved += OnWindowPointerMoved;
        PointerReleased += OnWindowPointerReleased;
        Closed += OnClosed;
        SideBySideScrollViewer.AddHandler(PointerWheelChangedEvent, StageScrollViewer_OnPointerWheelChanged, RoutingStrategies.Tunnel);
        SplitScreenScrollViewer.AddHandler(PointerWheelChangedEvent, StageScrollViewer_OnPointerWheelChanged, RoutingStrategies.Tunnel);
        DragDrop.AddDragOverHandler(StageDropSurface, StageSurface_OnDragOver);
        DragDrop.AddDropHandler(StageDropSurface, StageSurface_OnDrop);
        DragDrop.AddDragOverHandler(SideBySideDropSurface, StageSurface_OnDragOver);
        DragDrop.AddDropHandler(SideBySideDropSurface, StageSurface_OnDrop);

        UpdateSplitVisuals();
        UpdateDropHints();
        UpdateViewControls();
    }

    private void SideBySideViewTab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.SelectSideBySideView();
        UpdateViewControls();
        FitZoomToStage();
        e.Handled = true;
    }

    private void SplitScreenViewTab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.SelectSplitScreenView();
        UpdateViewControls();
        FitZoomToStage();
        e.Handled = true;
    }

    private async void AddMediaButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Select images to compare",
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.tif", "*.tiff"],
                    MimeTypes = ["image/*"],
                },
            ],
        });

        await LoadDroppedFilesAsync(files);
    }

    private void SplitDivider_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDraggingSplit = true;
        e.Pointer.Capture(SplitDragSurface);
        UpdateSplitRatio(e.GetPosition(StageSurface).X);
        e.Handled = true;
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingSplit)
        {
            return;
        }

        UpdateSplitRatio(e.GetPosition(StageSurface).X);
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingSplit)
        {
            return;
        }

        _isDraggingSplit = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void StageSurface_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateSplitVisuals();
    }

    private void StageViewport_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ConstrainStageScrollViewers();
    }

    private void StageSurface_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = e.DataTransfer.TryGetFiles() is { Length: > 0 };
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void StageSurface_OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
        {
            return;
        }

        await LoadDroppedFilesAsync(files);
        e.Handled = true;
    }

    private async Task LoadDroppedFilesAsync(IEnumerable<IStorageItem> items)
    {
        if (_viewModel is null)
        {
            return;
        }

        var files = items.OfType<IStorageFile>().Take(2).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        if (files.Length >= 2)
        {
            await _viewModel.LoadImageAsync(ImageSlot.Left, files[0]);
            await _viewModel.LoadImageAsync(ImageSlot.Right, files[1]);
            FitZoomToStage();
            return;
        }

        var slot = ResolveNextSlot();
        await _viewModel.LoadImageAsync(slot, files[0]);
        FitZoomToStage();
    }

    private ImageSlot ResolveNextSlot() =>
        _viewModel switch
        {
            null => ImageSlot.Left,
            { HasLeftImage: false } => ImageSlot.Left,
            { HasRightImage: false } => ImageSlot.Right,
            _ => ImageSlot.Right,
        };

    private void UpdateSplitRatio(double pointerX)
    {
        if (StageSurface.Bounds.Width <= 0)
        {
            return;
        }

        _splitRatio = Math.Clamp(pointerX / StageSurface.Bounds.Width, SplitRatioMin, SplitRatioMax);
        UpdateSplitVisuals();
    }

    private void UpdateSplitVisuals()
    {
        if (StageSurface.Bounds.Width <= 0)
        {
            return;
        }

        var splitX = StageSurface.Bounds.Width * _splitRatio;
        var stageWidth = StageSurface.Bounds.Width;
        var stageHeight = StageSurface.Bounds.Height;
        var rightWidth = Math.Max(0, stageWidth - splitX);

        LeftImageLayer.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, splitX), stageHeight));
        RightImageLayer.Clip = new RectangleGeometry(new Rect(splitX, 0, rightWidth, stageHeight));

        SplitDivider.Margin = new Thickness(Math.Max(0, splitX - 1), 0, 0, 0);
        SplitDragSurface.Margin = new Thickness(Math.Max(0, splitX - (SplitDragSurfaceWidth / 2)), 0, 0, 0);

        if (_viewModel is not null)
        {
            var leftPercent = (int)Math.Round(_splitRatio * 100);
            _viewModel.SplitPercentageText = $"{leftPercent} / {100 - leftPercent}";
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateSplitVisuals();
        UpdateDropHints();
        UpdateViewControls();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.LeftImage) or nameof(MainWindowViewModel.RightImage) or nameof(MainWindowViewModel.HasAnyImage))
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

    private void ConstrainStageScrollViewers()
    {
        var width = Math.Max(0, StageViewport.Bounds.Width);
        var height = Math.Max(0, StageViewport.Bounds.Height);

        SideBySideScrollViewer.Width = width;
        SideBySideScrollViewer.Height = height;
        SplitScreenScrollViewer.Width = width;
        SplitScreenScrollViewer.Height = height;
    }

    private void StageScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel is null || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        e.Handled = true;

        var scrollViewer = GetActiveScrollViewer();
        var surface = GetActiveSurface();
        var stagePoint = e.GetPosition(surface);
        var pointerInViewer = e.GetPosition(scrollViewer);
        var oldZoom = _viewModel.ZoomScale;

        if (e.Delta.Y > 0)
        {
            _viewModel.SetZoomScale(_viewModel.ZoomScale * ZoomStepFactor);
        }
        else if (e.Delta.Y < 0)
        {
            _viewModel.SetZoomScale(_viewModel.ZoomScale / ZoomStepFactor);
        }

        if (!oldZoom.Equals(_viewModel.ZoomScale))
        {
            PreservePointerAnchor(surface, scrollViewer, stagePoint, pointerInViewer);
        }
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

    private void FitZoomToStage()
    {
        if (_viewModel is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            ConstrainStageScrollViewers();

            var scrollViewer = GetActiveScrollViewer();
            if (scrollViewer.Bounds.Width <= 0 || scrollViewer.Bounds.Height <= 0)
            {
                return;
            }

            var targetWidth = _viewModel.IsSideBySideView ? _viewModel.SideBySideStageWidth : _viewModel.StageWidth;
            var scaleX = scrollViewer.Bounds.Width / Math.Max(1, targetWidth);
            var scaleY = scrollViewer.Bounds.Height / Math.Max(1, _viewModel.StageHeight);
            _viewModel.SetZoomScale(Math.Min(scaleX, scaleY));
        }, DispatcherPriority.Loaded);
    }

    private void PreservePointerAnchor(Control surface, ScrollViewer scrollViewer, Point stagePoint, Point pointerInViewer)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var translated = surface.TranslatePoint(stagePoint, scrollViewer);
            if (translated is null)
            {
                return;
            }

            var deltaX = translated.Value.X - pointerInViewer.X;
            var deltaY = translated.Value.Y - pointerInViewer.Y;
            var offset = scrollViewer.Offset;
            scrollViewer.Offset = new Vector(
                Math.Max(0, offset.X + deltaX),
                Math.Max(0, offset.Y + deltaY));
        }, DispatcherPriority.Loaded);
    }

    private ScrollViewer GetActiveScrollViewer() =>
        _viewModel?.IsSplitScreenView == true ? SplitScreenScrollViewer : SideBySideScrollViewer;

    private Control GetActiveSurface() =>
        _viewModel?.IsSplitScreenView == true ? StageSurface : SideBySideSurface;

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

    private void OnClosed(object? sender, EventArgs e)
    {
        SideBySideScrollViewer.RemoveHandler(PointerWheelChangedEvent, StageScrollViewer_OnPointerWheelChanged);
        SplitScreenScrollViewer.RemoveHandler(PointerWheelChangedEvent, StageScrollViewer_OnPointerWheelChanged);
        DragDrop.RemoveDragOverHandler(StageDropSurface, StageSurface_OnDragOver);
        DragDrop.RemoveDropHandler(StageDropSurface, StageSurface_OnDrop);
        DragDrop.RemoveDragOverHandler(SideBySideDropSurface, StageSurface_OnDragOver);
        DragDrop.RemoveDropHandler(SideBySideDropSurface, StageSurface_OnDrop);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.DisposeImages();
        }
    }
}
