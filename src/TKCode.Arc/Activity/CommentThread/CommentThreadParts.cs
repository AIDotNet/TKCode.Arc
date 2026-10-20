using System.Text.RegularExpressions;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The small buttons of a <see cref="CommentThread"/>, one look per Arc class.</summary>
internal enum CommentThreadButtonKind
{
    /// <summary><c>.action</c>: quiet 28px text button (Reply, Edit, Delete, Cancel).</summary>
    Action,

    /// <summary><c>.action[data-tone=danger]</c>: the confirming Delete.</summary>
    Danger,

    /// <summary><c>.iconAction</c>: 28px icon button (add reaction, close reactions).</summary>
    Icon,

    /// <summary>The 22px close button of the "Replying to" bar.</summary>
    Close,

    /// <summary><c>.emoji</c>: a 28px picker emoji.</summary>
    Emoji,

    /// <summary><c>.resolve</c> / <c>.reopen</c>: 32px outlined pill with an icon.</summary>
    Pill,

    /// <summary><c>.send</c>: the filled foreground pill (Send, Save).</summary>
    Send,

    /// <summary><c>.ghost</c>: the composer's Cancel.</summary>
    Ghost
}

/// <summary>A button of <see cref="CommentThread"/>: optional Lucide glyph plus text, styled by <see cref="Kind"/>.</summary>
internal sealed class CommentThreadButton : AvaloniaButton
{
    public static readonly StyledProperty<CommentThreadButtonKind> KindProperty =
        AvaloniaProperty.Register<CommentThreadButton, CommentThreadButtonKind>(nameof(Kind));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<CommentThreadButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<CommentThreadButton, double>(nameof(GlyphSize), 14);

    public static readonly StyledProperty<double> GlyphStrokeProperty =
        AvaloniaProperty.Register<CommentThreadButton, double>(nameof(GlyphStroke), 1.75);

    /// <summary>Compact composer buttons (32px, 12px text).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<CommentThreadButton, bool>(nameof(IsCompact));

    /// <summary>The emoji the current user already chose (<c>aria-pressed</c>).</summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<CommentThreadButton, bool>(nameof(IsOn));

    public CommentThreadButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }
    public double GlyphStroke { get => GetValue(GlyphStrokeProperty); set => SetValue(GlyphStrokeProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadButton);

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadButtonPeer(this);

    /// <summary>A button peer; a picker emoji is a toggle (Arc <c>aria-pressed</c>).</summary>
    private sealed class CommentThreadButtonPeer(CommentThreadButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        private CommentThreadButton Button => (CommentThreadButton)Owner;

        public ToggleState ToggleState => Button.IsOn ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IToggleProvider) && Button.Kind != CommentThreadButtonKind.Emoji ? null : base.GetProviderCore(providerType);
    }
}

/// <summary>Layout roles of <see cref="CommentThreadPart"/>; the theme gives each its Arc padding, radius and colors.</summary>
internal enum CommentThreadPartRole
{
    Header,
    List,
    Empty,
    ComposerArea,
    Replying,
    Suggestions,
    Ring,
    ResolvedFace,
    ResolvedIcon
}

/// <summary>A themed box of a <see cref="CommentThread"/> (padding, radius and colors come from the theme by role).</summary>
internal sealed class CommentThreadPart : Border
{
    public static readonly StyledProperty<CommentThreadPartRole> RoleProperty =
        AvaloniaProperty.Register<CommentThreadPart, CommentThreadPartRole>(nameof(Role));

    public CommentThreadPartRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadPart);
}

/// <summary>Roles of <see cref="CommentThreadText"/>.</summary>
internal enum CommentThreadTextRole
{
    /// <summary><c>.title</c>: 14px / 500, ellipsis.</summary>
    Title,

    /// <summary><c>.subtitle</c>, <c>.time</c>, <c>.resolvedMeta</c>: 12px muted.</summary>
    Small,

    /// <summary><c>.name</c>: 500, ellipsis.</summary>
    Name,

    /// <summary><c>.deleted</c>: muted italic.</summary>
    Deleted,

    /// <summary><c>.empty</c>: muted.</summary>
    Empty,

    /// <summary><c>.prompt</c>: 12px foreground.</summary>
    Prompt,

    /// <summary><c>.replyingInner</c> text: 12px secondary.</summary>
    Replying,

