using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using StreamMark.Avalonia;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One answer of the model arena (spec 3.5, 3.6): an area of fixed height (the arena sets 272) that scrolls vertically, with
/// the streamed Markdown (14 / 22.4, 12 between blocks) and, after it, the error card of a failed answer. It is a Tab stop
/// whose keyboard ring (2px, inset) is drawn above the edge fade; ↑ ↓ PageUp PageDown Space Home End scroll it.
/// </summary>
/// <remarks>
/// <para>
/// Text: <see cref="Append"/> passes each release of the arena's pacer to an unpaced <see cref="Markdown"/> (the pacer has cut
/// whole words already), which fades it in over <see cref="RevealFadeDuration"/> at full motion and shows it at once
/// otherwise. <see cref="Reset"/> starts a new round: no text, the top, no error card, following again, all without motion.
/// </para>
/// <para>
/// Following: the scrolling and its follow logic are the AI chat's <see cref="AiFollowScroller"/> without its scroll-to-latest
/// button (the arena has none). While it follows, new text moves the offset 22% of the rest toward the bottom every frame (a
/// jump at Reduced and Off); the person's scroll up (wheel, scrollbar, a drag, ↑ PageUp Home) releases it, and only coming
/// back within 4px of the current bottom follows again (End aims at the bottom of the moment; text that keeps arriving can
/// leave it short, as in Arc).
/// </para>
/// <para>
/// Edge fade: an opacity mask hides the top <see cref="TopFade"/> (8) and bottom <see cref="BottomFade"/> (18) pixels of the
/// scrolling area, which is the padding at the ends, so only scrolled text fades. The keyboard ring is a layer above the mask.
/// </para>
/// <para>
/// Error card (<see cref="ShowError"/>): a 1px Border box, radius 14, 12 14 14 inside and 14 below the text, with CircleAlert
/// 14 in Danger and the title (14 / 500), the text in TextSecondary and a secondary small button (RotateCcw for a retry)
/// that raises <see cref="ActionRequested"/>. It is announced as an alert. Its height opens on the Smooth spring while it
/// fades in (160ms); <see cref="HideError"/> fades it out in 100ms while the height closes on the same spring. Reduced fades
/// in 120ms and moves the height at once (after the fade when it hides); Off switches at once.
/// </para>
/// <para>
/// Accessibility: the answer is a named group (<see cref="AnswerName"/>, "Model A answer"). Arc marks it <c>aria-busy</c>
/// while it streams; Avalonia has no busy state, so <see cref="IsStreaming"/> sets the automation item status to
/// <see cref="StreamingStatus"/> (and the <c>:streaming</c> pseudo-class), and clears it afterwards.
/// </para>
/// </remarks>
[TemplatePart(FollowPart, typeof(AiFollowScroller))]
[TemplatePart(BodyPart, typeof(StackPanel))]
[TemplatePart(MarkdownPart, typeof(Markdown))]
[TemplatePart(ErrorHostPart, typeof(ArenaAnswerErrorHost))]
[TemplatePart(ErrorCardPart, typeof(ArenaAnswerErrorCard))]
[TemplatePart(ErrorTitlePart, typeof(TextBlock))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(ErrorActionPart, typeof(Button))]
[TemplatePart(ErrorActionTextPart, typeof(TextBlock))]
[TemplatePart(ErrorActionIconPart, typeof(ArcGlyph))]
[TemplatePart(FocusRingPart, typeof(Border))]
[PseudoClasses(StreamingPseudoClass, ErrorPseudoClass, RetryPseudoClass)]
internal class ArenaAnswerView : TemplatedControl
{
    public const string FollowPart = "PART_Follow";
    public const string BodyPart = "PART_Body";
    public const string MarkdownPart = "PART_Markdown";
    public const string ErrorHostPart = "PART_ErrorHost";
    public const string ErrorCardPart = "PART_ErrorCard";
    public const string ErrorTitlePart = "PART_ErrorTitle";
    public const string ErrorTextPart = "PART_ErrorText";
    public const string ErrorActionPart = "PART_ErrorAction";
    public const string ErrorActionTextPart = "PART_ErrorActionText";
    public const string ErrorActionIconPart = "PART_ErrorActionIcon";
    public const string FocusRingPart = "PART_FocusRing";

