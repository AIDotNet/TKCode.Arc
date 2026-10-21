// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AtomUI.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One notification in the panel (Arc's <c>article.item</c>): the actor's avatar or the tone icon, the title with its
/// unread dot, a one-line preview, the time, and details that open under the text with "Mark read" / "Mark unread"
/// and "Dismiss". The title and preview are one button that expands the row. The row settles in with a 4px rise when
/// it mounts (the panel opening or a view adding it), staggered by its index.
/// </summary>
[TemplatePart(ArticlePart, typeof(Border))]
[TemplatePart(TogglePart, typeof(NotificationCenterButton))]
[TemplatePart(DotPart, typeof(NotificationCenterDot))]
[TemplatePart(DetailsPart, typeof(NotificationCenterReveal))]
[TemplatePart(ReadTogglePart, typeof(NotificationCenterButton))]
[TemplatePart(ReadGlyphPart, typeof(ArcContentSwap))]
[TemplatePart(ReadLabelPart, typeof(ArcContentSwap))]
[TemplatePart(DismissPart, typeof(NotificationCenterButton))]
[PseudoClasses(ReadPseudoClass, ExpandedPseudoClass, HoverPseudoClass, NarrowPseudoClass, ActorPseudoClass,
    SuccessPseudoClass, WarningPseudoClass)]
internal sealed class NotificationCenterRow : TemplatedControl
{
    public const string ArticlePart = "PART_Article";
    public const string TogglePart = "PART_Toggle";
    public const string DotPart = "PART_Dot";
    public const string DetailsPart = "PART_Details";
    public const string ReadTogglePart = "PART_ReadToggle";
    public const string ReadGlyphPart = "PART_ReadGlyph";
    public const string ReadLabelPart = "PART_ReadLabel";
    public const string DismissPart = "PART_Dismiss";

    public const string ReadPseudoClass = ":read";
    public const string ExpandedPseudoClass = ":expanded";
    public const string HoverPseudoClass = ":hover";
    public const string NarrowPseudoClass = ":narrow";
    public const string ActorPseudoClass = ":actor";
    public const string SuccessPseudoClass = ":success";
    public const string WarningPseudoClass = ":warning";

    /// <summary>Rows settle in 4px below their place, 35ms apart, up to the eighth row (Arc's item-in).</summary>
    private const double RiseOffset = 4;

    public static readonly StyledProperty<string?> TitleTextProperty =
        AvaloniaProperty.Register<NotificationCenterRow, string?>(nameof(TitleText));

    public static readonly StyledProperty<string?> PreviewTextProperty =
        AvaloniaProperty.Register<NotificationCenterRow, string?>(nameof(PreviewText));

    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<NotificationCenterRow, string?>(nameof(TimeText));

    public static readonly StyledProperty<string?> DetailTextProperty =
        AvaloniaProperty.Register<NotificationCenterRow, string?>(nameof(DetailText));

    public static readonly StyledProperty<string?> ActorNameProperty =
        AvaloniaProperty.Register<NotificationCenterRow, string?>(nameof(ActorName));

    public static readonly StyledProperty<ImageSource?> ActorSourceProperty =
        AvaloniaProperty.Register<NotificationCenterRow, ImageSource?>(nameof(ActorSource));

    public static readonly StyledProperty<Geometry?> EventGlyphProperty =
        AvaloniaProperty.Register<NotificationCenterRow, Geometry?>(nameof(EventGlyph));

    public static readonly StyledProperty<string?> DismissTextProperty =
        AvaloniaProperty.Register<NotificationCenterRow, string?>(nameof(DismissText));

    private readonly NotificationCenter _owner;
    private readonly TranslateTransform _rise = new();
    private MotionValue<double>? _riseProgress;
    private Border? _article;
    private NotificationCenterButton? _toggle;
    private NotificationCenterDot? _dot;
    private NotificationCenterReveal? _details;
    private NotificationCenterButton? _readToggle;
    private ArcContentSwap? _readGlyph;
    private ArcContentSwap? _readLabel;
    private NotificationCenterButton? _dismiss;
    private bool _isRead;
    private bool _expanded;
    private string? _readLabelText;

