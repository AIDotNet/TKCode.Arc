using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.CodeTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// One drawn line of the diff (or a hunk header): the gutter with the line numbers pinned at the left, the sign and the
/// syntax-tinted code, which scroll sideways with <see cref="ScrollX"/>. Hovering the row brightens the numbers and, after
/// a short delay, scales in the add-comment mark on the gutter's edge; the gutter is the row's button (Enter or Space,
/// or a click on it, opens the comment box). Rows are drawn rather than templated: a file has hundreds of them.
/// </summary>
internal sealed class CodeReviewLineRow : Control
{
    private static readonly SpringSpec s_markSpring = SpringSpec.FromPhysical(324, 36);

    private readonly CodeReviewPalette _palette;
    private readonly List<(TextLayout Layout, double X)> _runs = new();
    private readonly TextLayout?[] _numbers = new TextLayout?[6];
    private TextLayout? _signLayout;
    private Pen? _markPen;
    private Pen? _focusPen;
    private TextLayout? _hunkLayout;
    private TextLayout? _sectionLayout;
    private int _builtVersion = -1;
    private double _textWidth;

    private MotionValue<double>? _hover;
    private MotionValue<double>? _markOpacity;
    private MotionValue<double>? _markScale;
    private MotionValue<double>? _active;
    private MotionValue<double>? _fresh;
    private MotionValue<double>? _presence;
    private double _hoverT;
    private double _markT;
    private double _scale = 0.6;
    private double _activeT;
    private double _freshT;
    private double _presenceT = 1;
    private bool _hovered;
    private bool _isActive;
    private double _scrollX;
    private bool _compact;

