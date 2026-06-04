using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PropertyGenerator.Avalonia;

namespace Difflection.Views;

public partial class RuledImagePane : UserControl
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

    private readonly IDisposable _zoomSubscription;
    private bool _rulersDirty = true;

    public RuledImagePane()
    {
        InitializeComponent();

        _zoomSubscription = this.GetObservable(ZoomScaleProperty).Subscribe(new ActionObserver(RequestUpdateRulers));
        SizeChanged += Pane_OnSizeChanged;
        LayoutUpdated += Pane_OnLayoutUpdated;
        DetachedFromVisualTree += Pane_OnDetachedFromVisualTree;
    }

    [GeneratedStyledProperty]
    public partial IImage? ImageSource { get; set; }

    [GeneratedStyledProperty]
    public partial int ImageRevision { get; set; }

    [GeneratedStyledProperty(1.0)]
    public partial double ZoomScale { get; set; }

    [GeneratedStyledProperty]
    public partial double SurfaceWidth { get; set; }

    [GeneratedStyledProperty]
    public partial double SurfaceHeight { get; set; }

    public ScrollViewer ActiveScrollViewer => ScrollViewer;

    public Control ActiveSurface => Surface;

    public bool HasImage => ImageSource is not null;

    private void Pane_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RequestUpdateRulers();
    }

    private void Surface_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RequestUpdateRulers();
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        RequestUpdateRulers();
    }

    private void RequestUpdateRulers()
    {
        _rulersDirty = true;
    }

    private void Pane_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_rulersDirty)
        {
            return;
        }

        _rulersDirty = false;
        UpdateRulers();
    }

    private void UpdateRulers()
    {
        if (TopRuler is null || LeftRuler is null || Surface is null || Transform is null)
        {
            return;
        }

        TopRuler.ZoomScale = ZoomScale;
        LeftRuler.ZoomScale = ZoomScale;
        TopRuler.ContentOriginX = GetContentOrigin(Transform, TopRuler).X;
        LeftRuler.ContentOriginY = GetContentOrigin(Transform, LeftRuler).Y;
        TopRuler.PrimarySegmentLength = Math.Max(0, SurfaceWidth);
        LeftRuler.PrimarySegmentLength = Math.Max(0, SurfaceHeight);
    }

    private static Point GetContentOrigin(Control surface, Control ruler)
    {
        var translated = surface.TranslatePoint(new Point(0, 0), ruler);
        return translated ?? new Point(0, 0);
    }

    private void Pane_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _zoomSubscription.Dispose();
    }
}
