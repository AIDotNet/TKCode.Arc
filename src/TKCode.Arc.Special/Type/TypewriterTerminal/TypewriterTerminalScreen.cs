using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The text area of a <see cref="TypewriterTerminal"/>: draws the finished lines, the command being typed (its full
/// layout, clipped to the typed characters, so typing allocates nothing) and the caret. Its height is the whole
/// script's, so the window does not grow while it plays.
/// </summary>
internal sealed class TypewriterTerminalScreen : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<TypewriterTerminalScreen>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<TypewriterTerminalScreen>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<TypewriterTerminalScreen>();

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, double>(nameof(LineSpacing), 1.6);

    public static readonly StyledProperty<IBrush?> PromptBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(PromptBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(SuccessBrush));

    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(WarningBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(DangerBrush));

    public static readonly StyledProperty<IBrush?> CaretBrushProperty =
        AvaloniaProperty.Register<TypewriterTerminalScreen, IBrush?>(nameof(CaretBrush));

    private readonly List<TypewriterTerminalScreenLine> _lines = [];
    private TextLayout? _prompt;
    private double _promptWidth;
    private double _lineHeight;
    private double _cellWidth;
    private double _layoutWidth = double.NaN;
    private bool _dirty = true;

    static TypewriterTerminalScreen()
    {
        AffectsMeasure<TypewriterTerminalScreen>(FontFamilyProperty, FontSizeProperty, LineSpacingProperty);
    }

    internal TypewriterTerminal? Owner { get; set; }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

    public IBrush? PromptBrush { get => GetValue(PromptBrushProperty); set => SetValue(PromptBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }

    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    public IBrush? CaretBrush { get => GetValue(CaretBrushProperty); set => SetValue(CaretBrushProperty, value); }

    /// <summary>The height of one line of text.</summary>
    internal double LineHeight => _lineHeight;

    internal int LayoutCount => _lines.Count;

    /// <summary>The script changed: rebuild the line layouts on the next measure.</summary>
    public void Reset()
    {
        _dirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>The box of line <paramref name="index"/> (the final prompt when it is past the last line).</summary>
    public Rect LineBounds(int index)
    {
        if (index < _lines.Count)
        {
            var line = _lines[index];
            return new Rect(0, line.Top, Bounds.Width, line.Height);
        }
        return new Rect(0, IdleTop, Bounds.Width, _lineHeight);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ForegroundProperty || property == PromptBrushProperty || property == MutedBrushProperty
            || property == AccentBrushProperty || property == SuccessBrushProperty || property == WarningBrushProperty
            || property == DangerBrushProperty || property == FontFamilyProperty || property == FontSizeProperty
            || property == LineSpacingProperty)
        {
            _dirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (property == CaretBrushProperty)
        {
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        Build(width);
        var natural = 0.0;
        foreach (var line in _lines)
        {
            natural = Math.Max(natural, line.Width + (line.IsCommand ? _promptWidth : 0));
        }
        return new Size(double.IsFinite(width) ? width : natural, IdleTop + _lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!finalSize.Width.Equals(_layoutWidth))
        {
            Build(finalSize.Width);
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TypewriterTerminalScreenAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner || _prompt is null)
        {
            return;
        }
        var player = owner.Player;
        var shown = Math.Min(player.Line, _lines.Count);
        for (var i = 0; i < shown; i++)
        {
            var line = _lines[i];
            if (line.IsCommand)
            {
                _prompt.Draw(context, new Point(0, line.Top));
            }
            line.Layout.Draw(context, new Point(line.IsCommand ? _promptWidth : 0, line.Top));
        }
        Point caret;
        if (!player.IsFinished && player.Line < _lines.Count)
        {
            var line = _lines[player.Line];
            if (!line.IsCommand)
            {
                return;
            }
            _prompt.Draw(context, new Point(0, line.Top));
            caret = DrawTyped(context, line, player.Chars);
        }
        else
        {
            _prompt.Draw(context, new Point(0, IdleTop));
            caret = new Point(_promptWidth, IdleTop);
        }
        if (owner.IsCaretShown && CaretBrush is { } caretBrush)
        {
            var inset = _lineHeight * 0.18;
            context.FillRectangle(caretBrush, new Rect(caret.X, caret.Y + inset, Math.Max(2, _cellWidth * 0.9), _lineHeight - inset * 2));
        }
    }

    /// <summary>Draws the first <paramref name="chars"/> characters of a command and returns where the caret goes.</summary>
    private Point DrawTyped(DrawingContext context, TypewriterTerminalScreenLine line, int chars)
    {
        var origin = new Point(_promptWidth, line.Top);
        if (chars <= 0)
        {
            return origin;
        }
        var layout = line.Layout;
        var hit = layout.HitTestTextPosition(Math.Min(chars, line.Length));
        // Rows above the caret's row are whole; the caret's row is cut at the caret.
        if (hit.Top > 0)
        {
            using (context.PushClip(new Rect(origin.X, origin.Y, Math.Max(0, Bounds.Width - origin.X), hit.Top)))
            {
                layout.Draw(context, origin);
            }
        }
        using (context.PushClip(new Rect(origin.X, origin.Y + hit.Top, Math.Max(0, hit.X), Math.Max(hit.Height, _lineHeight))))
        {
            layout.Draw(context, origin);
        }
        return new Point(origin.X + hit.X, origin.Y + hit.Top);
    }

    private double IdleTop => _lines.Count == 0 ? 0 : _lines[^1].Top + _lines[^1].Height;

    private void Build(double width)
    {
        if (!_dirty && width.Equals(_layoutWidth))
        {
            return;
        }
        _dirty = false;
        _layoutWidth = width;
        foreach (var line in _lines)
        {
            line.Layout.Dispose();
        }
        _lines.Clear();
        _prompt?.Dispose();
        var typeface = new Typeface(FontFamily);
        var size = FontSize;
        _lineHeight = Math.Round(size * LineSpacing);
        var promptText = (Owner?.Prompt ?? "$") + " ";
        _prompt = new TextLayout(promptText, typeface, size, PromptBrush ?? Foreground, lineHeight: _lineHeight);
        _promptWidth = _prompt.WidthIncludingTrailingWhitespace;
        using (var cell = new TextLayout("M", typeface, size, null))
        {
            _cellWidth = cell.WidthIncludingTrailingWhitespace;
        }
        if (Owner is not { } owner)
        {
            return;
        }
        var top = 0.0;
        foreach (var source in owner.Lines)
        {
            var command = source.Kind == TypewriterTerminalLineKind.Command;
            var text = source.Text ?? string.Empty;
            var max = double.IsFinite(width) ? Math.Max(_cellWidth, width - (command ? _promptWidth : 0)) : double.PositiveInfinity;
            var layout = new TextLayout(text.Length == 0 ? " " : text, typeface, size, command ? Foreground : BrushOf(source.Tone),
                TextAlignment.Left, TextWrapping.Wrap, null, null, FlowDirection.LeftToRight, max, double.PositiveInfinity, _lineHeight);
            var height = Math.Max(_lineHeight, layout.Height);
            _lines.Add(new TypewriterTerminalScreenLine(layout, top, height, layout.WidthIncludingTrailingWhitespace, text.Length, command));
            top += height;
        }
    }

    private IBrush? BrushOf(TypewriterTerminalTone tone) => tone switch
    {
        TypewriterTerminalTone.Muted => MutedBrush,
        TypewriterTerminalTone.Accent => AccentBrush,
        TypewriterTerminalTone.Success => SuccessBrush,
        TypewriterTerminalTone.Warning => WarningBrush,
        TypewriterTerminalTone.Danger => DangerBrush,
        _ => Foreground
    } ?? Foreground;

    private sealed class TypewriterTerminalScreenAutomationPeer(TypewriterTerminalScreen owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => owner.Owner?.Transcript ?? base.GetNameCore();

        protected override bool IsContentElementCore() => true;
    }
}

/// <summary>One laid-out line of a <see cref="TypewriterTerminalScreen"/>.</summary>
internal sealed record TypewriterTerminalScreenLine(TextLayout Layout, double Top, double Height, double Width, int Length, bool IsCommand);
