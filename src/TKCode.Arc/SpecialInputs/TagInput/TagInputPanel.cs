using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The wrapping content of a <see cref="TagInput"/> (Arc <c>.content</c>): tags then the editor in one flex-wrap flow
/// (gap 6, padding 7, the editor takes the rest of the last line or wraps to its own with a 100px basis). Layers, bottom
/// to top: tag fills, the pick ring and the placeholder, tag labels and buttons, the editor. A reflow lets every tag and
/// the editor glide from its previous arrange position (FLIP on the morph spring, render transforms only); removed tags
/// leave from where they were without holding a slot.
/// </summary>
internal sealed class TagInputPanel : Panel
{
    public static readonly StyledProperty<Thickness> ContentInsetProperty =
        AvaloniaProperty.Register<TagInputPanel, Thickness>(nameof(ContentInset), new Thickness(7));

    public static readonly StyledProperty<double> ItemGapProperty =
        AvaloniaProperty.Register<TagInputPanel, double>(nameof(ItemGap), 6);

    public static readonly StyledProperty<double> EditorBasisProperty =
        AvaloniaProperty.Register<TagInputPanel, double>(nameof(EditorBasis), 100);

    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<TagInputPanel, IBrush?>(nameof(PlaceholderBrush));

    public static readonly StyledProperty<Thickness> PlaceholderInsetProperty =
        AvaloniaProperty.Register<TagInputPanel, Thickness>(nameof(PlaceholderInset), new Thickness(12, 0));

    private readonly List<TagInputChip> _chips = new();
    private readonly List<TagInputChip> _leaving = new();
    private readonly List<Rect> _rects = new();
    private readonly TagInputEditor _editor = new();
    private readonly TagInputPickRing _ring = new();
    private readonly TextBlock _placeholder;
    private readonly TranslateTransform _placeholderShift = new();
    private readonly TranslateTransform _shakeShift = new();
    private MotionValue<Vector>? _editorFlip;
    private MotionValue<double>? _placeholderPresence;
    private MotionValue<double>? _shake;
    private bool _placeholderShown;
    private bool _placeholderTravels;
    private Point? _editorLast;
    private double _lastWidth = double.NaN;
    private double _flowWidth;
    private bool _contentChanged;
    private TagInputChip? _picked;

    static TagInputPanel()
    {
        AffectsMeasure<TagInputPanel>(ContentInsetProperty, ItemGapProperty, EditorBasisProperty);
        BackgroundProperty.OverrideDefaultValue<TagInputPanel>(Brushes.Transparent);
    }

    public TagInputPanel()
    {
        _placeholder = new TextBlock
        {
            IsHitTestVisible = false,
            ClipToBounds = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = PlaceholderInset,
            RenderTransform = _placeholderShift,
            Opacity = 0,
            IsVisible = false,
            ZIndex = 1
        };
        AutomationProperties.SetAccessibilityView(_placeholder, AccessibilityView.Raw);
        Children.Add(_ring);
        Children.Add(_placeholder);
        Children.Add(_editor);
        RenderTransform = _shakeShift;
    }

    public Thickness ContentInset { get => GetValue(ContentInsetProperty); set => SetValue(ContentInsetProperty, value); }

    public double ItemGap { get => GetValue(ItemGapProperty); set => SetValue(ItemGapProperty, value); }

    public double EditorBasis { get => GetValue(EditorBasisProperty); set => SetValue(EditorBasisProperty, value); }

    public IBrush? PlaceholderBrush { get => GetValue(PlaceholderBrushProperty); set => SetValue(PlaceholderBrushProperty, value); }

    public Thickness PlaceholderInset { get => GetValue(PlaceholderInsetProperty); set => SetValue(PlaceholderInsetProperty, value); }

    internal TagInput? Owner { get; set; }

    public TagInputEditor Editor => _editor;

    internal TagInputPickRing Ring => _ring;

    internal TextBlock Placeholder => _placeholder;

    /// <summary>Tags in flow order (leaving tags excluded).</summary>
    internal IReadOnlyList<TagInputChip> Chips => _chips;

