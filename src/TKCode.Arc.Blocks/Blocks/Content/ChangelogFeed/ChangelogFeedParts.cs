// Ported from Arc's free changelog-feed block (MIT, Copyright (c) 2026 Elia Kuratli): the parts of changelog-feed.tsx and .module.css.
using System.Text.RegularExpressions;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The list: month titles, entries and the end note in one flow. A filter change takes children out of the flow at once: they
/// fade where they stood while the others spring to their new places (Arc's <c>layout="position"</c> with
/// <c>AnimatePresence mode="popLayout"</c>).
/// </summary>
/// <remarks>Code-driven because the distance of each child is known only after the new layout pass.</remarks>
internal sealed class ChangelogFlowPanel : Panel
{
    private sealed class Track
    {
        public bool Present { get; set; } = true;
        public bool Exiting { get; set; }
        public Rect Frozen { get; set; }
        public double LastY { get; set; } = double.NaN;
        public TranslateTransform Offset { get; } = new();
        public ScaleTransform Scale { get; } = new();
        public MotionValue<double>? Flip { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public int Generation { get; set; }
    }

    private readonly Dictionary<Control, Track> _tracks = new();
    private bool _flipArmed;

    private Track TrackOf(Control child)
    {
        if (!_tracks.TryGetValue(child, out var track))
        {
            track = new Track();
            var group = new TransformGroup();
            group.Children.Add(track.Scale);
            group.Children.Add(track.Offset);
            child.RenderTransform = group;
            _tracks[child] = track;
        }
        return track;
    }

    public bool IsPresent(Control child) => TrackOf(child).Present;

    public bool IsExiting(Control child) => TrackOf(child).Exiting;

    /// <summary>The next layout pass moves the remaining children on a spring.</summary>
    public void ArmFlip() => _flipArmed = ArcMotion.IsFull(this);

