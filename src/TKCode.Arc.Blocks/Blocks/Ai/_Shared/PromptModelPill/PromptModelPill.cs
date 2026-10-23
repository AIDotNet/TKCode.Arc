using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The prompt studio's model pill: the model's mark (14px), its short name (<see cref="AiModel.DisplayName"/>, medium) and
/// the effort word (regular, secondary color) 6px apart, then a ChevronDown 14 that does not turn. Same frame as the version
/// pill: 32px, padding 0 9, the pill radius on the surface, SurfaceMuted with the strong border under the pointer and while
/// open. Pressing it raises <see cref="PromptHeaderPill.OpenRequested"/> (the studio opens its model menu).
/// <para>
/// The effort shows only for a model that offers effort levels and while <see cref="Effort"/> is set; its word comes from
/// <see cref="EffortText"/>. A new model crossfades the mark (no movement); a new name or effort crossfades as a label (old
/// out 100ms, new in 160ms rising 2px), each on its own, and the width follows on the quick spring. Reduced motion
/// crossfades in 120ms and jumps the width; Off changes at once.
/// </para>
/// </summary>
[TemplatePart(MarkPart, typeof(ArcContentSwap))]
[TemplatePart(NamePart, typeof(ArcContentSwap))]
[TemplatePart(EffortPart, typeof(ArcContentSwap))]
[TemplatePart(TextSizePart, typeof(SpringSizeDecorator))]
internal sealed class PromptModelPill : PromptHeaderPill
{
    public const string MarkPart = "PART_Mark";
    public const string NamePart = "PART_Name";
    public const string EffortPart = "PART_Effort";
    public const string TextSizePart = "PART_TextSize";

    /// <summary>Gap between the name and the effort word (the effort layer carries it, so no effort leaves no gap).</summary>
    internal const double EffortGap = 6;

    /// <summary>The mark is 14px and 6px before the name (the mark layer carries the gap, so no mark leaves no gap).</summary>
    internal const double MarkSize = 14;

    internal const double MarkGap = 6;

    /// <summary>The model shown; null shows nothing but the chevron.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<PromptModelPill, AiModel?>(nameof(Model));

    /// <summary>The effort shown after the name (only for a model that offers effort levels).</summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<PromptModelPill, ModelEffort?>(nameof(Effort));

    /// <summary>The word for an effort level ("High"); null uses <see cref="ModelEffortNames.Name"/>.</summary>
    public static readonly StyledProperty<Func<ModelEffort, string>?> EffortTextProperty =
        AvaloniaProperty.Register<PromptModelPill, Func<ModelEffort, string>?>(nameof(EffortText));

    /// <summary>The providers that supply the mark of a model without its own (by <see cref="AiModel.ProviderId"/>).</summary>
    public static readonly StyledProperty<IEnumerable<AiModelProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<PromptModelPill, IEnumerable<AiModelProvider>?>(nameof(Providers));

    /// <summary>
    /// Makes the accessible name from the model's full name and the effort word (null when none shows), such as
    /// "Model: Atlas Medium, High effort"; null names the pill by the name and the effort.
    /// </summary>
    public static readonly StyledProperty<Func<string, string?, string>?> NameFormatProperty =
        AvaloniaProperty.Register<PromptModelPill, Func<string, string?, string>?>(nameof(NameFormat));

    private ArcContentSwap? _markSwap;
    private ArcContentSwap? _nameSwap;
    private ArcContentSwap? _effortSwap;
    private SpringSizeDecorator? _textSize;
    private ModelMark? _mark;
    private AiModel? _markModel;
    private PromptPillText? _name;
    private PromptPillText? _effort;
    private bool _hasEffortLayer;
    private AiModel? _observedModel;
    private INotifyCollectionChanged? _observedProviders;
    private bool _attached;