    internal IReadOnlyList<TagInputChip> LeavingChips => _leaving;

    internal TagInputChip? PickedChip => _picked;

    internal Vector EditorFlipOffset => _editorFlip?.Value ?? default;

    internal double ShakeOffset => _shakeShift.X;

    internal bool IsPlaceholderShown => _placeholderShown;

    internal double PlaceholderProgress => _placeholderPresence?.Value ?? (_placeholderShown ? 1 : 0);

    internal TagInputChip? FindChip(string tag)
    {
        foreach (var chip in _chips)
        {
            if (string.Equals(chip.TagText, tag, StringComparison.Ordinal))
            {
                return chip;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- tags

    /// <summary>Shows <paramref name="tags"/>: new tags enter at their slot, removed ones leave (AnimatePresence popLayout).</summary>
    public void Sync(IReadOnlyList<string> tags, bool animate)
    {
        var level = animate && IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var pool = new List<TagInputChip>(_chips);
        var next = new List<TagInputChip>(tags.Count);
        foreach (var tag in tags)
        {
            var index = -1;
            for (var i = 0; i < pool.Count; i++)
            {
                if (string.Equals(pool[i].TagText, tag, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }
            if (index >= 0)
            {
                next.Add(pool[index]);
                pool.RemoveAt(index);
            }
            else
            {
                var chip = CreateChip(tag);
                next.Add(chip);
                chip.Appear(level);
            }
        }
        _chips.Clear();
        _chips.AddRange(next);
        foreach (var chip in pool)
        {
            if (ReferenceEquals(chip, _picked))
            {
                // The ring leaves with its tag, on the same curve.
                SetPicked(null);
            }
            _leaving.Add(chip);
            chip.Leave(level, () => RemoveChip(chip));
        }
        OrderChildren();
        _contentChanged = true;
        InvalidateMeasure();
    }

    private TagInputChip CreateChip(string tag)
    {
        var chip = new TagInputChip(this, tag);
        if (Owner is { } owner)
        {
            AutomationProperties.SetName(chip.RemoveButton, owner.FormatRemoveLabel(tag));
        }
        var at = Children.IndexOf(_editor);
        Children.Insert(at, chip.Fill);
        Children.Insert(at + 1, chip);
        return chip;
    }

    private void RemoveChip(TagInputChip chip)
    {
        _leaving.Remove(chip);
        Children.Remove(chip);
        Children.Remove(chip.Fill);
        InvalidateArrange();
    }

    /// <summary>Keeps the tags' buttons before the editor in tab order and in value order.</summary>
    private void OrderChildren()
    {
        var previous = -1;
        var ordered = true;
        foreach (var chip in _chips)
        {
            var index = Children.IndexOf(chip);
            if (index < previous)
            {
                ordered = false;
                break;
            }
            previous = index;
        }
        if (ordered)
        {
            return;
        }
        foreach (var chip in _chips)
        {
            var from = Children.IndexOf(chip);
            var editor = Children.IndexOf(_editor);
            Children.Move(from, from < editor ? editor - 1 : editor);
        }
    }

    internal void UpdateRemoveLabels()
    {
        if (Owner is not { } owner)
        {
            return;
        }
        foreach (var chip in _chips)
        {
            AutomationProperties.SetName(chip.RemoveButton, owner.FormatRemoveLabel(chip.TagText));
        }
    }

    internal void OnRemoveRequested(TagInputChip chip) => Owner?.RemoveFromUser(chip.TagText);

    internal void OnChipClicked(TagInputChip chip) => Owner?.OnChipClicked(chip.TagText);

    internal void OnChipPressCancelled() => Owner?.OnChipPressCancelled();

    // ---------------------------------------------------------------- pick ring

    /// <summary>Picks <paramref name="chip"/> (or none): the ring glides to it, or fades where it was.</summary>
    public void SetPicked(TagInputChip? chip)
    {
        if (ReferenceEquals(_picked, chip))
        {
            return;
        }
        if (_picked is not null)
        {
            _picked.IsPicked = false;
        }
        _picked = chip;
        if (chip is not null)
        {
            chip.IsPicked = true;
        }
        UpdateRing();
    }

    private void UpdateRing(bool follow = false)
    {
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (_picked is { IsLeaving: false, LastPosition: not null } chip)
        {
            _ring.ShowAt(chip.LastRect, level, follow);
        }
        else if (_picked is null)
        {
            _ring.Clear(level);
        }
    }

    // ---------------------------------------------------------------- placeholder, shake

    public void SetPlaceholderText(string? text) => _placeholder.Text = text;

    /// <summary>
    /// The placeholder rises in (0.3em and a soft blur, standard + enter ease) when the field empties and leaves at once.
    /// Reduced: a fade on instant. The first state is applied without motion (<c>initial={false}</c>).
    /// </summary>
    public void SetPlaceholderShown(bool shown, bool animate)
    {
        if (shown == _placeholderShown)
        {
            return;
        }
        _placeholderShown = shown;
        _placeholderPresence ??= CreatePlaceholderPresence();
        var level = animate && IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (!shown || level == ArcMotionLevel.Off)
        {
            _placeholderPresence.Jump(shown ? 1 : 0);
            return;
        }
        _placeholderTravels = level == ArcMotionLevel.Full;
        _placeholderPresence.Jump(0);
        _ = _placeholderPresence.AnimateTo(1, _placeholderTravels
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Instant, TagInputMotion.DefaultEase));
    }

    private MotionValue<double> CreatePlaceholderPresence()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += ApplyPlaceholder;
        return value;
    }

    private void ApplyPlaceholder(double progress)
    {
        var p = Math.Clamp(progress, 0, 1);
        _placeholder.IsVisible = p > 0;
        _placeholder.Opacity = p;
        var rest = 1 - p;
        _placeholderShift.Y = _placeholderTravels ? rest * TagInputMotion.PlaceholderRise * _placeholder.FontSize : 0;
        var blur = _placeholderTravels ? rest * TagInputMotion.SoftBlur : 0;
        if (TagInputMotion.IsBlurVisible(blur))
        {
            _placeholder.Effect = ArcBlur.Get(blur);
        }
        else
        {
            _placeholder.Effect = null;
        }
    }

    /// <summary>A refused add: the content kicks sideways and rings home on a bouncy spring (full motion only).</summary>
    public void Shake()
    {
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_shake is null)
        {
            _shake = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shake.Changed += x => _shakeShift.X = x;
        }
        _ = _shake.AnimateTo(0, MotionSpec.Spring(TagInputMotion.Shake), TagInputMotion.ShakeVelocity);
    }

    // ---------------------------------------------------------------- input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Arc: a click on the empty part of the shell focuses the editor.
        if (e.Handled || !ReferenceEquals(e.Source, this) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _editor.Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlaceholderBrushProperty)
        {
            _placeholder.Foreground = PlaceholderBrush;
        }
        else if (change.Property == PlaceholderInsetProperty)
        {
            _placeholder.Padding = PlaceholderInset;
        }
    }

    // ---------------------------------------------------------------- layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = ContentInset;
        var inner = double.IsFinite(availableSize.Width)
            ? Math.Max(0, availableSize.Width - inset.Left - inset.Right)
            : double.PositiveInfinity;
        var itemSize = new Size(inner, double.PositiveInfinity);
        foreach (var child in Children)
        {
            if (ReferenceEquals(child, _placeholder))
            {
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            }
            else
            {
                child.Measure(itemSize);
            }
        }
        var height = Flow(inner) + inset.Top + inset.Bottom;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _flowWidth + inset.Left + inset.Right;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = ContentInset;
        var inner = Math.Max(0, finalSize.Width - inset.Left - inset.Right);
        Flow(inner);
        // Only reflows caused by tags arriving or leaving glide; a new width (the column resized) or new metrics (a late
        // font, a theme switch) land at once.
        var glide = _contentChanged && IsLoaded && ArcMotion.IsFull(this) && Math.Abs(finalSize.Width - _lastWidth) < 0.5;
        _contentChanged = false;
        for (var i = 0; i < _chips.Count; i++)
        {
            var chip = _chips[i];
            var rect = _rects[i].Translate(new Vector(inset.Left, inset.Top));
            chip.Arrange(rect);
            chip.Fill.Arrange(rect);
            if (chip.LastPosition is { } previous && previous != rect.Position)
            {
                if (glide)
                {
                    chip.FlipBy(previous - rect.Position);
                }
                else
                {
                    chip.ResetFlip();
                }
            }
            chip.LastPosition = rect.Position;
            chip.LastRect = rect;
        }
        var editorRect = _rects[^1].Translate(new Vector(inset.Left, inset.Top));
        _editor.Arrange(editorRect);
        if (_editorLast is { } last && last != editorRect.Position)
        {
            FlipEditor(last - editorRect.Position, glide);
        }
        _editorLast = editorRect.Position;
        foreach (var chip in _leaving)
        {
            chip.Arrange(chip.LastRect);
            chip.Fill.Arrange(chip.LastRect);
        }
        var all = new Rect(finalSize);
        _ring.Arrange(all);
        _placeholder.Arrange(all);
        _lastWidth = finalSize.Width;
        // A passive reflow moves a resting ring with its tag at once, like the tags themselves.
        UpdateRing(follow: !glide);
        return finalSize;
    }

