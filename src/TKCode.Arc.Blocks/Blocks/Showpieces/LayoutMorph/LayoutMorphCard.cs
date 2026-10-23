using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// Colors and type of the cards, resolved from the ArcDesign tokens by the stage (again on every theme change) and
/// shared by all cards so a frame allocates nothing.
/// </summary>
internal sealed class LayoutMorphPalette
{
    public Color CardFill;
    public Color CardBorder;
    public Color MediaFill;
    public Color Foreground;
    public Color TextSecondary;
    public Color TextMuted;
    public Color Rule;
    public Color FocusRing;
    public bool IsDark;
    public Typeface Body;
    public Typeface BodyMedium;
    public Typeface DisplayMedium;
    public FontFamily BodyFamily = FontFamily.Default;
    public FontFamily DisplayFamily = FontFamily.Default;
    public IBrush? CardFillBrush;
    public IPen? CardBorderPen;
    public IBrush? MediaFillBrush;
    public IPen? FocusPen;
    public IPen? RulePen;
}

/// <summary>
/// One card of a <see cref="LayoutMorph"/> stage: a self-drawn, focusable control whose projective
/// <see cref="Visual.RenderTransform"/> (one homography per frame, from the stage) places it in 3D. It draws its own
/// interpolated size, corners, picture or cover, caption layers, the open detail's text, the hover shadow and the
/// focus ring, so a flight never runs layout. It hit-tests its current rounded rectangle only.
/// </summary>
/// <remarks>
/// The drawing sits in a child (<see cref="LayoutMorphCardFace"/>) so the card can carry a bitmap cache while it moves (the
/// stage decides): the matrix, opacity and draw order stay on the card, outside the bitmap, and a change of the drawing
/// re-renders the bitmap (a compositor cache does not see changes of the cached visual's own drawing).
/// </remarks>
internal sealed class LayoutMorphCard : Control, ICustomHitTest
{
    private readonly LayoutMorphStage _stage;
    private readonly MatrixTransform _transform = new();
    private readonly LayoutMorphCardFace _face;
    private LayoutMorphItem _item;

    // Draw state (written by the stage; Repaint when it changes).
    internal double DrawWidth;
    internal double DrawHeight;
    internal double DrawRadius;
    internal Rect Media;
    internal double MediaRadius;
    internal double Lift;
    internal bool HidesContent;
    internal readonly double[] Captions = new double[4];
    internal LayoutMorphSheetLayout Sheet;
    internal bool IsSheetNarrow;

    // Projection (stage px) for tags, stagger origins and hit tests.
    internal Matrix ScreenMatrix = Matrix.Identity;
    internal double Depth;
    internal int Index;

    private LayoutMorphSheetCache? _sheet;

    public LayoutMorphCard(LayoutMorphStage stage, LayoutMorphItem item)
    {
        _stage = stage;
        _item = item;
        Focusable = true;
        FocusAdorner = null;
        ClipToBounds = false;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = _transform;
        Cursor = new Cursor(StandardCursorType.Hand);
        _face = new LayoutMorphCardFace(this);
        VisualChildren.Add(_face);
        UpdateAutomation();
    }

    public LayoutMorphItem Item
    {
        get => _item;
        set
        {
            if (ReferenceEquals(_item, value))
            {
                return;
            }
            _item = value;
            _sheet = null;
            UpdateAutomation();
            Repaint();
        }
    }

    /// <summary>The child that draws the card.</summary>
    internal LayoutMorphCardFace Face => _face;

    /// <summary>Redraws the card (its face; the card itself draws nothing).</summary>
    internal void Repaint() => _face.InvalidateVisual();

    /// <summary>The open detail (and a detail closing while its text still shows) draws the original picture.</summary>
    internal bool UsesOriginalPicture => (Index >= 0 && Index == _stage.OpenIndex) || Captions[(int)LayoutMorphCaption.Sheet] > 0.01;

    internal void UpdateAutomation()
    {
        AutomationProperties.SetName(this, _item.AccessibleName);
        AutomationProperties.SetHelpText(this, _item.AccessibleDetails);
    }

    /// <summary>Places the card; true when the matrix changed.</summary>
    internal bool SetMatrix(in Matrix matrix)
    {
        if (_transform.Matrix == matrix)
        {
            return false;
        }
        _transform.Matrix = matrix;
        return true;
    }

    internal void DropTextCache() => _sheet = null;

