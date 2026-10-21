using System.Net.Http;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Docs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Model;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The StreamMark plugin behind screenshots in docs pages: a <c>&lt;shot src="..." alt="..."&gt;&lt;/shot&gt;</c> block
/// (what <see cref="GalleryDocs"/> turns <c>![caption](images/Name.png)</c> into) renders as a
/// <see cref="GalleryShotView"/>.
/// </summary>
public sealed class GalleryShotPlugin : IMarkdownPlugin
{
    public const string Tag = "shot";

    public string Name => "arc-gallery-shot";

    public void Configure(MarkdownPluginBuilder builder) => builder.AddBlockComponent(Tag, static _ => new GalleryShotBlock());
}

/// <summary>The block view StreamMark lays out in the document; it hosts the screenshot for the block's <c>src</c>.</summary>
internal sealed class GalleryShotBlock : BlockView
{
    private GalleryShotView? _view;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment,
                                      bool contextChanged)
    {
        var attributes = ((ComponentBlock)block).Attributes;
        var source = attributes["src"] ?? string.Empty;
        var caption = attributes["alt"] ?? string.Empty;
        if (_view is not null && _view.Source == source && _view.Caption == caption)
        {
            return;
        }
        if (_view is not null)
        {
            RemoveChild(_view);
        }
        _view = new GalleryShotView(source, caption);
        AddChild(_view);
    }
}

/// <summary>
/// A screenshot in a docs page: the light or the dark capture, whichever matches the theme, in a rounded frame with its
/// caption below. Captures are taken at twice the logical size (tools/docshots), so the image shows at half its pixel
/// width and shrinks with a narrower column.
/// </summary>
public sealed class GalleryShotView : StackPanel
{
    /// <summary>Physical pixels per logical pixel in a capture.</summary>
    public const double CaptureScale = 2;

    private readonly Image _image = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private Bitmap? _bitmap;
    private bool? _isDarkShown;
    private bool? _isDarkRequested;
    private int _loadVersion;

    public GalleryShotView(string source, string caption)
    {
        Source = source;
        Caption = caption;
        Spacing = 8;
        Margin = new Thickness(0, 8);
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.HighQuality);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _image
        };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        Children.Add(frame);
        if (caption.Length > 0)
        {
            Children.Add(GalleryUi.Text(caption, ArcDesignTokenKind.TextMuted, 13));
        }
        ActualThemeVariantChanged += (_, _) => Load();
    }

    /// <summary>The capture's path in the screenshots assembly, such as <c>Docs/&lt;Category&gt;/&lt;Component&gt;/images/&lt;Name&gt;</c>.</summary>
    public string Source { get; }

    public string Caption { get; }

    /// <summary>The capture shown, or null while detached or when the asset is missing.</summary>
    public Bitmap? Bitmap => _bitmap;

    /// <summary>Whether the dark capture is the one shown.</summary>
    public bool IsDarkShown => _isDarkShown == true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A long page holds many captures; decoded bitmaps are released as soon as the page goes away, and a capture that
        // is still being fetched is dropped when it arrives.
        _loadVersion++;
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _isDarkShown = null;
        _isDarkRequested = null;
    }

    private void Load()
    {
        if (VisualRoot is null)
        {
            return;
        }
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        if (_isDarkRequested == dark)
        {
            return;
        }
        _isDarkRequested = dark;
        _ = ShowAsync(dark, ++_loadVersion);
    }

    /// <summary>
    /// Shows the dark or the light capture. Runs to the end before it returns when the source has the bytes at hand;
    /// otherwise the picture appears when they arrive, unless the view has been detached or asked for the other theme.
    /// </summary>
    private async Task ShowAsync(bool dark, int version)
    {
        Stream? stream = null;
        try
        {
            stream = await GalleryShots.Source.OpenAsync(GalleryDocs.ScreenshotUri(Source, dark));
            if (stream is null && dark)
            {
                stream = await GalleryShots.Source.OpenAsync(GalleryDocs.ScreenshotUri(Source, false));
            }
        }
        catch (Exception e) when (e is IOException or HttpRequestException or InvalidOperationException)
        {
            // A capture that cannot be fetched is a missing capture; the page stays readable without it.
            stream = null;
        }
        if (version != _loadVersion)
        {
            stream?.Dispose();
            return;
        }
        var previous = _bitmap;
        if (stream is not null)
        {
            using (stream)
            {
                _bitmap = new Bitmap(stream);
            }
            _image.MaxWidth = _bitmap.PixelSize.Width / CaptureScale;
            _image.MaxHeight = _bitmap.PixelSize.Height / CaptureScale;
        }
        else
        {
            _bitmap = null;
        }
        _image.Source = _bitmap;
        previous?.Dispose();
        _isDarkShown = dark;
    }
}