    private void FlipEditor(Vector delta, bool glide)
    {
        if (_editorFlip is null)
        {
            _editorFlip = new MotionValue<Vector>(this, default, MotionVectors.Vector);
            _editorFlip.Changed += v =>
            {
                _editor.Shift.X = v.X;
                _editor.Shift.Y = v.Y;
            };
        }
        if (!glide)
        {
            _editorFlip.Jump(default);
            return;
        }
        var velocity = _editorFlip.Velocity;
        _editorFlip.Jump(_editorFlip.Value + delta);
        _ = _editorFlip.AnimateTo(default, MotionSpec.Spring(ArcSpring.Morph), velocity);
    }

    /// <summary>
    /// Lays out tags then the editor on wrapping lines (flex-wrap, align-items center, align-content start) into
    /// <see cref="_rects"/>, relative to the content box. Returns the content height.
    /// </summary>
    private double Flow(double inner)
    {
        _rects.Clear();
        var gap = ItemGap;
        var basis = Math.Max(EditorBasis, _editor.MinWidth);
        double x = 0, y = 0, lineHeight = 0, widest = 0;
        var lineStart = 0;
        var count = _chips.Count + 1;
        for (var i = 0; i < count; i++)
        {
            var isEditor = i == _chips.Count;
            var desired = isEditor ? _editor.DesiredSize : _chips[i].DesiredSize;
            var width = isEditor ? basis : Math.Min(desired.Width, inner);
            var first = i == lineStart;
            if (!first && x + gap + width > inner + 0.5)
            {
                CenterLine(lineStart, i, y, lineHeight);
                y += lineHeight + gap;
                x = 0;
                lineHeight = 0;
                lineStart = i;
                first = true;
            }
            var left = first ? 0 : x + gap;
            _rects.Add(new Rect(left, 0, width, desired.Height));
            x = left + width;
            widest = Math.Max(widest, x);
            lineHeight = Math.Max(lineHeight, desired.Height);
        }
        CenterLine(lineStart, count, y, lineHeight);
        // flex-grow: the editor takes the rest of its line.
        var editor = _rects[^1];
        if (double.IsFinite(inner))
        {
            _rects[^1] = editor.WithWidth(Math.Max(editor.Width, inner - editor.X));
        }
        _flowWidth = widest;
        return y + lineHeight;
    }

    private void CenterLine(int start, int end, double y, double lineHeight)
    {
        for (var k = start; k < end; k++)
        {
            var rect = _rects[k];
            _rects[k] = rect.WithY(y + (lineHeight - rect.Height) / 2);
        }
    }
}