    /// <summary>Puts a child in or out of the flow; with <paramref name="animate"/> it fades (and leaves with a slight shrink and blur).</summary>
    public void SetPresent(Control child, bool present, bool animate, bool shrinks)
    {
        var track = TrackOf(child);
        if (track.Present == present)
        {
            return;
        }
        track.Present = present;
        var generation = ++track.Generation;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
        {
            track.Exiting = false;
            child.IsVisible = present;
            child.Opacity = 1;
            child.Effect = null;
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        if (track.Fade is null)
        {
            var blur = 0.0;
            track.Fade = new MotionValue<double>(this, child.Opacity, MotionVectors.Double);
            track.Fade.Changed += v =>
            {
                child.Opacity = Math.Clamp(v, 0, 1);
                blur = ArcMotion.IsFull(this) ? 2 * (1 - child.Opacity) : 0;
                child.Effect = ArcBlur.Get(blur);
                if (track.Exiting && shrinks)
                {
                    var scale = 0.98 + 0.02 * child.Opacity;
                    track.Scale.ScaleX = scale;
                    track.Scale.ScaleY = scale;
                }
            };
        }
        if (present)
        {
            track.Exiting = false;
            track.Scale.ScaleX = 1;
            track.Scale.ScaleY = 1;
            child.IsVisible = true;
            child.Opacity = 0;
            track.Fade.Jump(0);
            _ = track.Fade.AnimateTo(1, MotionSpec.Tween(full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            track.Exiting = true;
            track.Frozen = child.Bounds;
            _ = RunExit(child, track, generation, full);
        }
        InvalidateMeasure();
    }

    private async Task RunExit(Control child, Track track, int generation, bool full)
    {
        var arrived = await track.Fade!.AnimateTo(0, MotionSpec.Tween(full ? ArcMotionPresets.Exit : ArcMotionPresets.Instant, ArcEasings.Standard));
        if (arrived && generation == track.Generation)
        {
            track.Exiting = false;
            child.IsVisible = false;
            child.Effect = null;
            InvalidateMeasure();
        }
    }

    /// <summary>A present child's place in the flow.</summary>
    public double TopOf(Control child) => double.IsNaN(TrackOf(child).LastY) ? child.Bounds.Y : TrackOf(child).LastY;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 920 : availableSize.Width;
        var height = 0.0;
        foreach (var child in Children)
        {
            var track = TrackOf(child);
            if (track.Exiting)
            {
                child.Measure(track.Frozen.Size);
            }
            else if (track.Present)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                height += child.DesiredSize.Height;
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var child in Children)
        {
            var track = TrackOf(child);
            if (track.Exiting)
            {
                child.Arrange(track.Frozen);
                continue;
            }
            if (!track.Present)
            {
                track.LastY = double.NaN;
                continue;
            }
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            if (_flipArmed && !double.IsNaN(track.LastY) && Math.Abs(track.LastY - y) > 0.5)
            {
                StartFlip(track, track.LastY + track.Offset.Y - y);
            }
            track.LastY = y;
            y += child.DesiredSize.Height;
        }
        _flipArmed = false;
        return finalSize;
    }

    private void StartFlip(Track track, double from)
    {
        if (track.Flip is null)
        {
            track.Flip = new MotionValue<double>(this, from, MotionVectors.Double);
            track.Flip.Changed += v => track.Offset.Y = v;
        }
        track.Flip.Jump(from);
        track.Offset.Y = from;
        _ = track.Flip.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
    }
}

/// <summary>A row of the feed: the button with date, version, kind, title and summary, and the panel that opens under it, built on first open.</summary>
[PseudoClasses(":open", ":compact", ":new", ":improved", ":fixed")]
internal sealed class ChangelogEntryView : TemplatedControl
{
    private static readonly MotionSpec s_close = MotionSpec.Spring(SpringSpec.FromVisual(0.28, 0));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ChangelogEntryView, bool>(nameof(IsOpen));

    private ChangelogRowButton? _row;
    private BlockCollapse? _panel;
    private Control? _inner;
    private bool _compact;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _rise;
    private TranslateTransform? _riseOffset;

    public ChangelogEntryView(ChangelogEntry entry, string kindLabel)
    {
        Entry = entry;
        KindLabel = kindLabel;
        PseudoClasses.Set(":new", entry.Kind == ChangelogKind.New);
        PseudoClasses.Set(":improved", entry.Kind == ChangelogKind.Improved);
        PseudoClasses.Set(":fixed", entry.Kind == ChangelogKind.Fixed);
    }

    public ChangelogEntry Entry { get; }

    public string KindLabel { get; set; }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    public ChangelogRowButton? Row => _row;

    public BlockCollapse? Panel => _panel;

    /// <summary>Asks the feed to flip this entry.</summary>
    public event Action<ChangelogEntryView>? Toggled;

    public void SetCompact(bool compact)
    {
        _compact = compact;
        PseudoClasses.Set(":compact", compact);
        _inner?.Classes.Set("compact", compact);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_row is not null)
        {
            _row.Click -= OnRowClick;
        }
        _row = e.NameScope.Find<ChangelogRowButton>("PART_Row");
        _panel = e.NameScope.Find<BlockCollapse>("PART_Panel");
        var entry = Entry;
        Set(e, "PART_Date", entry.Date);
        Set(e, "PART_Version", "v" + entry.Version);
        Set(e, "PART_Kind", KindLabel);
        Set(e, "PART_Title", entry.Title);
        Set(e, "PART_Summary", entry.Summary);
        if (_row is not null)
        {
            _row.Click += OnRowClick;
            AutomationProperties.SetName(_row, $"{entry.Date}, {KindLabel}: {entry.Title}. {entry.Summary}");
            UpdateExpandedState();
        }
        if (_panel is not null)
        {
            _panel.CloseSpec = s_close;
            if (IsOpen)
            {
                EnsureInner();
            }
            _panel.SetInstantly(IsOpen);
        }
    }

    private static void Set(TemplateAppliedEventArgs e, string part, string text)
    {
        if (e.NameScope.Find<TextBlock>(part) is { } block)
        {
            block.Text = text;
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e) => Toggled?.Invoke(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(":open", IsOpen);
            UpdateExpandedState();
            if (_panel is not null)
            {
                Animate(IsOpen);
            }
        }
    }

    private void UpdateExpandedState()
    {
        if (_row is not null)
        {
            AutomationProperties.SetHelpText(_row, IsOpen ? "Expanded" : "Collapsed");
        }
    }

    private void Animate(bool open)
    {
        if (open)
        {
            EnsureInner();
        }
        _panel!.IsExpanded = open;
        if (_inner is null)
        {
            return;
        }
        var inner = _inner;
        if (_fade is null)
        {
            _riseOffset = new TranslateTransform();
            inner.RenderTransform = _riseOffset;
            _fade = new MotionValue<double>(this, inner.Opacity, MotionVectors.Double);
            _fade.Changed += v =>
            {
                inner.Opacity = Math.Clamp(v, 0, 1);
                inner.Effect = ArcMotion.IsFull(this) ? ArcBlur.Get(4 * (1 - inner.Opacity)) : null;
            };
            _rise = new MotionValue<double>(this, 0, MotionVectors.Double);
            _rise.Changed += v => _riseOffset.Y = v;
        }
        if (open)
        {
            var delay = TimeSpan.FromSeconds(0.06);
            _fade.Jump(0);
            inner.Opacity = 0;
            if (ArcMotion.IsFull(this))
            {
                _rise!.Jump(8);
                _riseOffset!.Y = 8;
                _ = _rise.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, delay));
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, delay));
            }
            else
            {
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            if (inner.Opacity != 1 && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
            {
                inner.Opacity = 1;
            }
        }
        else
        {
            _ = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
    }

    private void EnsureInner()
    {
        if (_inner is not null || _panel is null)
        {
            return;
        }
        _inner = BuildInner();
        _inner.Classes.Set("compact", _compact);
        _panel.Child = _inner;
    }

    private Control BuildInner()
    {
        var stack = new StackPanel { Spacing = 0 };
        var details = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 484 };
        foreach (var detail in Entry.Details)
        {
            var row = new DockPanel();
            var dash = new Border { Width = 6, Height = 1, Margin = new Thickness(1, 10, 9, 0), VerticalAlignment = VerticalAlignment.Top };
            ArcTokens.Bind(dash, Border.BackgroundProperty, ArcDesignTokenKind.TextMuted);
            DockPanel.SetDock(dash, Dock.Left);
            row.Children.Add(dash);
            row.Children.Add(BlockText.Create(detail, ArcDesignTokenKind.TypeSM, 14, ArcDesignTokenKind.TextSecondary));
            details.Children.Add(row);
        }
        stack.Children.Add(details);
        if (Entry.Media is { } media)
        {
            stack.Children.Add(BuildMedia(media));
        }
        return new ChangelogPanelInner { Child = stack };
    }