    /// <inheritdoc cref="ModelProperty"/>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="EffortTextProperty"/>
    public Func<ModelEffort, string>? EffortText { get => GetValue(EffortTextProperty); set => SetValue(EffortTextProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IEnumerable<AiModelProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="NameFormatProperty"/>
    public Func<string, string?, string>? NameFormat { get => GetValue(NameFormatProperty); set => SetValue(NameFormatProperty, value); }

    protected override Type StyleKeyOverride => typeof(PromptModelPill);

    /// <summary>The mark on screen, or null.</summary>
    internal ModelMark? CurrentMark => _mark;

    /// <summary>The name layer on screen.</summary>
    internal PromptPillText? CurrentName => _name;

    /// <summary>The effort layer on screen, or null when no effort shows.</summary>
    internal PromptPillText? CurrentEffort => _hasEffortLayer ? _effort : null;

    /// <summary>The width spring of the text.</summary>
    internal SpringSizeDecorator? TextSize => _textSize;

    /// <summary>The effort word shown, or null.</summary>
    internal string? ShownEffortText => Model is { SupportsEffort: true } && Effort is { } effort ? EffortWord(effort) : null;

    private string EffortWord(ModelEffort effort) => EffortText is { } text ? text(effort) : ModelEffortNames.Name(effort);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _markSwap = e.NameScope.Find<ArcContentSwap>(MarkPart);
        _nameSwap = e.NameScope.Find<ArcContentSwap>(NamePart);
        _effortSwap = e.NameScope.Find<ArcContentSwap>(EffortPart);
        _textSize = e.NameScope.Find<SpringSizeDecorator>(TextSizePart);
        // New parts start with the current content (their first content never animates).
        _mark = null;
        _markModel = null;
        _name = null;
        _effort = null;
        _hasEffortLayer = false;
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty)
        {
            ObserveModel();
            Refresh();
        }
        else if (change.Property == ProvidersProperty)
        {
            ObserveProviders();
            Refresh();
        }
        else if (change.Property == EffortProperty || change.Property == EffortTextProperty || change.Property == NameFormatProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var model = Model;
        var effort = ShownEffortText;
        UpdateMark(model);
        UpdateText(model?.DisplayName ?? string.Empty, effort);
        var fullName = model?.Name ?? string.Empty;
        AutomationProperties.SetName(this, NameFormat is { } format
            ? format(fullName, effort)
            : effort is null ? fullName : fullName + ", " + effort);
    }

    /// <summary>A different model brings a new mark (crossfaded); the same model with a new mark or color updates it in place.</summary>
    private void UpdateMark(AiModel? model)
    {
        if (_markSwap is null)
        {
            return;
        }
        var mark = ModelMark.Resolve(model, Providers, out var color);
        if (ReferenceEquals(_markModel, model) && _mark is not null)
        {
            _mark.Mark = mark;
            _mark.MarkColor = color;
            return;
        }
        _markModel = model;
        _mark = mark is null ? null : new ModelMark { Mark = mark, MarkColor = color, Size = MarkSize, Margin = new Thickness(0, 0, MarkGap, 0) };
        _markSwap.Content = _mark;
    }

    /// <summary>Each word changes (and crossfades) only when it differs from the one on screen.</summary>
    private void UpdateText(string name, string? effort)
    {
        if (_nameSwap is not null && (_name is null || _name.Text != name))
        {
            _name = new PromptPillText { Text = name };
            _nameSwap.Content = _name;
        }
        if (_effortSwap is null)
        {
            return;
        }
        if (effort is null)
        {
            if (_hasEffortLayer)
            {
                _hasEffortLayer = false;
                _effortSwap.Content = null;
            }
            return;
        }
        if (!_hasEffortLayer || _effort is null || _effort.Text != effort)
        {
            _effort = new PromptPillText { Text = effort, IsSecondary = true, Margin = new Thickness(EffortGap, 0, 0, 0) };
            _hasEffortLayer = true;
            _effortSwap.Content = _effort;
        }
    }

    #region Data observation

    // The model and the providers are app data that can outlive the pill: followed only while the pill is in a window.

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveModel();
        ObserveProviders();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveModel();
        ObserveProviders();
    }

    private void ObserveModel()
    {
        var next = _attached ? Model : null;
        if (ReferenceEquals(next, _observedModel))
        {
            return;
        }
        if (_observedModel is not null)
        {
            _observedModel.PropertyChanged -= OnModelPropertyChanged;
        }
        _observedModel = next;
        if (next is not null)
        {
            next.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void ObserveProviders()
    {
        var next = _attached ? Providers as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _observedProviders))
        {
            return;
        }
        if (_observedProviders is not null)
        {
            _observedProviders.CollectionChanged -= OnProvidersChanged;
        }
        _observedProviders = next;
        if (next is not null)
        {
            next.CollectionChanged += OnProvidersChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiModel.NameProperty || e.Property == AiModel.ShortNameProperty || e.Property == AiModel.MarkProperty ||
            e.Property == AiModel.MarkColorProperty || e.Property == AiModel.ProviderIdProperty || e.Property == AiModel.EffortLevelsProperty)
        {
            Refresh();
        }
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    #endregion
}