    public CodeReviewLineRow(CodeReviewPalette palette, CodeReviewRow row, Func<CodeReviewLineKind, int, string> name)
    {
        _palette = palette;
        Row = row;
        ClipToBounds = true;
        Cursor = row.Kind == CodeReviewLineKind.Hunk ? null : new Cursor(StandardCursorType.Arrow);
        if (row.Kind == CodeReviewLineKind.Hunk)
        {
            Focusable = false;
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        }
        else
        {
            Focusable = true;
            IsTabStop = false;
            AutomationProperties.SetName(this, name(row.Kind, row.Number));
            AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Button);
        }
    }

    /// <summary>The line and its numbers.</summary>
    public CodeReviewRow Row { get; private set; }

    /// <summary>The syntax colors of the line, or null for plain text.</summary>
    public ImmutableArray<HighlightToken>? Tokens { get; private set; }

    /// <summary>Raised when the gutter is clicked or Enter / Space is pressed on the row.</summary>
    public event Action<CodeReviewLineRow>? Invoked;

    /// <summary>Raised for Up, Down, Home and End on the row.</summary>
    public event Action<CodeReviewLineRow, Key>? Navigate;

    /// <summary>The leaving animation finished (the diff removes the row).</summary>
    public event Action<CodeReviewLineRow>? Left;

    public bool IsHunk => Row.Kind == CodeReviewLineKind.Hunk;

    /// <summary>Whether the comment box under this line is open (tints the line and keeps the mark).</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }
            _isActive = value;
            Tween(ref _active, value ? 1 : 0, CodeReviewMotion.Tint, TimeSpan.Zero, v => _activeT = v);
            UpdateMark();
            UpdateHover();
        }
    }

    /// <summary>The horizontal scroll of the code (the gutter stays).</summary>
    public double ScrollX
    {
        get => _scrollX;
        set
        {
            if (Math.Abs(_scrollX - value) > 0.01)
            {
                _scrollX = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>One number column instead of two (below the compact width).</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact != value)
            {
                _compact = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Width of the pinned gutter.</summary>
    public double GutterWidth
    {
        get
        {
            var p = _palette;
            var pad = _compact ? p.CompactGutterPadding : p.GutterPadding;
            return pad.Left + p.NumberWidth * (_compact ? 1 : 2) + pad.Right;
        }
    }

    /// <summary>Width the line needs from the gutter's edge: sign, code and Arc's 24px end padding.</summary>
    public double ContentWidth
    {
        get
        {
            EnsureText();
            return _palette.SignWidth + _textWidth + _palette.SidePadding;
        }
    }

    /// <summary>Replaces the line (a renumbered diff) and its colors.</summary>
    public void Update(CodeReviewRow row, ImmutableArray<HighlightToken>? tokens, Func<CodeReviewLineKind, int, string> name)
    {
        var textChanged = !ReferenceEquals(row.Line, Row.Line) || row.HunkHeader != Row.HunkHeader;
        var tokensChanged = tokens.HasValue != Tokens.HasValue || (tokens.HasValue && Tokens.HasValue && tokens.Value != Tokens.Value);
        Row = row;
        Tokens = tokens;
        if (!IsHunk)
        {
            AutomationProperties.SetName(this, name(row.Kind, row.Number));
        }
        if (textChanged || tokensChanged)
        {
            _builtVersion = -1;
        }
        InvalidateVisual();
    }

    /// <summary>Starts with the strong "just applied" tint, holds it, then eases to the added tint.</summary>
    public void Flash()
    {
        _freshT = 1;
        if (VisualRoot is null)
        {
            _flashOnAttach = true;
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _freshT = 0;
            return;
        }
        _fresh ??= Value(v => _freshT = v, 1);
        _fresh.Jump(1);
        _ = _fresh.AnimateTo(0, MotionSpec.Tween(CodeReviewMotion.FreshFade, ArcEasings.Standard, CodeReviewMotion.FreshHold));
        InvalidateVisual();
    }

    /// <summary>Grows in from zero height (a line a suggestion added).</summary>
    public void Enter()
    {
        if (VisualRoot is null)
        {
            // Not in the tree yet: grow once attached (the motion level is known then).
            _presenceT = 0;
            Opacity = 0;
            _enterOnAttach = true;
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            _presenceT = 1;
            Opacity = 1;
            InvalidateMeasure();
            return;
        }
        _presenceT = 0;
        Opacity = 0;
        _presence ??= Value(v =>
        {
            _presenceT = Math.Clamp(v, 0, 1.2);
            Opacity = Math.Clamp(v * 1.6, 0, 1);
            InvalidateMeasure();
        }, 0);
        _presence.Jump(0);
        _ = _presence.AnimateTo(1, MotionSpec.Spring(CodeReviewMotion.Spring));
    }

    /// <summary>Folds to zero height and raises <see cref="Left"/> (a line a suggestion replaced).</summary>
    public async void Leave()
    {
        Focusable = false;
        IsHitTestVisible = false;
        if (VisualRoot is null || !ArcMotion.IsFull(this))
        {
            Left?.Invoke(this);
            return;
        }
        _presence ??= Value(v =>
        {
            _presenceT = Math.Clamp(v, 0, 1.2);
            Opacity = Math.Clamp(v * 3 - 2, 0, 1);
            InvalidateMeasure();
        }, 1);
        await _presence.AnimateTo(0, MotionSpec.Spring(CodeReviewMotion.Spring));
        Left?.Invoke(this);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = IsHunk ? _palette.HunkHeight : _palette.LineHeight;
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, height * _presenceT);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch || IsHunk)
        {
            return;
        }
        _hovered = true;
        UpdateHover();
        UpdateMark();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateHover();
        UpdateMark();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsHunk || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // The gutter (and the mark on its edge) is the button; the code is for reading and selecting.
        if (e.GetPosition(this).X <= GutterWidth + 9)
        {
            e.Handled = true;
            Focus(NavigationMethod.Pointer);
            Invoked?.Invoke(this);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsHunk)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                Invoked?.Invoke(this);
                break;
            case Key.Up:
            case Key.Down:
            case Key.Home:
            case Key.End:
                e.Handled = true;
                Navigate?.Invoke(this, e.Key);
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateMark();
        UpdateHover();
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateMark();
        UpdateHover();
        InvalidateVisual();
    }

    private bool ShowsMark => _hovered || _isActive || IsKeyboardFocusWithin && Classes.Contains(":focus-visible");

    private void UpdateHover()
    {
        var on = _hovered || _isActive || IsKeyboardFocusWithin && Classes.Contains(":focus-visible");
        Tween(ref _hover, on ? 1 : 0, CodeReviewMotion.Hover, on && !_isActive ? CodeReviewMotion.MarkDelay : TimeSpan.Zero,
            v => _hoverT = v);
    }

    private void UpdateMark()
    {
        var on = ShowsMark;
        var delay = on && !_isActive ? CodeReviewMotion.MarkDelay : TimeSpan.Zero;
        Tween(ref _markOpacity, on ? 1 : 0, CodeReviewMotion.Hover, delay, v => _markT = v);
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _scale = on ? 1 : _palette.MarkRestScale;
            InvalidateVisual();
            return;
        }
        _markScale ??= Value(v => _scale = v, _scale);
        _ = _markScale.AnimateTo(on ? 1 : _palette.MarkRestScale, MotionSpec.Spring(s_markSpring, delay));
    }

    private void Tween(ref MotionValue<double>? value, double target, TimeSpan duration, TimeSpan delay, Action<double> write)
    {
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            value?.Jump(target);
            write(target);
            InvalidateVisual();
            return;
        }
        value ??= Value(write, 1 - target);
        _ = value.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard, delay));
    }

    private MotionValue<double> Value(Action<double> write, double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            write(v);
            InvalidateVisual();
        };
        return value;
    }

    private void EnsureText()
    {
        if (_builtVersion == _palette.Version)
        {
            return;
        }
        _builtVersion = _palette.Version;
        foreach (var run in _runs)
        {
            run.Layout.Dispose();
        }
        _runs.Clear();
        _hunkLayout?.Dispose();
        _sectionLayout?.Dispose();
        _signLayout?.Dispose();
        _signLayout = null;
        for (var i = 0; i < _numbers.Length; i++)
        {
            _numbers[i]?.Dispose();
            _numbers[i] = null;
        }
        _markPen = new Pen(_palette.Brush(_palette.AccentForeground), 2.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _focusPen = new Pen(_palette.Brush(_palette.FocusInset), 2);
        _hunkLayout = null;
        _sectionLayout = null;
        if (IsHunk)
        {
            _hunkLayout = new TextLayout(Row.HunkHeader, _palette.MonoFace, _palette.CodeFontSize, _palette.Brush(_palette.TextMuted));
            if (Row.Line.Text.Length > 0)
            {
                _sectionLayout = new TextLayout(Row.Line.Text, _palette.MonoFace, _palette.CodeFontSize, _palette.Brush(_palette.TextSecondary));
            }
            _textWidth = 0;
            return;
        }
        // Numbers in the muted and the bright color (hover crossfades them), for the old, new and single column.
        int[] numbers = [Row.OldNumber, Row.NewNumber, Row.Number];
        for (var i = 0; i < 3; i++)
        {
            if (numbers[i] > 0)
            {
                var digits = numbers[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                _numbers[i] = new TextLayout(digits, _palette.MonoFace, _palette.CodeFontSize, _palette.Brush(_palette.TextMuted));
                _numbers[i + 3] = new TextLayout(digits, _palette.MonoFace, _palette.CodeFontSize, _palette.Brush(_palette.Foreground));
            }
        }
        if (Row.Kind is CodeReviewLineKind.Added or CodeReviewLineKind.Deleted)
        {
            var added = Row.Kind == CodeReviewLineKind.Added;
            _signLayout = new TextLayout(added ? "+" : "\u2212", _palette.MonoFace, _palette.CodeFontSize,
                _palette.Brush(added ? _palette.Success : _palette.Danger));
        }
        var text = Row.Line.Text;
        var cw = _palette.CharWidth;
        _textWidth = text.Length * cw;
        if (text.Length == 0)
        {
            return;
        }
        var plain = _palette.Brush(_palette.Foreground);
        var position = 0;
        if (Tokens is { IsDefaultOrEmpty: false } tokens)
        {
            foreach (var token in tokens)
            {
                var start = Math.Clamp(token.Start, 0, text.Length);
                var end = Math.Clamp(token.Start + token.Length, start, text.Length);
                if (start > position)
                {
                    AddRun(text[position..start], position, _palette.MonoFace, plain);
                }
                if (end > start)
                {
                    var face = (token.Style & HighlightFontStyle.Italic) != 0 ? _palette.MonoItalic : _palette.MonoFace;
                    AddRun(text[start..end], start, face, _palette.Brush(Color.FromUInt32(token.Foreground)));
                }
                position = Math.Max(position, end);
            }
        }
        if (position < text.Length)
        {
            AddRun(text[position..], position, _palette.MonoFace, plain);
        }
    }

    private void AddRun(string text, int start, Typeface face, IBrush brush)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        _runs.Add((new TextLayout(text, face, _palette.CodeFontSize, brush), start * _palette.CharWidth));
    }

    public override void Render(DrawingContext context)
    {
        EnsureText();
        var p = _palette;
        var bounds = new Rect(Bounds.Size);
        if (IsHunk)
        {
            context.FillRectangle(p.Brush(p.HunkFill), bounds);
            var side = _compact ? p.CompactSidePadding : p.SidePadding;
            if (_hunkLayout is not null)
            {
                var y = (p.HunkHeight - _hunkLayout.Height) / 2;
                _hunkLayout.Draw(context, new Point(side, y));
                // The section name follows the header after a gap of one code character pair (12 at 12px).
                _sectionLayout?.Draw(context, new Point(side + _hunkLayout.WidthIncludingTrailingWhitespace + p.CodeFontSize, y));
            }
            return;
        }

        var kind = Row.Kind;
        Color line, gutter;
        switch (kind)
        {
            case CodeReviewLineKind.Added:
                line = Oklch.MixOklab(p.AddedLine, p.FreshLine, _freshT);
                gutter = Oklch.MixOklab(p.AddedGutter, p.FreshGutter, _freshT);
                break;
            case CodeReviewLineKind.Deleted:
                line = p.DeletedLine;
                gutter = p.DeletedGutter;
                break;
            default:
                line = p.Surface;
                gutter = p.Surface;
                break;
        }
        if (_activeT > 0)
        {
            line = Oklch.MixOklab(line, p.ActiveLine, _activeT);
            gutter = Oklch.MixOklab(gutter, p.ActiveGutter, _activeT);
        }
        var gutterWidth = GutterWidth;
        context.FillRectangle(p.Brush(line), bounds);

        // Sign and code scroll; the gutter is drawn last, over them.
        var codeX = gutterWidth - _scrollX;
        var lineHeight = p.LineHeight;
        using (context.PushClip(new Rect(gutterWidth, 0, Math.Max(0, bounds.Width - gutterWidth), bounds.Height)))
        {
            _signLayout?.Draw(context, new Point(codeX + p.SignInset, (lineHeight - _signLayout.Height) / 2));
            var textX = codeX + p.SignWidth;
            foreach (var (layout, x) in _runs)
            {
                layout.Draw(context, new Point(textX + x, (lineHeight - layout.Height) / 2));
            }
        }

        var gutterRect = new Rect(0, 0, gutterWidth, bounds.Height);
        context.FillRectangle(p.Brush(gutter), gutterRect);
        var bright = Math.Clamp(_hoverT, 0, 1);
        if (_compact)
        {
            DrawNumber(context, 2, p.CompactGutterPadding.Left, bright);
        }
        else
        {
            DrawNumber(context, 0, p.GutterPadding.Left, bright);
            DrawNumber(context, 1, p.GutterPadding.Left + p.NumberWidth, bright);
        }

        if (_markT > 0.001)
        {
            var size = p.MarkSize * _scale;
            var center = new Point(gutterWidth, lineHeight / 2);
            var rect = new Rect(center.X - size / 2, center.Y - size / 2, size, size);
            using (context.PushOpacity(Math.Clamp(_markT, 0, 1)))
            {
                var radius = p.MarkSize / 3 * _scale;
                context.DrawRectangle(p.Brush(p.Accent), null, rect, radius, radius);
                var glyph = ArcIcons.Plus;
                var glyphSize = p.MarkSize * 2 / 3 * _scale;
                var scale = glyphSize / 24;
                using (context.PushTransform(Matrix.CreateScale(scale, scale) *
                                             Matrix.CreateTranslation(center.X - glyphSize / 2, center.Y - glyphSize / 2)))
                {
                    context.DrawGeometry(null, _markPen, glyph);
                }
            }
        }

        if (IsKeyboardFocusWithin && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, _focusPen, gutterRect.Deflate(1));
        }
    }

    private void DrawNumber(DrawingContext context, int column, double columnX, double bright)
    {
        var muted = _numbers[column];
        var lit = _numbers[column + 3];
        if (muted is null || lit is null)
        {
            return;
        }
        // Right aligned in its column, the gutter's end padding from the column's end.
        var p = _palette;
        var at = new Point(columnX + p.NumberWidth - p.GutterPadding.Right - muted.WidthIncludingTrailingWhitespace,
            (p.LineHeight - muted.Height) / 2);
        if (bright < 1)
        {
            muted.Draw(context, at);
        }
        if (bright > 0)
        {
            using (context.PushOpacity(bright))
            {
                lit.Draw(context, at);
            }
        }
    }

    private bool _enterOnAttach;
    private bool _flashOnAttach;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_enterOnAttach)
        {
            _enterOnAttach = false;
            Enter();
        }
        if (_flashOnAttach)
        {
            _flashOnAttach = false;
            Flash();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hovered = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
