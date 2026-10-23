using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The tab list of the extraction's form pane (spec 3.4, Arc's small tabs): a 38px SurfaceMuted shell (1px Border, radius
/// 18, 3 inside) with Fields, JSON and Schema 2px apart, each 30 high, 0 12 inside, radius 15, 12 / 16.8 medium, TextMuted
/// and AccentStrong when selected; a Surface block with a 1px Border and the resting shadow glides under the selected tab
/// (morph spring; Reduced and Off jump). One Tab stop (the selected tab); Left / Right move and select (wrapping), Home and
/// End go to the ends. It only draws the strip; the owner shows the panel.
/// </summary>
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(FieldsPart, typeof(ExtractionTabButton))]
[TemplatePart(JsonPart, typeof(ExtractionTabButton))]
[TemplatePart(SchemaPart, typeof(ExtractionTabButton))]
internal sealed class ExtractionTabStrip : TemplatedControl
{
    public const string HighlightPart = "PART_Highlight";
    public const string FieldsPart = "PART_Fields";
    public const string JsonPart = "PART_Json";
    public const string SchemaPart = "PART_Schema";

    /// <summary>The selected tab (two-way).</summary>
    public static readonly StyledProperty<ExtractionTab> SelectedTabProperty =
        AvaloniaProperty.Register<ExtractionTabStrip, ExtractionTab>(nameof(SelectedTab), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The Fields tab's text.</summary>
    public static readonly StyledProperty<string?> FieldsTextProperty =
        AvaloniaProperty.Register<ExtractionTabStrip, string?>(nameof(FieldsText));

    /// <summary>The JSON tab's text.</summary>
    public static readonly StyledProperty<string?> JsonTextProperty =
        AvaloniaProperty.Register<ExtractionTabStrip, string?>(nameof(JsonText));

    /// <summary>The Schema tab's text.</summary>
    public static readonly StyledProperty<string?> SchemaTextProperty =
        AvaloniaProperty.Register<ExtractionTabStrip, string?>(nameof(SchemaText));

    /// <summary>The accessible name of the list ("Output").</summary>
    public static readonly StyledProperty<string?> ListNameProperty =
        AvaloniaProperty.Register<ExtractionTabStrip, string?>(nameof(ListName));

    private GlidingIndicator? _highlight;
    private ExtractionTabButton? _fields;
    private ExtractionTabButton? _json;
    private ExtractionTabButton? _schema;
    private Rect? _placed;
    private bool _glide;

    static ExtractionTabStrip()
    {
        FocusableProperty.OverrideDefaultValue<ExtractionTabStrip>(false);
    }

    /// <inheritdoc cref="SelectedTabProperty"/>
    public ExtractionTab SelectedTab { get => GetValue(SelectedTabProperty); set => SetValue(SelectedTabProperty, value); }

    /// <inheritdoc cref="FieldsTextProperty"/>
    public string? FieldsText { get => GetValue(FieldsTextProperty); set => SetValue(FieldsTextProperty, value); }

    /// <inheritdoc cref="JsonTextProperty"/>
    public string? JsonText { get => GetValue(JsonTextProperty); set => SetValue(JsonTextProperty, value); }

    /// <inheritdoc cref="SchemaTextProperty"/>
    public string? SchemaText { get => GetValue(SchemaTextProperty); set => SetValue(SchemaTextProperty, value); }

    /// <inheritdoc cref="ListNameProperty"/>
    public string? ListName { get => GetValue(ListNameProperty); set => SetValue(ListNameProperty, value); }

    /// <summary>The gliding block (tests).</summary>
    internal GlidingIndicator? Highlight => _highlight;

    /// <summary>The strip's width with every tab at its own width (whatever room it was given): the frame, the padding, the tabs and the 2px gaps.</summary>
    internal double NaturalWidth
    {
        get
        {
            var width = Padding.Left + Padding.Right + BorderThickness.Left + BorderThickness.Right;
            var count = 0;
            foreach (var button in Buttons())
            {
                width += button.DesiredSize.Width;
                count++;
            }
            return width + Math.Max(0, count - 1) * TabGap;
        }
    }

    /// <summary>The space between the tabs (the theme's 2).</summary>
    internal const double TabGap = 2;

    /// <summary>The button of <paramref name="tab"/>.</summary>
    internal ExtractionTabButton? ButtonOf(ExtractionTab tab) => tab switch
    {
        ExtractionTab.Fields => _fields,
        ExtractionTab.Json => _json,
        _ => _schema
    };

    /// <summary>Focuses the selected tab.</summary>
    public bool FocusSelected(NavigationMethod method = NavigationMethod.Tab) => ButtonOf(SelectedTab)?.Focus(method) == true;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var button in Buttons())
        {
            button.Click -= OnTabClick;
        }
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _fields = e.NameScope.Find<ExtractionTabButton>(FieldsPart);
        _json = e.NameScope.Find<ExtractionTabButton>(JsonPart);
        _schema = e.NameScope.Find<ExtractionTabButton>(SchemaPart);
        if (_fields is not null)
        {
            _fields.Tab = ExtractionTab.Fields;
        }
        if (_json is not null)
        {
            _json.Tab = ExtractionTab.Json;
        }
        if (_schema is not null)
        {
            _schema.Tab = ExtractionTab.Schema;
        }
        foreach (var button in Buttons())
        {
            button.Click += OnTabClick;
        }
        _placed = null;
        _glide = false;
        UpdateTexts();
        UpdateSelection();
    }

    private IEnumerable<ExtractionTabButton> Buttons()
    {
        if (_fields is not null)
        {
            yield return _fields;
        }
        if (_json is not null)
        {
            yield return _json;
        }
        if (_schema is not null)
        {
            yield return _schema;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedTabProperty)
        {
            _glide = true;
            UpdateSelection();
            InvalidateArrange();
            if (ControlAutomationPeer.FromElement(this) is StripPeer peer)
            {
                peer.SelectionChanged();
            }
        }
        else if (change.Property == FieldsTextProperty || change.Property == JsonTextProperty || change.Property == SchemaTextProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == ListNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    private void UpdateTexts()
    {
        if (_fields is not null)
        {
            _fields.Content = FieldsText;
        }
        if (_json is not null)
        {
            _json.Content = JsonText;
        }
        if (_schema is not null)
        {
            _schema.Content = SchemaText;
        }
    }

    private void UpdateSelection()
    {
        foreach (var button in Buttons())
        {
            var selected = button.Tab == SelectedTab;
            button.IsSelected = selected;
            // Roving tab stop: only the selected tab is reached with Tab.
            KeyboardNavigation.SetIsTabStop(button, selected);
        }
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ExtractionTabButton button)
        {
            Select(button.Tab);
        }
    }

    /// <summary>The person chose <paramref name="tab"/>.</summary>
    internal void Select(ExtractionTab tab)
    {
        if (tab != SelectedTab)
        {
            SetCurrentValue(SelectedTabProperty, tab);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not ExtractionTabButton source)
        {
            return;
        }
        var index = (int)source.Tab;
        int? to = e.Key switch
        {
            Key.Left => (index + 2) % 3,
            Key.Right => (index + 1) % 3,
            Key.Home => 0,
            Key.End => 2,
            _ => null
        };
        if (to is not { } target)
        {
            return;
        }
        e.Handled = true;
        // Automatic activation: the tab that takes focus is the selected one.
        Select((ExtractionTab)target);
        ButtonOf((ExtractionTab)target)?.Focus(NavigationMethod.Directional);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // The block follows the selected tab: gliding when the selection changed, jumping on a resize or the first layout.
        if (ButtonOf(SelectedTab) is { } tab && _highlight is not null && tab.Bounds.Width > 0)
        {
            var origin = tab.TranslatePoint(default, _highlight) ?? tab.Bounds.Position;
            var rect = new Rect(origin, tab.Bounds.Size);
            if (_placed != rect || !_highlight.HasTarget)
            {
                var animate = _glide && _highlight.HasTarget;
                // The outline is drawn on the rectangle's edge: half a stroke in, like a CSS border.
                _highlight.MoveTo(rect.Deflate(_highlight.StrokeThickness / 2), animate);
                _placed = rect;
            }
        }
        _glide = false;
        return size;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StripPeer(this);

    /// <summary>A tab list (Arc's <c>role="tablist"</c>, "Output") whose selection is the selected tab.</summary>
    private sealed class StripPeer(ExtractionTabStrip owner) : ControlAutomationPeer(owner), ISelectionProvider
    {
        private ExtractionTabStrip Strip => (ExtractionTabStrip)Owner;

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => true;

        public IReadOnlyList<AutomationPeer> GetSelection() =>
            Strip.ButtonOf(Strip.SelectedTab) is { } tab && CreatePeerForElement(tab) is { } peer ? [peer] : [];

        public void SelectionChanged() => RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var children = new List<AutomationPeer>(3);
            foreach (var tab in Strip.Buttons())
            {
                children.Add(CreatePeerForElement(tab));
            }
            return children;
        }

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionProvider) ? this : base.GetProviderCore(providerType);
    }
}