    /// <summary>The card's center in stage px (projected).</summary>
    internal Point ScreenCenter => ShowpieceProjection.Apply(ScreenMatrix, new Point(DrawWidth / 2, DrawHeight / 2));

    /// <summary>The middle of the card's top edge in stage px (projected; the floating tag's anchor).</summary>
    internal Point ScreenTop => ShowpieceProjection.Apply(ScreenMatrix, new Point(DrawWidth / 2, 0));

    public bool HitTest(Point point)
    {
        if (!IsHitTestVisible || DrawWidth <= 0 || DrawHeight <= 0)
        {
            return false;
        }
        var rect = new Rect(0, 0, DrawWidth, DrawHeight);
        if (!rect.Contains(point))
        {
            return false;
        }
        // Outside the corner arcs does not count.
        var r = Math.Min(DrawRadius, Math.Min(DrawWidth, DrawHeight) / 2);
        var cx = Math.Clamp(point.X, r, DrawWidth - r);
        var cy = Math.Clamp(point.Y, r, DrawHeight - r);
        var dx = point.X - cx;
        var dy = point.Y - cy;
        return dx * dx + dy * dy <= r * r + 0.01;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _stage.OnCardFocusChanged(this, true, e.NavigationMethod);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _stage.OnCardFocusChanged(this, false);
    }

    internal bool IsKeyboardFocused => IsFocused && Classes.Contains(":focus-visible");

    protected override AutomationPeer OnCreateAutomationPeer() => new LayoutMorphCardAutomationPeer(this);

    internal void Invoke() => _stage.InvokeCard(this);

    // Rendering ----------------------------------------------------------------------------------------------------------

    /// <summary>Draws the card into its face.</summary>
    internal void DrawFace(DrawingContext context)
    {
        var palette = _stage.Palette;
        var w = DrawWidth;
        var h = DrawHeight;
        if (w <= 1 || h <= 1 || palette.CardFillBrush is null)
        {
            return;
        }
        var radius = Math.Min(DrawRadius, Math.Min(w, h) / 2);
        var outer = new RoundedRect(new Rect(0, 0, w, h), radius);
        if (Lift > 0.004)
        {
            // Arc's --lift shadow: 0 6l 16l rgba(0, 0, 0, .08l).
            var shadow = new BoxShadow
            {
                OffsetY = 6 * Lift,
                Blur = 16 * Lift,
                Color = Color.FromArgb((byte)Math.Round(255 * 0.08 * Math.Min(1, Lift) * (palette.IsDark ? 3 : 1)), 0, 0, 0)
            };
            context.DrawRectangle(palette.CardFillBrush, null, outer, new BoxShadows(shadow));
        }
        else
        {
            context.DrawRectangle(palette.CardFillBrush, null, outer);
        }
        if (!HidesContent)
        {
            var inner = new RoundedRect(new Rect(1, 1, w - 2, h - 2), Math.Max(0, radius - 1));
            using (context.PushClip(inner))
            {
                DrawMedia(context, palette);
                DrawCaptions(context, palette, w, h);
            }
        }
        context.DrawRectangle(null, palette.CardBorderPen, new RoundedRect(new Rect(0.5, 0.5, w - 1, h - 1), Math.Max(0, radius - 0.5)));
        if (IsKeyboardFocused && palette.FocusPen is not null && Captions[(int)LayoutMorphCaption.Sheet] < 0.5)
        {
            // 2px ring, 2px outside the card: its center line sits 3px out.
            context.DrawRectangle(null, palette.FocusPen, new RoundedRect(new Rect(-3, -3, w + 6, h + 6), radius + 3));
        }
    }

    private void DrawMedia(DrawingContext context, LayoutMorphPalette palette)
    {
        var media = Media;
        if (media.Width <= 0.5 || media.Height <= 0.5)
        {
            return;
        }
        var clip = new RoundedRect(media, Math.Min(MediaRadius, Math.Min(media.Width, media.Height) / 2));
        if (_item.Image is { } image && image.Size.Width > 0 && image.Size.Height > 0)
        {
            // A copy at the size it is drawn (LayoutMorphPictures), except in the open detail.
            var picture = UsesOriginalPicture ? image : _stage.Pictures.Picture(image);
            using (context.PushClip(clip))
            {
                context.DrawImage(picture, CoverSource(SourceSize(picture), media.Size), media);
            }
            return;
        }
        context.DrawRectangle(palette.MediaFillBrush, null, clip);
        DrawCover(context, palette, media, clip);
    }

