// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): cta-section.tsx, cta-section-data.ts and
// cta-section.module.css, made data driven (variants, actions, points, faces, a replaceable visual and dismissal as
// properties and events instead of the preview's simulation).

using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A call to action in three shapes: a centered closing section, a split layout beside a setup card that completes itself
/// once it is in view, and a compact banner that closes its gap when dismissed. With no properties set it shows Arc's copy
/// for the chosen <see cref="Variant"/>. Actions (<see cref="PrimaryAction"/>, <see cref="SecondaryAction"/>) are links, run
/// a command, raise <see cref="ActionInvoked"/>, or, with none of these, confirm in place so a preview always answers a
/// press. The banner closes with <see cref="Dismiss"/> or its dismiss button and then raises <see cref="Dismissed"/>.
/// </summary>
/// <remarks>Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(CenteredActionsPart, typeof(CtaFlowPanel))]
[TemplatePart(SplitActionsPart, typeof(CtaFlowPanel))]
[TemplatePart(BannerActionsPart, typeof(DockPanel))]
[TemplatePart(PointsPart, typeof(Panel))]
[TemplatePart(StagePart, typeof(Border))]
[TemplatePart(StageContentPart, typeof(ContentPresenter))]
[TemplatePart(BannerHostPart, typeof(CtaBannerHost))]
[TemplatePart(DismissPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(NoteFacesPart, typeof(SiteFaceStack))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(CenteredPseudoClass, SplitPseudoClass, BannerPseudoClass, StackedPseudoClass, NarrowPseudoClass)]
public class CtaSection : TemplatedControl
{
    public const string CenteredActionsPart = "PART_CenteredActions";
    public const string SplitActionsPart = "PART_SplitActions";
    public const string BannerActionsPart = "PART_BannerActions";
    public const string PointsPart = "PART_Points";
    public const string StagePart = "PART_Stage";
    public const string StageContentPart = "PART_StageContent";
    public const string BannerHostPart = "PART_BannerHost";
    public const string DismissPart = "PART_Dismiss";
    public const string NoteFacesPart = "PART_NoteFaces";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set for <see cref="CtaVariant.Centered"/>.</summary>
    public const string CenteredPseudoClass = ":centered";

    /// <summary>Set for <see cref="CtaVariant.Split"/>.</summary>
    public const string SplitPseudoClass = ":split";

    /// <summary>Set for <see cref="CtaVariant.Banner"/>.</summary>
    public const string BannerPseudoClass = ":banner";

    /// <summary>Set while the control is at most 860 wide: the split layout is one column.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the control is at most 560 wide: tighter padding, stacked actions, a stacked banner.</summary>
    public const string NarrowPseudoClass = ":narrow";

    #region Public properties

    /// <summary>The shape: Centered (default), Split or Banner.</summary>
    public static readonly StyledProperty<CtaVariant> VariantProperty =
        AvaloniaProperty.Register<CtaSection, CtaVariant>(nameof(Variant));

    /// <summary>The headline; null shows the variant's Arc headline.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CtaSection, string?>(nameof(Title));

    /// <summary>The supporting text; null shows the variant's Arc text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<CtaSection, string?>(nameof(Description));

    /// <summary>The main action; null shows the variant's Arc action.</summary>
    public static readonly StyledProperty<CtaAction?> PrimaryActionProperty =
        AvaloniaProperty.Register<CtaSection, CtaAction?>(nameof(PrimaryAction));

    /// <summary>The second action (Centered and Split); null shows the variant's Arc action. The banner never shows one.</summary>
    public static readonly StyledProperty<CtaAction?> SecondaryActionProperty =
        AvaloniaProperty.Register<CtaSection, CtaAction?>(nameof(SecondaryAction));

    /// <summary>False hides the second action (Arc: <c>secondaryAction = null</c>).</summary>
    public static readonly StyledProperty<bool> IsSecondaryActionVisibleProperty =
        AvaloniaProperty.Register<CtaSection, bool>(nameof(IsSecondaryActionVisible), true);

    /// <summary>Small print under the actions of the Centered shape; null shows "No credit card required", empty hides it.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<CtaSection, string?>(nameof(Note));

    /// <summary>The faces beside the note in the Centered shape (at most four are shown).</summary>
    public static readonly DirectProperty<CtaSection, AvaloniaList<IImage>> FacesProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, AvaloniaList<IImage>>(nameof(Faces), o => o.Faces);

    /// <summary>The short benefit lines of the Split shape; null shows Arc's three, an empty list hides them.</summary>
    public static readonly StyledProperty<AvaloniaList<string>?> PointsProperty =
        AvaloniaProperty.Register<CtaSection, AvaloniaList<string>?>(nameof(Points));

    /// <summary>Replaces the Split shape's setup card (any control or content); null shows the setup card.</summary>
    public static readonly StyledProperty<object?> VisualProperty =
        AvaloniaProperty.Register<CtaSection, object?>(nameof(Visual));

    /// <summary>The workspace shown on the setup card; its first letter is the mark.</summary>
    public static readonly StyledProperty<string> SetupWorkspaceProperty =
        AvaloniaProperty.Register<CtaSection, string>(nameof(SetupWorkspace), "Northwind");

    /// <summary>The setup card's steps; null shows Arc's three.</summary>
    public static readonly StyledProperty<AvaloniaList<string>?> SetupStepsProperty =
        AvaloniaProperty.Register<CtaSection, AvaloniaList<string>?>(nameof(SetupSteps));

    /// <summary>The teammates' faces that slide in on the setup card's last step.</summary>
    public static readonly DirectProperty<CtaSection, AvaloniaList<IImage>> SetupFacesProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, AvaloniaList<IImage>>(nameof(SetupFaces), o => o.SetupFaces);

    /// <summary>Shows the banner's dismiss button (Arc: passing <c>onDismiss</c>).</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<CtaSection, bool>(nameof(IsDismissible));

    /// <summary>
    /// Whether the banner is shown (two way). Dismissing plays the exit motion first and then sets this to false.
    /// Setting it to false from outside plays the same exit; setting it to true brings the banner back at once.
    /// </summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<CtaSection, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The dismiss button's accessible name.</summary>
    public static readonly StyledProperty<string> DismissLabelProperty =
        AvaloniaProperty.Register<CtaSection, string>(nameof(DismissLabel), "Dismiss");

    /// <summary>An action was pressed (before it opens its link, runs its command or confirms in place).</summary>
    public static readonly RoutedEvent<CtaActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<CtaSection, CtaActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>The banner was dismissed and its exit motion finished.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DismissedEvent =
        RoutedEvent.Register<CtaSection, RoutedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    private readonly AvaloniaList<IImage> _faces = new();
    private readonly AvaloniaList<IImage> _setupFaces = new();

    /// <inheritdoc cref="VariantProperty"/>
    public CtaVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PrimaryActionProperty"/>
    public CtaAction? PrimaryAction { get => GetValue(PrimaryActionProperty); set => SetValue(PrimaryActionProperty, value); }

    /// <inheritdoc cref="SecondaryActionProperty"/>
    public CtaAction? SecondaryAction { get => GetValue(SecondaryActionProperty); set => SetValue(SecondaryActionProperty, value); }

    /// <inheritdoc cref="IsSecondaryActionVisibleProperty"/>
    public bool IsSecondaryActionVisible { get => GetValue(IsSecondaryActionVisibleProperty); set => SetValue(IsSecondaryActionVisibleProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="FacesProperty"/>
    public AvaloniaList<IImage> Faces => _faces;

    /// <inheritdoc cref="PointsProperty"/>
    public AvaloniaList<string>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    /// <inheritdoc cref="VisualProperty"/>
    public object? Visual { get => GetValue(VisualProperty); set => SetValue(VisualProperty, value); }

    /// <inheritdoc cref="SetupWorkspaceProperty"/>
    public string SetupWorkspace { get => GetValue(SetupWorkspaceProperty); set => SetValue(SetupWorkspaceProperty, value); }

    /// <inheritdoc cref="SetupStepsProperty"/>
    public AvaloniaList<string>? SetupSteps { get => GetValue(SetupStepsProperty); set => SetValue(SetupStepsProperty, value); }

    /// <inheritdoc cref="SetupFacesProperty"/>
    public AvaloniaList<IImage> SetupFaces => _setupFaces;

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible { get => GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="DismissLabelProperty"/>
    public string DismissLabel { get => GetValue(DismissLabelProperty); set => SetValue(DismissLabelProperty, value); }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<CtaActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <inheritdoc cref="DismissedEvent"/>
    public event EventHandler<RoutedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    #endregion

    #region Template properties

    internal static readonly DirectProperty<CtaSection, string> DisplayTitleProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, string>(nameof(DisplayTitle), o => o.DisplayTitle);

    internal static readonly DirectProperty<CtaSection, string> DisplayDescriptionProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, string>(nameof(DisplayDescription), o => o.DisplayDescription);

    internal static readonly DirectProperty<CtaSection, string> DisplayNoteProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, string>(nameof(DisplayNote), o => o.DisplayNote);

    internal static readonly DirectProperty<CtaSection, bool> HasNoteRowProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, bool>(nameof(HasNoteRow), o => o.HasNoteRow);

    internal static readonly DirectProperty<CtaSection, bool> HasNoteTextProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, bool>(nameof(HasNoteText), o => o.HasNoteText);

    internal static readonly DirectProperty<CtaSection, bool> HasFacesProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, bool>(nameof(HasFaces), o => o.HasFaces);

    internal static readonly DirectProperty<CtaSection, double> TitleFontSizeProperty =
        AvaloniaProperty.RegisterDirect<CtaSection, double>(nameof(TitleFontSize), o => o.TitleFontSize);

    private string _displayTitle = string.Empty;
    private string _displayDescription = string.Empty;
    private string _displayNote = string.Empty;
    private bool _hasNoteRow = true;
    private bool _hasNoteText = true;
    private bool _hasFaces;
    private double _titleFontSize = 30;

    /// <summary>The headline shown: <see cref="Title"/> or the variant's.</summary>
    internal string DisplayTitle { get => _displayTitle; private set => SetAndRaise(DisplayTitleProperty, ref _displayTitle, value); }

    /// <summary>The text shown: <see cref="Description"/> or the variant's.</summary>
    internal string DisplayDescription { get => _displayDescription; private set => SetAndRaise(DisplayDescriptionProperty, ref _displayDescription, value); }

    /// <summary>The small print shown.</summary>
    internal string DisplayNote { get => _displayNote; private set => SetAndRaise(DisplayNoteProperty, ref _displayNote, value); }

    /// <summary>The note line has a note or faces.</summary>
    internal bool HasNoteRow { get => _hasNoteRow; private set => SetAndRaise(HasNoteRowProperty, ref _hasNoteRow, value); }

    internal bool HasNoteText { get => _hasNoteText; private set => SetAndRaise(HasNoteTextProperty, ref _hasNoteText, value); }

    internal bool HasFaces { get => _hasFaces; private set => SetAndRaise(HasFacesProperty, ref _hasFaces, value); }

    /// <summary>clamp(30, 16 + 3.4cqi, 52): the headline's size from the block's own width.</summary>
    internal double TitleFontSize { get => _titleFontSize; private set => SetAndRaise(TitleFontSizeProperty, ref _titleFontSize, value); }

    #endregion

    private readonly CtaAction[] _defaultPrimary;
    private readonly CtaAction[] _defaultSecondary;
    private readonly List<CtaActionButton> _buttons = new();
    private readonly CtaSetupCard _setupCard = new();
    private Panel? _centeredActions;
    private Panel? _splitActions;
    private DockPanel? _bannerActions;
    private Panel? _points;
    private Border? _stage;
    private ContentPresenter? _stageContent;
    private CtaBannerHost? _bannerHost;
    private Avalonia.Controls.Button? _dismiss;
    private SiteFaceStack? _noteFaces;
    private SiteAnnouncer? _announcer;
    private AvaloniaList<string>? _observedPoints;
    private AvaloniaList<string>? _observedSteps;
    private ViewportVisibility? _stageWatch;
    private ViewportVisibility? _cardWatch;
    private bool _splitStarted;
    private bool _dismissing;
    private bool _syncing;

    static CtaSection()
    {
        FocusableProperty.OverrideDefaultValue<CtaSection>(false);
    }

    /// <summary>Creates the section.</summary>
    public CtaSection()
    {
        _defaultPrimary = CtaCopy.Primaries();
        _defaultSecondary = CtaCopy.Secondaries();
        _faces.CollectionChanged += OnFacesChanged;
        _setupFaces.CollectionChanged += (_, _) => _setupCard.Faces = _setupFaces;
        _setupCard.Faces = _setupFaces;
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
        PseudoClasses.Set(CenteredPseudoClass, true);
        Refresh();
    }

    #region Test hooks

    internal CtaSetupCard SetupCard => _setupCard;

    internal IReadOnlyList<CtaActionButton> ActionButtons => _buttons;

    internal CtaBannerHost? BannerHost => _bannerHost;

    internal Border? Stage => _stage;

    internal string? Announcement => _announcer?.Announcement;

    internal bool IsSplitStarted => _splitStarted;

    internal bool IsStageRevealed => _stage?.Classes.Contains("revealed") == true;

    #endregion

    #region Display state

    private void Refresh()
    {
        var variant = Variant;
        DisplayTitle = Title ?? CtaCopy.Title(variant);
        DisplayDescription = Description ?? CtaCopy.Description(variant);
        var note = Note ?? CtaCopy.Note;
        DisplayNote = note;
        HasNoteText = note.Length > 0;
        HasFaces = _faces.Count > 0;
        HasNoteRow = HasNoteText || HasFaces;
        AutomationProperties.SetName(this, DisplayTitle);
    }

    private void OnFacesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    internal CtaAction EffectivePrimary => PrimaryAction ?? _defaultPrimary[(int)Variant];

    internal CtaAction? EffectiveSecondary => !IsSecondaryActionVisible || Variant == CtaVariant.Banner ? null : SecondaryAction ?? _defaultSecondary[(int)Variant];

    /// <summary>The benefit lines shown in the Split shape.</summary>
    internal IReadOnlyList<string> EffectivePoints => Points is { } points ? points.ToArray() : CtaCopy.Points;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismissClick;
        }
        _centeredActions = e.NameScope.Find<Panel>(CenteredActionsPart);
        _splitActions = e.NameScope.Find<Panel>(SplitActionsPart);
        _bannerActions = e.NameScope.Find<DockPanel>(BannerActionsPart);
        _points = e.NameScope.Find<Panel>(PointsPart);
        _stage = e.NameScope.Find<Border>(StagePart);
        _stageContent = e.NameScope.Find<ContentPresenter>(StageContentPart);
        _bannerHost = e.NameScope.Find<CtaBannerHost>(BannerHostPart);
        _dismiss = e.NameScope.Find<Avalonia.Controls.Button>(DismissPart);
        _noteFaces = e.NameScope.Find<SiteFaceStack>(NoteFacesPart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        if (_dismiss is not null)
        {
            _dismiss.Click += OnDismissClick;
        }
        if (_noteFaces is not null)
        {
            _noteFaces.Images = _faces;
        }
        RebuildActions();
        RebuildPoints();
        ApplyStageContent();
        ApplyDismiss();
        if (_bannerHost is not null && !IsOpen)
        {
            _bannerHost.SetClosed();
        }
        StartSplitIfNeeded();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartSplitIfNeeded();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSplit();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == VariantProperty)
        {
            OnVariantChanged();
        }
        else if (property == TitleProperty || property == DescriptionProperty || property == NoteProperty)
        {
            Refresh();
        }
        else if (property == PrimaryActionProperty || property == SecondaryActionProperty || property == IsSecondaryActionVisibleProperty)
        {
            RebuildActions();
        }
        else if (property == PointsProperty)
        {
            Observe(ref _observedPoints, Points, OnPointsChanged);
            RebuildPoints();
        }
        else if (property == VisualProperty)
        {
            ApplyStageContent();
            StopSplit();
            StartSplitIfNeeded();
        }
        else if (property == SetupWorkspaceProperty)
        {
            _setupCard.Workspace = SetupWorkspace;
        }
        else if (property == SetupStepsProperty)
        {
            Observe(ref _observedSteps, SetupSteps, (_, _) => _setupCard.Steps = SetupSteps?.ToArray());
            _setupCard.Steps = SetupSteps?.ToArray();
        }
        else if (property == IsDismissibleProperty)
        {
            ApplyDismiss();
        }
        else if (property == DismissLabelProperty)
        {
            ApplyDismiss();
        }
        else if (property == IsOpenProperty)
        {
            OnIsOpenChanged(change.GetNewValue<bool>());
        }
    }

    private static void Observe(ref AvaloniaList<string>? slot, AvaloniaList<string>? next, NotifyCollectionChangedEventHandler handler)
    {
        if (slot is not null)
        {
            slot.CollectionChanged -= handler;
        }
        slot = next;
        if (slot is not null)
        {
            slot.CollectionChanged += handler;
        }
    }

    private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildPoints();

    private void OnVariantChanged()
    {
        var variant = Variant;
        PseudoClasses.Set(CenteredPseudoClass, variant == CtaVariant.Centered);
        PseudoClasses.Set(SplitPseudoClass, variant == CtaVariant.Split);
        PseudoClasses.Set(BannerPseudoClass, variant == CtaVariant.Banner);
        Refresh();
        // Arc re-mounts the section for each shape: confirmations, the stage entrance and the setup run start over.
        StopSplit();
        RebuildActions();
        StartSplitIfNeeded();
    }

    #endregion

    #region Responsive

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = e.NewSize.Width;
        PseudoClasses.Set(StackedPseudoClass, width <= 860);
        PseudoClasses.Set(NarrowPseudoClass, width <= 560);
        TitleFontSize = Math.Clamp(16 + 0.034 * width, 30, 52);
    }

    #endregion

    #region Actions

    private void RebuildActions()
    {
        foreach (var button in _buttons)
        {
            button.Click -= OnActionClick;
        }
        _buttons.Clear();
        _centeredActions?.Children.Clear();
        _splitActions?.Children.Clear();
        if (_bannerActions is not null)
        {
            for (var i = _bannerActions.Children.Count - 1; i >= 0; i--)
            {
                if (_bannerActions.Children[i] is CtaActionButton)
                {
                    _bannerActions.Children.RemoveAt(i);
                }
            }
        }
        // Each shape keeps its own pair of buttons, so switching shapes starts every confirmation over, like Arc's re-mount.
        FillActions(_centeredActions, CtaVariant.Centered, ControlSize.Large);
        FillActions(_splitActions, CtaVariant.Split, ControlSize.Large);
        if (_bannerActions is not null)
        {
            var banner = Add(new CtaActionButton(PrimaryAction ?? _defaultPrimary[(int)CtaVariant.Banner], true, ControlSize.Small));
            DockPanel.SetDock(banner, Avalonia.Controls.Dock.Left);
            _bannerActions.Children.Insert(0, banner);
        }
    }

    private void FillActions(Panel? panel, CtaVariant variant, ControlSize size)
    {
        if (panel is null)
        {
            return;
        }
        panel.Children.Add(Add(new CtaActionButton(PrimaryAction ?? _defaultPrimary[(int)variant], true, size)));
        if (IsSecondaryActionVisible)
        {
            panel.Children.Add(Add(new CtaActionButton(SecondaryAction ?? _defaultSecondary[(int)variant], false, size)));
        }
    }

    private CtaActionButton Add(CtaActionButton button)
    {
        button.Click += OnActionClick;
        _buttons.Add(button);
        return button;
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CtaActionButton button)
        {
            return;
        }
        var action = button.Action;
        var hasHref = !string.IsNullOrWhiteSpace(action.Href);
        var simulated = !hasHref && action.Command is null;
        var args = new CtaActionEventArgs(ActionInvokedEvent, this, action, button.IsPrimary, simulated && !button.IsConfirmed);
        RaiseEvent(args);
        if (action.Command is { } command && command.CanExecute(action.CommandParameter))
        {
            command.Execute(action.CommandParameter);
        }
        if (hasHref)
        {
            if (!args.Handled)
            {
                SiteLinks.TryLaunch(this, action.Href);
            }
            return;
        }
        if (simulated && !args.Handled)
        {
            button.ToggleConfirmed();
            _announcer?.Announce(button.CurrentText);
        }
    }

    #endregion

    #region Points and stage

    private void RebuildPoints()
    {
        if (_points is null)
        {
            return;
        }
        _points.Children.Clear();
        foreach (var text in EffectivePoints)
        {
            _points.Children.Add(new CtaPoint { Text = text });
        }
        _points.IsVisible = _points.Children.Count > 0;
    }

    private void ApplyStageContent()
    {
        if (_stageContent is null)
        {
            return;
        }
        _stageContent.Content = Visual ?? _setupCard;
    }

    private void StartSplitIfNeeded()
    {
        if (_splitStarted || Variant != CtaVariant.Split || _stage is null || !this.IsAttachedToVisualTree())
        {
            return;
        }
        _splitStarted = true;
        _setupCard.Workspace = SetupWorkspace;
        _setupCard.Steps = SetupSteps?.ToArray();
        _setupCard.Reset();
        // The stage appears once 35% of it is in view; the setup card starts its steps once half of it is. Reduced motion and
        // Off show the stage at once (Arc: initial={false} when reduced).
        if (ArcMotion.IsFull(this))
        {
            _stageWatch = new ViewportVisibility(_stage, 0.35);
            _stageWatch.VisibilityChanged += OnStageVisible;
        }
        else
        {
            _stage.Classes.Set("revealed", true);
        }
        if (Visual is null)
        {
            _cardWatch = new ViewportVisibility(_setupCard, 0.5);
            _cardWatch.VisibilityChanged += OnCardVisible;
        }
    }

    private void OnStageVisible(object? sender, bool visible)
    {
        if (visible)
        {
            _stage?.Classes.Set("revealed", true);
        }
    }

    private void OnCardVisible(object? sender, bool visible)
    {
        if (visible)
        {
            _setupCard.Start();
        }
    }

    private void StopSplit()
    {
        if (_stageWatch is not null)
        {
            _stageWatch.VisibilityChanged -= OnStageVisible;
            _stageWatch.Dispose();
            _stageWatch = null;
        }
        if (_cardWatch is not null)
        {
            _cardWatch.VisibilityChanged -= OnCardVisible;
            _cardWatch.Dispose();
            _cardWatch = null;
        }
        if (_splitStarted)
        {
            _splitStarted = false;
            // Hidden again while the shape is off screen, so the next entrance replays from its start without a flash.
            _stage?.Classes.Set("revealed", false);
            _setupCard.Reset();
        }
    }

    #endregion

    #region Dismissal

    private void ApplyDismiss()
    {
        if (_dismiss is null)
        {
            return;
        }
        _dismiss.IsVisible = IsDismissible;
        AutomationProperties.SetName(_dismiss, DismissLabel);
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Dismiss();

    /// <summary>
    /// Closes the banner: it fades out in 160 ms while its height springs to 0, then <see cref="IsOpen"/> becomes false and
    /// <see cref="Dismissed"/> is raised. Does nothing when it is closed already or closing.
    /// </summary>
    public void Dismiss()
    {
        if (!IsOpen || _dismissing)
        {
            return;
        }
        _dismissing = true;
        if (_bannerHost is null)
        {
            Finish();
            return;
        }
        _bannerHost.Close(Finish);

        void Finish()
        {
            _dismissing = false;
            _syncing = true;
            try
            {
                IsOpen = false;
            }
            finally
            {
                _syncing = false;
            }
            RaiseEvent(new RoutedEventArgs(DismissedEvent, this));
        }
    }

    /// <summary>Shows the banner again at once.</summary>
    public void Show()
    {
        IsOpen = true;
    }

    private void OnIsOpenChanged(bool open)
    {
        if (_syncing || _bannerHost is null)
        {
            return;
        }
        if (open)
        {
            _dismissing = false;
            _bannerHost.Open();
        }
        else
        {
            // Set from outside: the same exit, without raising Dismissed.
            _bannerHost.Close(null);
        }
    }

    #endregion
}

/// <summary>Arc's copy for each shape (cta-section-data.ts).</summary>
internal static class CtaCopy
{
    internal const string Note = "No credit card required";

    internal static readonly string[] Points =
    [
        "Import from any tracker in one step",
        "Single sign-on and audit logs included",
        "Cancel anytime from settings"
    ];

    internal static string Title(CtaVariant variant) => variant switch
    {
        CtaVariant.Split => "Your team, set up before lunch",
        CtaVariant.Banner => "Workflows are here",
        _ => "Start building in minutes"
    };

    internal static string Description(CtaVariant variant) => variant switch
    {
        CtaVariant.Split => "Import your projects, bring everyone in, and keep working the way you already do.",
        CtaVariant.Banner => "Automate handoffs between teams with no code.",
        _ => "Set up a workspace, invite your team, and ship the first project this week. Free for 14 days."
    };

    /// <summary>Per variant (Centered, Split, Banner).</summary>
    internal static CtaAction[] Primaries() =>
    [
        new CtaAction { Label = "Start free trial", ConfirmedLabel = "Trial started" },
        new CtaAction { Label = "Create a workspace", ConfirmedLabel = "Workspace created" },
        new CtaAction { Label = "See how it works", ConfirmedLabel = "Opened" }
    ];

    /// <summary>Per variant (Centered, Split, Banner: none).</summary>
    internal static CtaAction[] Secondaries() =>
    [
        new CtaAction { Label = "Talk to sales", ConfirmedLabel = "We'll email you" },
        new CtaAction { Label = "Book a demo", ConfirmedLabel = "Demo requested" },
        new CtaAction { Label = string.Empty }
    ];
}

/// <summary>One benefit line of the split shape: a Success check and the text.</summary>
internal sealed class CtaPoint : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CtaPoint, string?>(nameof(Text));

    static CtaPoint()
    {
        FocusableProperty.OverrideDefaultValue<CtaPoint>(false);
    }

    /// <summary>The line.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