    public const string StreamingPseudoClass = ":streaming";

    /// <summary>Set while the error card is shown (or asked to show).</summary>
    public const string ErrorPseudoClass = ":error";

    /// <summary>Set while the error card's action is a retry (it shows RotateCcw).</summary>
    public const string RetryPseudoClass = ":retry";

    /// <summary>Within this distance (px) of the bottom the answer follows again (spec 9.3).</summary>
    internal const double ReattachDistance = 4;

    /// <summary>How far ↑ and ↓ scroll (the main package's scroll area: the browser's ≈ 40px).</summary>
    internal const double LineStep = 40;

    #region Properties

    /// <summary>The accessible name of the answer ("Model A answer").</summary>
    public static readonly StyledProperty<string?> AnswerNameProperty =
        AvaloniaProperty.Register<ArenaAnswerView, string?>(nameof(AnswerName));

    /// <summary>Whether the answer is still being written (Arc's <c>aria-busy</c>): sets the item status to <see cref="StreamingStatus"/>.</summary>
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<ArenaAnswerView, bool>(nameof(IsStreaming));

    /// <summary>The automation item status while <see cref="IsStreaming"/> ("Writing"); none without one.</summary>
    public static readonly StyledProperty<string?> StreamingStatusProperty =
        AvaloniaProperty.Register<ArenaAnswerView, string?>(nameof(StreamingStatus));

    /// <summary>
    /// How long each appended piece fades in at full motion (160ms, Arc's arena); Reduced and Off show it at once. An answer
    /// keeps the duration it started with; a change applies from the next <see cref="Reset"/>.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> RevealFadeDurationProperty =
        AvaloniaProperty.Register<ArenaAnswerView, TimeSpan>(nameof(RevealFadeDuration), TimeSpan.FromMilliseconds(160));

    /// <summary>
    /// Whether the Markdown paces appended text itself (false, the default: the arena's pacer releases whole words on a
    /// shared clock and each piece shows as one batch).
    /// </summary>
    public static readonly StyledProperty<bool> IsPacedProperty =
        AvaloniaProperty.Register<ArenaAnswerView, bool>(nameof(IsPaced));

    /// <summary>The height (px) the edge fade hides at the top (theme: 8, the top padding).</summary>
    public static readonly StyledProperty<double> TopFadeProperty =
        AvaloniaProperty.Register<ArenaAnswerView, double>(nameof(TopFade), 8);

    /// <summary>The height (px) the edge fade hides at the bottom (theme: 18, the bottom padding).</summary>
    public static readonly StyledProperty<double> BottomFadeProperty =
        AvaloniaProperty.Register<ArenaAnswerView, double>(nameof(BottomFade), 18);

    /// <summary>Whether new text moves the answer to the bottom (read-only).</summary>
    public static readonly DirectProperty<ArenaAnswerView, bool> IsFollowingProperty =
        AvaloniaProperty.RegisterDirect<ArenaAnswerView, bool>(nameof(IsFollowing), o => o.IsFollowing);

    /// <summary>Whether the error card is shown or opening (read-only).</summary>
    public static readonly DirectProperty<ArenaAnswerView, bool> IsErrorShownProperty =
        AvaloniaProperty.RegisterDirect<ArenaAnswerView, bool>(nameof(IsErrorShown), o => o.IsErrorShown);

    /// <summary>The error card's title (read-only; <see cref="ShowError"/> sets it).</summary>
    public static readonly DirectProperty<ArenaAnswerView, string?> ErrorTitleProperty =
        AvaloniaProperty.RegisterDirect<ArenaAnswerView, string?>(nameof(ErrorTitle), o => o.ErrorTitle);