    /// <summary><c>.resolvedTitle</c>: 500, ellipsis.</summary>
    ResolvedTitle
}

/// <summary>A themed text of a <see cref="CommentThread"/> (size, weight and color come from the theme by role).</summary>
internal sealed class CommentThreadText : TextBlock
{
    public static readonly StyledProperty<CommentThreadTextRole> RoleProperty =
        AvaloniaProperty.Register<CommentThreadText, CommentThreadTextRole>(nameof(Role));

    /// <summary>Color of an emphasized run (the name in "Replying to Mina").</summary>
    public static readonly StyledProperty<IBrush?> EmphasisBrushProperty =
        AvaloniaProperty.Register<CommentThreadText, IBrush?>(nameof(EmphasisBrush));

    public CommentThreadTextRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public IBrush? EmphasisBrush { get => GetValue(EmphasisBrushProperty); set => SetValue(EmphasisBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadText);

    private string? _text;
    private string? _part;
    private FontWeight _partWeight = FontWeight.Medium;
    private bool _usesParts;

    /// <summary>
    /// Shows <paramref name="text"/> with the first <paramref name="part"/> inside it emphasized (Arc's <c>highlight</c>:
    /// a translated sentence keeps its highlighted name wherever the language puts it).
    /// </summary>
    public void SetEmphasized(string text, string? part, FontWeight weight)
    {
        _text = text;
        _part = part;
        _partWeight = weight;
        _usesParts = true;
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmphasisBrushProperty && _usesParts)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var text = _text ?? string.Empty;
        var at = string.IsNullOrEmpty(_part) ? -1 : text.IndexOf(_part, StringComparison.Ordinal);
        Inlines ??= new InlineCollection();
        Inlines.Clear();
        if (at < 0)
        {
            Inlines.Add(new Run(text));
            return;
        }
        if (at > 0)
        {
            Inlines.Add(new Run(text[..at]));
        }
        Inlines.Add(new Run(_part) { FontWeight = _partWeight, Foreground = EmphasisBrush });
        if (at + _part!.Length < text.Length)
        {
            Inlines.Add(new Run(text[(at + _part.Length)..]));
        }
    }
}

/// <summary>
/// A comment body (Arc's <c>Body</c>): plain text, wrapped, with "@Full Name" mentions of known people as accent runs
/// (AccentStrong, 500). The mention pattern is built once per people list by the thread.
/// </summary>
internal sealed class CommentThreadBody : TextBlock
{
    public static readonly StyledProperty<IBrush?> MentionBrushProperty =
        AvaloniaProperty.Register<CommentThreadBody, IBrush?>(nameof(MentionBrush));

    public static readonly StyledProperty<FontWeight> MentionWeightProperty =
        AvaloniaProperty.Register<CommentThreadBody, FontWeight>(nameof(MentionWeight), FontWeight.Medium);

    public IBrush? MentionBrush { get => GetValue(MentionBrushProperty); set => SetValue(MentionBrushProperty, value); }
    public FontWeight MentionWeight { get => GetValue(MentionWeightProperty); set => SetValue(MentionWeightProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadBody);

    private string _body = string.Empty;
    private Regex? _mentions;

    /// <summary>The mentions found in the body, in order (tests).</summary>
    public IReadOnlyList<string> Mentions { get; private set; } = Array.Empty<string>();

    /// <summary>Shows <paramref name="body"/>, highlighting matches of <paramref name="mentions"/> (null: none).</summary>
    public void SetBody(string body, Regex? mentions)
    {
        if (body == _body && ReferenceEquals(mentions, _mentions) && Inlines is { Count: > 0 })
        {
            return;
        }
        _body = body;
        _mentions = mentions;
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MentionBrushProperty || change.Property == MentionWeightProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Inlines ??= new InlineCollection();
        Inlines.Clear();
        var found = new List<string>();
        if (_mentions is null)
        {
            Inlines.Add(new Run(_body));
        }
        else
        {
            // Split with a capture group: odd parts are the mentions (Arc's text.split(/(@(?:names))/)).
            var parts = _mentions.Split(_body);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                {
                    continue;
                }
                if (i % 2 == 1)
                {
                    found.Add(parts[i]);
                    Inlines.Add(new Run(parts[i]) { Foreground = MentionBrush, FontWeight = MentionWeight });
                }
                else
                {
                    Inlines.Add(new Run(parts[i]));
                }
            }
        }
        Mentions = found;
    }

