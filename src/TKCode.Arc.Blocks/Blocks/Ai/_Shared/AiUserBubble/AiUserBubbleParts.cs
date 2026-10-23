using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The row of a user message (Arc's right-aligned, bottom-aligned flex row): the last child is the bubble, an optional
/// first child sits <see cref="Gap"/> to its left (the Copy / Edit group). The bubble shrinks to its content up to
/// min(82% of the row, 544), or 88% of the row when <see cref="IsNarrow"/>, and never past what the group leaves
/// (CSS flex shrinks the bubble before the fixed-size buttons). With <see cref="FillsBubble"/> the last child is given
/// exactly that width (the edit form, which is as wide as the widest bubble even for a short message). The row itself
/// takes the whole available width, so hovering anywhere on it counts.
/// </summary>
internal sealed class AiUserBubbleLane : Panel
{
    /// <summary>The widest bubble (Arc 544px).</summary>
    internal const double MaxBubbleWidth = 544;

    /// <summary>The bubble's share of the row (Arc 82%).</summary>
    internal const double BubbleRatio = 0.82;

    /// <summary>The bubble's share of the row in a narrow chat (Arc 88%, no 544 cap).</summary>
    internal const double NarrowBubbleRatio = 0.88;

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AiUserBubbleLane, double>(nameof(Gap), 4);

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AiUserBubbleLane, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<bool> FillsBubbleProperty =
        AvaloniaProperty.Register<AiUserBubbleLane, bool>(nameof(FillsBubble));

    static AiUserBubbleLane()
    {
        AffectsMeasure<AiUserBubbleLane>(GapProperty, IsNarrowProperty, FillsBubbleProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public bool FillsBubble { get => GetValue(FillsBubbleProperty); set => SetValue(FillsBubbleProperty, value); }

    /// <summary>The widest a bubble may be in a row <paramref name="rowWidth"/> wide (before the action group is taken off).</summary>
    internal static double BubbleLimit(double rowWidth, bool narrow)
    {
        if (double.IsInfinity(rowWidth) || double.IsNaN(rowWidth))
        {
            return MaxBubbleWidth;
        }
        return narrow ? rowWidth * NarrowBubbleRatio : Math.Min(rowWidth * BubbleRatio, MaxBubbleWidth);
    }

    /// <summary>The width the bubble was allowed in the last measure.</summary>
    internal double LastLimit { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }
        var bubble = Children[^1];
        var actions = Children.Count > 1 ? Children[0] : null;
        var width = availableSize.Width;
        var actionsSize = default(Size);
        if (actions is { IsVisible: true })
        {
            actions.Measure(Size.Infinity);
            actionsSize = actions.DesiredSize;
        }
        var limit = BubbleLimit(width, IsNarrow);
        if (actions is { IsVisible: true } && !double.IsInfinity(width))
        {
            limit = Math.Min(limit, width - actionsSize.Width - Gap);
        }
        limit = Math.Max(0, limit);
        LastLimit = limit;
        bubble.Measure(new Size(limit, double.PositiveInfinity));
        var bubbleWidth = FillsBubble ? limit : Math.Min(limit, bubble.DesiredSize.Width);
        var height = Math.Max(bubble.DesiredSize.Height, actionsSize.Height);
        var desiredWidth = double.IsInfinity(width)
            ? bubbleWidth + (actions is { IsVisible: true } ? actionsSize.Width + Gap : 0)
            : width;
        return new Size(desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var bubble = Children[^1];
        var bubbleWidth = FillsBubble ? LastLimit : Math.Min(LastLimit, bubble.DesiredSize.Width);
        var bubbleHeight = bubble.DesiredSize.Height;
        var bubbleX = Math.Max(0, finalSize.Width - bubbleWidth);
        bubble.Arrange(new Rect(bubbleX, finalSize.Height - bubbleHeight, bubbleWidth, bubbleHeight));
        if (Children.Count > 1)
        {
            var actions = Children[0];
            var size = actions.DesiredSize;
            actions.Arrange(new Rect(bubbleX - Gap - size.Width, finalSize.Height - size.Height, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// Holds a user row at the height of its content and springs that height when the row swaps between the bubble and the
/// editor (Arc: the turn slides to its new height on a critically damped spring, ω ≈ 20, while the form fades in). The
/// content is anchored to the bottom and clipped at the top while the height moves, so the form slides in from above as
/// in Arc. Only a change armed by <see cref="Arm"/> springs; every other change (typing a new line, new text, a new width)
/// follows at once, so a growing editor never hides its caret line.
/// </summary>
/// <remarks>
/// Driven from code with a <see cref="MotionValue{T}"/> because the target is the content's measured height and only
/// swaps animate; <see cref="Primitives.SpringSizeDecorator"/> springs every content change and anchors to the top.
/// Reduced and Off jump (no spatial motion); the swap's crossfade still runs at Reduced.
/// </remarks>
internal sealed class AiUserBubbleSizer : Decorator
{
    /// <summary>Overflow kept visible left and right of the clip while moving (focus rings).</summary>
    internal const double ClipBleed = 6;

    /// <summary>The spring of a swap (Arc's measured ω ≈ 20–21 rad/s, critically damped).</summary>
    internal static readonly ArcSpring SwapSpring = ArcSpring.Quick;

    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private double _target = double.NaN;
    private double _laidOut = double.NaN;
    private bool _armed;

    /// <summary>True while the height is springing.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>The height the content asks for (the spring's target).</summary>
    internal double TargetHeight => _target;

    /// <summary>Makes the next change of the content's height spring (call right before swapping the content).</summary>
    public void Arm()
    {
        _armed = true;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var armed = _armed;
        _armed = false;
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = child.DesiredSize.Height;
        _height ??= CreateHeight(target);
        if (double.IsNaN(_target))
        {
            _target = target;
            _height.Jump(target);
        }
        else if (Math.Abs(target - _target) >= 0.5)
        {
            _target = target;
            var full = IsLoaded && ArcMotion.IsFull(this);
            if (full && (armed || _height.IsAnimating))
            {
                // A swap, or a change while a swap is still moving: keep moving (the spring keeps its velocity).
                _ = _height.AnimateTo(target, MotionSpec.Spring(SwapSpring));
            }
            else
            {
                _height.Jump(target);
            }
        }
        return new Size(child.DesiredSize.Width, Math.Min(_height.Value, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        var height = child.DesiredSize.Height;
        child.Arrange(new Rect(0, finalSize.Height - height, finalSize.Width, height));
        if (IsMoving || height > finalSize.Height + 0.5)
        {
            _clip.Rect = new Rect(-ClipBleed, 0, finalSize.Width + 2 * ClipBleed, finalSize.Height);
            Clip = _clip;
            InvalidateVisual();
        }
        else
        {
            Clip = null;
        }
        return finalSize;
    }

    private MotionValue<double> CreateHeight(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            // Frames that move by less than a device pixel would lay out the whole thread for nothing; the end always lays out.
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var snapped = Math.Round(v * scale);
            // (The last frame reports the target while IsAnimating is still true, hence the comparison with the target.)
            if (snapped != _laidOut || Math.Abs(v - value.Target) < 0.01)
            {
                _laidOut = snapped;
                InvalidateMeasure();
            }
        };
        return value;
    }
}

/// <summary>
/// A 30 × 30 icon button of a user row (Arc's message actions): a Lucide glyph in TextMuted on a transparent square
/// with radius 10; hover (mouse and pen only) fills SurfaceMuted and turns the glyph Foreground; pressing scales it to
/// 0.92 on the CSS spring; the keyboard focus ring sits 2px outside. A new <see cref="Glyph"/> crossfades in (100 / 160ms),
/// which is how Copy turns into a check.
/// </summary>
[TemplatePart(GlyphPart, typeof(ArcContentSwap))]
[PseudoClasses(HoverPseudoClass)]
internal sealed class AiUserBubbleActionButton : AvaloniaButton
{
    public const string GlyphPart = "PART_Glyph";

    /// <summary>Set while a mouse or pen is over the button (Arc's hover media query excludes touch).</summary>
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiUserBubbleActionButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AiUserBubbleActionButton, double>(nameof(GlyphSize), 15);

    private ArcContentSwap? _swap;

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <summary>The glyph shown now (tests).</summary>
    internal Geometry? ShownGlyph => (_swap?.Content as ArcGlyph)?.Data;

    protected override Type StyleKeyOverride => typeof(AiUserBubbleActionButton);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(GlyphPart);
        ShowGlyph();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty)
        {
            ShowGlyph();
        }
        else if (change.Property == GlyphSizeProperty && _swap?.Content is ArcGlyph glyph)
        {
            glyph.Size = GlyphSize;
        }
    }

    /// <summary>A new glyph instance each time: the outgoing one is still in the crossfade's exiting layer.</summary>
    private void ShowGlyph()
    {
        if (_swap is null)
        {
            return;
        }
        _swap.Content = Glyph is { } data
            ? new ArcGlyph { Data = data, Size = GlyphSize, StrokeThickness = 1.75 }
            : null;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>One file listed above the text of a bubble: a 14px glyph (FileText unless the attachment has its own) and the name, 14 / 21 in TextSecondary, 6px apart.</summary>
internal sealed class AiUserBubbleAttachment : TemplatedControl
{
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<AiUserBubbleAttachment, string?>(nameof(FileName));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiUserBubbleAttachment, Geometry?>(nameof(Icon));

    static AiUserBubbleAttachment()
    {
        FocusableProperty.OverrideDefaultValue<AiUserBubbleAttachment>(false);
    }

    public string? FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The attachment shown.</summary>
    internal AiAttachment? Attachment { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new AttachmentPeer(this);

    /// <summary>Read as its file name; the glyph is decoration.</summary>
    private sealed class AttachmentPeer(AiUserBubbleAttachment owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => ((AiUserBubbleAttachment)Owner).FileName;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// What a user row shows while not editing: the Copy and Edit buttons (shown while <see cref="IsRevealed"/>) and the
/// bubble with the attachments above the text. It follows <see cref="Message"/>: a new text or a changed attachment list
/// shows at once. Copy shows a check while <see cref="IsCopied"/>. The owner (<see cref="AiUserBubble"/>) makes a new
/// row each time it comes back from editing and binds every property; the row only reports presses.
/// </summary>
[TemplatePart(CopyPart, typeof(AiUserBubbleActionButton))]
[TemplatePart(EditPart, typeof(AiUserBubbleActionButton))]
[TemplatePart(ActionsPart, typeof(Panel))]
[TemplatePart(BubblePart, typeof(Border))]
[TemplatePart(AttachmentsPart, typeof(Panel))]
[TemplatePart(TextPart, typeof(AiUserBubbleText))]
[PseudoClasses(RevealedPseudoClass, HasTextPseudoClass, HasAttachmentsPseudoClass)]
internal sealed class AiUserBubbleRow : TemplatedControl
{
    public const string CopyPart = "PART_Copy";
    public const string EditPart = "PART_Edit";
    public const string ActionsPart = "PART_Actions";
    public const string BubblePart = "PART_Bubble";
    public const string AttachmentsPart = "PART_Attachments";
    public const string TextPart = "PART_Text";

    /// <summary>Set while the Copy and Edit buttons show.</summary>
    public const string RevealedPseudoClass = ":revealed";

    /// <summary>Set while the message has text.</summary>
    public const string HasTextPseudoClass = ":has-text";

    /// <summary>Set while the message has attachments.</summary>
    public const string HasAttachmentsPseudoClass = ":has-attachments";

    public static readonly StyledProperty<AiChatUserMessage?> MessageProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, AiChatUserMessage?>(nameof(Message));

    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, bool>(nameof(IsRevealed));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<bool> IsCopiedProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, bool>(nameof(IsCopied));

    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, string?>(nameof(CopyText));

    public static readonly StyledProperty<string?> CopiedTextProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, string?>(nameof(CopiedText));

    public static readonly StyledProperty<string?> EditTextProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, string?>(nameof(EditText));

    public static readonly StyledProperty<string?> AttachmentsTextProperty =
        AvaloniaProperty.Register<AiUserBubbleRow, string?>(nameof(AttachmentsText));

    private AiUserBubbleActionButton? _copy;
    private AiUserBubbleActionButton? _edit;
    private Panel? _attachments;
    private AiUserBubbleText? _text;
    private AiChatUserMessage? _watched;
    private readonly List<AiAttachment> _watchedAttachments = new();
    private NavigationMethod? _focusPending;

    static AiUserBubbleRow()
    {
        FocusableProperty.OverrideDefaultValue<AiUserBubbleRow>(false);
    }

    public AiChatUserMessage? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public bool IsCopied { get => GetValue(IsCopiedProperty); set => SetValue(IsCopiedProperty, value); }

    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    public string? CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    public string? EditText { get => GetValue(EditTextProperty); set => SetValue(EditTextProperty, value); }

    public string? AttachmentsText { get => GetValue(AttachmentsTextProperty); set => SetValue(AttachmentsTextProperty, value); }

    /// <summary>Raised when Copy is pressed.</summary>
    public event EventHandler? CopyClicked;

    /// <summary>Raised when Edit is pressed.</summary>
    public event EventHandler? EditClicked;

    internal AiUserBubbleActionButton? CopyButton => _copy;

    internal AiUserBubbleActionButton? EditButton => _edit;

    internal AiUserBubbleText? TextBlock => _text;

    internal Panel? AttachmentsPanel => _attachments;

    internal Control? Bubble { get; private set; }

    internal Control? Actions { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_copy is not null)
        {
            _copy.Click -= OnCopyClick;
        }
        if (_edit is not null)
        {
            _edit.Click -= OnEditClick;
        }
        base.OnApplyTemplate(e);
        _copy = e.NameScope.Find<AiUserBubbleActionButton>(CopyPart);
        _edit = e.NameScope.Find<AiUserBubbleActionButton>(EditPart);
        _attachments = e.NameScope.Find<Panel>(AttachmentsPart);
        _text = e.NameScope.Find<AiUserBubbleText>(TextPart);
        Bubble = e.NameScope.Find<Control>(BubblePart);
        Actions = e.NameScope.Find<Control>(ActionsPart);
        if (_copy is not null)
        {
            _copy.Click += OnCopyClick;
            Tooltip.SetSide(_copy, TooltipSide.Bottom);
        }
        if (_edit is not null)
        {
            _edit.Click += OnEditClick;
            Tooltip.SetSide(_edit, TooltipSide.Bottom);
        }
        UpdateText();
        UpdateAttachments();
        UpdateCopy();
        UpdateEdit();
        UpdateAttachmentsName();
        TryFocus();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Message);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        TryFocus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MessageProperty)
        {
            if (this.IsAttachedToVisualTree())
            {
                Watch(change.GetNewValue<AiChatUserMessage?>());
            }
            UpdateText();
            UpdateAttachments();
        }
        else if (change.Property == IsRevealedProperty)
        {
            PseudoClasses.Set(RevealedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsCopiedProperty || change.Property == CopyTextProperty || change.Property == CopiedTextProperty)
        {
            UpdateCopy();
        }
        else if (change.Property == EditTextProperty)
        {
            UpdateEdit();
        }
        else if (change.Property == AttachmentsTextProperty)
        {
            UpdateAttachmentsName();
        }
    }

    /// <summary>
    /// Follows the message's text, its attachment list and each attachment's name and icon while the row is on screen
    /// (no subscription otherwise, so a row that was swapped out is not kept alive by the message).
    /// </summary>
    private void Watch(AiChatUserMessage? message)
    {
        if (ReferenceEquals(message, _watched))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnMessagePropertyChanged;
            _watched.Attachments.CollectionChanged -= OnAttachmentsChanged;
        }
        WatchAttachments(null);
        _watched = message;
        if (message is not null)
        {
            message.PropertyChanged += OnMessagePropertyChanged;
            message.Attachments.CollectionChanged += OnAttachmentsChanged;
            // The message may have changed while the row was off screen.
            UpdateText();
            UpdateAttachments();
        }
    }

    private void WatchAttachments(IReadOnlyList<AiAttachment>? attachments)
    {
        foreach (var attachment in _watchedAttachments)
        {
            attachment.PropertyChanged -= OnAttachmentPropertyChanged;
        }
        _watchedAttachments.Clear();
        if (attachments is null)
        {
            return;
        }
        foreach (var attachment in attachments)
        {
            attachment.PropertyChanged += OnAttachmentPropertyChanged;
            _watchedAttachments.Add(attachment);
        }
    }

    private void OnMessagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiChatUserMessage.TextProperty)
        {
            UpdateText();
        }
    }

    private void OnAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateAttachments();

    private void OnAttachmentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if ((e.Property == AiAttachment.NameProperty || e.Property == AiAttachment.IconProperty) && _attachments is not null)
        {
            foreach (var child in _attachments.Children)
            {
                if (child is AiUserBubbleAttachment item && ReferenceEquals(item.Attachment, sender))
                {
                    Fill(item, item.Attachment);
                }
            }
        }
    }

    private void UpdateText()
    {
        var text = Message?.Text ?? string.Empty;
        PseudoClasses.Set(HasTextPseudoClass, text.Length > 0);
        if (_text is null)
        {
            return;
        }
        _text.Source = text;
        AutomationProperties.SetName(_text, _text.DisplayText);
    }

    /// <summary>Lists the attachments again, keeping the items of attachments that stay in place.</summary>
    private void UpdateAttachments()
    {
        var attachments = Message?.Attachments;
        PseudoClasses.Set(HasAttachmentsPseudoClass, attachments is { Count: > 0 });
        if (_watched is not null)
        {
            WatchAttachments(attachments);
        }
        if (_attachments is null)
        {
            return;
        }
        var children = _attachments.Children;
        var count = attachments?.Count ?? 0;
        for (var i = 0; i < count; i++)
        {
            var attachment = attachments![i];
            if (i < children.Count && children[i] is AiUserBubbleAttachment existing && ReferenceEquals(existing.Attachment, attachment))
            {
                Fill(existing, attachment);
                continue;
            }
            var item = new AiUserBubbleAttachment { Attachment = attachment };
            Fill(item, attachment);
            if (i < children.Count)
            {
                children[i] = item;
            }
            else
            {
                children.Add(item);
            }
        }
        if (children.Count > count)
        {
            children.RemoveRange(count, children.Count - count);
        }
    }

    private static void Fill(AiUserBubbleAttachment item, AiAttachment? attachment)
    {
        item.FileName = attachment?.Name;
        item.Icon = attachment?.Icon ?? ArcIcons.FileText;
    }

    private void UpdateCopy()
    {
        if (_copy is null)
        {
            return;
        }
        var copied = IsCopied;
        _copy.Glyph = copied ? ArcIcons.Check : ArcIcons.Copy;
        var name = copied ? CopiedText ?? CopyText : CopyText;
        AutomationProperties.SetName(_copy, name);
        Tooltip.SetTip(_copy, name);
    }

    private void UpdateEdit()
    {
        if (_edit is null)
        {
            return;
        }
        AutomationProperties.SetName(_edit, EditText);
        Tooltip.SetTip(_edit, EditText);
    }

    private void UpdateAttachmentsName()
    {
        if (_attachments is not null)
        {
            AutomationProperties.SetName(_attachments, AttachmentsText);
        }
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e) => CopyClicked?.Invoke(this, EventArgs.Empty);

    private void OnEditClick(object? sender, RoutedEventArgs e) => EditClicked?.Invoke(this, EventArgs.Empty);

    /// <summary>Focuses Edit (with <paramref name="method"/>, so the keyboard ring shows when it should), as soon as it can take focus.</summary>
    public void FocusEditButton(NavigationMethod method)
    {
        _focusPending = method;
        TryFocus();
    }

    private void TryFocus()
    {
        if (_focusPending is not { } method || _edit is null || !_edit.IsAttachedToVisualTree() || !_edit.Focus(method))
        {
            return;
        }
        _focusPending = null;
    }
}