    /// <summary>The size source rectangles are measured in: pixels for a bitmap (its <c>Size</c> is in DIPs), else the image's size.</summary>
    internal static Size SourceSize(IImage image) =>
        image is Bitmap bitmap ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height) : image.Size;

    /// <summary>The source rectangle that fills <paramref name="target"/> and crops the overflow (centered).</summary>
    internal static Rect CoverSource(Size image, Size target)
    {
        var scale = Math.Max(target.Width / image.Width, target.Height / image.Height);
        var w = target.Width / scale;
        var h = target.Height / scale;
        return new Rect((image.Width - w) / 2, (image.Height - h) / 2, w, h);
    }

    /// <summary>A cover: the title at the top left (two lines at most) and the value at the bottom left, sized by the short side.</summary>
    private void DrawCover(DrawingContext context, LayoutMorphPalette palette, Rect media, RoundedRect clip)
    {
        var side = Math.Min(media.Width, media.Height);
        var pad = LayoutMorphEngine.CoverPadding(side);
        var cache = _stage.TextCache;
        var width = Math.Max(1, media.Width - 2 * pad);
        using (context.PushClip(clip))
        {
            if (!string.IsNullOrEmpty(_item.Title))
            {
                var title = cache.GetWrapped(_item.Title, palette.BodyMedium, LayoutMorphEngine.CoverTitleSize(side), width, 2);
                title.Draw(context, cache.Brush(palette.Foreground), new Point(media.X + pad, media.Y + pad));
            }
            if (!string.IsNullOrEmpty(_item.Value))
            {
                var value = cache.GetTrimmed(_item.Value!, palette.Body, LayoutMorphEngine.CoverValueSize(side), width);
                value.Draw(context, cache.Brush(palette.TextSecondary), new Point(media.X + pad, media.Bottom - pad - value.Height));
            }
        }
    }

    private void DrawCaptions(DrawingContext context, LayoutMorphPalette palette, double w, double h)
    {
        var compact = Captions[(int)LayoutMorphCaption.Compact];
        if (compact > 0.01)
        {
            using (context.PushOpacity(compact))
            {
                DrawCompact(context, palette, w, h);
            }
        }
        var row = Captions[(int)LayoutMorphCaption.Row];
        if (row > 0.01)
        {
            using (context.PushOpacity(row))
            {
                DrawRow(context, palette, w, h);
            }
        }
        var detail = Captions[(int)LayoutMorphCaption.Detail];
        if (detail > 0.01)
        {
            using (context.PushOpacity(detail))
            {
                DrawDetail(context, palette, w, h);
            }
        }
        var sheet = Captions[(int)LayoutMorphCaption.Sheet];
        if (sheet > 0.01)
        {
            using (context.PushOpacity(sheet))
            {
                DrawSheet(context, palette, w, h);
            }
        }
    }

    private static void DrawLine(DrawingContext context, ShowpieceText text, IBrush brush, double x, double lineTop, double lineHeight)
        => text.Draw(context, brush, new Point(x, lineTop + (lineHeight - text.Height) / 2));

    /// <summary>Grid: title and second line (12/16), 10px from the sides, 8px above the bottom.</summary>
    private void DrawCompact(DrawingContext context, LayoutMorphPalette palette, double w, double h)
    {
        var cache = _stage.TextCache;
        var left = 1 + 10;
        var width = Math.Max(1, w - 2 - 20);
        var top = h - 1 - 8 - 32;
        if (!string.IsNullOrEmpty(_item.Title))
        {
            DrawLine(context, cache.GetTrimmed(_item.Title, palette.BodyMedium, 12, width), cache.Brush(palette.Foreground), left, top, 16);
        }
        if (_item.SecondLine is { Length: > 0 } second)
        {
            DrawLine(context, cache.GetTrimmed(second, palette.Body, 12, width), cache.Brush(palette.TextMuted), left, top + 16, 16);
        }
    }

    /// <summary>List: title (14/18) and meta (12/16) beside the thumbnail, the value (14) on the right.</summary>
    private void DrawRow(DrawingContext context, LayoutMorphPalette palette, double w, double h)
    {
        var cache = _stage.TextCache;
        var left = 1 + 62;
        var right = w - 1 - 16;
        var valueWidth = 0.0;
        if (_item.Value is { Length: > 0 } value)
        {
            var text = cache.Get(value, palette.Body, 14);
            valueWidth = text.WidthIncludingTrailingWhitespace;
            DrawLine(context, text, cache.Brush(palette.TextSecondary), right - valueWidth, (h - 19.6) / 2, 19.6);
        }
        var width = Math.Max(1, right - left - (valueWidth > 0 ? valueWidth + 14 : 0));
        var meta = _item.Meta ?? _item.Subtitle;
        var blockHeight = string.IsNullOrEmpty(meta) ? 18 : 34;
        var top = (h - blockHeight) / 2;
        if (!string.IsNullOrEmpty(_item.Title))
        {
            DrawLine(context, cache.GetTrimmed(_item.Title, palette.BodyMedium, 14, width), cache.Brush(palette.Foreground), left, top, 18);
        }
        if (!string.IsNullOrEmpty(meta))
        {
            DrawLine(context, cache.GetTrimmed(meta, palette.Body, 12, width), cache.Brush(palette.TextMuted), left, top + 18, 16);
        }
    }

    /// <summary>Carousel: title and value (16/22) on one line, the second line (12/16) under them, 16px in.</summary>
    private void DrawDetail(DrawingContext context, LayoutMorphPalette palette, double w, double h)
    {
        var cache = _stage.TextCache;
        var left = 1 + 16;
        var right = w - 1 - 16;
        var top = h - 1 - 16 - 40;
        var valueWidth = 0.0;
        if (_item.Value is { Length: > 0 } value)
        {
            var text = cache.Get(value, palette.Body, 16);
            valueWidth = text.WidthIncludingTrailingWhitespace;
            DrawLine(context, text, cache.Brush(palette.Foreground), right - valueWidth, top, 22);
        }
        var width = Math.Max(1, right - left - (valueWidth > 0 ? valueWidth + 12 : 0));
        if (!string.IsNullOrEmpty(_item.Title))
        {
            DrawLine(context, cache.GetTrimmed(_item.Title, palette.BodyMedium, 16, width), cache.Brush(palette.Foreground), left, top, 22);
        }
        if (_item.SecondLine is { Length: > 0 } second)
        {
            DrawLine(context, cache.GetTrimmed(second, palette.Body, 12, Math.Max(1, right - left)), cache.Brush(palette.TextMuted), left, top + 24, 16);
        }
    }

    private void DrawSheet(DrawingContext context, LayoutMorphPalette palette, double w, double h)
    {
        var rect = Sheet.In(w, h);
        if (rect.Width < 8 || rect.Height < 8)
        {
            return;
        }
        if (_sheet is null || !_sheet.Matches(_item, rect.Width, IsSheetNarrow))
        {
            _sheet = new LayoutMorphSheetCache(_item, palette, rect.Width, IsSheetNarrow);
        }
        _sheet.Draw(context, palette, rect);
    }
}

