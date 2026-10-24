using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using AtomUI.Localization;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The card of one workflow step on a <see cref="WorkflowCanvas"/> (visual rules from Arc's free hero-section block,
/// HeroRelay): icon, title, the description or result under it, and the run state at the end. A running card takes the
/// accent, a skipped one fades, a failed one shows its error. The canvas creates one card per
/// <see cref="WorkflowNode"/>; style it through the semantic part <c>arc.workflow.node</c>.
/// </summary>
[TemplatePart(IconPart, typeof(ContentPresenter))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(StatePart, typeof(WorkflowStateMark))]
[TemplatePart(LayoutPart, typeof(WorkflowCardPanel))]
[TemplatePart(ContentPart, typeof(ContentPresenter))]
[PseudoClasses(PendingPseudoClass, RunningPseudoClass, DonePseudoClass, FailedPseudoClass, SkippedPseudoClass, CompactPseudoClass,
    NarrowPseudoClass, SelectedPseudoClass, DraggingPseudoClass, LockedPseudoClass, RefusingPseudoClass, CustomPseudoClass)]
public class WorkflowNodeCard : TemplatedControl
{
    internal const string IconPart = "PART_Icon";
    internal const string TitlePart = "PART_Title";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string StatePart = "PART_State";
    internal const string LayoutPart = "PART_Layout";
    internal const string ContentPart = "PART_Content";

    internal const string PendingPseudoClass = ":pending";
    internal const string RunningPseudoClass = ":running";
    internal const string DonePseudoClass = ":done";
    internal const string FailedPseudoClass = ":failed";
    internal const string SkippedPseudoClass = ":skipped";
    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string SelectedPseudoClass = ":selected";
    internal const string DraggingPseudoClass = ":dragging";
    internal const string LockedPseudoClass = ":locked";
    internal const string RefusingPseudoClass = ":connect-invalid";
    internal const string CustomPseudoClass = ":custom";

    /// <summary>The step this card shows.</summary>
    public static readonly StyledProperty<WorkflowNode?> NodeProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, WorkflowNode?>(nameof(Node));

    /// <summary>
    /// Content for the card in place of its icon, title, subtitle and state, built from the <see cref="Node"/> when the
    /// template matches it (a <see cref="WorkflowNodeTemplate"/> matches a step's <see cref="WorkflowNode.Kind"/>). The
    /// frame, the run's colors, the selection and the focus ring stay. Without <see cref="WorkflowNode.Size"/> the card
    /// is as tall as its content. On a canvas, set <see cref="WorkflowCanvas.NodeTemplates"/> instead.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>
    /// The state the card shows. It is the step's <see cref="WorkflowNode.State"/>, except that on a canvas a step that
    /// starts while the data is still drawing toward it keeps showing <see cref="WorkflowNodeState.Pending"/> until the
    /// data arrives (<see cref="WorkflowCanvas.QueueRunVisuals"/>).
    /// </summary>
    public static readonly DirectProperty<WorkflowNodeCard, WorkflowNodeState> DisplayStateProperty =
        AvaloniaProperty.RegisterDirect<WorkflowNodeCard, WorkflowNodeState>(nameof(DisplayState), o => o.DisplayState);

    // Inputs from the theme and the ArcDesign tokens.
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, IBrush?>(nameof(AccentBrush));

    internal static readonly StyledProperty<double> RunningBorderAccentShareProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, double>(nameof(RunningBorderAccentShare), 0.7);

    internal static readonly StyledProperty<double> RunningFillAccentShareProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, double>(nameof(RunningFillAccentShare), 0.04);

    internal static readonly StyledProperty<IBrush?> FailedBorderBrushProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, IBrush?>(nameof(FailedBorderBrush));

    internal static readonly StyledProperty<BoxShadows> CardShadowProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, BoxShadows>(nameof(CardShadow));

    internal static readonly StyledProperty<double> SkippedOpacityProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, double>(nameof(SkippedOpacity), 0.5);

    // Outputs the template draws.
    internal static readonly StyledProperty<IBrush?> StateBackgroundProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, IBrush?>(nameof(StateBackground));

    internal static readonly StyledProperty<IBrush?> StateBorderBrushProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, IBrush?>(nameof(StateBorderBrush));

    internal static readonly StyledProperty<BoxShadows> StateShadowProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, BoxShadows>(nameof(StateShadow));

    /// <summary>
    /// The card's shadows split for <see cref="WorkflowCardShadow"/>: <see cref="StateShadow"/> the outer ones (under the
    /// frame), this the inset ones (over its fill). The frame itself draws none.
    /// </summary>
    internal static readonly StyledProperty<BoxShadows> StateInsetShadowProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, BoxShadows>(nameof(StateInsetShadow));

    internal static readonly StyledProperty<double> StateOpacityProperty =
        AvaloniaProperty.Register<WorkflowNodeCard, double>(nameof(StateOpacity), 1);

    private WorkflowNodeState _displayState;
    private WorkflowNodeState? _hostState;
    private ContentPresenter? _icon;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private WorkflowStateMark? _state;
    private WorkflowCardPanel? _layout;
    private ContentPresenter? _content;
    private WorkflowNode? _observed;
    private ILanguageManager? _languages;

    static WorkflowNodeCard()
    {
        FocusableProperty.OverrideDefaultValue<WorkflowNodeCard>(false);
    }

    /// <summary>Creates a card; the canvas sets its <see cref="Node"/>.</summary>
    public WorkflowNodeCard()
    {
        ArcTokens.Bind(this, AccentBrushProperty, ArcDesignTokenKind.Accent);
    }

    /// <inheritdoc cref="NodeProperty"/>
    public WorkflowNode? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <summary>
    /// True once the card has been measured (its template applied). A large workflow measures only the cards near the
    /// view (see <see cref="WorkflowCanvas"/> virtualization); the others are not realized and have no size yet.
    /// </summary>
    internal bool IsRealized { get; set; }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>True when the card shows content from <see cref="ContentTemplate"/> (tests).</summary>
    internal bool IsCustom => PseudoClasses.Contains(CustomPseudoClass);

    /// <inheritdoc cref="DisplayStateProperty"/>
    public WorkflowNodeState DisplayState
    {
        get => _displayState;
        private set => SetAndRaise(DisplayStateProperty, ref _displayState, value);
    }

    internal IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    internal double RunningBorderAccentShare
    {
        get => GetValue(RunningBorderAccentShareProperty);
        set => SetValue(RunningBorderAccentShareProperty, value);
    }

    internal double RunningFillAccentShare
    {
        get => GetValue(RunningFillAccentShareProperty);
        set => SetValue(RunningFillAccentShareProperty, value);
    }

    internal IBrush? FailedBorderBrush
    {
        get => GetValue(FailedBorderBrushProperty);
        set => SetValue(FailedBorderBrushProperty, value);
    }

    internal BoxShadows CardShadow
    {
        get => GetValue(CardShadowProperty);
        set => SetValue(CardShadowProperty, value);
    }

    internal double SkippedOpacity
    {
        get => GetValue(SkippedOpacityProperty);
        set => SetValue(SkippedOpacityProperty, value);
    }

    internal IBrush? StateBackground
    {
        get => GetValue(StateBackgroundProperty);
        set => SetValue(StateBackgroundProperty, value);
    }

    internal IBrush? StateBorderBrush
    {
        get => GetValue(StateBorderBrushProperty);
        set => SetValue(StateBorderBrushProperty, value);
    }

    internal BoxShadows StateInsetShadow
    {
        get => GetValue(StateInsetShadowProperty);
        set => SetValue(StateInsetShadowProperty, value);
    }

    internal BoxShadows StateShadow
    {
        get => GetValue(StateShadowProperty);
        set => SetValue(StateShadowProperty, value);
    }

    internal double StateOpacity
    {
        get => GetValue(StateOpacityProperty);
        set => SetValue(StateOpacityProperty, value);
    }

    /// <summary>The line under the title (tests).</summary>
    internal string? SubtitleText => _subtitle?.Text;

    /// <summary>The state mark (tests).</summary>
    internal WorkflowStateMark? StateMark => _state;

    /// <summary>Whether the canvas selected this card's step.</summary>
    internal bool IsSelected => PseudoClasses.Contains(SelectedPseudoClass);

    /// <summary>Lets the canvas mark the card selected: an accent border and halo, and "selected" for screen readers.</summary>
    internal void SetSelected(bool selected)
    {
        if (IsSelected != selected)
        {
            PseudoClasses.Set(SelectedPseudoClass, selected);
            Refresh();
        }
    }

    /// <summary>Lets the canvas fade the card while a connection is made that cannot go to it.</summary>
    internal void SetRefusing(bool refusing) => PseudoClasses.Set(RefusingPseudoClass, refusing);

    /// <summary>Lets the canvas lift the card above the others while it is dragged.</summary>
    internal void SetDragging(bool dragging)
    {
        PseudoClasses.Set(DraggingPseudoClass, dragging);
        ZIndex = dragging ? 1 : 0;
    }

    /// <summary>Lets the canvas switch the card to its narrow size (Arc's narrow layout).</summary>
    internal void SetNarrow(bool narrow) => PseudoClasses.Set(NarrowPseudoClass, narrow);

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new WorkflowNodeCardAutomationPeer(this);

    /// <summary>A card reads as one item of the canvas's group, named by its step (see <see cref="AutomationNameOf"/>).</summary>
    private sealed class WorkflowNodeCardAutomationPeer(WorkflowNodeCard owner) : Avalonia.Automation.Peers.ControlAutomationPeer(owner)
    {
        protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore()
            => Avalonia.Automation.Peers.AutomationControlType.ListItem;
    }

    /// <summary>Lets the canvas hold back the shown state (see <see cref="DisplayState"/>); null follows the step.</summary>
    internal void SetHostState(WorkflowNodeState? state)
    {
        _hostState = state;
        Refresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<ContentPresenter>(IconPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _state = e.NameScope.Find<WorkflowStateMark>(StatePart);
        _layout = e.NameScope.Find<WorkflowCardPanel>(LayoutPart);
        _content = e.NameScope.Find<ContentPresenter>(ContentPart);
        UpdateContent();
        UpdateIcon();
        UpdateSize();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Node);
        _languages = WorkflowText.Languages;
        if (_languages is not null)
        {
            _languages.LanguageChanged += OnLanguageChanged;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The card may outlive its place on screen while the workflow lives on: stop listening to the step.
        Observe(null);
        if (_languages is not null)
        {
            _languages.LanguageChanged -= OnLanguageChanged;
            _languages = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NodeProperty)
        {
            _hostState = null;
            if (VisualRoot is not null)
            {
                Observe(Node);
            }
            UpdateContent();
            UpdateIcon();
            UpdateSize();
            Refresh();
        }
        else if (change.Property == ContentTemplateProperty)
        {
            UpdateContent();
        }
        else if (change.Property == AccentBrushProperty || change.Property == BackgroundProperty ||
                 change.Property == BorderBrushProperty || change.Property == RunningBorderAccentShareProperty ||
                 change.Property == RunningFillAccentShareProperty || change.Property == FailedBorderBrushProperty ||
                 change.Property == CardShadowProperty || change.Property == SkippedOpacityProperty)
        {
            UpdateStateVisuals();
        }
    }

    private void Observe(WorkflowNode? node)
    {
        if (ReferenceEquals(_observed, node))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnNodePropertyChanged;
        }
        _observed = node;
        if (node is not null)
        {
            node.PropertyChanged += OnNodePropertyChanged;
        }
    }

    private void OnNodePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WorkflowNode.IconProperty)
        {
            UpdateIcon();
        }
        else if (e.Property == WorkflowNode.KindProperty)
        {
            UpdateContent();
            Refresh();
        }
        else if (e.Property == WorkflowNode.SizeProperty || e.Property == WorkflowNode.AppearanceProperty)
        {
            UpdateSize();
            Refresh();
        }
        else if (e.Property != WorkflowNode.PositionProperty)
        {
            Refresh();
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
        }
        else
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    /// <summary>Shows the template's content when <see cref="ContentTemplate"/> matches the step, else the default row.</summary>
    private void UpdateContent()
    {
        var node = Node;
        var custom = node is not null && ContentTemplate is { } template && template.Match(node);
        PseudoClasses.Set(CustomPseudoClass, custom);
        if (_layout is not null)
        {
            _layout.IsVisible = !custom;
        }
        if (_content is null)
        {
            return;
        }
        // The card builds the content itself (a presenter would also ask the template to match no content at all) and gives
        // it the step as its data context: a presenter showing a control does not pass its own on.
        _content.Content = null;
        if (custom && ContentTemplate!.Build(node) is { } built)
        {
            built.DataContext = node;
            _content.Content = built;
        }
        _content.IsVisible = custom;
    }

    private void UpdateSize()
    {
        var node = Node;
        PseudoClasses.Set(CompactPseudoClass, node?.Appearance == WorkflowNodeAppearance.Compact);
        if (_layout is not null)
        {
            _layout.IsCompact = node?.Appearance == WorkflowNodeAppearance.Compact;
        }
        if (node?.Size is { } size)
        {
            Width = size.Width;
            Height = size.Height;
        }
        else
        {
            ClearValue(WidthProperty);
            ClearValue(HeightProperty);
        }
    }

    private void UpdateIcon()
    {
        if (_icon is null)
        {
            return;
        }
        // Clear before replacing (see the null-template rule in the implementation guide).
        _icon.Content = null;
        _icon.Content = Node?.Icon switch
        {
            Geometry geometry => CreateGlyph(geometry),
            IImage image => new Image { Source = image, Width = 18, Height = 18 },
            var other => other
        };
    }

    private static ArcGlyph CreateGlyph(Geometry geometry)
    {
        // Arc draws step glyphs at 16–17 with a 1.75 stroke in the secondary text color.
        var glyph = new ArcGlyph
        {
            Data = geometry,
            Size = 17,
            StrokeThickness = 1.75,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return glyph;
    }

    /// <summary>Shows the step: state, pseudo-classes, text, time, colors and the screen reader name.</summary>
    private void Refresh()
    {
        var node = Node;
        var state = _hostState ?? node?.State ?? WorkflowNodeState.Idle;
        DisplayState = state;
        PseudoClasses.Set(PendingPseudoClass, state == WorkflowNodeState.Pending);
        PseudoClasses.Set(RunningPseudoClass, state == WorkflowNodeState.Running);
        PseudoClasses.Set(DonePseudoClass, state == WorkflowNodeState.Done);
        PseudoClasses.Set(FailedPseudoClass, state == WorkflowNodeState.Failed);
        PseudoClasses.Set(SkippedPseudoClass, state == WorkflowNodeState.Skipped);
        PseudoClasses.Set(LockedPseudoClass, node?.IsLocked == true);

        var subtitle = node is null ? null : SubtitleOf(node, state);
        if (_title is not null)
        {
            _title.Text = node?.Title;
        }
        if (_subtitle is not null)
        {
            _subtitle.Text = subtitle;
            _subtitle.IsVisible = !string.IsNullOrEmpty(subtitle);
        }
        var meta = state == WorkflowNodeState.Done && node?.Duration is { } duration ? WorkflowText.Duration(duration) : null;
        if (_state is not null)
        {
            // The time first, so the check arrives with it.
            _state.Meta = meta;
            _state.State = state;
        }
        UpdateStateVisuals();
        AutomationProperties.SetName(this, node is null ? null : AutomationNameOf(node, state, subtitle, meta, IsSelected));
    }

    private static string? SubtitleOf(WorkflowNode node, WorkflowNodeState state) => state switch
    {
        WorkflowNodeState.Done => node.Result ?? node.Description,
        WorkflowNodeState.Failed => node.Error ?? node.Description,
        WorkflowNodeState.Skipped => WorkflowText.State(WorkflowNodeState.Skipped),
        _ => node.Description
    };

    /// <summary>"Find account, action, Done, 118 ms, Enterprise, 212 seats": the state is always in words.</summary>
    private static string AutomationNameOf(WorkflowNode node, WorkflowNodeState state, string? subtitle, string? meta, bool selected)
    {
        var parts = new List<string>(5);
        void Add(string? part)
        {
            if (!string.IsNullOrWhiteSpace(part) && !parts.Contains(part))
            {
                parts.Add(part);
            }
        }
        Add(node.Title);
        Add(node.Kind);
        Add(WorkflowText.State(state));
        Add(meta);
        Add(subtitle);
        if (selected)
        {
            Add(WorkflowText.Get(Localization.WorkflowLangResourceKind.CardSelected));
        }
        return string.Join(", ", parts);
    }

    /// <summary>Frame colors, shadow and opacity for the shown state (accent mixes in OKLab, like Arc's color-mix).</summary>
    private void UpdateStateVisuals()
    {
        var surface = Background;
        var border = BorderBrush;
        IBrush? fill = surface;
        IBrush? outline = border;
        switch (DisplayState)
        {
            case not WorkflowNodeState.Skipped when IsSelected && AccentBrush is not null:
                // A selected card's accent border wins over the run's colors (the fill still shows a running step).
                outline = AccentBrush;
                if (DisplayState == WorkflowNodeState.Running && AccentBrush is ISolidColorBrush selectedAccent &&
                    surface is ISolidColorBrush selectedSurface)
                {
                    fill = new ImmutableSolidColorBrush(Oklch.MixOklab(selectedSurface.Color, selectedAccent.Color, RunningFillAccentShare));
                }
                break;
            case WorkflowNodeState.Running when AccentBrush is ISolidColorBrush accent:
                if (border is ISolidColorBrush b)
                {
                    outline = new ImmutableSolidColorBrush(Oklch.MixOklab(b.Color, accent.Color, RunningBorderAccentShare));
                }
                if (surface is ISolidColorBrush s)
                {
                    fill = new ImmutableSolidColorBrush(Oklch.MixOklab(s.Color, accent.Color, RunningFillAccentShare));
                }
                break;
            case WorkflowNodeState.Failed:
                outline = FailedBorderBrush ?? border;
                break;
        }
        StateBackground = fill;
        StateBorderBrush = outline;
        var skipped = DisplayState == WorkflowNodeState.Skipped;
        var shadows = skipped ? default : CardShadow;
        StateShadow = WorkflowCardShadow.Outer(shadows);
        StateInsetShadow = WorkflowCardShadow.Inset(shadows);
        StateOpacity = skipped ? SkippedOpacity : 1;
    }
}
