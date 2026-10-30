using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="Card.DetailsOpenChanged"/>.</summary>
public sealed class CardDetailsOpenChangedEventArgs : EventArgs
{
    public CardDetailsOpenChangedEventArgs(bool isOpen, DismissReason reason)
    {
        IsOpen = isOpen;
        Reason = reason;
    }

    /// <summary>The new state of the quick look.</summary>
    public bool IsOpen { get; }

    /// <summary>Why it closed (<see cref="DismissReason.Programmatic"/> when it opened).</summary>
    public DismissReason Reason { get; }
}

/// <summary>
/// Arc card: a contained group of related content and actions for browsable grid items (projects, listings, posts),
/// with optional media, an owner byline and a status line that updates in place. A pointed-at card lifts 2px on a snappy
/// spring while its border and shadow deepen and the media drifts to 1.04 (mouse only).
/// <para>
/// Set <see cref="Details"/> and the title becomes a button and the whole card a trigger: the card grows into a larger
/// quick look without leaving the page (P8): its surface, media, title, description, byline and action fly into a
/// <see cref="DialogSurface"/> on the overlay layer on one critically damped spring (0.3s; 0.26s back) while the corners
/// morph, the details rise in after 120ms and the page dims behind a 7px backdrop blur. Escape, the scrim or the close
/// button send it back into the card and return focus to the title. Reduced motion fades the quick look instead.
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(Control))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(StatusPart, typeof(Control))]
[PseudoClasses(HoverPseudoClass, PressedPseudoClass, InteractivePseudoClass, TriggerFocusPseudoClass, QuickLookPseudoClass)]
[PseudoClasses(HasMediaPseudoClass, HasDescriptionPseudoClass, HasContentPseudoClass, HasFooterPseudoClass, HasBylinePseudoClass,
    HasAvatarPseudoClass, HasMetaPseudoClass, HasStatusPseudoClass, HasActionPseudoClass)]
public class Card : ContentControl
{
    public const string TitlePart = "PART_Title";
    public const string SurfacePart = "PART_Surface";
    public const string StatusPart = "PART_Status";
    public const string HoverPseudoClass = ":hover";
    public const string PressedPseudoClass = ":pressed";
    public const string InteractivePseudoClass = ":interactive";
    public const string TriggerFocusPseudoClass = ":trigger-focus";
    public const string QuickLookPseudoClass = ":quick-look";
    public const string HasMediaPseudoClass = ":has-media";
    public const string HasDescriptionPseudoClass = ":has-description";
    public const string HasContentPseudoClass = ":has-content";
    public const string HasFooterPseudoClass = ":has-footer";
    public const string HasBylinePseudoClass = ":has-byline";
    public const string HasAvatarPseudoClass = ":has-avatar";
    public const string HasMetaPseudoClass = ":has-meta";
    public const string HasStatusPseudoClass = ":has-status";
    public const string HasActionPseudoClass = ":has-action";

    /// <summary>Arc's ZOOM: the photo drifts to 1.04 while the card is pointed at.</summary>
    private const double HoverZoom = 1.04;

    /// <summary>Description line length in "0" widths (Arc: max-width 34ch).</summary>
    private const double DescriptionChars = 34;

