using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using PropertyGenerator.Avalonia;

namespace Difflection.Views;

public partial class RuledSplitImagePane : UserControl
{
    private sealed class ActionObserver<T>(Action onNext) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value)
        {
            onNext();
        }
    }

    private const double SplitDragSurfaceWidth = 24;
    private const double SplitRatioMin = 0.0;
    private const double SplitRatioMax = 1.0;

    public event EventHandler<double>? SplitRatioChanged;

    private readonly IDisposable _zoomSubscription;
    private readonly IDisposable _leftImageSubscription;
    private readonly IDisposable _rightImageSubscription;
    private bool _isDraggingSplit;
    private double _splitRatio = 0.5;
    private bool _splitVisualsDirty = true;

    public RuledSplitImagePane()
    {
        InitializeComponent();

        _zoomSubscription = this.GetObservable(ZoomScaleProperty).Subscribe(new ActionObserver<double>(RequestUpdateSplitVisuals));
        _leftImageSubscription = this.GetObservable(LeftImageProperty).Subscribe(new ActionObserver<IImage?>(UpdateSplitDividerBrush));
        _rightImageSubscription = this.GetObservable(RightImageProperty).Subscribe(new ActionObserver<IImage?>(UpdateSplitDividerBrush));
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        SizeChanged += Pane_OnSizeChanged;
        LayoutUpdated += Pane_OnLayoutUpdated;
        DetachedFromVisualTree += Pane_OnDetachedFromVisualTree;
    }

    [GeneratedStyledProperty]
    public partial IImage? LeftImage { get; set; }

    [GeneratedStyledProperty]
    public partial IImage? RightImage { get; set; }

    [GeneratedStyledProperty(1.0)]
    public partial double ZoomScale { get; set; }

    [GeneratedStyledProperty]
    public partial double SurfaceWidth { get; set; }

    [GeneratedStyledProperty]
    public partial double SurfaceHeight { get; set; }

    public ScrollViewer ActiveScrollViewer => ScrollViewer;

    public Control ActiveSurface => Surface;

    public void FitToRatio(double ratio)
    {
        _splitRatio = Math.Clamp(ratio, SplitRatioMin, SplitRatioMax);
        UpdateSplitVisuals();
        SplitRatioChanged?.Invoke(this, _splitRatio);
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

    [UsedImplicitly]
    private void Pane_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = GetDroppedFiles(e.DataTransfer).Any();
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    [UsedImplicitly]
    private void Pane_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        _ = Pane_OnDropAsync(e);
    }

    private async Task Pane_OnDropAsync(DragEventArgs e)
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

        await stage.LoadDroppedFilesAsync(files);
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
        _leftImageSubscription.Dispose();
        _rightImageSubscription.Dispose();
    }

    private void UpdateSplitRatio(double pointerX)
    {
        if (Surface.Bounds.Width <= 0)
        {
            return;
        }

        _splitRatio = Math.Clamp(pointerX / Surface.Bounds.Width, SplitRatioMin, SplitRatioMax);
        UpdateSplitVisuals();
        SplitRatioChanged?.Invoke(this, _splitRatio);
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

    private void UpdateSplitDividerBrush()
    {
        var luminance = GetAverageLuminance(LeftImage, RightImage);
        SplitDivider.Background = luminance >= 0.5 ? Brushes.Black : Brushes.White;
    }

    private static double GetAverageLuminance(params IImage?[] images)
    {
        var totalLuminance = 0.0;
        var imageCount = 0;

        foreach (var image in images.OfType<Bitmap>())
        {
            totalLuminance += GetAverageLuminance(image);
            imageCount++;
        }

        return imageCount == 0 ? 0.0 : totalLuminance / imageCount;
    }

    private static double GetAverageLuminance(Bitmap bitmap)
    {
        if (bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
        {
            return 0.0;
        }

        const int bytesPerPixel = 4;
        var stride = bitmap.PixelSize.Width * bytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelSize.Height];
        using var framebuffer = new ManagedFramebuffer(pixels, bitmap.PixelSize, stride);
        bitmap.CopyPixels(framebuffer);

        var stepX = Math.Max(1, bitmap.PixelSize.Width / 64);
        var stepY = Math.Max(1, bitmap.PixelSize.Height / 64);
        double total = 0;
        var count = 0;

        for (var y = 0; y < bitmap.PixelSize.Height; y += stepY)
        {
            var row = y * stride;
            for (var x = 0; x < bitmap.PixelSize.Width; x += stepX)
            {
                var index = row + x * bytesPerPixel;
                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                total += ((0.2126 * red) + (0.7152 * green) + (0.0722 * blue)) / 255.0;
                count++;
            }
        }

        return count == 0 ? 0.0 : total / count;
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

    private sealed class ManagedFramebuffer : ILockedFramebuffer
    {
        private GCHandle _handle;

        public ManagedFramebuffer(byte[] pixels, PixelSize size, int rowBytes)
        {
            _handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Address = _handle.AddrOfPinnedObject();
            Size = size;
            RowBytes = rowBytes;
        }

        public IntPtr Address { get; }

        public PixelSize Size { get; }

        public int RowBytes { get; }

        public Vector Dpi { get; } = new(96, 96);

        public PixelFormat Format => PixelFormats.Bgra8888;

        public AlphaFormat AlphaFormat => AlphaFormat.Premul;

        public void Dispose()
        {
            if (!_handle.IsAllocated) return;
            _handle.Free();
            _handle = default;
        }
    }
}