    /// <summary>The foreground at 8%; low enough that it reads in both themes without following them.</summary>
    private static readonly IBrush PhotoEdgeBrush = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(0x14, 0x80, 0x80, 0x80));

    private static Control BuildMedia(ChangelogMedia media)
    {
        if (media is ChangelogPhoto photo)
        {
            var figure = new StackPanel { Spacing = 8, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 18, 0, 0) };
            Control picture = photo.Image is { } image
                ? new Image { Source = image, Stretch = Stretch.UniformToFill }
                : new ArcImage { Source = photo.ImageSource, Stretch = Stretch.UniformToFill };
            AutomationProperties.SetName(picture, photo.Alt);
            var grid = new Panel();
            grid.Children.Add(picture);
            var edge = new Border { IsHitTestVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
            edge.BorderThickness = new Thickness(1);
            edge.BorderBrush = PhotoEdgeBrush;
            grid.Children.Add(edge);
            var frame = new BlogAspectFrame { Ratio = 1.6, Child = grid }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
            ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            figure.Children.Add(frame);
            if (!string.IsNullOrEmpty(photo.Caption))
            {
                figure.Children.Add(BlockText.Create(photo.Caption, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted));
            }
            return figure;
        }
        var code = (ChangelogCode)media;
        var card = new Border { MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 18, 0, 0), ClipToBounds = true }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(card, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        var column = new StackPanel();
        var head = new Border { Height = 40, Padding = new Thickness(14, 0, 6, 0) };
        ArcTokens.Bind(head, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(head, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);
        var headRow = new DockPanel();
        var copy = new CopyButton { Value = code.Code, Label = "Copy " + code.File, IsIconOnly = true, Variant = CopyButtonVariant.Plain, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(copy, Dock.Right);
        headRow.Children.Add(copy);
        var file = BlockText.Create(code.File, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted, wrapping: TextWrapping.NoWrap);
        file.VerticalAlignment = VerticalAlignment.Center;
        headRow.Children.Add(file);
        head.Child = headRow;
        column.Children.Add(head);
        var text = BlockText.Create(code.Code, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.Foreground, wrapping: TextWrapping.NoWrap, lineHeight: 20.4, tracking: 0);
        ArcTokens.Bind(text, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        text.Margin = new Thickness(16, 14, 16, 16);
        column.Children.Add(new ScrollViewer
        {
            Content = text,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
        card.Child = column;
        return card;
    }
}

/// <summary>The padding around an open entry's content; a decorator so the theme can follow the block's width.</summary>
internal sealed class ChangelogPanelInner : Decorator
{
}

/// <summary>The row of an entry: a button with the feed's hover fill.</summary>
internal sealed class ChangelogRowButton : BlockButton
{
    protected override ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusStep8;
}

/// <summary>A filter chip: a dot that grows into a checked mark, the label and the count.</summary>
[PseudoClasses(":new", ":improved", ":fixed")]
internal sealed class ChangelogChip : ToggleButton
{
    private LazyFocusRing? _focusRing;

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<ChangelogChip, int>(nameof(Count));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ChangelogChip, string?>(nameof(Label));

    public ChangelogChip(ChangelogKind kind)
    {
        Kind = kind;
        PseudoClasses.Set(":new", kind == ChangelogKind.New);
        PseudoClasses.Set(":improved", kind == ChangelogKind.Improved);
        PseudoClasses.Set(":fixed", kind == ChangelogKind.Fixed);
    }

    public ChangelogKind Kind { get; }

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Asks the feed to flip this filter.</summary>
    public event Action<ChangelogKind>? Pressed;

    protected override void Toggle() => Pressed?.Invoke(Kind);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing = BlockFocus.AttachRing(this, e.NameScope.Find<Panel>(BlockFocus.RootPart), ArcDesignTokenKind.RadiusPill);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>"Show all": fades in beside the chips.</summary>
internal sealed class ChangelogClearButton : BlockButton
{
}

/// <summary>Takes no layout space of its own: its child hangs 7 below the control, 6 from its right edge (Arc's absolute note under the pill).</summary>
internal sealed class ChangelogHangingNote : Decorator
{
    public bool AlignsLeft { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var x = AlignsLeft ? 6 : -child.DesiredSize.Width - 6;
            child.Arrange(new Rect(x, 7, child.DesiredSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// One pill that becomes a field and then a confirmation: its width morphs while the content swaps a beat later inside it. A
/// bad address shakes it; Escape or leaving an empty field goes back.
/// </summary>
[TemplatePart("PART_Swap", typeof(BlockSwap))]
[PseudoClasses(":idle", ":editing", ":done", ":error", ":compact")]
internal sealed class ChangelogSubscribe : TemplatedControl
{
    private static readonly Regex s_email = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant);
    private static readonly double[] s_shake = [0, -6, 5, -3, 2, 0];

    private static readonly BlockSwapMotion s_swap = new()
    {
        EnterScale = 0.96,
        EnterBlur = 2,
        ExitScale = 0.96,
        ExitBlur = 2,
        EnterFade = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, TimeSpan.FromSeconds(0.05)),
        EnterMove = MotionSpec.Spring(ArcSpring.Morph, TimeSpan.FromSeconds(0.03)),
        Exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard),
    };

    private static readonly BlockSwapMotion s_note = new()
    {
        EnterY = -4,
        EnterBlur = 2,
        EnterFade = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter),
        EnterMove = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter),
        Exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard),
    };

    private readonly TranslateTransform _shakeOffset = new();
    private MotionValue<double>? _shake;
    private BlockSwap? _swapHost;
    private BlockSwap? _noteHost;
    private Border? _pill;
    private NewsletterEmailBox? _input;
    private string _email = string.Empty;
    private ChangelogSubscribeState _state;
    private bool _error;
    private string? _noteText;

    public ChangelogSubscribe()
    {
        BlockFocusModality.Watch();
        PseudoClasses.Set(":idle", true);
    }

    public string SubscribeLabel { get; set; } = "Subscribe";

    public string SubscribedLabel { get; set; } = "Subscribed";

    public string UndoLabel { get; set; } = "Undo";

    public string Placeholder { get; set; } = "you@example.com";

    public string EmailLabel { get; set; } = "Email address";

    public string SubmitName { get; set; } = "Subscribe to release notes";

    public string InvalidMessage { get; set; } = "Enter a valid email address";

    public string DoneMessage { get; set; } = "Subscribed.";

    public string UndoNameFormat { get; set; } = "Undo subscription for {0}";

    public ChangelogSubscribeState State => _state;

    public bool HasError => _error;

    public string? NoteText => _noteText;

    public string Email => _email.Trim();

    public NewsletterEmailBox? Input => _input;

    public Control? CurrentContent => _swapHost?.Current;

    public event Action<string>? Subscribed;

    public event Action<string>? Undone;

    public event Action<ChangelogSubscribeState>? StateChanged;

    public void SetCompact(bool compact)
    {
        PseudoClasses.Set(":compact", compact);
        if (_input is not null)
        {
            _input.Width = compact ? 172 : 188;
        }
        if (_noteHost?.Parent is ChangelogHangingNote note)
        {
            note.AlignsLeft = compact;
            note.InvalidateArrange();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swapHost = e.NameScope.Find<BlockSwap>("PART_Swap");
        _noteHost = e.NameScope.Find<BlockSwap>("PART_Note");
        _pill = e.NameScope.Find<Border>("PART_Pill");
        if (e.NameScope.Find<Control>("PART_Shake") is { } shake)
        {
            shake.RenderTransform = _shakeOffset;
        }
        _swapHost?.Set(Build(_state));
    }

    /// <summary>Opens the field, as pressing the pill does.</summary>
    public void Edit() => Go(ChangelogSubscribeState.Editing, focus: true);

    /// <summary>Submits the field's text.</summary>
    public void Submit()
    {
        if (!s_email.IsMatch(_email.Trim()))
        {
            _error = true;
            PseudoClasses.Set(":error", true);
            SetNote(InvalidMessage);
            PlayShake();
            _input?.Focus(BlockFocusModality.For(this));
            return;
        }
        _error = false;
        PseudoClasses.Set(":error", false);
        Go(ChangelogSubscribeState.Done, focus: true);
        Subscribed?.Invoke(_email.Trim());
    }

    /// <summary>Cancels the field, as Escape does.</summary>
    public void Cancel()
    {
        _error = false;
        PseudoClasses.Set(":error", false);
        Go(ChangelogSubscribeState.Idle, focus: true);
    }

    /// <summary>Takes the subscription back.</summary>
    public void Undo()
    {
        var email = _email.Trim();
        _email = string.Empty;
        Go(ChangelogSubscribeState.Idle, focus: true);
        Undone?.Invoke(email);
    }

    private void Go(ChangelogSubscribeState next, bool focus)
    {
        if (_state == next)
        {
            return;
        }
        _state = next;
        PseudoClasses.Set(":idle", next == ChangelogSubscribeState.Idle);
        PseudoClasses.Set(":editing", next == ChangelogSubscribeState.Editing);
        PseudoClasses.Set(":done", next == ChangelogSubscribeState.Done);
        var content = Build(next);
        _swapHost?.Swap(content, s_swap);
        SetNote(_error ? InvalidMessage : next == ChangelogSubscribeState.Done ? DoneMessage : null);
        StateChanged?.Invoke(next);
        if (focus)
        {
            var target = FocusTargetOf(content);
            var method = BlockFocusModality.For(this);
            Dispatcher.UIThread.Post(() => target?.Focus(method), DispatcherPriority.Loaded);
        }
    }

    private void SetNote(string? text)
    {
        if (_noteText == text)
        {
            return;
        }
        _noteText = text;
        Control? note = null;
        if (!string.IsNullOrEmpty(text))
        {
            note = BlockText.Create(text, ArcDesignTokenKind.TypeXS, 12, _error ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted, wrapping: TextWrapping.NoWrap, lineHeight: 16);
        }
        _noteHost?.Swap(note, s_note);
    }

    private void PlayShake()
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        if (_shake is null)
        {
            _shake = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shake.Changed += t =>
            {
                var at = Math.Clamp(t, 0, 1) * (s_shake.Length - 1);
                var i = Math.Min((int)at, s_shake.Length - 2);
                _shakeOffset.X = s_shake[i] + (s_shake[i + 1] - s_shake[i]) * (at - i);
            };
        }
        _shake.Jump(0);
        _ = _shake.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
    }

    private static Control? FocusTargetOf(Control content) => content switch
    {
        ChangelogSubscribeIdle idle => idle,
        StackPanel { Tag: Control target } => target,
        _ => null
    };

    private Control Build(ChangelogSubscribeState state) => state switch
    {
        ChangelogSubscribeState.Editing => BuildForm(),
        ChangelogSubscribeState.Done => BuildDone(),
        _ => BuildIdle()
    };

    private Control BuildIdle()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(new ArcGlyph { Data = ArcIcons.Bell, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = SubscribeLabel, VerticalAlignment = VerticalAlignment.Center });
        var button = new ChangelogSubscribeIdle { Content = row };
        AutomationProperties.SetName(button, SubscribeLabel);
        button.Click += (_, _) => Edit();
        return button;
    }

    private Control BuildForm()
    {
        var input = new NewsletterEmailBox
        {
            Text = _email,
            PlaceholderText = Placeholder,
            Width = PseudoClasses.Contains(":compact") ? 172 : 188,
            FontSize = 14,
            LineHeight = 19.6,
            LetterSpacing = -0.14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(input, EmailLabel);
        _input = input;
        input.PropertyChanged += (_, change) =>
        {
            if (change.Property != TextBox.TextProperty)
            {
                return;
            }
            _email = input.Text ?? string.Empty;
            if (_error)
            {
                _error = false;
                PseudoClasses.Set(":error", false);
                SetNote(null);
            }
        };
        input.KeyDown += OnFormKeyDown;
        input.LostFocus += OnFormLostFocus;
        var submit = new ChangelogSubmitButton { Content = new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 14 } };
        AutomationProperties.SetName(submit, SubmitName);
        submit.Click += (_, _) => Submit();
        submit.KeyDown += OnFormKeyDown;
        submit.LostFocus += OnFormLostFocus;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(15, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Tag = input };
        row.Children.Add(input);
        row.Children.Add(submit);
        return row;
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
        else if (e.Key == Key.Enter && ReferenceEquals(sender, _input))
        {
            e.Handled = true;
            Submit();
        }
    }

    private void OnFormLostFocus(object? sender, FocusChangedEventArgs e)
    {
        // Leaving the form with nothing typed folds it back, without moving focus.
        Dispatcher.UIThread.Post(() =>
        {
            if (_state != ChangelogSubscribeState.Editing || !string.IsNullOrWhiteSpace(_email))
            {
                return;
            }
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            if (focused is not null && _swapHost?.Current is { } form && (ReferenceEquals(focused, form) || form.IsVisualAncestorOf(focused)))
            {
                return;
            }
            _error = false;
            PseudoClasses.Set(":error", false);
            Go(ChangelogSubscribeState.Idle, focus: false);
        }, DispatcherPriority.Background);
    }

    private Control BuildDone()
    {
        var check = new StrokeDrawGlyph { Data = Geometry.Parse("M4 12.5l5 5L20 6.5"), Size = 15, StrokeThickness = 2.2, VerticalAlignment = VerticalAlignment.Center, Progress = ArcMotion.IsFull(this) ? 0 : 1 };
        ArcTokens.Bind(check, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        var undo = new ChangelogUndoButton { Content = UndoLabel, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(undo, string.Format(UndoNameFormat, _email.Trim()));
        undo.Click += (_, _) => Undo();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Margin = new Thickness(13, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center, Tag = undo };
        row.Children.Add(check);
        var text = new TextBlock { Text = SubscribedLabel, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(text);
        row.Children.Add(undo);
        if (ArcMotion.IsFull(this))
        {
            check.AttachedToVisualTree += (_, _) => _ = check.Play(TimeSpan.FromSeconds(0.42), ArcEasings.Standard, TimeSpan.FromSeconds(0.12));
        }
        return row;
    }
}

/// <summary>The pill's idle face.</summary>
internal sealed class ChangelogSubscribeIdle : BlockButton
{
}

/// <summary>The 28 round arrow in the field.</summary>
internal sealed class ChangelogSubmitButton : BlockButton
{
}

/// <summary>Undo in the confirmation.</summary>
internal sealed class ChangelogUndoButton : BlockButton
{
}