    public NotificationCenterRow(NotificationCenter owner, NotificationItem item)
    {
        _owner = owner;
        Item = item;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
    }

    static NotificationCenterRow()
    {
        FocusableProperty.OverrideDefaultValue<NotificationCenterRow>(false);
    }

    public NotificationItem Item { get; set; }

    public string? TitleText { get => GetValue(TitleTextProperty); set => SetValue(TitleTextProperty, value); }
    public string? PreviewText { get => GetValue(PreviewTextProperty); set => SetValue(PreviewTextProperty, value); }
    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }
    public string? DetailText { get => GetValue(DetailTextProperty); set => SetValue(DetailTextProperty, value); }
    public string? ActorName { get => GetValue(ActorNameProperty); set => SetValue(ActorNameProperty, value); }
    public ImageSource? ActorSource { get => GetValue(ActorSourceProperty); set => SetValue(ActorSourceProperty, value); }
    public Geometry? EventGlyph { get => GetValue(EventGlyphProperty); set => SetValue(EventGlyphProperty, value); }
    public string? DismissText { get => GetValue(DismissTextProperty); set => SetValue(DismissTextProperty, value); }

    /// <summary>Whether the details are open.</summary>
    public bool IsExpanded => _expanded;

    /// <summary>The title-and-preview button (focus target after marking or dismissing a neighbor).</summary>
    public NotificationCenterButton? Toggle => _toggle;

    internal NotificationCenterButton? ReadToggle => _readToggle;

    internal NotificationCenterButton? DismissButton => _dismiss;

    internal NotificationCenterReveal? Details => _details;

    internal NotificationCenterDot? Dot => _dot;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _article = e.NameScope.Find<Border>(ArticlePart);
        _toggle = e.NameScope.Find<NotificationCenterButton>(TogglePart);
        _dot = e.NameScope.Find<NotificationCenterDot>(DotPart);
        _details = e.NameScope.Find<NotificationCenterReveal>(DetailsPart);
        _readToggle = e.NameScope.Find<NotificationCenterButton>(ReadTogglePart);
        _readGlyph = e.NameScope.Find<ArcContentSwap>(ReadGlyphPart);
        _readLabel = e.NameScope.Find<ArcContentSwap>(ReadLabelPart);
        _dismiss = e.NameScope.Find<NotificationCenterButton>(DismissPart);
        if (_article is not null)
        {
            _article.RenderTransform = _rise;
            _article.Opacity = Math.Clamp(_riseProgress?.Value ?? 1, 0, 1);
        }
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        if (_readToggle is not null)
        {
            _readToggle.Click += OnReadToggleClick;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click += OnDismissClick;
        }
        // The first state of every part lands without motion.
        _dot?.SetPresent(!_isRead, animate: false);
        _details?.Set(_expanded);
        _readLabelText = null;
        UpdateReadAction(animate: false);
        UpdateAutomation();
    }

    private void Detach()
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        if (_readToggle is not null)
        {
            _readToggle.Click -= OnReadToggleClick;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismissClick;
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.ToggleExpanded(Item);
    }

    private void OnReadToggleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.ToggleRead(Item, _readToggle?.IsKeyboardActivation == true);
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.DismissItem(Item, _dismiss?.IsKeyboardActivation == true);
    }

    /// <summary>
    /// Shows <paramref name="item"/> in its current state. A row that becomes read loses its dot after
    /// <paramref name="exitDelay"/> (the bulk cascade).
    /// </summary>
    public void Update(NotificationItem item, bool expanded, bool animate, TimeSpan exitDelay)
    {
        Item = item;
        var labels = _owner.Labels;
        var actor = item.HasActor ? item.ActorName ?? string.Empty : null;
        TitleText = item.Title;
        TimeText = item.Time;
        PreviewText = item.Description ?? (actor is not null ? labels.FromActor(actor) : labels.ViewDetails);
        DetailText = item.Description ?? (actor is not null ? labels.SharedUpdate(actor) : labels.ReadyToReview);
        ActorName = item.ActorName;
        ActorSource = item.ActorSource;
        DismissText = labels.Dismiss;
        EventGlyph = item.Tone switch
        {
            NotificationTone.Success => ArcIcons.CircleCheck,
            NotificationTone.Warning => ArcIcons.TriangleAlert,
            _ => ArcIcons.MessageCircle
        };
        PseudoClasses.Set(ActorPseudoClass, item.HasActor);
        PseudoClasses.Set(SuccessPseudoClass, item.Tone == NotificationTone.Success);
        PseudoClasses.Set(WarningPseudoClass, item.Tone == NotificationTone.Warning);

        _isRead = item.IsRead;
        PseudoClasses.Set(ReadPseudoClass, _isRead);
        _dot?.SetPresent(!_isRead, animate, exitDelay);
        UpdateReadAction(animate);

        if (expanded != _expanded)
        {
            _expanded = expanded;
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            if (_details is not null)
            {
                if (expanded)
                {
                    _details.Show(animate);
                }
                else
                {
                    _details.Hide(animate);
                }
            }
        }
        UpdateAutomation();
    }

    /// <summary>The Mark read / Mark unread action: the glyph pops and the label rises into the new state.</summary>
    private void UpdateReadAction(bool animate)
    {
        var labels = _owner.Labels;
        var text = _isRead ? labels.MarkUnread : labels.MarkRead;
        if (_readGlyph is null || _readLabel is null || text == _readLabelText)
        {
            return;
        }
        _readLabelText = text;
        if (!animate)
        {
            // Rebuilt in place: the first content of a swap never animates.
            _readGlyph.Mode = ContentSwapMode.None;
            _readLabel.Mode = ContentSwapMode.None;
        }
        _readGlyph.Content = new ArcGlyph
        {
            Data = _isRead ? ArcIcons.CircleDot : ArcIcons.Check,
            Size = 14,
            StrokeThickness = 1.75
        };
        _readLabel.Content = text;
        _readGlyph.Mode = ContentSwapMode.Pop;
        _readLabel.Mode = ContentSwapMode.BlurSwap;
        if (_readToggle is not null)
        {
            AutomationProperties.SetName(_readToggle, text);
        }
    }

    private void UpdateAutomation()
    {
        var labels = _owner.Labels;
        var name = $"{Item.Title}{(Item.IsRead ? string.Empty : ", " + labels.UnreadState)}. {(_expanded ? labels.HideDetails : labels.ShowDetails)}";
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, name);
            _toggle.IsOn = _expanded;
        }
        if (_dismiss is not null)
        {
            AutomationProperties.SetName(_dismiss, labels.Dismiss);
        }
        AutomationProperties.SetName(this, Item.Title);
    }

    /// <summary>
    /// Arc's item-in: the row fades in from 4px below its place over 240ms on the enter curve after
    /// <paramref name="delay"/> (full motion only; the rows of a reduced or still panel are simply there).
    /// </summary>
    public void PlayRise(TimeSpan delay)
    {
        _riseProgress ??= CreateRise();
        if (!IsLoaded && TopLevel.GetTopLevel(this) is null)
        {
            _riseProgress.Jump(1);
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            _riseProgress.Jump(1);
            return;
        }
        _riseProgress.Jump(0);
        _ = _riseProgress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
    }

    private MotionValue<double> CreateRise()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += p =>
        {
            if (_article is not null)
            {
                _article.Opacity = Math.Clamp(p, 0, 1);
            }
            _rise.Y = RiseOffset * (1 - p);
        };
        return value;
    }

    /// <summary>Moves focus to the row's title button.</summary>
    public bool FocusToggle(NavigationMethod method) => _toggle?.Focus(method) == true;

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

    /// <summary>Applies the panel's narrow layout (viewport ≤ 380).</summary>
    public void SetNarrow(bool narrow) => PseudoClasses.Set(NarrowPseudoClass, narrow);
}