    /// <summary>The error card's text (read-only; <see cref="ShowError"/> sets it).</summary>
    public static readonly DirectProperty<ArenaAnswerView, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<ArenaAnswerView, string?>(nameof(ErrorText), o => o.ErrorText);

    /// <summary>The error card's button label (read-only; <see cref="ShowError"/> sets it).</summary>
    public static readonly DirectProperty<ArenaAnswerView, string?> ErrorActionTextProperty =
        AvaloniaProperty.RegisterDirect<ArenaAnswerView, string?>(nameof(ErrorActionText), o => o.ErrorActionText);

    /// <summary>Raised (bubbling) when the person presses the error card's button.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActionRequestedEvent =
        RoutedEvent.Register<ArenaAnswerView, RoutedEventArgs>(nameof(ActionRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="AnswerNameProperty"/>
    public string? AnswerName { get => GetValue(AnswerNameProperty); set => SetValue(AnswerNameProperty, value); }

    /// <inheritdoc cref="IsStreamingProperty"/>
    public bool IsStreaming { get => GetValue(IsStreamingProperty); set => SetValue(IsStreamingProperty, value); }

    /// <inheritdoc cref="StreamingStatusProperty"/>
    public string? StreamingStatus { get => GetValue(StreamingStatusProperty); set => SetValue(StreamingStatusProperty, value); }

    /// <inheritdoc cref="RevealFadeDurationProperty"/>
    public TimeSpan RevealFadeDuration { get => GetValue(RevealFadeDurationProperty); set => SetValue(RevealFadeDurationProperty, value); }

    /// <inheritdoc cref="IsPacedProperty"/>
    public bool IsPaced { get => GetValue(IsPacedProperty); set => SetValue(IsPacedProperty, value); }

    /// <inheritdoc cref="TopFadeProperty"/>
    public double TopFade { get => GetValue(TopFadeProperty); set => SetValue(TopFadeProperty, value); }

    /// <inheritdoc cref="BottomFadeProperty"/>
    public double BottomFade { get => GetValue(BottomFadeProperty); set => SetValue(BottomFadeProperty, value); }

    /// <inheritdoc cref="IsFollowingProperty"/>
    public bool IsFollowing { get => _isFollowing; private set => SetAndRaise(IsFollowingProperty, ref _isFollowing, value); }

    /// <inheritdoc cref="IsErrorShownProperty"/>
    public bool IsErrorShown { get => _isErrorShown; private set => SetAndRaise(IsErrorShownProperty, ref _isErrorShown, value); }

    /// <inheritdoc cref="ErrorTitleProperty"/>
    public string? ErrorTitle { get => _errorTitle; private set => SetAndRaise(ErrorTitleProperty, ref _errorTitle, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => _errorText; private set => SetAndRaise(ErrorTextProperty, ref _errorText, value); }

    /// <inheritdoc cref="ErrorActionTextProperty"/>
    public string? ErrorActionText { get => _errorActionText; private set => SetAndRaise(ErrorActionTextProperty, ref _errorActionText, value); }

    /// <inheritdoc cref="ActionRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ActionRequested
    {
        add => AddHandler(ActionRequestedEvent, value);
        remove => RemoveHandler(ActionRequestedEvent, value);
    }

    #endregion

    private readonly StringBuilder _text = new();
    private bool _completed;
    private bool _isFollowing = true;
    private bool _isErrorShown;
    private string? _errorTitle;
    private string? _errorText;
    private string? _errorActionText;
    private bool _isRetry;
    private AiFollowScroller? _follow;
    private StackPanel? _body;
    private Markdown? _markdown;
    private ArenaAnswerErrorHost? _errorHost;
    private ArenaAnswerErrorCard? _errorCard;
    private Button? _errorAction;
    private TextBlock? _errorTitlePart;
    private TextBlock? _errorTextPart;
    private TextBlock? _errorActionTextPart;
    private ArcGlyph? _errorActionIcon;
    private Border? _focusRing;
    private AiFadePresence? _errorPresence;
    private double _maskHeight = double.NaN;

    static ArenaAnswerView()
    {
        FocusableProperty.OverrideDefaultValue<ArenaAnswerView>(true);
    }

    /// <summary>Every text appended since the last <see cref="Reset"/>, as given.</summary>
    internal string Text => _text.ToString();

    /// <summary>Whether <see cref="Complete"/> was called since the last <see cref="Reset"/>.</summary>
    internal bool IsCompleted => _completed;

    /// <summary>The follow scroller part.</summary>
    internal AiFollowScroller? Follow => _follow;

    /// <summary>The scroll area inside the follow scroller.</summary>
    internal ScrollArea? Scroll => _follow?.Scroll;

    /// <summary>The Markdown part.</summary>
    internal Markdown? Body => _markdown;

    /// <summary>The error card's slot.</summary>
    internal ArenaAnswerErrorHost? ErrorHost => _errorHost;

    /// <summary>The error card.</summary>
    internal ArenaAnswerErrorCard? ErrorCard => _errorCard;

    /// <summary>The error card's button.</summary>
    internal Button? ErrorAction => _errorAction;

    /// <summary>The keyboard ring layer.</summary>
    internal Border? FocusRing => _focusRing;

    #region Text

    /// <summary>
    /// Shows the next piece of the answer (the pacer's release, whole words) at the end, fading in at full motion. Ignored
    /// after <see cref="Complete"/> until <see cref="Reset"/>.
    /// </summary>
    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text) || _completed)
        {
            return;
        }
        _text.Append(text);
        _markdown?.Append(text);
    }