/// <summary>One tab of <see cref="ExtractionTabStrip"/> (Arc's <c>role="tab"</c>).</summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class ExtractionTabButton : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";

    /// <summary>Whether this is the selected tab.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ExtractionTabButton, bool>(nameof(IsSelected));

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>The tab it stands for.</summary>
    public ExtractionTab Tab { get; set; }

    protected override Type StyleKeyOverride => typeof(ExtractionTabButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            var selected = change.GetNewValue<bool>();
            PseudoClasses.Set(SelectedPseudoClass, selected);
            if (ControlAutomationPeer.FromElement(this) is TabPeer peer)
            {
                peer.RaiseSelectedChanged(selected);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    /// <summary>A tab item, selected while selected (Arc's <c>aria-selected</c>).</summary>
    private sealed class TabPeer(ExtractionTabButton owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        private ExtractionTabButton Tab => (ExtractionTabButton)Owner;

        public bool IsSelected => Tab.IsSelected;

        public ISelectionProvider? SelectionContainer =>
            Tab.FindAncestorOfType<ExtractionTabStrip>() is { } strip ? CreatePeerForElement(strip) as ISelectionProvider : null;

        public void Select() => Tab.FindAncestorOfType<ExtractionTabStrip>()?.Select(Tab.Tab);

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void RaiseSelectedChanged(bool selected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !selected, selected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionItemProvider) ? this : base.GetProviderCore(providerType);
    }
}

/// <summary>
/// The body of the extraction (spec 3.1, 3.2, 7): the viewer (first child) and the form pane (second child) side by side
/// on a row <see cref="BodyHeight"/> high, the pane <c>min(FormPaneWidth, FormPaneShare × width)</c> wide; or, while
/// <see cref="IsStacked"/>, one above the other (<see cref="StackedViewerHeight"/> and <see cref="StackedFormHeight"/>).
/// Every further child (the link overlay) covers the whole body. Each pane scrolls on its own, so measuring is cheap.
/// </summary>
internal sealed class ExtractionBody : Panel
{
    /// <summary>Whether the panes are stacked.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ExtractionBody, bool>(nameof(IsStacked));

    /// <summary>The height of the side-by-side row (600).</summary>
    public static readonly StyledProperty<double> BodyHeightProperty =
        AvaloniaProperty.Register<ExtractionBody, double>(nameof(BodyHeight), 600);

    /// <summary>The widest the form pane grows (436).</summary>
    public static readonly StyledProperty<double> FormPaneWidthProperty =
        AvaloniaProperty.Register<ExtractionBody, double>(nameof(FormPaneWidth), 436);

    /// <summary>The form pane's share of the width (0.45).</summary>
    public static readonly StyledProperty<double> FormPaneShareProperty =
        AvaloniaProperty.Register<ExtractionBody, double>(nameof(FormPaneShare), 0.45);

    /// <summary>The viewer's height when stacked (379).</summary>
    public static readonly StyledProperty<double> StackedViewerHeightProperty =
        AvaloniaProperty.Register<ExtractionBody, double>(nameof(StackedViewerHeight), 379);

    /// <summary>The form's height when stacked (478).</summary>
    public static readonly StyledProperty<double> StackedFormHeightProperty =
        AvaloniaProperty.Register<ExtractionBody, double>(nameof(StackedFormHeight), 478);

    static ExtractionBody()
    {
        AffectsMeasure<ExtractionBody>(IsStackedProperty, BodyHeightProperty, FormPaneWidthProperty, FormPaneShareProperty,
            StackedViewerHeightProperty, StackedFormHeightProperty);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <inheritdoc cref="BodyHeightProperty"/>
    public double BodyHeight { get => GetValue(BodyHeightProperty); set => SetValue(BodyHeightProperty, value); }

    /// <inheritdoc cref="FormPaneWidthProperty"/>
    public double FormPaneWidth { get => GetValue(FormPaneWidthProperty); set => SetValue(FormPaneWidthProperty, value); }

    /// <inheritdoc cref="FormPaneShareProperty"/>
    public double FormPaneShare { get => GetValue(FormPaneShareProperty); set => SetValue(FormPaneShareProperty, value); }

    /// <inheritdoc cref="StackedViewerHeightProperty"/>
    public double StackedViewerHeight { get => GetValue(StackedViewerHeightProperty); set => SetValue(StackedViewerHeightProperty, value); }

    /// <inheritdoc cref="StackedFormHeightProperty"/>
    public double StackedFormHeight { get => GetValue(StackedFormHeightProperty); set => SetValue(StackedFormHeightProperty, value); }

    /// <summary>The form pane's width at a body width.</summary>
    internal double FormWidthAt(double width) => Math.Min(FormPaneWidth, Math.Round(width * FormPaneShare, 1));

    private (Rect Viewer, Rect Form, Rect All) Layout(double width)
    {
        if (IsStacked)
        {
            var viewer = new Rect(0, 0, width, StackedViewerHeight);
            return (viewer, new Rect(0, viewer.Bottom, width, StackedFormHeight), new Rect(0, 0, width, viewer.Height + StackedFormHeight));
        }
        var form = FormWidthAt(width);
        return (new Rect(0, 0, Math.Max(0, width - form), BodyHeight), new Rect(width - form, 0, form, BodyHeight), new Rect(0, 0, width, BodyHeight));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? FormPaneWidth / FormPaneShare : availableSize.Width;
        var (viewer, form, all) = Layout(width);
        for (var i = 0; i < Children.Count; i++)
        {
            var rect = i == 0 ? viewer : i == 1 ? form : all;
            Children[i].Measure(rect.Size);
        }
        return all.Size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (viewer, form, all) = Layout(finalSize.Width);
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(i == 0 ? viewer : i == 1 ? form : all);
        }
        return new Size(finalSize.Width, all.Height);
    }
}

/// <summary>How the footer of the extraction lays out its three parts (spec 7).</summary>
internal enum ExtractionFooterMode
{
    /// <summary>One row: the summary, the run's meta pushed right, Accept all.</summary>
    Row,

    /// <summary>Two rows (below 700): the summary and the meta, then Accept all on the left at its own width.</summary>
    Wrap,

    /// <summary>Three rows (below 520): the summary, the meta on the right, Accept all across the width.</summary>
    Narrow
}

/// <summary>
/// The footer's row (spec 3.6, 7): the summary (first child), the meta (second) and the Accept all button (third), 16
/// apart in one row, centered on the row's height; wrapped and narrow modes put them on two or three rows 10 apart.
/// </summary>
internal sealed class ExtractionFooter : Panel
{
    /// <summary>The layout.</summary>
    public static readonly StyledProperty<ExtractionFooterMode> ModeProperty =
        AvaloniaProperty.Register<ExtractionFooter, ExtractionFooterMode>(nameof(Mode));

    /// <summary>The space between the parts in a row (16).</summary>
    public const double Gap = 16;

    /// <summary>The space between rows (10).</summary>
    public const double RowGap = 10;

    static ExtractionFooter()
    {
        AffectsMeasure<ExtractionFooter>(ModeProperty);
    }

    /// <inheritdoc cref="ModeProperty"/>
    public ExtractionFooterMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    private Control? Summary => Children.Count > 0 ? Children[0] : null;

    private Control? Meta => Children.Count > 1 ? Children[1] : null;

    private Control? Action => Children.Count > 2 ? Children[2] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        Meta?.Measure(infinite);
        var meta = Meta?.DesiredSize ?? default;
        if (Mode == ExtractionFooterMode.Narrow)
        {
            Action?.Measure(new Size(width, double.PositiveInfinity));
        }
        else
        {
            Action?.Measure(infinite);
        }
        var action = Action?.DesiredSize ?? default;
        var summaryRoom = Mode switch
        {
            ExtractionFooterMode.Row => width - meta.Width - action.Width - 2 * Gap,
            ExtractionFooterMode.Wrap => width - meta.Width - Gap,
            _ => width
        };
        Summary?.Measure(new Size(Math.Max(0, summaryRoom), double.PositiveInfinity));
        var summary = Summary?.DesiredSize ?? default;
        return Mode switch
        {
            ExtractionFooterMode.Row => new Size(Fit(width, summary.Width + meta.Width + action.Width + 2 * Gap),
                Math.Max(summary.Height, Math.Max(meta.Height, action.Height))),
            ExtractionFooterMode.Wrap => new Size(Fit(width, Math.Max(summary.Width + meta.Width + Gap, action.Width)),
                Math.Max(summary.Height, meta.Height) + RowGap + action.Height),
            _ => new Size(Fit(width, Math.Max(summary.Width, Math.Max(meta.Width, action.Width))),
                summary.Height + RowGap + meta.Height + RowGap + action.Height)
        };
    }

    private static double Fit(double available, double wanted) => double.IsInfinity(available) ? wanted : available;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var summary = Summary?.DesiredSize ?? default;
        var meta = Meta?.DesiredSize ?? default;
        var action = Action?.DesiredSize ?? default;
        switch (Mode)
        {
            case ExtractionFooterMode.Row:
            {
                var height = finalSize.Height;
                var actionX = width - action.Width;
                var metaX = actionX - Gap - meta.Width;
                Action?.Arrange(new Rect(actionX, (height - action.Height) / 2, action.Width, action.Height));
                Meta?.Arrange(new Rect(metaX, (height - meta.Height) / 2, meta.Width, meta.Height));
                Summary?.Arrange(new Rect(0, (height - summary.Height) / 2, Math.Max(0, Math.Min(summary.Width, metaX - Gap)), summary.Height));
                break;
            }
            case ExtractionFooterMode.Wrap:
            {
                var row = Math.Max(summary.Height, meta.Height);
                Summary?.Arrange(new Rect(0, (row - summary.Height) / 2, Math.Max(0, Math.Min(summary.Width, width - meta.Width - Gap)), summary.Height));
                Meta?.Arrange(new Rect(width - meta.Width, (row - meta.Height) / 2, meta.Width, meta.Height));
                Action?.Arrange(new Rect(0, row + RowGap, action.Width, action.Height));
                break;
            }
            default:
            {
                Summary?.Arrange(new Rect(0, 0, Math.Min(summary.Width, width), summary.Height));
                var y = summary.Height + RowGap;
                Meta?.Arrange(new Rect(Math.Max(0, width - meta.Width), y, Math.Min(meta.Width, width), meta.Height));
                y += meta.Height + RowGap;
                Action?.Arrange(new Rect(0, y, width, action.Height));
                break;
            }
        }
        return finalSize;
    }
}
