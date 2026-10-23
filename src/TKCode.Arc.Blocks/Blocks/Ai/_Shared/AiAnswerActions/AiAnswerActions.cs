using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiAnswerActions.FeedbackChangedEvent"/>: the rating as it stands now, the reason of a down rating, and the rating before.</summary>
internal sealed class AiAnswerActionsFeedbackEventArgs : RoutedEventArgs
{
    public AiAnswerActionsFeedbackEventArgs(RoutedEvent routedEvent, object source, AiAnswerFeedbackValue value, string? reason,
        AiAnswerFeedbackValue previous)
        : base(routedEvent, source)
    {
        Value = value;
        Reason = reason;
        Previous = previous;
    }

    /// <summary>The rating; <see cref="AiAnswerFeedbackValue.None"/> when the person took it back.</summary>
    public AiAnswerFeedbackValue Value { get; }

    /// <summary>The reason chosen for a down rating, otherwise null.</summary>
    public string? Reason { get; }

    /// <summary>The rating before.</summary>
    public AiAnswerFeedbackValue Previous { get; }
}

/// <summary>
/// The bar under an AI answer (Arc answer footer): on the left a toolbar, set 10px left so the glyphs line up with the text,
/// of 36px round buttons 2px apart: Copy answer, Answer again (opens <see cref="AiRegenerateMenu"/>), Good answer, Bad
/// answer (opens <see cref="AiFeedbackReasonPicker"/>), the link to the answer (only with <see cref="HasShare"/>), then the
/// version pager behind a thin line once there are two versions (<see cref="VersionCount"/>). On the right the
/// <see cref="ModelLine"/>; when both do not fit on one row the model line moves under the toolbar, 4px below.
/// <para>
/// Copy raises <see cref="CopyRequested"/> (the owner copies), turns into a check for <see cref="CopiedDuration"/> (1.6s)
/// and raises <see cref="Copied"/>. The link raises <see cref="LinkRequested"/>; the owner gets the link, copies it and
/// calls <see cref="ShowLinkCopied"/> for the same check. The ratings are exclusive: Good answer rates at once; Bad answer
/// asks for a reason first and rates when one is chosen (closing the popover without one changes nothing); pressing a
/// chosen rating takes it back; a chosen rating is filled and its tooltip offers to remove it. <see cref="FeedbackChanged"/>
/// reports every final rating; <see cref="Feedback"/> is two-way. Answer again raises <see cref="RegenerateChosen"/> with
/// the mode (and the model for "Switch model"); the pager raises <see cref="VersionChanged"/> for the person's moves.
/// </para>
/// <para>
/// While the answer streams (<see cref="IsBusy"/>) the bar keeps its height but shows nothing, takes no pointer and no
/// tab stops, and its menus close. When it comes back its content fades in (150ms) rising 4px (200ms); reduced motion
/// fades in 120ms without rising; Off shows it at once. Every word comes from the owner.
/// </para>
/// </summary>
[TemplatePart(LayoutPart, typeof(AiAnswerActionsLayout))]
[TemplatePart(ToolbarPart, typeof(AiAnswerToolbar))]
[TemplatePart(CopyPart, typeof(AiAnswerActionButton))]
[TemplatePart(RegeneratePart, typeof(AiAnswerActionButton))]
[TemplatePart(GoodPart, typeof(AiAnswerRatingButton))]
[TemplatePart(BadPart, typeof(AiAnswerRatingButton))]
[TemplatePart(LinkPart, typeof(AiAnswerActionButton))]
[TemplatePart(PagerPart, typeof(AiAnswerVersionPager))]
[TemplatePart(MenuPart, typeof(AiRegenerateMenu))]
[TemplatePart(ReasonsPart, typeof(AiFeedbackReasonPicker))]
[PseudoClasses(BusyPseudoClass, CopiedPseudoClass, LinkCopiedPseudoClass)]
internal class AiAnswerActions : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string ToolbarPart = "PART_Toolbar";
    public const string CopyPart = "PART_Copy";
    public const string RegeneratePart = "PART_Regenerate";
    public const string GoodPart = "PART_Good";
    public const string BadPart = "PART_Bad";
    public const string LinkPart = "PART_Link";
    public const string PagerPart = "PART_Pager";
    public const string MenuPart = "PART_Menu";
    public const string ReasonsPart = "PART_Reasons";

    /// <summary>Set while the answer streams: the bar keeps its place but shows nothing.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set while Copy shows its check.</summary>
    public const string CopiedPseudoClass = ":copied";

    /// <summary>Set while the link button shows its check.</summary>
    public const string LinkCopiedPseudoClass = ":link-copied";

    /// <summary>How long the content takes to fade in after the answer (150ms).</summary>
    internal static readonly TimeSpan RevealFade = TimeSpan.FromMilliseconds(150);

    /// <summary>How long the content takes to rise into place after the answer (200ms).</summary>
    internal static readonly TimeSpan RevealRise = TimeSpan.FromMilliseconds(200);

    /// <summary>How far the content rises into place (4px).</summary>
    internal const double RevealOffset = 4;

    private static readonly Easing s_linear = new LinearEasing();

    #region Properties

    /// <summary>The rating of the shown version (two-way).</summary>
    public static readonly StyledProperty<AiAnswerFeedbackValue> FeedbackProperty =
        AvaloniaProperty.Register<AiAnswerActions, AiAnswerFeedbackValue>(nameof(Feedback),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether the link button shows (there is a link provider).</summary>
    public static readonly StyledProperty<bool> HasShareProperty =
        AvaloniaProperty.Register<AiAnswerActions, bool>(nameof(HasShare));

    /// <summary>The answer streams: the bar keeps its height but shows nothing and takes no input.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<AiAnswerActions, bool>(nameof(IsBusy));

    /// <summary>How many versions the answer has; two or more show the pager.</summary>
    public static readonly StyledProperty<int> VersionCountProperty =
        AvaloniaProperty.Register<AiAnswerActions, int>(nameof(VersionCount));

    /// <summary>The shown version, from 0 (two-way).</summary>
    public static readonly StyledProperty<int> VersionIndexProperty =
        AvaloniaProperty.Register<AiAnswerActions, int>(nameof(VersionIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The model line on the right (an <see cref="AiModelLine"/>); null leaves the right side empty.</summary>
    public static readonly StyledProperty<Control?> ModelLineProperty =
        AvaloniaProperty.Register<AiAnswerActions, Control?>(nameof(ModelLine));

    /// <summary>The models of "Switch model" (the owner leaves the current one out).</summary>
    public static readonly StyledProperty<IEnumerable<AiModel>?> ModelsProperty =
        AvaloniaProperty.Register<AiAnswerActions, IEnumerable<AiModel>?>(nameof(Models));

    /// <summary>The providers, for the marks of models without their own.</summary>
    public static readonly StyledProperty<IEnumerable<AiModelProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<AiAnswerActions, IEnumerable<AiModelProvider>?>(nameof(Providers));

    /// <summary>The reasons offered for a down rating; none rates down at once.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> FeedbackReasonsProperty =
        AvaloniaProperty.Register<AiAnswerActions, IEnumerable<string>?>(nameof(FeedbackReasons));

    /// <summary>How long Copy and the link show their check (1.6s).</summary>
    public static readonly StyledProperty<TimeSpan> CopiedDurationProperty =
        AvaloniaProperty.Register<AiAnswerActions, TimeSpan>(nameof(CopiedDuration), TimeSpan.FromSeconds(1.6));

    /// <summary>How long the thanks for a down rating stay (1.5s).</summary>
    public static readonly StyledProperty<TimeSpan> ConfirmationDurationProperty =
        AvaloniaProperty.Register<AiAnswerActions, TimeSpan>(nameof(ConfirmationDuration), TimeSpan.FromSeconds(1.5));

    /// <summary>The toolbar's accessible name ("Answer actions").</summary>
    public static readonly StyledProperty<string?> ToolbarNameProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(ToolbarName));

    /// <summary>Copy's name and tooltip ("Copy answer").</summary>
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(CopyText));

    /// <summary>Copy's name and tooltip while it shows its check ("Copied"); null keeps <see cref="CopyText"/>.</summary>
    public static readonly StyledProperty<string?> CopiedTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(CopiedText));

    /// <summary>The name and tooltip of the button that opens the menu ("Answer again"), and the menu's name.</summary>
    public static readonly StyledProperty<string?> RegenerateTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(RegenerateText));

    /// <summary>The thumbs up's name and tooltip ("Good answer").</summary>
    public static readonly StyledProperty<string?> GoodTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(GoodText));

    /// <summary>The thumbs down's name and tooltip ("Bad answer").</summary>
    public static readonly StyledProperty<string?> BadTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(BadText));

    /// <summary>The tooltip of a chosen rating ("Remove feedback"); null keeps the rating's own.</summary>
    public static readonly StyledProperty<string?> RemoveFeedbackTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(RemoveFeedbackText));

    /// <summary>The link button's name and tooltip ("Copy link to this answer").</summary>
    public static readonly StyledProperty<string?> LinkTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(LinkText));

    /// <summary>The link button's name and tooltip while it shows its check ("Copied"); null keeps <see cref="LinkText"/>.</summary>
    public static readonly StyledProperty<string?> LinkCopiedTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(LinkCopiedText));

    /// <summary>The menu's "Try again".</summary>
    public static readonly StyledProperty<string?> TryAgainTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(TryAgainText));

    /// <summary>The menu's "More concise".</summary>
    public static readonly StyledProperty<string?> MoreConciseTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(MoreConciseText));

    /// <summary>The menu's "More detailed".</summary>
    public static readonly StyledProperty<string?> MoreDetailedTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(MoreDetailedText));

    /// <summary>The menu's "Switch model".</summary>
    public static readonly StyledProperty<string?> SwitchModelTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(SwitchModelText));

    /// <summary>The heading of the list of models ("Answer again with").</summary>
    public static readonly StyledProperty<string?> AnswerAgainWithTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(AnswerAgainWithText));

    /// <summary>The question of the reason popover ("What went wrong?").</summary>
    public static readonly StyledProperty<string?> WhatWentWrongTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(WhatWentWrongText));

    /// <summary>The first line of the thanks ("Thanks for the feedback").</summary>
    public static readonly StyledProperty<string?> ThanksTitleProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(ThanksTitle));

    /// <summary>The second line of the thanks.</summary>
    public static readonly StyledProperty<string?> ThanksDescriptionProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(ThanksDescription));

    /// <summary>Makes the second line of the thanks from the chosen reason; null shows <see cref="ThanksDescription"/>.</summary>
    public static readonly StyledProperty<Func<string, string>?> ThanksDescriptionForProperty =
        AvaloniaProperty.Register<AiAnswerActions, Func<string, string>?>(nameof(ThanksDescriptionFor));

    /// <summary>The previous version button's name and tooltip.</summary>
    public static readonly StyledProperty<string?> PreviousVersionTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(PreviousVersionText));

    /// <summary>The next version button's name and tooltip.</summary>
    public static readonly StyledProperty<string?> NextVersionTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, string?>(nameof(NextVersionText));

    /// <summary>Names the pager from the shown version and the count, both from 1 ("Version 2 of 2").</summary>
    public static readonly StyledProperty<Func<int, int, string>?> VersionNameProperty =
        AvaloniaProperty.Register<AiAnswerActions, Func<int, int, string>?>(nameof(VersionName));

    /// <summary>The pager's visible count ("2 / 2"); null rolls the numbers around " / ".</summary>
    public static readonly StyledProperty<Func<int, int, string>?> VersionCountTextProperty =
        AvaloniaProperty.Register<AiAnswerActions, Func<int, int, string>?>(nameof(VersionCountText));

    /// <summary>Raised (bubbling) when the person presses Copy; the owner copies the answer.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CopyRequestedEvent =
        RoutedEvent.Register<AiAnswerActions, RoutedEventArgs>(nameof(CopyRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) right after <see cref="CopyRequestedEvent"/>, when Copy shows its check: the owner announces it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CopiedEvent =
        RoutedEvent.Register<AiAnswerActions, RoutedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person chooses how to answer again.</summary>
    public static readonly RoutedEvent<AiRegenerateChosenEventArgs> RegenerateChosenEvent =
        RoutedEvent.Register<AiAnswerActions, AiRegenerateChosenEventArgs>(nameof(RegenerateChosen), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when a rating is final: up at once, down with its reason, or taken back.</summary>
    public static readonly RoutedEvent<AiAnswerActionsFeedbackEventArgs> FeedbackChangedEvent =
        RoutedEvent.Register<AiAnswerActions, AiAnswerActionsFeedbackEventArgs>(nameof(FeedbackChanged), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person presses the link button; the owner copies the link and calls <see cref="ShowLinkCopied"/>.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> LinkRequestedEvent =
        RoutedEvent.Register<AiAnswerActions, RoutedEventArgs>(nameof(LinkRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person moves to another version.</summary>
    public static readonly RoutedEvent<AiVersionPagerEventArgs> VersionChangedEvent =
        RoutedEvent.Register<AiAnswerActions, AiVersionPagerEventArgs>(nameof(VersionChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="FeedbackProperty"/>
    public AiAnswerFeedbackValue Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <inheritdoc cref="HasShareProperty"/>
    public bool HasShare { get => GetValue(HasShareProperty); set => SetValue(HasShareProperty, value); }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <inheritdoc cref="VersionCountProperty"/>
    public int VersionCount { get => GetValue(VersionCountProperty); set => SetValue(VersionCountProperty, value); }

    /// <inheritdoc cref="VersionIndexProperty"/>
    public int VersionIndex { get => GetValue(VersionIndexProperty); set => SetValue(VersionIndexProperty, value); }

    /// <inheritdoc cref="ModelLineProperty"/>
    public Control? ModelLine { get => GetValue(ModelLineProperty); set => SetValue(ModelLineProperty, value); }

    /// <inheritdoc cref="ModelsProperty"/>
    public IEnumerable<AiModel>? Models { get => GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IEnumerable<AiModelProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="FeedbackReasonsProperty"/>
    public IEnumerable<string>? FeedbackReasons { get => GetValue(FeedbackReasonsProperty); set => SetValue(FeedbackReasonsProperty, value); }

    /// <inheritdoc cref="CopiedDurationProperty"/>
    public TimeSpan CopiedDuration { get => GetValue(CopiedDurationProperty); set => SetValue(CopiedDurationProperty, value); }

    /// <inheritdoc cref="ConfirmationDurationProperty"/>
    public TimeSpan ConfirmationDuration { get => GetValue(ConfirmationDurationProperty); set => SetValue(ConfirmationDurationProperty, value); }

    /// <inheritdoc cref="ToolbarNameProperty"/>
    public string? ToolbarName { get => GetValue(ToolbarNameProperty); set => SetValue(ToolbarNameProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    /// <inheritdoc cref="CopiedTextProperty"/>
    public string? CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    /// <inheritdoc cref="RegenerateTextProperty"/>
    public string? RegenerateText { get => GetValue(RegenerateTextProperty); set => SetValue(RegenerateTextProperty, value); }

    /// <inheritdoc cref="GoodTextProperty"/>
    public string? GoodText { get => GetValue(GoodTextProperty); set => SetValue(GoodTextProperty, value); }

    /// <inheritdoc cref="BadTextProperty"/>
    public string? BadText { get => GetValue(BadTextProperty); set => SetValue(BadTextProperty, value); }

    /// <inheritdoc cref="RemoveFeedbackTextProperty"/>
    public string? RemoveFeedbackText { get => GetValue(RemoveFeedbackTextProperty); set => SetValue(RemoveFeedbackTextProperty, value); }

    /// <inheritdoc cref="LinkTextProperty"/>
    public string? LinkText { get => GetValue(LinkTextProperty); set => SetValue(LinkTextProperty, value); }

    /// <inheritdoc cref="LinkCopiedTextProperty"/>
    public string? LinkCopiedText { get => GetValue(LinkCopiedTextProperty); set => SetValue(LinkCopiedTextProperty, value); }

    /// <inheritdoc cref="TryAgainTextProperty"/>
    public string? TryAgainText { get => GetValue(TryAgainTextProperty); set => SetValue(TryAgainTextProperty, value); }

    /// <inheritdoc cref="MoreConciseTextProperty"/>
    public string? MoreConciseText { get => GetValue(MoreConciseTextProperty); set => SetValue(MoreConciseTextProperty, value); }

    /// <inheritdoc cref="MoreDetailedTextProperty"/>
    public string? MoreDetailedText { get => GetValue(MoreDetailedTextProperty); set => SetValue(MoreDetailedTextProperty, value); }

    /// <inheritdoc cref="SwitchModelTextProperty"/>
    public string? SwitchModelText { get => GetValue(SwitchModelTextProperty); set => SetValue(SwitchModelTextProperty, value); }

    /// <inheritdoc cref="AnswerAgainWithTextProperty"/>
    public string? AnswerAgainWithText { get => GetValue(AnswerAgainWithTextProperty); set => SetValue(AnswerAgainWithTextProperty, value); }

    /// <inheritdoc cref="WhatWentWrongTextProperty"/>
    public string? WhatWentWrongText { get => GetValue(WhatWentWrongTextProperty); set => SetValue(WhatWentWrongTextProperty, value); }

    /// <inheritdoc cref="ThanksTitleProperty"/>
    public string? ThanksTitle { get => GetValue(ThanksTitleProperty); set => SetValue(ThanksTitleProperty, value); }

    /// <inheritdoc cref="ThanksDescriptionProperty"/>
    public string? ThanksDescription { get => GetValue(ThanksDescriptionProperty); set => SetValue(ThanksDescriptionProperty, value); }

    /// <inheritdoc cref="ThanksDescriptionForProperty"/>
    public Func<string, string>? ThanksDescriptionFor { get => GetValue(ThanksDescriptionForProperty); set => SetValue(ThanksDescriptionForProperty, value); }

    /// <inheritdoc cref="PreviousVersionTextProperty"/>
    public string? PreviousVersionText { get => GetValue(PreviousVersionTextProperty); set => SetValue(PreviousVersionTextProperty, value); }

    /// <inheritdoc cref="NextVersionTextProperty"/>
    public string? NextVersionText { get => GetValue(NextVersionTextProperty); set => SetValue(NextVersionTextProperty, value); }

    /// <inheritdoc cref="VersionNameProperty"/>
    public Func<int, int, string>? VersionName { get => GetValue(VersionNameProperty); set => SetValue(VersionNameProperty, value); }

    /// <inheritdoc cref="VersionCountTextProperty"/>
    public Func<int, int, string>? VersionCountText { get => GetValue(VersionCountTextProperty); set => SetValue(VersionCountTextProperty, value); }

    /// <inheritdoc cref="CopyRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CopyRequested { add => AddHandler(CopyRequestedEvent, value); remove => RemoveHandler(CopyRequestedEvent, value); }

    /// <inheritdoc cref="CopiedEvent"/>
    public event EventHandler<RoutedEventArgs>? Copied { add => AddHandler(CopiedEvent, value); remove => RemoveHandler(CopiedEvent, value); }

    /// <inheritdoc cref="RegenerateChosenEvent"/>
    public event EventHandler<AiRegenerateChosenEventArgs>? RegenerateChosen
    {
        add => AddHandler(RegenerateChosenEvent, value);
        remove => RemoveHandler(RegenerateChosenEvent, value);
    }

    /// <inheritdoc cref="FeedbackChangedEvent"/>
    public event EventHandler<AiAnswerActionsFeedbackEventArgs>? FeedbackChanged
    {
        add => AddHandler(FeedbackChangedEvent, value);
        remove => RemoveHandler(FeedbackChangedEvent, value);
    }

    /// <inheritdoc cref="LinkRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? LinkRequested { add => AddHandler(LinkRequestedEvent, value); remove => RemoveHandler(LinkRequestedEvent, value); }

    /// <inheritdoc cref="VersionChangedEvent"/>
    public event EventHandler<AiVersionPagerEventArgs>? VersionChanged
    {
        add => AddHandler(VersionChangedEvent, value);
        remove => RemoveHandler(VersionChangedEvent, value);
    }

    #endregion

    private readonly TranslateTransform _rise = new();
    private AiAnswerActionsLayout? _layout;
    private AiAnswerToolbar? _toolbar;
    private AiAnswerActionButton? _copy;
    private AiAnswerActionButton? _regenerate;
    private AiAnswerRatingButton? _good;
    private AiAnswerRatingButton? _bad;
    private AiAnswerActionButton? _link;
    private AiAnswerVersionPager? _pager;
    private AiRegenerateMenu? _menu;
    private AiFeedbackReasonPicker? _reasons;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _offset;
    private DateTimeOffset? _copiedUntil;
    private DateTimeOffset? _linkCopiedUntil;
    private DispatcherTimer? _copiedTimer;

    /// <summary>Where the time of the copied checks comes from (tests move it by hand and call <see cref="Tick"/>).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>Whether Copy shows its check.</summary>
    internal bool IsCopied => _copiedUntil is not null;

    /// <summary>Whether the link button shows its check.</summary>
    internal bool IsLinkCopied => _linkCopiedUntil is not null;

    internal AiAnswerActionsLayout? LayoutPanel => _layout;

    internal AiAnswerToolbar? Toolbar => _toolbar;

    internal AiAnswerActionButton? CopyButton => _copy;

    internal AiAnswerActionButton? RegenerateButton => _regenerate;

    internal AiAnswerRatingButton? GoodButton => _good;

    internal AiAnswerRatingButton? BadButton => _bad;

    internal AiAnswerActionButton? LinkButton => _link;

    internal AiAnswerVersionPager? Pager => _pager;

    internal AiRegenerateMenu? Menu => _menu;

    internal AiFeedbackReasonPicker? ReasonPicker => _reasons;

    /// <summary>The time source of the reason popover's thanks (tests).</summary>
    internal TimeProvider ReasonTime
    {
        get => _reasons?.Time ?? TimeProvider.System;
        set
        {
            if (_reasons is not null)
            {
                _reasons.Time = value;
            }
        }
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        _layout = e.NameScope.Find<AiAnswerActionsLayout>(LayoutPart);
        _toolbar = e.NameScope.Find<AiAnswerToolbar>(ToolbarPart);
        _copy = e.NameScope.Find<AiAnswerActionButton>(CopyPart);
        _regenerate = e.NameScope.Find<AiAnswerActionButton>(RegeneratePart);
        _good = e.NameScope.Find<AiAnswerRatingButton>(GoodPart);
        _bad = e.NameScope.Find<AiAnswerRatingButton>(BadPart);
        _link = e.NameScope.Find<AiAnswerActionButton>(LinkPart);
        _pager = e.NameScope.Find<AiAnswerVersionPager>(PagerPart);
        _menu = e.NameScope.Find<AiRegenerateMenu>(MenuPart);
        _reasons = e.NameScope.Find<AiFeedbackReasonPicker>(ReasonsPart);
        Wire();
        if (_layout is not null)
        {
            _layout.RenderTransform = _rise;
            _fade?.Dispose();
            _offset?.Dispose();
            _fade = new MotionValue<double>(_layout, IsBusy ? 0 : 1, MotionVectors.Double);
            _offset = new MotionValue<double>(_layout, 0, MotionVectors.Double);
            _fade.Changed += v => _layout.Opacity = Math.Clamp(v, 0, 1);
            _offset.Changed += v => _rise.Y = v;
            _layout.Opacity = _fade.Value;
        }
        UpdateTexts();
        UpdateCopy();
        UpdateLink();
        UpdateRatings();
        UpdatePager();
        UpdateMenus();
        UpdateBusy(animate: false);
    }

    private void Wire()
    {
        if (_copy is not null)
        {
            _copy.Click += OnCopyClick;
        }
        if (_regenerate is not null)
        {
            _regenerate.Click += OnRegenerateClick;
            _regenerate.KeyDown += OnRegenerateKeyDown;
        }
        if (_good is not null)
        {
            _good.Click += OnGoodClick;
        }
        if (_bad is not null)
        {
            _bad.Click += OnBadClick;
        }
        if (_link is not null)
        {
            _link.Click += OnLinkClick;
        }
        if (_pager is not null)
        {
            _pager.IndexChanged += OnPagerIndexChanged;
        }
        if (_menu is not null)
        {
            _menu.Chosen += OnMenuChosen;
            _menu.PropertyChanged += OnMenuPropertyChanged;
        }
        if (_reasons is not null)
        {
            _reasons.ReasonChosen += OnReasonChosen;
            _reasons.PropertyChanged += OnReasonsPropertyChanged;
        }
    }

    private void Unwire()
    {
        if (_copy is not null)
        {
            _copy.Click -= OnCopyClick;
        }
        if (_regenerate is not null)
        {
            _regenerate.Click -= OnRegenerateClick;
            _regenerate.KeyDown -= OnRegenerateKeyDown;
        }
        if (_good is not null)
        {
            _good.Click -= OnGoodClick;
        }
        if (_bad is not null)
        {
            _bad.Click -= OnBadClick;
        }
        if (_link is not null)
        {
            _link.Click -= OnLinkClick;
        }
        if (_pager is not null)
        {
            _pager.IndexChanged -= OnPagerIndexChanged;
        }
        if (_menu is not null)
        {
            _menu.Chosen -= OnMenuChosen;
            _menu.PropertyChanged -= OnMenuPropertyChanged;
        }
        if (_reasons is not null)
        {
            _reasons.ReasonChosen -= OnReasonChosen;
            _reasons.PropertyChanged -= OnReasonsPropertyChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            UpdateBusy(animate: true);
        }
        else if (change.Property == FeedbackProperty || change.Property == RemoveFeedbackTextProperty)
        {
            UpdateRatings();
        }
        else if (change.Property == VersionCountProperty || change.Property == VersionIndexProperty)
        {
            // Another version: its own rating, and no menu left open over the previous one.
            if (change.Property == VersionIndexProperty)
            {
                CloseMenus(returnFocus: false);
            }
            UpdatePager();
        }
        else if (change.Property == PreviousVersionTextProperty || change.Property == NextVersionTextProperty
                 || change.Property == VersionNameProperty || change.Property == VersionCountTextProperty)
        {
            UpdatePager();
        }
        else if (change.Property == CopyTextProperty || change.Property == CopiedTextProperty)
        {
            UpdateCopy();
        }
        else if (change.Property == LinkTextProperty || change.Property == LinkCopiedTextProperty || change.Property == HasShareProperty)
        {
            UpdateLink();
        }
        else if (change.Property == RegenerateTextProperty || change.Property == GoodTextProperty || change.Property == BadTextProperty
                 || change.Property == ToolbarNameProperty)
        {
            UpdateTexts();
            UpdateRatings();
            UpdateMenus();
        }
        else if (change.Property == ModelsProperty || change.Property == ProvidersProperty || change.Property == FeedbackReasonsProperty
                 || change.Property == TryAgainTextProperty || change.Property == MoreConciseTextProperty
                 || change.Property == MoreDetailedTextProperty || change.Property == SwitchModelTextProperty
                 || change.Property == AnswerAgainWithTextProperty || change.Property == WhatWentWrongTextProperty
                 || change.Property == ThanksTitleProperty || change.Property == ThanksDescriptionProperty
                 || change.Property == ThanksDescriptionForProperty || change.Property == ConfirmationDurationProperty)
        {
            UpdateMenus();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The checks do not outlive the bar's stay in a window.
        _copiedTimer?.Stop();
        if (_copiedUntil is not null || _linkCopiedUntil is not null)
        {
            _copiedUntil = null;
            _linkCopiedUntil = null;
            UpdateCopy();
            UpdateLink();
        }
    }

    #endregion

    #region State to parts

    private static void SetText(Control? button, string? name, string? tip = null)
    {
        if (button is null)
        {
            return;
        }
        AutomationProperties.SetName(button, name ?? string.Empty);
        var tooltip = tip ?? name;
        Tooltip.SetTip(button, string.IsNullOrEmpty(tooltip) ? null : tooltip);
    }

    private void UpdateTexts()
    {
        SetText(_regenerate, RegenerateText);
        if (_toolbar is not null)
        {
            AutomationProperties.SetName(_toolbar, ToolbarName ?? string.Empty);
        }
    }

    private void UpdateCopy()
    {
        var copied = _copiedUntil is not null;
        PseudoClasses.Set(CopiedPseudoClass, copied);
        if (_copy is null)
        {
            return;
        }
        _copy.Glyph = copied ? ArcIcons.Check : ArcIcons.Copy;
        SetText(_copy, copied ? CopiedText ?? CopyText : CopyText);
    }

    private void UpdateLink()
    {
        var copied = _linkCopiedUntil is not null;
        PseudoClasses.Set(LinkCopiedPseudoClass, copied);
        if (_link is null)
        {
            return;
        }
        _link.IsVisible = HasShare;
        _link.Glyph = copied ? ArcIcons.Check : ArcIcons.Link2;
        SetText(_link, copied ? LinkCopiedText ?? LinkText : LinkText);
    }

    private void UpdateRatings()
    {
        var feedback = Feedback;
        if (_good is not null)
        {
            _good.IsPressedOn = feedback == AiAnswerFeedbackValue.Up;
            SetText(_good, GoodText, _good.IsPressedOn ? RemoveFeedbackText : null);
        }
        if (_bad is not null)
        {
            _bad.IsPressedOn = feedback == AiAnswerFeedbackValue.Down;
            SetText(_bad, BadText, _bad.IsPressedOn ? RemoveFeedbackText : null);
        }
    }

    private void UpdatePager()
    {
        if (_pager is null)
        {
            return;
        }
        _pager.Count = VersionCount;
        _pager.Index = VersionIndex;
        _pager.PreviousText = PreviousVersionText;
        _pager.NextText = NextVersionText;
        _pager.GroupName = VersionName;
        _pager.CountText = VersionCountText;
    }

    private void UpdateMenus()
    {
        if (_menu is not null)
        {
            _menu.PlacementTarget = _regenerate;
            _menu.Models = Models;
            _menu.Providers = Providers;
            _menu.MenuName = RegenerateText;
            _menu.TryAgainText = TryAgainText;
            _menu.MoreConciseText = MoreConciseText;
            _menu.MoreDetailedText = MoreDetailedText;
            _menu.SwitchModelText = SwitchModelText;
            _menu.AnswerAgainWithText = AnswerAgainWithText;
        }
        if (_reasons is not null)
        {
            _reasons.PlacementTarget = _bad;
            _reasons.Title = WhatWentWrongText;
            _reasons.Reasons = FeedbackReasons;
            _reasons.ThanksTitle = ThanksTitle;
            _reasons.ThanksDescription = ThanksDescription;
            _reasons.ThanksDescriptionFor = ThanksDescriptionFor;
            _reasons.ConfirmationDuration = ConfirmationDuration;
        }
    }

    #endregion

    #region Busy

    /// <summary>
    /// Busy: the content is hidden at once, takes no pointer and leaves the tab order, and the menus close. Back: it fades in
    /// and rises into place. Code-driven (MotionValue) because the two directions differ (out at once, in with motion) and
    /// opacity and rise take different times; a ControlTheme transition applies one timing both ways.
    /// </summary>
    private void UpdateBusy(bool animate)
    {
        var busy = IsBusy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_layout is null || _fade is null || _offset is null)
        {
            return;
        }
        _layout.IsHitTestVisible = !busy;
        KeyboardNavigation.SetTabNavigation(_layout, busy ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        if (busy)
        {
            CloseMenus(returnFocus: false);
            _fade.Jump(0);
            _offset.Jump(0);
            return;
        }
        var level = animate && TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        switch (level)
        {
            case ArcMotionLevel.Full:
                _offset.Jump(RevealOffset);
                _ = _fade.AnimateTo(1, MotionSpec.Tween(RevealFade, ArcEasings.Standard));
                _ = _offset.AnimateTo(0, MotionSpec.Tween(RevealRise, ArcEasings.Standard));
                break;
            case ArcMotionLevel.Reduced:
                _offset.Jump(0);
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                _fade.Jump(1);
                _offset.Jump(0);
                break;
        }
    }

    private void CloseMenus(bool returnFocus)
    {
        _menu?.Close(returnFocus, NavigationMethod.Pointer, immediate: !returnFocus);
        _reasons?.Close(returnFocus, NavigationMethod.Pointer, immediate: !returnFocus);
    }

    #endregion

    #region Copy and link

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(CopyRequestedEvent, this));
        ShowCopied();
    }

    /// <summary>Shows Copy's check for <see cref="CopiedDuration"/> (a new copy starts the time again) and raises <see cref="Copied"/>.</summary>
    internal void ShowCopied()
    {
        _copiedUntil = Time.GetUtcNow() + CopiedDuration;
        UpdateCopy();
        StartCopiedTimer();
        RaiseEvent(new RoutedEventArgs(CopiedEvent, this));
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(LinkRequestedEvent, this));
    }

    /// <summary>Shows the link button's check for <see cref="CopiedDuration"/>: the owner calls it once the link is on the clipboard.</summary>
    public void ShowLinkCopied()
    {
        _linkCopiedUntil = Time.GetUtcNow() + CopiedDuration;
        UpdateLink();
        StartCopiedTimer();
    }

    /// <summary>Turns the link button's check back at once (the copy failed after all).</summary>
    public void ClearLinkCopied()
    {
        if (_linkCopiedUntil is null)
        {
            return;
        }
        _linkCopiedUntil = null;
        UpdateLink();
    }

    private void StartCopiedTimer()
    {
        if (_copiedTimer is null)
        {
            _copiedTimer = new DispatcherTimer();
            _copiedTimer.Tick += (_, _) => Tick();
        }
        _copiedTimer.Stop();
        _copiedTimer.Interval = NextDue() is { } due && due - Time.GetUtcNow() is { } wait && wait > TimeSpan.Zero
            ? wait
            : TimeSpan.FromMilliseconds(1);
        _copiedTimer.Start();
    }

    private DateTimeOffset? NextDue() =>
        (_copiedUntil, _linkCopiedUntil) switch
        {
            ({ } a, { } b) => a < b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null
        };

    /// <summary>Turns the checks back once their time is up (the timer calls it; tests call it after moving the time).</summary>
    internal void Tick()
    {
        var now = Time.GetUtcNow();
        if (_copiedUntil is { } copy && now >= copy)
        {
            _copiedUntil = null;
            UpdateCopy();
        }
        if (_linkCopiedUntil is { } link && now >= link)
        {
            _linkCopiedUntil = null;
            UpdateLink();
        }
        _copiedTimer?.Stop();
        if (NextDue() is { } due && _copiedTimer is not null)
        {
            // Early (a timer runs on its own clock), or another check is still showing: wait for the rest.
            _copiedTimer.Interval = due > now ? due - now : TimeSpan.FromMilliseconds(1);
            _copiedTimer.Start();
        }
    }

    #endregion

    #region Answer again

    private void OnRegenerateClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy || _menu is null || _regenerate is null)
        {
            return;
        }
        _reasons?.Close(returnFocus: false, NavigationMethod.Pointer);
        _menu.Toggle(_regenerate.LastActivationWasKeyboard);
    }

    private void OnRegenerateKeyDown(object? sender, KeyEventArgs e)
    {
        // ↓ opens the menu like Enter and Space (Radix trigger).
        if (e.Handled || IsBusy || e.Key != Key.Down || _menu is null || _menu.IsOpen
            || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        e.Handled = true;
        _reasons?.Close(returnFocus: false, NavigationMethod.Pointer);
        _menu.Open(keyboard: true);
    }

    private void OnMenuChosen(object? sender, AiRegenerateChosenEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new AiRegenerateChosenEventArgs(RegenerateChosenEvent, this, e.Mode, e.Model));
    }

    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiRegenerateMenu.IsOpenProperty && _regenerate is not null)
        {
            _regenerate.IsExpanded = e.GetNewValue<bool>();
        }
    }

    #endregion

    #region Ratings

    private void OnGoodClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy)
        {
            return;
        }
        _reasons?.Close(returnFocus: false, NavigationMethod.Pointer);
        Rate(Feedback == AiAnswerFeedbackValue.Up ? AiAnswerFeedbackValue.None : AiAnswerFeedbackValue.Up, null);
    }

    private void OnBadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy || _bad is null)
        {
            return;
        }
        if (Feedback == AiAnswerFeedbackValue.Down)
        {
            // Pressing the chosen rating takes it back.
            _reasons?.Close(returnFocus: false, NavigationMethod.Pointer);
            Rate(AiAnswerFeedbackValue.None, null);
            return;
        }
        if (_reasons is null || !HasReasons())
        {
            Rate(AiAnswerFeedbackValue.Down, null);
            return;
        }
        _menu?.Close(returnFocus: false, NavigationMethod.Pointer);
        _reasons.Toggle(_bad.LastActivationWasKeyboard);
    }

    private bool HasReasons()
    {
        if (FeedbackReasons is not { } reasons)
        {
            return false;
        }
        foreach (var reason in reasons)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                return true;
            }
        }
        return false;
    }

    private void OnReasonChosen(object? sender, AiFeedbackReasonEventArgs e)
    {
        e.Handled = true;
        Rate(AiAnswerFeedbackValue.Down, e.Reason);
    }

    private void OnReasonsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiFeedbackReasonPicker.IsOpenProperty && _bad is not null)
        {
            _bad.IsExpanded = e.GetNewValue<bool>();
        }
    }

    private void Rate(AiAnswerFeedbackValue value, string? reason)
    {
        var previous = Feedback;
        SetCurrentValue(FeedbackProperty, value);
        RaiseEvent(new AiAnswerActionsFeedbackEventArgs(FeedbackChangedEvent, this, value, reason, previous));
    }

    #endregion

    #region Versions

    private void OnPagerIndexChanged(object? sender, AiVersionPagerEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(VersionIndexProperty, e.Index);
        RaiseEvent(new AiVersionPagerEventArgs(VersionChangedEvent, this, e.Index, e.Count));
    }

    #endregion
}
