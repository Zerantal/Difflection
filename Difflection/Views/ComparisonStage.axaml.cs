using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Difflection.ViewModels;
using JetBrains.Annotations;

namespace Difflection.Views;

public partial class ComparisonStage : UserControl
{
    private const double ZoomStepFactor = 1.15;

    private MainWindowViewModel? _viewModel;

    public ComparisonStage()
    {
        InitializeComponent();

        SplitPane.SplitRatioChanged += SplitPane_OnSplitRatioChanged;
        DataContextChanged += OnDataContextChanged;
        AddHandler(PointerWheelChangedEvent, StageOverlay_OnPointerWheelChanged, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    public void FitZoomToStage()
    {
        FitZoomToStageInternal();
    }

    internal async Task LoadDroppedFilesAsync(IEnumerable<IStorageItem> items)
    {
        if (_viewModel is null)
        {
            return;
        }

        var files = items.OfType<IStorageFile>().ToArray();
        if (files.Length == 0)
        {
            return;
        }

        await _viewModel.ImageSet.AddFilesToCurrentComparisonAsync(files);
        FitZoomToStage();
    }

    [UsedImplicitly]
    private void StageOverlay_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = GetDroppedFiles(e.DataTransfer).Any();
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    [UsedImplicitly]
    private void StageOverlay_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        _ = StageOverlay_OnDropAsync(e);
    }

    private async Task StageOverlay_OnDropAsync(DragEventArgs e)
    {
        var files = GetDroppedFiles(e.DataTransfer).Take(2).ToArray();
        if (!files.Any())
        {
            return;
        }

        await LoadDroppedFilesAsync(files);
    }

    private void StageOverlay_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel is null || !_viewModel.ComparisonDisplay.HasAnyImage)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;

            var zoomTarget = GetWheelTargetPane(e);
            if (zoomTarget is null)
            {
                return;
            }

            var scrollViewer = zoomTarget.Value.ScrollViewer;
            var surface = zoomTarget.Value.Surface;
            var stagePoint = e.GetPosition(surface);
            var pointerInViewer = e.GetPosition(scrollViewer);
            var oldZoom = _viewModel.ToolState.ZoomScale;
            var oldOffset = scrollViewer.Offset;
            var relativePoint = GetRelativePoint(stagePoint, surface);
            var otherTarget = _viewModel.ToolState.IsSideBySideView && _viewModel.ComparisonDisplay.HasBothImages
                ? GetOtherSideBySideTarget(scrollViewer)
                : null;
            var otherOldOffset = otherTarget is null ? default : otherTarget.Value.ScrollViewer.Offset;
            var otherRelativePoint = otherTarget is null
                ? default
                : new Point(relativePoint.X * otherTarget.Value.Surface.Bounds.Width, relativePoint.Y * otherTarget.Value.Surface.Bounds.Height);
            var otherPointerInViewer = otherTarget is null
                ? default
                : otherTarget.Value.Surface.TranslatePoint(otherRelativePoint, otherTarget.Value.ScrollViewer) ?? otherRelativePoint;

            if (e.Delta.Y > 0)
            {
                _viewModel.ToolState.SetZoomScale(_viewModel.ToolState.ZoomScale * ZoomStepFactor);
            }
            else if (e.Delta.Y < 0)
            {
                _viewModel.ToolState.SetZoomScale(_viewModel.ToolState.ZoomScale / ZoomStepFactor);
            }

            if (!oldZoom.Equals(_viewModel.ToolState.ZoomScale))
            {
                ApplyZoomAnchor(scrollViewer, pointerInViewer, oldOffset, oldZoom, _viewModel.ToolState.ZoomScale);
                if (otherTarget is not null)
                {
                    ApplyZoomAnchor(otherTarget.Value.ScrollViewer, otherPointerInViewer, otherOldOffset, oldZoom, _viewModel.ToolState.ZoomScale);
                }
            }