    /// <summary>The pattern for "@Full Name" mentions of <paramref name="names"/> (longest first), or null for none.</summary>
    public static Regex? CreatePattern(IEnumerable<string> names)
    {
        var escaped = names.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal)
                           .OrderByDescending(name => name.Length).Select(Regex.Escape).ToArray();
        return escaped.Length == 0 ? null : new Regex($"(@(?:{string.Join("|", escaped)}))", RegexOptions.CultureInvariant);
    }
}

/// <summary>
/// A row of overlapping portraits (Arc's <c>.stack</c>): each avatar overlaps the previous one by 5px over a 2px ring
/// of the surface. Avatars are reused when the people change.
/// </summary>
internal sealed class CommentThreadAvatarStack : StackPanel
{
    private readonly double _diameter;

    public CommentThreadAvatarStack(double diameter)
    {
        _diameter = diameter;
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadAvatarStack);

    /// <summary>The names shown, in order (tests).</summary>
    public IEnumerable<string?> Names => Children.OfType<CommentThreadPart>().Select(ring => (ring.Child as Avatar)?.DisplayName);

    /// <summary>Shows the first three of <paramref name="people"/>.</summary>
    public void SetPeople(IEnumerable<CommentAuthor> people)
    {
        var index = 0;
        foreach (var person in people.Take(3))
        {
            if (index >= Children.Count)
            {
                Children.Add(new CommentThreadPart
                {
                    Role = CommentThreadPartRole.Ring,
                    Child = ActivityAvatar.Create(_diameter)
                });
            }
            var ring = (CommentThreadPart)Children[index];
            ActivityAvatar.Update((Avatar)ring.Child!, person.Name, person.Avatar);
            index++;
        }
        while (Children.Count > index)
        {
            Children.RemoveAt(Children.Count - 1);
        }
    }
}

/// <summary>A panel that shows its first child shrinking (ellipsis) and the rest at their size after it (Arc's name + time row).</summary>
internal sealed class CommentThreadInlinePanel : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CommentThreadInlinePanel, double>(nameof(Gap));

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    static CommentThreadInlinePanel()
    {
        AffectsMeasure<CommentThreadInlinePanel>(GapProperty);
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadInlinePanel);

    protected override Size MeasureOverride(Size availableSize)
    {
        double rest = 0, height = 0;
        var visible = 0;
        for (var i = 1; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(Size.Infinity);
            rest += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            visible++;
        }
        rest += visible * Gap;
        if (Children.Count > 0)
        {
            var first = Children[0];
            first.Measure(new Size(Math.Max(0, availableSize.Width - rest), availableSize.Height));
            height = Math.Max(height, first.DesiredSize.Height);
            rest += first.DesiredSize.Width;
        }
        return new Size(rest, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        // Baseline-ish: every child is centered on the tallest one (12px text sits on the 14px name's line).
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            var width = child.DesiredSize.Width;
            if (i == 0)
            {
                double rest = 0;
                for (var j = 1; j < Children.Count; j++)
                {
                    if (Children[j].IsVisible)
                    {
                        rest += Children[j].DesiredSize.Width + Gap;
                    }
                }
                width = Math.Min(width, Math.Max(0, finalSize.Width - rest));
            }
            var y = (finalSize.Height - child.DesiredSize.Height) / 2;
            child.Arrange(new Rect(x, y, width, child.DesiredSize.Height));
            x += width + Gap;
        }
        return finalSize;
    }
}

/// <summary>The replies list under a comment: indented, hanging from a quiet 1px rail (Arc's <c>.replies::before</c>).</summary>
internal sealed class CommentThreadRail : Decorator
{
    public static readonly StyledProperty<IBrush?> RailBrushProperty =
        AvaloniaProperty.Register<CommentThreadRail, IBrush?>(nameof(RailBrush));

    /// <summary>Top and bottom insets of the rail.</summary>
    public static readonly StyledProperty<Thickness> RailInsetProperty =
        AvaloniaProperty.Register<CommentThreadRail, Thickness>(nameof(RailInset));

