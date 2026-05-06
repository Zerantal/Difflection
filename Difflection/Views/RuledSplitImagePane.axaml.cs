using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Difflection.Views;

public partial class RuledSplitImagePane : UserControl
{
    private sealed class ActionObserver(Action onNext) : IObserver<double>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(double value)
        {
            onNext();
        }
    }

    private const double SplitDragSurfaceWidth = 24;
    private const double SplitRatioMin = 0.0;
    private const double SplitRatioMax = 1.0;

    public static readonly StyledProperty<IImage?> LeftImageProperty =
        AvaloniaProperty.Register<RuledSplitImagePane, IImage?>(nameof(LeftImage));

    public static readonly StyledProperty<IImage?> RightImageProperty =
        AvaloniaProperty.Register<RuledSplitImagePane, IImage?>(nameof(RightImage));

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<RuledSplitImagePane, double>(nameof(ZoomScale), 1.0);

    public static readonly StyledProperty<double> SurfaceWidthProperty =
        AvaloniaProperty.Register<RuledSplitImagePane, double>(nameof(SurfaceWidth));

    public static readonly StyledProperty<double> SurfaceHeightProperty =
        AvaloniaProperty.Register<RuledSplitImagePane, double>(nameof(SurfaceHeight));

    private readonly IDisposable _zoomSubscription;
    private bool _isDraggingSplit;
    private double _splitRatio = 0.5;
    private bool _splitVisualsDirty = true;

    public RuledSplitImagePane()
    {
        InitializeComponent();

        _zoomSubscription = this.GetObservable(ZoomScaleProperty).Subscribe(new ActionObserver(RequestUpdateSplitVisuals));
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        SizeChanged += Pane_OnSizeChanged;
        LayoutUpdated += Pane_OnLayoutUpdated;
        DetachedFromVisualTree += Pane_OnDetachedFromVisualTree;
    }

    public IImage? LeftImage
    {
        get => GetValue(LeftImageProperty);
        set => SetValue(LeftImageProperty, value);
    }

    public IImage? RightImage
    {
        get => GetValue(RightImageProperty);
        set => SetValue(RightImageProperty, value);
    }

    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public double SurfaceWidth
    {
        get => GetValue(SurfaceWidthProperty);
        set => SetValue(SurfaceWidthProperty, value);
    }

    public double SurfaceHeight
    {
        get => GetValue(SurfaceHeightProperty);
        set => SetValue(SurfaceHeightProperty, value);
    }

    public ScrollViewer ActiveScrollViewer => ScrollViewer;

    public Control ActiveSurface => Surface;

    public void FitToRatio(double ratio)
    {
        _splitRatio = Math.Clamp(ratio, SplitRatioMin, SplitRatioMax);
        UpdateSplitVisuals();
    }

    public void RefreshLayout()
    {
        UpdateSplitVisuals();
    }

    private void Pane_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RequestUpdateSplitVisuals();
    }

    private void Surface_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RequestUpdateSplitVisuals();
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        RequestUpdateSplitVisuals();
    }

    private void Pane_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = GetDroppedFiles(e.DataTransfer).Any();
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Pane_OnDrop(object? sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e.DataTransfer).Take(2).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        var stage = this.FindAncestorOfType<ComparisonStage>();
        if (stage is null)
        {
            return;
        }

        await stage.LoadDroppedFilesAsync(null, files);
        e.Handled = true;
    }

    private void SplitDivider_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDraggingSplit = true;
        e.Pointer.Capture(SplitDragSurface);
        UpdateSplitRatio(e.GetPosition(Surface).X);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingSplit)
        {
            return;
        }

        UpdateSplitRatio(e.GetPosition(Surface).X);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingSplit)
        {
            return;
        }

        _isDraggingSplit = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void Pane_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _zoomSubscription.Dispose();
    }

    private void UpdateSplitRatio(double pointerX)
    {
        if (Surface.Bounds.Width <= 0)
        {
            return;
        }

        _splitRatio = Math.Clamp(pointerX / Surface.Bounds.Width, SplitRatioMin, SplitRatioMax);
        UpdateSplitVisuals();
    }

    private void UpdateSplitVisuals()
    {
        if (Surface.Bounds.Width <= 0)
        {
            return;
        }

        var splitX = Surface.Bounds.Width * _splitRatio;
        var stageWidth = Surface.Bounds.Width;
        var stageHeight = Surface.Bounds.Height;
        var rightWidth = Math.Max(0, stageWidth - splitX);

        LeftImageLayer.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, splitX), stageHeight));
        RightImageLayer.Clip = new RectangleGeometry(new Rect(splitX, 0, rightWidth, stageHeight));

        SplitDivider.Margin = new Thickness(Math.Max(0, splitX - 1), 0, 0, 0);
        SplitDragSurface.Margin = new Thickness(Math.Max(0, splitX - (SplitDragSurfaceWidth / 2)), 0, 0, 0);

        UpdateRulers();
    }

    private void RequestUpdateSplitVisuals()
    {
        _splitVisualsDirty = true;
    }

    private void Pane_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_splitVisualsDirty)
        {
            return;
        }

        _splitVisualsDirty = false;
        UpdateSplitVisuals();
    }

    private void UpdateRulers()
    {
        if (TopRuler is null || LeftRuler is null || Surface is null || Transform is null)
        {
            return;
        }

        var zoom = Math.Max(ZoomScale, 0.0001);
        var leftVisibleLength = Math.Max(0, LeftRuler.Bounds.Height) / zoom;
        var topVisibleLength = Math.Max(0, TopRuler.Bounds.Width) / zoom;

        TopRuler.ZoomScale = ZoomScale;
        TopRuler.ContentOriginX = GetContentOrigin(Transform, TopRuler).X;
        TopRuler.PrimarySegmentLength = Math.Max(SurfaceWidth, topVisibleLength);

        LeftRuler.ZoomScale = ZoomScale;
        LeftRuler.ContentOriginY = GetContentOrigin(Transform, LeftRuler).Y;
        LeftRuler.PrimarySegmentLength = Math.Max(SurfaceHeight, leftVisibleLength);
    }

    private static Point GetContentOrigin(Control surface, Control ruler)
    {
        var translated = surface.TranslatePoint(new Point(0, 0), ruler);
        return translated ?? new Point(0, 0);
    }

    private static IEnumerable<IStorageFile> GetDroppedFiles(IDataTransfer dataTransfer)
    {
        return dataTransfer.Items
            .Select(item => item.TryGetRaw(DataFormat.File))
            .OfType<IStorageFile>();
    }
}
