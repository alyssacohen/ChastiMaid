using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.Views.Controls;

public partial class WebcamPreviewControl : UserControl
{
    public static readonly StyledProperty<WebcamPreviewService?> ServiceProperty =
        AvaloniaProperty.Register<WebcamPreviewControl, WebcamPreviewService?>(nameof(Service));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<WebcamPreviewControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    private IDisposable? _bitmapSub;
    private IDisposable? _frameSub;

    public WebcamPreviewControl()
    {
        InitializeComponent();
    }

    public WebcamPreviewService? Service
    {
        get => GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ServiceProperty)
        {
            return;
        }

        _bitmapSub?.Dispose();
        _frameSub?.Dispose();

        var svc = change.GetNewValue<WebcamPreviewService?>();
        if (svc is null)
        {
            PreviewImage.Source = null;
            NoSignalText.IsVisible = true;
            return;
        }

        _bitmapSub = svc.WhenAnyValue(x => x.PreviewBitmap).Subscribe(bmp =>
        {
            PreviewImage.Source = bmp;
            NoSignalText.IsVisible = bmp is null;
        });

        _frameSub = svc.WhenAnyValue(x => x.FrameCount).Subscribe(_ =>
            PreviewImage.InvalidateVisual());
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bitmapSub?.Dispose();
        _frameSub?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