    public IBrush? RailBrush { get => GetValue(RailBrushProperty); set => SetValue(RailBrushProperty, value); }
    public Thickness RailInset { get => GetValue(RailInsetProperty); set => SetValue(RailInsetProperty, value); }

    static CommentThreadRail()
    {
        AffectsRender<CommentThreadRail>(RailBrushProperty, RailInsetProperty);
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadRail);

    public override void Render(DrawingContext context)
    {
        if (RailBrush is not { } brush)
        {
            return;
        }
        var top = RailInset.Top;
        var bottom = Bounds.Height - RailInset.Bottom;
        if (bottom > top)
        {
            context.FillRectangle(brush, new Rect(0, top, 1, bottom - top));
        }
    }
}

/// <summary>The dashed 28px circle that stands in for a deleted comment's avatar.</summary>
internal sealed class CommentThreadDeletedDot : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<CommentThreadDeletedDot, IBrush?>(nameof(Stroke));

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    private IPen? _pen;
    private IBrush? _penBrush;

    static CommentThreadDeletedDot()
    {
        AffectsRender<CommentThreadDeletedDot>(StrokeProperty);
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadDeletedDot);

    public override void Render(DrawingContext context)
    {
        if (Stroke is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _penBrush = brush;
            _pen = new ImmutablePen(brush.ToImmutable(), 1, new ImmutableDashStyle([3, 3], 0));
        }
        var size = Bounds.Size;
        context.DrawEllipse(null, _pen, new Rect(0.5, 0.5, size.Width - 1, size.Height - 1));
    }
}

/// <summary>
/// The actions of a comment, quiet until the comment is hovered or holds focus (Arc's <c>.actions</c> opacity), always
/// shown while a prompt or the picker is open and on touch screens.
/// </summary>
internal sealed class CommentThreadReveal : Decorator
{
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<CommentThreadReveal, bool>(nameof(IsRevealed));

    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadReveal);
}

/// <summary>A comment's footer: reactions then actions, wrapping (Arc's <c>.footer</c>: 4px row gap, 8px column gap).</summary>
internal sealed class CommentThreadFooter : WrapPanel
{
    protected override Type StyleKeyOverride => typeof(CommentThreadFooter);
}

/// <summary>Spacing roles of <see cref="CommentThreadStack"/>.</summary>
internal enum CommentThreadStackRole
{
    /// <summary><c>.main</c>: 2px.</summary>
    Main,

    /// <summary><c>.actions</c>: 2px, horizontal.</summary>
    Actions,

    /// <summary><c>.composerActions</c>: 4px, horizontal.</summary>
    ComposerActions,

    /// <summary><c>.replyingInner</c>: 6px, horizontal.</summary>
    Replying,

    /// <summary><c>.resolved</c>: 12px, horizontal.</summary>
    Resolved,

    /// <summary>A suggestion row: 8px between portrait and name.</summary>
    Suggestion,

    /// <summary>The resolved chip's text column: no gap.</summary>
    Plain
}

/// <summary>A themed stack of a <see cref="CommentThread"/> (spacing from the theme by role).</summary>
internal sealed class CommentThreadStack : StackPanel
{
    public static readonly StyledProperty<CommentThreadStackRole> RoleProperty =
        AvaloniaProperty.Register<CommentThreadStack, CommentThreadStackRole>(nameof(Role));

    public CommentThreadStackRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadStack);
}

/// <summary>
/// "Show 2 replies" / "Hide replies" (Arc's <c>.toggle</c>): a chevron that turns 90° on the snappy spring, the label,
/// and while collapsed a stack of up to three repliers. Exposes expand / collapse to automation.
/// </summary>
internal sealed class CommentThreadReplyToggle : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CommentThreadReplyToggle, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CommentThreadReplyToggle, string?>(nameof(Label));

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    protected override Type StyleKeyOverride => typeof(CommentThreadReplyToggle);

    public CommentThreadReplyToggle()
    {
        Stack = new CommentThreadAvatarStack(18);
        Content = Stack;
    }

    /// <summary>The repliers shown while collapsed.</summary>
    public CommentThreadAvatarStack Stack { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            Stack.IsVisible = !IsExpanded;
            if (GetAutomationPeer() is { } peer)
            {
                peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    change.GetOldValue<bool>() ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                    change.GetNewValue<bool>() ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            }
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    private AutomationPeer? GetAutomationPeer() => ControlAutomationPeer.FromElement(this);

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadReplyTogglePeer(this);

    private sealed class CommentThreadReplyTogglePeer(CommentThreadReplyToggle owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private CommentThreadReplyToggle Toggle => (CommentThreadReplyToggle)Owner;

        public ExpandCollapseState ExpandCollapseState => Toggle.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Toggle.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Toggle.IsExpanded)
            {
                Invoke();
            }
        }
    }
}

