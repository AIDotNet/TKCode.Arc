using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>Default items panel for <see cref="RadioGroup"/>; updates orientation and token-backed spacing without recreating item containers.</summary>
internal sealed class RadioGroupPanel : StackPanel
{
    private IDisposable? _spacingBinding;

    internal void Sync(Orientation orientation, bool borderless)
    {
        Orientation = orientation;
        var tokenKind = (orientation == Orientation.Horizontal, borderless) switch
        {
            (true, true) => RadioGroupTokenKind.BorderlessHorizontalGap,
            (true, false) => RadioGroupTokenKind.HorizontalGap,
            (false, true) => RadioGroupTokenKind.BorderlessRowGap,
            (false, false) => RadioGroupTokenKind.RowGap
        };
        _spacingBinding?.Dispose();
        _spacingBinding = this.Bind(SpacingProperty, this.GetResourceObservable(RadioGroupTokens.Key(tokenKind)));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _spacingBinding?.Dispose();
        _spacingBinding = null;
    }
}

/// <summary>Visual style of a <see cref="RadioGroup"/>.</summary>
public enum RadioGroupVariant
{
    /// <summary>Bordered rows with a gliding selection highlight (Arc default).</summary>
    Default,

    /// <summary>Minimal inline radio options without row borders, card fill or gliding highlight.</summary>
    Borderless
}

/// <summary>
/// Arc radio group: choose one option from a visible set. In the default variant one highlight (a <c>ControlOn</c>
/// border with a faint fill) glides to the chosen row on the morph spring while the new dot springs in and the old one
/// shrinks away, so a change reads as a single physical move. <see cref="Orientation"/> arranges the options vertically
/// or horizontally, and <see cref="Variant"/> switches between bordered rows and a minimal borderless radio style.
/// Arrow keys take the same path; the group is one tab stop (the selected row). Options come from
/// <see cref="ItemsControl.ItemsSource"/> or inline <see cref="RadioOption"/> items;
/// <see cref="SelectingItemsControl.SelectedValue"/> (two-way) is the selected option's <see cref="RadioOption.Value"/>.
/// The <see cref="HeaderedSelectingItemsControl.Header"/> is the legend and the group's accessible name.
/// </summary>
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[PseudoClasses(HorizontalPseudoClass, BorderlessPseudoClass)]
public class RadioGroup : HeaderedSelectingItemsControl
{
    public const string HighlightPart = "PART_Highlight";

    /// <summary>Set while <see cref="Orientation"/> is <see cref="Avalonia.Layout.Orientation.Horizontal"/>.</summary>
    public const string HorizontalPseudoClass = ":horizontal";

    /// <summary>Set while <see cref="Variant"/> is <see cref="RadioGroupVariant.Borderless"/>.</summary>
    public const string BorderlessPseudoClass = ":borderless";

    /// <summary>How the options are arranged: <see cref="Avalonia.Layout.Orientation.Vertical"/> (default) or <see cref="Avalonia.Layout.Orientation.Horizontal"/>.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<RadioGroup, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>Row chrome: <see cref="RadioGroupVariant.Default"/> (bordered rows with gliding highlight) or <see cref="RadioGroupVariant.Borderless"/>.</summary>
    public static readonly StyledProperty<RadioGroupVariant> VariantProperty =
        AvaloniaProperty.Register<RadioGroup, RadioGroupVariant>(nameof(Variant), RadioGroupVariant.Default);

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc cref="VariantProperty"/>
    public RadioGroupVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    private readonly TogglesGroupHost _host;
    private bool _usesHeaderName;
    private bool _pendingLayoutSync;

    static RadioGroup()
    {
        SelectionDataValidation.Enable<RadioGroup>();
        FocusableProperty.OverrideDefaultValue<RadioGroup>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<RadioGroup>(KeyboardNavigationMode.Once);
    }