/// <summary>
/// The opened detail's text, laid out once per item and width: title (Display 28/30.8, −0.84 letter spacing; 22/24.2
/// on narrow screens), meta (14), description (14/19.6, 1–3 lines; left out on narrow screens) and the facts grid at
/// the foot (two columns 16 apart, rows 12 apart, a rule and 14px above).
/// </summary>
internal sealed class LayoutMorphSheetCache
{
    private readonly LayoutMorphItem _item;
    private readonly double _width;
    private readonly bool _narrow;
    private readonly TextLayout _title;
    private readonly TextLayout? _meta;
    private readonly TextLayout? _description;
    private readonly List<(TextLayout Label, TextLayout Value)> _facts = new();

    public LayoutMorphSheetCache(LayoutMorphItem item, LayoutMorphPalette palette, double width, bool narrow)
    {
        _item = item;
        _width = width;
        _narrow = narrow;
        var titleSize = narrow ? 22 : 28;
        _title = new TextLayout(item.Title, palette.DisplayMedium, titleSize, new SolidColorBrush(palette.Foreground),
            textWrapping: TextWrapping.Wrap, textTrimming: TextTrimming.CharacterEllipsis, maxWidth: Math.Max(1, width - 32),
            lineHeight: titleSize * 1.1, letterSpacing: -0.03 * titleSize, maxLines: 2);
        var meta = item.Meta ?? item.Subtitle;
        if (!string.IsNullOrEmpty(meta))
        {
            _meta = new TextLayout(meta, palette.Body, 14, new SolidColorBrush(palette.TextSecondary),
                textTrimming: TextTrimming.CharacterEllipsis, maxWidth: Math.Max(1, width), lineHeight: 19.6, maxLines: 1);
        }
        if (!narrow && !string.IsNullOrEmpty(item.Description))
        {
            _description = new TextLayout(item.Description!, palette.Body, 14, new SolidColorBrush(palette.TextSecondary),
                textWrapping: TextWrapping.Wrap, textTrimming: TextTrimming.CharacterEllipsis, maxWidth: Math.Max(1, width),
                lineHeight: 19.6, maxLines: 3);
        }
        var count = Math.Min(item.Facts.Count, narrow ? 2 : 4);
        var column = Math.Max(1, (width - 16) / 2);
        for (var i = 0; i < count; i++)
        {
            var fact = item.Facts[i];
            _facts.Add((
                new TextLayout(fact.Label, palette.Body, 12, new SolidColorBrush(palette.TextMuted),
                    textTrimming: TextTrimming.CharacterEllipsis, maxWidth: column, lineHeight: 16.8, maxLines: 1),
                new TextLayout(fact.Value, palette.BodyMedium, 14, new SolidColorBrush(palette.Foreground),
                    textTrimming: TextTrimming.CharacterEllipsis, maxWidth: column, lineHeight: 19.6, maxLines: 1)));
        }
    }