/// <summary>A comment (Arc's <c>article</c>): a Group named "Author, time" ("Deleted comment" when deleted).</summary>
internal sealed class CommentThreadArticle : Border
{
    protected override Type StyleKeyOverride => typeof(CommentThreadArticle);

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadArticlePeer(this);

    private sealed class CommentThreadArticlePeer(CommentThreadArticle owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The resolved check: a 24px success-tinted circle with a 14px check.</summary>
internal sealed class CommentThreadResolvedIcon : Border
{
    public CommentThreadResolvedIcon()
    {
        Child = new ArcGlyph { Data = ArcIcons.Check, Size = 14, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadResolvedIcon);
}

/// <summary>
/// A horizontal flex row of a <see cref="CommentThread"/>: children sit side by side <see cref="Gap"/> apart at their own
/// width, except the child at <see cref="FillIndex"/>, which takes the remaining width (<c>flex: 1; min-width: 0</c>).
/// Each child aligns by its own <c>VerticalAlignment</c> within the row.
/// </summary>
internal sealed class CommentThreadRow : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CommentThreadRow, double>(nameof(Gap));

    public static readonly StyledProperty<int> FillIndexProperty =
        AvaloniaProperty.Register<CommentThreadRow, int>(nameof(FillIndex), 1);

    public static readonly StyledProperty<CommentThreadRowRole> RoleProperty =
        AvaloniaProperty.Register<CommentThreadRow, CommentThreadRowRole>(nameof(Role));

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public int FillIndex { get => GetValue(FillIndexProperty); set => SetValue(FillIndexProperty, value); }
    public CommentThreadRowRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    static CommentThreadRow()
    {
        AffectsMeasure<CommentThreadRow>(GapProperty, FillIndexProperty);
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadRow);

    protected override Size MeasureOverride(Size availableSize)
    {
        double fixedWidth = 0, height = 0;
        var visible = 0;
        Control? fill = null;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            visible++;
            if (i == FillIndex)
            {
                fill = child;
                continue;
            }
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            fixedWidth += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var gaps = Math.Max(0, visible - 1) * Gap;
        var width = fixedWidth + gaps;
        if (fill is not null)
        {
            var remaining = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width - width) : double.PositiveInfinity;
            fill.Measure(new Size(remaining, availableSize.Height));
            height = Math.Max(height, fill.DesiredSize.Height);
            width += double.IsFinite(remaining) ? remaining : fill.DesiredSize.Width;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double fixedWidth = 0;
        var visible = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            if (!Children[i].IsVisible)
            {
                continue;
            }
            visible++;
            if (i != FillIndex)
            {
                fixedWidth += Children[i].DesiredSize.Width;
            }
        }
        var fillWidth = Math.Max(0, finalSize.Width - fixedWidth - Math.Max(0, visible - 1) * Gap);
        double x = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            var width = i == FillIndex ? fillWidth : child.DesiredSize.Width;
            var childHeight = child.VerticalAlignment == VerticalAlignment.Stretch ? finalSize.Height : Math.Min(child.DesiredSize.Height, finalSize.Height);
            var y = child.VerticalAlignment switch
            {
                VerticalAlignment.Center => (finalSize.Height - childHeight) / 2,
                VerticalAlignment.Bottom => finalSize.Height - childHeight,
                _ => 0
            };
            child.Arrange(new Rect(x, y, width, childHeight));
            x += width + Gap;
        }
        return finalSize;
    }
}

/// <summary>Gap roles of <see cref="CommentThreadRow"/>.</summary>
internal enum CommentThreadRowRole
{
    /// <summary><c>.comment</c>: avatar and comment, 10px.</summary>
    Comment,

    /// <summary><c>.header</c>: heading and resolve, 12px.</summary>
    Header,

    /// <summary><c>.resolved</c>: icon, text, stack and reopen, 12px.</summary>
    Resolved
}