            return;
        }

        var scrollTarget = GetWheelTargetPane(e);
        if (scrollTarget is null)
        {
            return;
        }

        e.Handled = true;
        var step = 48.0;
        var deltaX = e.Delta.X * step;
        var deltaY = e.Delta.Y * step;
        var target = scrollTarget.Value;
        var offset = target.ScrollViewer.Offset;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            deltaX += deltaY;
            deltaY = 0;
        }

        var newOffset = new Vector(
            Math.Max(0, offset.X - deltaX),
            Math.Max(0, offset.Y - deltaY));

        target.ScrollViewer.Offset = newOffset;
        if (_viewModel.ToolState.IsSideBySideView && _viewModel.ComparisonDisplay.HasBothImages)
        {
            var otherTarget = GetOtherSideBySideTarget(target.ScrollViewer);
            if (otherTarget is not null)
            {
                otherTarget.Value.ScrollViewer.Offset = newOffset;
            }
        }
    }

    private static IEnumerable<IStorageFile> GetDroppedFiles(IDataTransfer dataTransfer)
    {
        return dataTransfer.Items
            .Select(item => item.TryGetRaw(DataFormat.File))
            .OfType<IStorageFile>();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeViewModelEvents();

        _viewModel = DataContext as MainWindowViewModel;

        SubscribeViewModelEvents();

        UpdateSideBySideLayout();
        FitZoomToStage();
        SyncSplitPercentageText();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ComparisonDisplayViewModel.StageWidth)
            or nameof(ComparisonDisplayViewModel.StageHeight)
            or nameof(ComparisonDisplayViewModel.SideBySideStageWidth)
            or nameof(ComparisonDisplayViewModel.SideBySideStageHeight)
            or nameof(ComparisonDisplayViewModel.HasLeftImage)
            or nameof(ComparisonDisplayViewModel.HasRightImage)
            or nameof(ComparisonDisplayViewModel.HasBothImages)
            or nameof(ComparisonToolStateViewModel.SelectedViewMode))
        {
            UpdateSideBySideLayout();
            FitZoomToStage();
        }
    }

    private void UpdateSideBySideLayout()
    {
        Grid.SetColumnSpan(SideBySideLeftPane, _viewModel?.ComparisonDisplay.HasBothImages == true ? 1 : 3);
    }

    private static Point GetRelativePoint(Point point, Control surface)
    {
        var width = Math.Max(1, surface.Bounds.Width);
        var height = Math.Max(1, surface.Bounds.Height);
        return new Point(point.X / width, point.Y / height);
    }

    private void FitZoomToStageInternal()
    {
        if (_viewModel is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_viewModel.ComparisonDisplay.HasAnyImage)
            {
                return;
            }

            var scrollViewer = GetActiveFitScrollViewer();
            if (scrollViewer.Bounds.Width <= 0 || scrollViewer.Bounds.Height <= 0)
            {
                return;
            }

            var targetWidth = _viewModel.ToolState.IsDifferenceView
                ? _viewModel.ComparisonDisplay.DifferenceImageWidth
                : Math.Max(_viewModel.ComparisonDisplay.LeftImageWidth, _viewModel.ComparisonDisplay.RightImageWidth);
            var targetHeight = _viewModel.ToolState.IsDifferenceView
                ? _viewModel.ComparisonDisplay.DifferenceImageHeight
                : Math.Max(_viewModel.ComparisonDisplay.LeftImageHeight, _viewModel.ComparisonDisplay.RightImageHeight);

            var scaleX = scrollViewer.Bounds.Width / Math.Max(1, targetWidth);
            var scaleY = scrollViewer.Bounds.Height / Math.Max(1, targetHeight);
            _viewModel.ToolState.SetZoomScale(Math.Min(scaleX, scaleY));
        }, DispatcherPriority.Loaded);
    }

    private void SplitPane_OnSplitRatioChanged(object? sender, double ratio)
    {
        SyncSplitPercentageText(ratio);
    }

    private void SyncSplitPercentageText(double? splitRatio = null)
    {
        if (_viewModel is null)
        {
            return;
        }

        var ratio = splitRatio ?? 0.5;
        var leftPercent = Math.Round(Math.Clamp(ratio, 0.0, 1.0) * 100.0);
        var rightPercent = 100.0 - leftPercent;
        _viewModel.ToolState.SplitPercentageText = $"{leftPercent:0} / {rightPercent:0}";
    }

    private static void ApplyZoomAnchor(ScrollViewer scrollViewer, Point anchorInViewer, Vector oldOffset, double oldZoom, double newZoom)
    {
        var ratio = newZoom / Math.Max(0.0001, oldZoom);
        scrollViewer.Offset = new Vector(
            Math.Max(0, (oldOffset.X + anchorInViewer.X) * ratio - anchorInViewer.X),
            Math.Max(0, (oldOffset.Y + anchorInViewer.Y) * ratio - anchorInViewer.Y));
    }

    private (ScrollViewer ScrollViewer, Control Surface)? GetWheelTargetPane(PointerWheelEventArgs e)
    {
        if (_viewModel?.ToolState.IsSplitScreenView == true)
        {
            return (SplitPane.ActiveScrollViewer, SplitPane.ActiveSurface);
        }

        if (_viewModel?.ToolState.IsDifferenceView == true)
        {
            return (DifferencePane.ActiveScrollViewer, DifferencePane.ActiveSurface);
        }

        if (!HasSecondPane)
        {
            return (SideBySideLeftPane.ActiveScrollViewer, SideBySideLeftPane.ActiveSurface);
        }

        var rightPoint = e.GetPosition(SideBySideRightPane);
        if (SideBySideRightPane.IsVisible && SideBySideRightPane.Bounds.Contains(rightPoint))
        {
            return (SideBySideRightPane.ActiveScrollViewer, SideBySideRightPane.ActiveSurface);
        }

        return (SideBySideLeftPane.ActiveScrollViewer, SideBySideLeftPane.ActiveSurface);
    }

    private (ScrollViewer ScrollViewer, Control Surface)? GetOtherSideBySideTarget(ScrollViewer currentScrollViewer)
    {
        if (ReferenceEquals(currentScrollViewer, SideBySideLeftPane.ActiveScrollViewer))
        {
            return (SideBySideRightPane.ActiveScrollViewer, SideBySideRightPane.ActiveSurface);
        }

        if (ReferenceEquals(currentScrollViewer, SideBySideRightPane.ActiveScrollViewer))
        {
            return (SideBySideLeftPane.ActiveScrollViewer, SideBySideLeftPane.ActiveSurface);
        }

        return null;
    }

    private bool HasSecondPane => _viewModel?.ComparisonDisplay.HasBothImages == true;

    private ScrollViewer GetActiveFitScrollViewer()
    {
        if (_viewModel?.ToolState.IsSplitScreenView == true)
        {
            return SplitPane.ActiveScrollViewer;
        }

        if (_viewModel?.ToolState.IsDifferenceView == true)
        {
            return DifferencePane.ActiveScrollViewer;
        }

        return SideBySideLeftPane.ActiveScrollViewer;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        RemoveHandler(PointerWheelChangedEvent, StageOverlay_OnPointerWheelChanged);
        SplitPane.SplitRatioChanged -= SplitPane_OnSplitRatioChanged;

        UnsubscribeViewModelEvents();

        _viewModel?.ComparisonDisplay.DisposeImages();
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