    /// <summary>Ends the answer: nothing more is appended until <see cref="Reset"/>.</summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }
        _completed = true;
        _markdown?.Complete();
    }

    /// <summary>
    /// Starts a new round without motion (spec 5.1): the text goes, the offset returns to the top and follows again, and
    /// the error card closes at once.
    /// </summary>
    public void Reset()
    {
        _text.Clear();
        _completed = false;
        ClearMarkdown();
        HideErrorNow();
        _follow?.ResetToTop();
        IsFollowing = _follow?.IsFollowing ?? true;
    }

    private void ClearMarkdown()
    {
        if (_markdown is not { } markdown)
        {
            return;
        }
        // The view drops its blocks at once when its source changes; the next layout is empty.
        markdown.SetCurrentValue(Markdown.TextProperty, null);
        if (markdown.Stream is not null)
        {
            markdown.SetCurrentValue(Markdown.StreamProperty, null);
        }
    }

    #endregion

    #region Error card

    /// <summary>
    /// Shows the error card after the text: <paramref name="title"/> ("Connection dropped"), <paramref name="text"/> and a
    /// button labelled <paramref name="actionText"/> ("Retry model A", or "Try a sample prompt") that raises
    /// <see cref="ActionRequested"/>; <paramref name="isRetry"/> puts RotateCcw before the label (Arc shows it only for a
    /// retry). Calling it again while shown changes the texts in place. Call <see cref="Complete"/> first: the text of a
    /// failed answer is final, and StreamMark draws a caret at the end of a stream that has not completed.
    /// </summary>
    public void ShowError(string title, string text, string actionText, bool isRetry = true)
    {
        ErrorTitle = title;
        ErrorText = text;
        ErrorActionText = actionText;
        _isRetry = isRetry;
        PseudoClasses.Set(RetryPseudoClass, isRetry);
        var opening = !_isErrorShown;
        IsErrorShown = true;
        UpdateCardTexts();
        if (!opening)
        {
            return;
        }
        PseudoClasses.Set(ErrorPseudoClass, true);
        _errorPresence?.SetShown(true);
        if (_errorHost is not null)
        {
            _errorHost.IsOpen = true;
        }
    }

    /// <summary>
    /// Hides the error card: it fades out in 100ms while its height closes on the Smooth spring (Reduced: a 120ms fade, then
    /// the height at once; Off: at once). A keyboard focus on its button moves to the answer.
    /// </summary>
    public void HideError()
    {
        if (!_isErrorShown)
        {
            return;
        }
        SetErrorHidden();
        if (_errorPresence is not { } presence || _errorHost is not { } host)
        {
            return;
        }
        presence.SetShown(false);
        // Reduced keeps the room while the card fades and then closes it at once (OnErrorFadedOut); Full closes on the spring
        // while it fades; Off has hidden the card already.
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Reduced || !presence.IsAnimating)
        {
            host.IsOpen = false;
        }
    }

    /// <summary>Hides the card without any motion (a new round).</summary>
    private void HideErrorNow()
    {
        if (_isErrorShown)
        {
            SetErrorHidden();
        }
        if (_errorCard is { } card)
        {
            ResetPresence(card);
        }
        if (_errorHost is { } host)
        {
            host.IsOpen = false;
            host.Snap();
        }
    }

    private void SetErrorHidden()
    {
        if (_errorCard is { IsKeyboardFocusWithin: true } && _errorAction is { } action)
        {
            // The button is going away under the keyboard: stay in the answer (its ring shows for a keyboard user).
            Focus(action.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
        IsErrorShown = false;
        PseudoClasses.Set(ErrorPseudoClass, false);
        if (_errorCard is not null)
        {
            // Cleared so the next card is announced even with the same words.
            AutomationProperties.SetName(_errorCard, null);
        }
    }

    /// <summary>The card's words, glyph and names (its parts are hosted by the scroll area, so no TemplateBinding reaches them).</summary>
    private void UpdateCardTexts()
    {
        if (_errorTitlePart is not null)
        {
            _errorTitlePart.Text = ErrorTitle;
        }
        if (_errorTextPart is not null)
        {
            _errorTextPart.Text = ErrorText;
        }
        if (_errorActionTextPart is not null)
        {
            _errorActionTextPart.Text = ErrorActionText;
        }
        if (_errorActionIcon is not null)
        {
            _errorActionIcon.IsVisible = _isRetry;
        }
        if (_errorAction is not null)
        {
            AutomationProperties.SetName(_errorAction, ErrorActionText);
        }
        if (_errorCard is not null)
        {
            // Named only while shown: a hidden card says nothing, and the next one is announced even with the same words.
            AutomationProperties.SetName(_errorCard, _isErrorShown ? CardName() : null);
        }
    }

    private string? CardName()
    {
        var title = ErrorTitle;
        var text = ErrorText;
        return string.IsNullOrEmpty(title) ? text : string.IsNullOrEmpty(text) ? title : title + ". " + text;
    }

    private void ResetPresence(ArenaAnswerErrorCard card)
    {
        if (_errorPresence is not null)
        {
            _errorPresence.Exited -= OnErrorFadedOut;
            _errorPresence.Dispose();
        }
        _errorPresence = new AiFadePresence(card, _isErrorShown);
        _errorPresence.Exited += OnErrorFadedOut;
    }

    private void OnErrorFadedOut(object? sender, EventArgs e)
    {
        if (!_isErrorShown && _errorHost is not null)
        {
            _errorHost.IsOpen = false;
        }
    }

    private void OnErrorActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ActionRequestedEvent, this));
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _follow = e.NameScope.Find<AiFollowScroller>(FollowPart);
        _body = e.NameScope.Find<StackPanel>(BodyPart);
        _markdown = e.NameScope.Find<Markdown>(MarkdownPart);
        _errorHost = e.NameScope.Find<ArenaAnswerErrorHost>(ErrorHostPart);
        _errorCard = e.NameScope.Find<ArenaAnswerErrorCard>(ErrorCardPart);
        _errorAction = e.NameScope.Find<Button>(ErrorActionPart);
        _errorTitlePart = e.NameScope.Find<TextBlock>(ErrorTitlePart);
        _errorTextPart = e.NameScope.Find<TextBlock>(ErrorTextPart);
        _errorActionTextPart = e.NameScope.Find<TextBlock>(ErrorActionTextPart);
        _errorActionIcon = e.NameScope.Find<ArcGlyph>(ErrorActionIconPart);
        _focusRing = e.NameScope.Find<Border>(FocusRingPart);
        Hook();
    }

    private void Hook()
    {
        if (_follow is { } follow)
        {
            follow.ReattachDistance = ReattachDistance;
            follow.FollowingChanged += OnFollowingChanged;
            IsFollowing = follow.IsFollowing;
            _maskHeight = double.NaN;
            UpdateMask();
        }
        if (_body is not null)
        {
            _body.Margin = Padding;
        }
        if (_markdown is { } markdown)
        {
            markdown.TemplateApplied += OnMarkdownTemplateApplied;
            ApplyMarkdownOptions();
            ApplyMarkdownResources();
            if (_text.Length > 0)
            {
                // A new template mid-answer: what was shown comes back as one piece.
                markdown.Append(_text.ToString());
            }
            if (_completed)
            {
                markdown.Complete();
            }
        }
        if (_errorAction is { } action)
        {
            action.Click += OnErrorActionClick;
        }
        if (_errorCard is { } card)
        {
            ResetPresence(card);
        }
        UpdateCardTexts();
        if (_errorHost is { } host)
        {
            host.IsOpen = _isErrorShown;
            host.Snap();
        }
    }

    private void Unhook()
    {
        if (_follow is { } follow)
        {
            follow.FollowingChanged -= OnFollowingChanged;
            follow.OpacityMask = null;
        }
        if (_markdown is { } markdown)
        {
            markdown.TemplateApplied -= OnMarkdownTemplateApplied;
        }
        if (_errorAction is { } action)
        {
            action.Click -= OnErrorActionClick;
        }
        if (_errorPresence is not null)
        {
            _errorPresence.Exited -= OnErrorFadedOut;
            _errorPresence.Dispose();
            _errorPresence = null;
        }
    }

    private static void OnMarkdownTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // The text is selectable with the pointer but is not a Tab stop: the answer itself is (Arc: tabindex 0 on the scroller).
        if (e.NameScope.Find<Control>(Markdown.ViewPart) is { } view)
        {
            KeyboardNavigation.SetIsTabStop(view, false);
        }
    }

    private void ApplyMarkdownOptions()
    {
        if (_markdown is not { } markdown)
        {
            return;
        }
        markdown.IsPaced = IsPaced;
        markdown.RevealFadeDuration = RevealFadeDuration;
        // Arc's arena under reduced motion appends the text without a fade (spec 5.4).
        markdown.RevealFadeInReducedMotion = false;
    }

    /// <summary>The arena's code block (spec 3.5): SurfaceMuted fill, 1px Border, radius 14.</summary>
    private void ApplyMarkdownResources()
    {
        if (_markdown is not { } markdown)
        {
            return;
        }
        var resources = markdown.Resources;
        SetResource(resources, MarkdownResourceKeys.CodeBackground, new MarkdownResourceAlias(ArcDesignTokenKind.SurfaceMuted));
        SetResource(resources, MarkdownResourceKeys.CodeBorderBrush, new MarkdownResourceAlias(ArcDesignTokenKind.Border));
        SetResource(resources, MarkdownResourceKeys.CornerRadius, new MarkdownResourceAlias(ArcDesignTokenKind.RadiusStep14));
    }

    private static void SetResource(IResourceDictionary resources, string key, object value)
    {
        // Every write restyles the document, so only real changes are written.
        if (!resources.TryGetValue(key, out var current) || !Equals(current, value))
        {
            resources[key] = value;
        }
    }

    #endregion

    #region Following and keys

    private void OnFollowingChanged(object? sender, EventArgs e) => IsFollowing = _follow?.IsFollowing ?? true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _follow is not { } follow || follow.Scroll is not { } scroll
            || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        var page = scroll.Viewport.Height * 0.9;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        double? delta = e.Key switch
        {
            Key.Up => -LineStep,
            Key.Down => LineStep,
            Key.PageUp => -page,
            Key.PageDown => page,
            Key.Space => shift ? -page : page,
            _ => null
        };
        double? target = e.Key switch
        {
            Key.Home => 0,
            Key.End => Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height),
            _ => null
        };
        if (delta is null && target is null)
        {
            return;
        }
        // The keys come to the answer, not to the scroll area: tell the follow logic this is the person's scroll.
        follow.NoteUserInput();
        if (delta is { } d)
        {
            scroll.ScrollBy(new Vector(0, d));
        }
        else
        {
            scroll.ScrollToOffset(new Vector(scroll.Offset.X, target!.Value));
        }
        e.Handled = true;
    }

    #endregion

    #region Edge fade

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateMask();
        return size;
    }

    /// <summary>
    /// The mask: transparent at both edges, opaque from <see cref="TopFade"/> down to <see cref="BottomFade"/> above the
    /// bottom. Its stops are relative, so a new immutable brush is published only when the height changes.
    /// </summary>
    private void UpdateMask()
    {
        if (_follow is not { } follow)
        {
            return;
        }
        var height = Math.Round(follow.Bounds.Height, 2);
        if (height == _maskHeight)
        {
            return;
        }
        _maskHeight = height;
        if (height <= 0)
        {
            follow.OpacityMask = null;
            return;
        }
        var top = Math.Clamp(TopFade / height, 0, 0.5);
        var bottom = Math.Clamp(1 - BottomFade / height, 0.5, 1);
        follow.OpacityMask = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Colors.Transparent),
                new ImmutableGradientStop(top, Colors.Black),
                new ImmutableGradientStop(bottom, Colors.Black),
                new ImmutableGradientStop(1, Colors.Transparent)
            ],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
    }

    /// <summary>The edge fade's opaque band as fractions of the height (tests).</summary>
    internal (double Top, double Bottom)? MaskBand =>
        _follow?.OpacityMask is ImmutableLinearGradientBrush { GradientStops.Count: 4 } brush
            ? (brush.GradientStops[1].Offset, brush.GradientStops[2].Offset)
            : null;

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AnswerNameProperty)
        {
            AutomationProperties.SetName(this, AnswerName);
        }
        else if (change.Property == IsStreamingProperty || change.Property == StreamingStatusProperty)
        {
            PseudoClasses.Set(StreamingPseudoClass, IsStreaming);
            if (IsStreaming && !string.IsNullOrEmpty(StreamingStatus))
            {
                AutomationProperties.SetItemStatus(this, StreamingStatus);
            }
            else
            {
                ClearValue(AutomationProperties.ItemStatusProperty);
            }
        }
        else if (change.Property == RevealFadeDurationProperty || change.Property == IsPacedProperty)
        {
            ApplyMarkdownOptions();
        }
        else if (change.Property == PaddingProperty)
        {
            if (_body is not null)
            {
                _body.Margin = Padding;
            }
        }
        else if (change.Property == TopFadeProperty || change.Property == BottomFadeProperty)
        {
            _maskHeight = double.NaN;
            UpdateMask();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArenaAnswerViewAutomationPeer(this);

    /// <summary>The answer as a named, focusable group (Arc: a <c>tabindex=0</c> region named "Model A answer").</summary>
    private sealed class ArenaAnswerViewAutomationPeer(ArenaAnswerView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The error card of an <see cref="ArenaAnswerView"/>: a box announced as an alert (an assertive live region named by its
/// title and text; the owner sets the name each time it shows and clears it when it hides).
/// </summary>
internal sealed class ArenaAnswerErrorCard : Border
{
    public ArenaAnswerErrorCard()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Assertive);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArenaAnswerErrorCardAutomationPeer(this);

    private sealed class ArenaAnswerErrorCardAutomationPeer(ArenaAnswerErrorCard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