    /// <summary>One critically damped spring carries the surface both ways; closing is a touch quicker (Arc grow / settle).</summary>
    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.3, 0);

    private static readonly SpringSpec s_settle = SpringSpec.FromVisual(0.26, 0);

    #region 公共属性定义

    /// <summary>Heading (required). Becomes the quick look trigger when <see cref="Details"/> is set.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Card, string?>(nameof(Title));

    /// <summary>Supporting line under the title (at most 34 characters per line).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Card, string?>(nameof(Description));

    /// <summary>Image or visual at the top; it drifts in slightly while the card is pointed at.</summary>
    public static readonly StyledProperty<object?> MediaProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Media));

    /// <summary>Trailing footer control (a button); stays separately focusable and clickable over the card.</summary>
    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Action));

    /// <summary>Small leading footer visual, such as the owner's <see cref="TKCode.Arc.Avatar"/> (shown in a 32px circle).</summary>
    public static readonly StyledProperty<object?> AvatarProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Avatar));

    /// <summary>Who the card belongs to, such as the owner's name.</summary>
    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Meta));

    /// <summary>A short status under the meta ("Updated 2 hours ago"); new words rise in and are announced politely.</summary>
    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<Card, string?>(nameof(Status));

    /// <summary>Quick look content. When set, the whole card opens and grows into a larger view.</summary>
    public static readonly StyledProperty<object?> DetailsProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Details));

    /// <summary>Quick look open state (Arc open / defaultOpen / onOpenChange). Two-way; ignored without <see cref="Details"/>.</summary>
    public static readonly StyledProperty<bool> IsDetailsOpenProperty =
        AvaloniaProperty.Register<Card, bool>(nameof(IsDetailsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Scrim behind the quick look (the theme sets Arc's oklch(10% 0 0 / 0.46)).</summary>
    public static readonly StyledProperty<IBrush?> ScrimBrushProperty =
        AvaloniaProperty.Register<Card, IBrush?>(nameof(ScrimBrush));

    /// <summary>Backdrop blur behind the quick look in px (the theme sets Arc's 7; off under ReduceTransparency).</summary>
    public static readonly StyledProperty<double> BackdropBlurProperty =
        AvaloniaProperty.Register<Card, double>(nameof(BackdropBlur));

    /// <summary>Accessible name of the quick look's close button.</summary>
    public static readonly StyledProperty<string> CloseButtonNameProperty =
        AvaloniaProperty.Register<Card, string>(nameof(CloseButtonName), "Close quick look");

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Media { get => GetValue(MediaProperty); set => SetValue(MediaProperty, value); }
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    public object? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public string? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public object? Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public bool IsDetailsOpen { get => GetValue(IsDetailsOpenProperty); set => SetValue(IsDetailsOpenProperty, value); }
    public IBrush? ScrimBrush { get => GetValue(ScrimBrushProperty); set => SetValue(ScrimBrushProperty, value); }
    public double BackdropBlur { get => GetValue(BackdropBlurProperty); set => SetValue(BackdropBlurProperty, value); }
    public string CloseButtonName { get => GetValue(CloseButtonNameProperty); set => SetValue(CloseButtonNameProperty, value); }

    #endregion

    /// <summary>Raised when the quick look opens or closes (the close motion may still be playing).</summary>
    public event EventHandler<CardDetailsOpenChangedEventArgs>? DetailsOpenChanged;

    private CardParts? _parts;
    private CardTitle? _title;
    private CardQuickLook? _quickLook;
    private ModalPresence? _presence;
    private SharedElementScope? _flight;
    private MotionValue<double>? _zoom;
    private MotionValue<double>? _scrim;
    private MotionValue<double>? _surfaceFade;
    private MotionValue<double>? _closeReveal;
    private MotionValue<double>? _detailsReveal;
    private MotionValue<double>? _quickZoom;
    private readonly ScaleTransform _closeScale = new();
    private readonly TranslateTransform _detailsShift = new();
    private DismissReason _pendingReason = DismissReason.Programmatic;
    private bool _pendingOpen;
    private bool _held;
    private bool _morph;
    private bool _detailsTravel;
    private bool _pressed;
    private IInputElement? _returnFocus;
    private bool _returnFocusVisible;
    private double _measuredFontSize;
    private FontFamily? _measuredFontFamily;

    static Card()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Card>(AutomationControlType.Group);
        FocusableProperty.OverrideDefaultValue<Card>(false);
        FocusModality.Watch();
    }

    /// <summary>Opens the quick look (when <see cref="Details"/> is set).</summary>
    public void OpenDetails() => SetCurrentValue(IsDetailsOpenProperty, true);

    /// <summary>Sends the quick look back into the card.</summary>
    public void CloseDetails() => RequestClose(DismissReason.Programmatic);

    /// <summary>True when the card opens a quick look (it has <see cref="Details"/>).</summary>
    public bool IsInteractive => Details is not null;

    /// <summary>The quick look panel (created on first open).</summary>
    internal CardQuickLook? QuickLook => _quickLook;

    /// <summary>The modal presence of the quick look (created on first open).</summary>
    internal ModalPresence? Presence => _presence;

    /// <summary>The shared-element flight between the card and the quick look (created on first open).</summary>
    internal SharedElementScope? Flight => _flight;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_title is not null)
        {
            _title.Click -= OnTitleClick;
            _title.GotFocus -= OnTitleGotFocus;
            _title.LostFocus -= OnTitleLostFocus;
        }
        base.OnApplyTemplate(e);
        _parts = CardParts.Find(e.NameScope);
        _title = _parts.Title as CardTitle;
        if (_title is not null)
        {
            _title.Click += OnTitleClick;
            _title.GotFocus += OnTitleGotFocus;
            _title.LostFocus += OnTitleLostFocus;
            _title.IsInteractive = IsInteractive;
        }
        if (_parts.Status is { } status)
        {
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        }
        _parts.SetZoom(_zoom?.Value ?? 1);
        _measuredFontSize = 0;
        UpdateSlots();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Arc caps the description at 34ch: measured from the "0" glyph of its own font.
        if (_parts?.Description is { } description &&
            (!description.FontSize.Equals(_measuredFontSize) || !ReferenceEquals(description.FontFamily, _measuredFontFamily)))
        {
            _measuredFontSize = description.FontSize;
            _measuredFontFamily = description.FontFamily;
            var zero = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(description.FontFamily), description.FontSize, null);
            description.MaxWidth = Math.Ceiling(zero.WidthIncludingTrailingWhitespace * DescriptionChars);
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == MediaProperty || change.Property == ActionProperty || change.Property == AvatarProperty ||
                 change.Property == MetaProperty || change.Property == DetailsProperty)
        {
            // Slot controls belong to the card logically, so they can move between the card and the quick look while
            // DataContext, styles and resources keep flowing from here.
            if (change.OldValue is ILogical { LogicalParent: var parent } oldLogical && ReferenceEquals(parent, this))
            {
                LogicalChildren.Remove(oldLogical);
            }
            if (change.NewValue is ILogical { LogicalParent: null } newLogical)
            {
                LogicalChildren.Add(newLogical);
            }
            if (change.Property == DetailsProperty)
            {
                if (_title is not null)
                {
                    _title.IsInteractive = IsInteractive;
                }
                if (change.NewValue is null && IsDetailsOpen)
                {
                    RequestClose(DismissReason.Programmatic);
                }
            }
            UpdateSlots();
        }
        else if (change.Property == ContentProperty || change.Property == DescriptionProperty || change.Property == StatusProperty)
        {
            UpdateSlots();
        }
        else if (change.Property == IsDetailsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenCore();
            }
            else
            {
                CloseCore();
            }
        }
        else if (_presence is not null && (change.Property == ScrimBrushProperty || change.Property == BackdropBlurProperty))
        {
            _presence.ScrimBrush = ScrimBrush;
            _presence.BackdropBlur = BackdropBlur;
        }
    }

    /// <summary>
    /// Places each slot. A control can live in one place only: while the quick look holds the slots, the card keeps their
    /// boxes at their last size (it is hidden behind the flight, or under the scrim when motion is reduced) and the
    /// controls show in the quick look. Plain values (text, data) show in both.
    /// </summary>
    private void UpdateSlots()
    {
        var quick = _quickLook?.Parts;
        Place(_parts?.MediaPresenter, quick?.MediaPresenter, Media);
        Place(_parts?.AvatarPresenter, quick?.AvatarPresenter, Avatar);
        Place(_parts?.MetaPresenter, quick?.MetaPresenter, Meta);
        Place(_parts?.ActionPresenter, quick?.ActionPresenter, Action);
        Place(_parts?.ContentHost, quick?.ContentHost, Content);
        if (quick?.DetailsPresenter is { } details && !ReferenceEquals(details.Content, Details))
        {
            details.Content = Details;
        }

        var hasMedia = Media is not null;
        var hasAvatar = Avatar is not null;
        var hasMeta = Meta is not null;
        var hasStatus = !string.IsNullOrEmpty(Status);
        var hasAction = Action is not null;
        var hasFooter = hasAvatar || hasMeta || hasStatus || hasAction;
        SetFlags(hasMedia, hasAvatar, hasMeta, hasStatus, hasAction, hasFooter);
        PseudoClasses.Set(HasDescriptionPseudoClass, !string.IsNullOrEmpty(Description));
        PseudoClasses.Set(HasContentPseudoClass, Content is not null);
        PseudoClasses.Set(InteractivePseudoClass, IsInteractive);
        if (_quickLook is { } look)
        {
            look.SetFlag(HasContentPseudoClass, Content is not null);
        }
    }

    private void SetFlags(bool media, bool avatar, bool meta, bool status, bool action, bool footer)
    {
        PseudoClasses.Set(HasMediaPseudoClass, media);
        PseudoClasses.Set(HasAvatarPseudoClass, avatar);
        PseudoClasses.Set(HasMetaPseudoClass, meta);
        PseudoClasses.Set(HasStatusPseudoClass, status);
        PseudoClasses.Set(HasActionPseudoClass, action);
        PseudoClasses.Set(HasFooterPseudoClass, footer);
        PseudoClasses.Set(HasBylinePseudoClass, avatar || meta || status);
        if (_quickLook is { } look)
        {
            look.SetFlag(HasMediaPseudoClass, media);
            look.SetFlag(HasAvatarPseudoClass, avatar);
            look.SetFlag(HasMetaPseudoClass, meta);
            look.SetFlag(HasStatusPseudoClass, status);
            look.SetFlag(HasActionPseudoClass, action);
            look.SetFlag(HasFooterPseudoClass, footer);
            look.SetFlag(HasBylinePseudoClass, avatar || meta || status);
        }
    }

    private void Place(ContentPresenter? card, ContentPresenter? quick, object? value)
    {
        if (value is Control control)
        {
            if (_held && quick is not null)
            {
                if (card is not null && ReferenceEquals(card.Content, control))
                {
                    Lock(card);
                    card.Content = null;
                }
                quick.Content = control;
                return;
            }
            if (quick is not null && ReferenceEquals(quick.Content, control))
            {
                quick.Content = null;
            }
            if (card is not null)
            {
                card.Content = control;
                Unlock(card);
            }
            return;
        }
        if (card is not null)
        {
            Unlock(card);
            card.Content = value;
        }
        if (quick is not null)
        {
            quick.Content = value;
        }
    }

    private static void Lock(Control presenter)
    {
        var size = presenter.Bounds.Size;
        if (size.Width > 0 && size.Height > 0)
        {
            presenter.Width = size.Width;
            presenter.Height = size.Height;
        }
    }

    private static void Unlock(Control presenter)
    {
        presenter.ClearValue(WidthProperty);
        presenter.ClearValue(HeightProperty);
    }

    #endregion

    #region Pointer, focus and hover

    private void OnTitleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenDetails();
    }

    private void OnTitleGotFocus(object? sender, FocusChangedEventArgs e) =>
        PseudoClasses.Set(TriggerFocusPseudoClass, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);

    private void OnTitleLostFocus(object? sender, FocusChangedEventArgs e) => PseudoClasses.Set(TriggerFocusPseudoClass, false);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Touch and pen never lift (Arc's hover only answers a fine pointer).
        if (e.Pointer.Type == PointerType.Mouse && !IsDetailsOpen)
        {
            SetHover(true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // The title's hit area stretches over the whole card; nested actions handle their own presses first.
        if (e.Handled || !IsInteractive || IsDetailsOpen || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        PseudoClasses.Set(PressedPseudoClass, true);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        EndPress();
        e.Pointer.Capture(null);
        e.Handled = true;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            // Like a Radix trigger: the pressed title takes focus, so closing returns focus to it.
            _title?.Focus(NavigationMethod.Pointer);
            OpenDetails();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPress();
    }

    private void EndPress()
    {
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    private void SetHover(bool hovered)
    {
        if (PseudoClasses.Contains(HoverPseudoClass) == hovered)
        {
            return;
        }
        PseudoClasses.Set(HoverPseudoClass, hovered);
        // The photo drifts in slowly while the card is pointed at (considered × 2), and eases back a little faster.
        // Code-driven: the in and out legs use different durations, which a ControlTheme transition cannot pick by direction.
        _zoom ??= CreateValue(1, v => _parts?.SetZoom(v));
        if (!ArcMotion.IsFull(this))
        {
            _zoom.Jump(1);
            return;
        }
        var duration = hovered ? ArcMotionPresets.Considered * 2 : ArcMotionPresets.Considered;
        _ = _zoom.AnimateTo(hovered ? HoverZoom : 1, MotionSpec.Tween(duration, ArcEasings.Standard));
    }

    #endregion

    #region Quick look

    private MotionValue<double> CreateValue(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    private void EnsureQuickLook()
    {
        if (_quickLook is not null)
        {
            return;
        }
        var look = new CardQuickLook();
        look.Bind(ModalSurface.TitleProperty, this.GetObservable(TitleProperty));
        look.Bind(ModalSurface.DescriptionProperty, this.GetObservable(DescriptionProperty));
        look.Bind(ModalSurface.CloseButtonNameProperty, this.GetObservable(CloseButtonNameProperty));
        look.Bind(CardQuickLook.StatusProperty, this.GetObservable(StatusProperty));
        look.CloseRequested += (_, _) => RequestClose(DismissReason.CloseButton);
        look.PartsChanged += (_, _) => OnQuickLookParts();
        LogicalChildren.Add(look);
        _quickLook = look;

        _presence = new ModalPresence(look, this)
        {
            Entrance = ModalEntrance.Custom,
            ContainmentRoot = this,
            CustomMotion = RunQuickLookMotion
        };
        _presence.Dismissing += (_, e) =>
        {
            // The card owns the decision, so IsDetailsOpen (and its two-way bindings) sees every close.
            e.Cancel = true;
            RequestClose(e.Reason);
        };
        _presence.Closed += OnPresenceClosed;
        _flight = new SharedElementScope(this, look) { RevealsSourceOnArrival = false };

        _scrim = CreateValue(0, v => _presence.ScrimOpacity = v);
        _surfaceFade = CreateValue(1, v => look.Opacity = Math.Clamp(v, 0, 1));
        _closeReveal = CreateValue(0, ApplyCloseReveal);
        _detailsReveal = CreateValue(0, ApplyDetailsReveal);
        _quickZoom = CreateValue(1, v => look.Parts?.SetZoom(v));
    }

    private void OnQuickLookParts()
    {
        if (_quickLook?.Parts is not { } parts)
        {
            return;
        }
        if (parts.CloseSlot is { } close)
        {
            close.RenderTransformOrigin = RelativePoint.Center;
            close.RenderTransform = _closeScale;
        }
        if (parts.Details is { } details)
        {
            details.RenderTransform = _detailsShift;
        }
        ApplyCloseReveal(_closeReveal?.Value ?? 1);
        ApplyDetailsReveal(_detailsReveal?.Value ?? 1);
        parts.SetZoom(_quickZoom?.Value ?? 1);
        UpdateSlots();
    }

    private void ApplyCloseReveal(double p)
    {
        if (_quickLook?.Parts?.CloseSlot is not { } close)
        {
            return;
        }
        close.Opacity = Math.Clamp(p, 0, 1);
        var scale = _morph ? 0.9 + 0.1 * p : 1;
        _closeScale.ScaleX = scale;
        _closeScale.ScaleY = scale;
    }

    private void ApplyDetailsReveal(double p)
    {
        if (_quickLook?.Parts?.Details is not { } details)
        {
            return;
        }
        details.Opacity = Math.Clamp(p, 0, 1);
        _detailsShift.Y = _detailsTravel ? 6 * (1 - p) : 0;
    }

    private void RequestClose(DismissReason reason)
    {
        if (!IsDetailsOpen)
        {
            return;
        }
        _pendingReason = reason;
        SetCurrentValue(IsDetailsOpenProperty, false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_pendingOpen && IsDetailsOpen)
        {
            Dispatcher.Post(OpenCore);
        }
    }

    private void OpenCore()
    {
        if (!IsDetailsOpen)
        {
            return;
        }
        // Arc: open = details ? open : false.
        if (Details is null)
        {
            SetCurrentValue(IsDetailsOpenProperty, false);
            return;
        }
        if (TopLevel.GetTopLevel(this) is null)
        {
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        EnsureQuickLook();
        var presence = _presence!;
        var look = _quickLook!;
        if (presence.State == ModalState.Closed)
        {
            // Decided at mount, like Arc's morph flag: a quick look revived mid-return keeps the path it came in on.
            _morph = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
            _quickZoom!.Jump(_zoom?.Value ?? 1);
            _surfaceFade!.Jump(_morph ? 1 : 0);
            _closeReveal!.Jump(0);
            _detailsReveal!.Jump(0);
            _scrim!.Jump(0);
        }
        presence.ScrimBrush = ScrimBrush;
        presence.BackdropBlur = BackdropBlur;
        look.DescriptionMeasure = _parts?.Description is { IsEffectivelyVisible: true, Bounds.Width: > 0 } description
            ? Math.Ceiling(description.Bounds.Width)
            : double.PositiveInfinity;
        look.SetFlag(CardQuickLook.ReturningPseudoClass, false);
        SetHover(false);
        _zoom?.Jump(1);
        // The card is the modal's containment root (popups of the controls it lends to the quick look stay inside the
        // focus trap), so the presence does not treat the title as the opener: the card returns focus itself.
        if (presence.State == ModalState.Closed && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused &&
            (ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused)))
        {
            _returnFocus = focused as IInputElement;
            _returnFocusVisible = focused is StyledElement { Classes: var classes } && classes.Contains(":focus-visible");
        }
        _held = true;
        UpdateSlots();
        PseudoClasses.Set(QuickLookPseudoClass, _morph);
        if (!presence.Open())
        {
            _held = false;
            UpdateSlots();
            PseudoClasses.Set(QuickLookPseudoClass, false);
            _pendingOpen = true;
            return;
        }
        if (KeyboardCaused(_returnFocusVisible) && presence.FindFocusTarget() is InputElement first)
        {
            first.Focus(NavigationMethod.Tab);
        }
        DetailsOpenChanged?.Invoke(this, new CardDetailsOpenChangedEventArgs(true, DismissReason.Programmatic));
    }

    private void CloseCore()
    {
        _pendingOpen = false;
        var reason = _pendingReason;
        _pendingReason = DismissReason.Programmatic;
        if (_presence is null || !_presence.IsOpen)
        {
            return;
        }
        _presence.Close(reason);
        ReturnFocus(reason);
        DetailsOpenChanged?.Invoke(this, new CardDetailsOpenChangedEventArgs(false, reason));
    }

    /// <summary>
    /// The quick look's enter and exit (<see cref="ModalEntrance.Custom"/>). Full motion: the keyed pieces fly out of the
    /// card on the grow spring while the scrim fades in (standard, enter), and the close button (fast) and details
    /// (standard, rising 6px) arrive after 120ms; closing flies them back on the settle spring while the extras fade at
    /// once, then hands the slots back to the card and reveals it in the same frame. Reduced and Off fade only.
    /// Code-driven: the panel lives on the overlay layer outside any theme, its exit gates the removal, a reopen must
    /// reverse with the spring's velocity, and the flight reads boxes from two visual trees every frame.
    /// </summary>
    private async Task<bool> RunQuickLookMotion(ModalPresence presence, bool entering)
    {
        var look = _quickLook!;
        var flight = _flight!;
        if (entering)
        {
            _held = true;
            UpdateSlots();
            look.SetFlag(CardQuickLook.ReturningPseudoClass, false);
            PseudoClasses.Set(QuickLookPseudoClass, _morph);
            var instant = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
            if (!_morph)
            {
                _closeReveal!.Jump(1);
                _detailsTravel = false;
                _detailsReveal!.Jump(1);
                _quickZoom!.Jump(1);
                var fadeIn = _surfaceFade!.AnimateTo(1, instant);
                var scrimIn = _scrim!.AnimateTo(1, instant);
                return await fadeIn & await scrimIn;
            }
            _surfaceFade!.Jump(1);
            _detailsTravel = true;
            var reveal = ArcMotionPresets.Instant;
            _ = _closeReveal!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, reveal));
            _ = _detailsReveal!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, reveal));
            _ = _quickZoom!.AnimateTo(1, MotionSpec.Spring(s_grow));
            var scrim = _scrim!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
            var grown = await flight.FlyAsync(1, MotionSpec.Spring(s_grow));
            return grown & await scrim;
        }

        if (!_morph)
        {
            var fadeOut = _surfaceFade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            var scrimOut = _scrim!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            var gone = await fadeOut & await scrimOut;
            if (gone)
            {
                HandBack();
            }
            return gone;
        }
        // Closing: the panel takes the card's look and lands exactly where the card sits, then hands back without a seam.
        look.SetFlag(CardQuickLook.ReturningPseudoClass, true);
        var fade = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _detailsTravel = false;
        _ = _closeReveal!.AnimateTo(0, fade);
        _ = _detailsReveal!.AnimateTo(0, fade);
        var scrimAway = _scrim!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        var landed = await flight.FlyAsync(0, MotionSpec.Spring(s_settle));
        if (landed)
        {
            HandBack();
            flight.RevealSource();
            _surfaceFade!.Jump(0);
        }
        return landed & await scrimAway;
    }

    /// <summary>Escape or the close button returns focus to the title (Radix returns it to the trigger).</summary>
    private void ReturnFocus(DismissReason reason)
    {
        var target = _returnFocus as InputElement;
        var visible = _returnFocusVisible;
        _returnFocus = null;
        _returnFocusVisible = false;
        if (target is null || _presence is null || TopLevel.GetTopLevel(target)?.FocusManager is not { } focus)
        {
            return;
        }
        var current = focus.GetFocusedElement();
        if (current is not null && !_presence.Contains(current))
        {
            return;
        }
        if (target.IsEffectivelyVisible && target.IsEffectivelyEnabled)
        {
            target.Focus(reason == DismissReason.Escape || KeyboardCaused(visible) ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
    }

    /// <summary>
    /// Whether a key opened or closed the quick look: the last press or key of the window, or (before any) whether the
    /// title showed the keyboard ring. A click never lights the ring up, even on a title that had it from the keyboard.
    /// </summary>
    private bool KeyboardCaused(bool titleHadRing) => FocusModality.LastWasPointer(this) is { } pointer ? !pointer : titleHadRing;

    /// <summary>The slots return to the card (it shows again in the same frame).</summary>
    private void HandBack()
    {
        _held = false;
        UpdateSlots();
        PseudoClasses.Set(QuickLookPseudoClass, false);
    }

    private void OnPresenceClosed(object? sender, ModalClosedEventArgs e)
    {
        _flight?.Reset();
        if (_held)
        {
            HandBack();
        }
        if (IsDetailsOpen)
        {
            // Removed without a close request (the card left the window).
            _pendingReason = e.Reason;
            SetCurrentValue(IsDetailsOpenProperty, false);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new CardAutomationPeer(this);

    private sealed class CardAutomationPeer : ControlAutomationPeer
    {
        public CardAutomationPeer(Card owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((Card)Owner).Title ?? base.GetNameCore();

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