    public RadioGroup()
    {
        _host = new TogglesGroupHost(this, flipInRtl: false, ApplyOption);
        SelectedValueBinding = TogglesValueBinding.Create();
        SelectionChanged += (_, _) => _host.OnSelectionChanged();
    }

    protected override Type StyleKeyOverride => typeof(RadioGroup);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<RadioGroupItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new RadioGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        ApplyOption(container, item);
        if (container is RadioGroupItem row)
        {
            row.SetBorderless(Variant == RadioGroupVariant.Borderless);
        }
    }

    private static void ApplyOption(Control container, object? item)
    {
        if (container is RadioGroupItem row && item is RadioOption option)
        {
            row.Content = option.Label ?? option.Value?.ToString();
            row.Description = option.Description;
            row.IsEnabled = option.IsEnabled;
            row.IsVisible = option.IsVisible;
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        SyncItemsPanel();
        _host.UpdateTabStop();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is RadioGroupItem row)
        {
            row.ClearValue(ContentControl.ContentProperty);
            row.ClearValue(RadioGroupItem.DescriptionProperty);
            row.ClearValue(IsEnabledProperty);
            row.SetBorderless(false);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        if (highlight is not null)
        {
            highlight.PropertyChanged += (_, args) =>
            {
                // The fill is the stroke color at 5% (color-mix with transparent), which depends on the accent.
                if (args.Property == GlidingIndicator.StrokeProperty)
                {
                    highlight.Fill = TogglesBrushes.WithOpacity(highlight.Stroke, 0.05);
                }
            };
            highlight.Fill = TogglesBrushes.WithOpacity(highlight.Stroke, 0.05);
        }
        _host.AttachIndicator(highlight, inset: 0.5, fadeInFirst: false);
        ScheduleLayoutSync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty)
        {
            var name = AutomationProperties.GetName(this);
            if (Header is string header && (string.IsNullOrEmpty(name) || _usesHeaderName))
            {
                AutomationProperties.SetName(this, header);
                _usesHeaderName = true;
            }
            else if (_usesHeaderName && Header is not string)
            {
                ClearValue(AutomationProperties.NameProperty);
                _usesHeaderName = false;
            }
        }
        else if (change.Property == IsEnabledProperty)
        {
            _host.UpdateTabStop();
        }
        else if (change.Property == OrientationProperty)
        {
            var horizontal = Orientation == Orientation.Horizontal;
            PseudoClasses.Set(HorizontalPseudoClass, horizontal);
            _host.FlipInRtl = horizontal;
            ScheduleLayoutSync();
        }
        else if (change.Property == VariantProperty)
        {
            var borderless = Variant == RadioGroupVariant.Borderless;
            PseudoClasses.Set(BorderlessPseudoClass, borderless);
            foreach (var container in GetRealizedContainers())
            {
                if (container is RadioGroupItem row)
                {
                    row.SetBorderless(borderless);
                }
            }
            ScheduleLayoutSync();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_pendingLayoutSync)
        {
            _pendingLayoutSync = false;
            LayoutUpdated -= OnLayoutSync;
        }
    }

    private void SyncItemsPanel()
    {
        if (ItemsPanelRoot is RadioGroupPanel panel)
        {
            panel.Sync(Orientation, Variant == RadioGroupVariant.Borderless);
        }
    }

    private void ScheduleLayoutSync()
    {
        SyncItemsPanel();
        _host.UpdateTabStop();
        if (!_pendingLayoutSync)
        {
            _pendingLayoutSync = true;
            LayoutUpdated += OnLayoutSync;
        }
    }

    private void OnLayoutSync(object? sender, EventArgs e)
    {
        _pendingLayoutSync = false;
        LayoutUpdated -= OnLayoutSync;
        SyncItemsPanel();
        _host.UpdateTabStop();
        _host.RefreshIndicator();
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TogglesGroupAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_host.HandleKey(e))
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _host.UpdateTabStop();
    }
}