    public bool Matches(LayoutMorphItem item, double width, bool narrow) =>
        ReferenceEquals(item, _item) && Math.Abs(width - _width) < 0.5 && narrow == _narrow;

    public void Draw(DrawingContext context, LayoutMorphPalette palette, Rect rect)
    {
        var y = rect.Y;
        _title.Draw(context, new Point(rect.X, y));
        y += _title.Height + 8;
        if (_meta is not null)
        {
            _meta.Draw(context, new Point(rect.X, y));
            y += _meta.Height + 8;
        }
        if (_description is not null)
        {
            y += 4;
            _description.Draw(context, new Point(rect.X, y));
        }
        if (_facts.Count == 0)
        {
            return;
        }
        const double rowHeight = 16.8 + 19.6;
        var rows = (_facts.Count + 1) / 2;
        var gridHeight = rows * rowHeight + (rows - 1) * 12;
        var top = rect.Bottom - gridHeight;
        var ruleY = Math.Round(top - 14) - 0.5;
        context.DrawLine(palette.RulePen!, new Point(rect.X, ruleY), new Point(rect.Right, ruleY));
        var column = (rect.Width - 16) / 2;
        for (var i = 0; i < _facts.Count; i++)
        {
            var x = rect.X + (i % 2) * (column + 16);
            var rowTop = top + (i / 2) * (rowHeight + 12);
            _facts[i].Label.Draw(context, new Point(x, rowTop));
            _facts[i].Value.Draw(context, new Point(x, rowTop + 16.8));
        }
    }
}

/// <summary>
/// The drawing of a <see cref="LayoutMorphCard"/>, the card's only child: the card's size and transform, not focusable, not
/// hit-tested (the card tests its rounded rectangle) and hidden from automation (the card is the button).
/// </summary>
internal sealed class LayoutMorphCardFace : Control
{
    private readonly LayoutMorphCard _card;

    public LayoutMorphCardFace(LayoutMorphCard card)
    {
        _card = card;
        Focusable = false;
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    /// <summary>Drawn at least once: a bitmap cache set on the card before its first frame would not be used.</summary>
    public bool HasRendered => RenderCount > 0;

    /// <summary>How often the face was drawn (tests: a cached card's bitmap is rendered again each time).</summary>
    public int RenderCount { get; private set; }

    public override void Render(DrawingContext context)
    {
        RenderCount++;
        _card.DrawFace(context);
    }
}

/// <summary>A card is a button to screen readers: named by its item, described by its details, invoked to open.</summary>
internal sealed class LayoutMorphCardAutomationPeer : ControlAutomationPeer, IInvokeProvider
{
    public LayoutMorphCardAutomationPeer(LayoutMorphCard owner) : base(owner)
    {
    }

    private new LayoutMorphCard Owner => (LayoutMorphCard)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    /// <summary>The card is one button: its face is not an element of its own.</summary>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override string? GetNameCore() => Owner.Item.AccessibleName;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public void Invoke() => Owner.Invoke();
}
