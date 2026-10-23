using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The model line under an AI answer (Arc answer): the model's mark (14px), its name, then "·"-separated figures from the
/// owner, "790 tokens · 20.3s", 12 / 16.8, the name in the secondary text color, the figures in the muted one with tabular
/// figures, 8px apart, the dots 3px in the muted color at 70%. A stopped version (<see cref="IsStopped"/>) shows only the
/// model. Narrow, the parts wrap 4px apart and a name that is still too long ends in an ellipsis. It reads as one line of
/// text ("Lumen Pro 4, 812 tokens, 18.6s").
/// </summary>
[TemplatePart(NamePart, typeof(TextBlock))]
[PseudoClasses(StoppedPseudoClass)]
internal sealed class AiModelLine : TemplatedControl
{
    public const string NamePart = "PART_Name";

    /// <summary>Set while the line shows only the model.</summary>
    public const string StoppedPseudoClass = ":stopped";

    /// <summary>The model.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<AiModelLine, AiModel?>(nameof(Model));

    /// <summary>The providers, for the mark of a model without its own.</summary>
    public static readonly StyledProperty<IEnumerable<AiModelProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<AiModelLine, IEnumerable<AiModelProvider>?>(nameof(Providers));

    /// <summary>The token count as shown ("790 tokens"); null or empty leaves it out.</summary>
    public static readonly StyledProperty<string?> TokensTextProperty =
        AvaloniaProperty.Register<AiModelLine, string?>(nameof(TokensText));

    /// <summary>The time as shown ("20.3s"); null or empty leaves it out.</summary>
    public static readonly StyledProperty<string?> DurationTextProperty =
        AvaloniaProperty.Register<AiModelLine, string?>(nameof(DurationText));

    /// <summary>The version was stopped: only the model shows.</summary>
    public static readonly StyledProperty<bool> IsStoppedProperty =
        AvaloniaProperty.Register<AiModelLine, bool>(nameof(IsStopped));

    /// <summary>The model's name (from <see cref="Model"/>; template use).</summary>
    internal static readonly DirectProperty<AiModelLine, string?> ModelNameProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, string?>(nameof(ModelName), o => o.ModelName);

    /// <summary>The mark to draw (the model's or its provider's; template use).</summary>
    internal static readonly DirectProperty<AiModelLine, object?> MarkProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, object?>(nameof(Mark), o => o.Mark);

    /// <summary>The fill of a geometry mark (template use).</summary>
    internal static readonly DirectProperty<AiModelLine, IBrush?> MarkColorProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, IBrush?>(nameof(MarkColor), o => o.MarkColor);

    /// <summary>Whether the model shows (a name or a mark; template use).</summary>
    internal static readonly DirectProperty<AiModelLine, bool> HasModelProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, bool>(nameof(HasModel), o => o.HasModel);

    /// <summary>Whether the token count shows (template use).</summary>
    internal static readonly DirectProperty<AiModelLine, bool> ShowsTokensProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, bool>(nameof(ShowsTokens), o => o.ShowsTokens);

    /// <summary>Whether the time shows (template use).</summary>
    internal static readonly DirectProperty<AiModelLine, bool> ShowsDurationProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, bool>(nameof(ShowsDuration), o => o.ShowsDuration);

    /// <summary>Whether a dot leads the token count (something shows before it; template use).</summary>
    internal static readonly DirectProperty<AiModelLine, bool> TokensDotProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, bool>(nameof(TokensDot), o => o.TokensDot);

    /// <summary>Whether a dot leads the time (something shows before it; template use).</summary>
    internal static readonly DirectProperty<AiModelLine, bool> DurationDotProperty =
        AvaloniaProperty.RegisterDirect<AiModelLine, bool>(nameof(DurationDot), o => o.DurationDot);

    private AiModel? _observed;
    private string? _modelName;
    private object? _mark;
    private IBrush? _markColor;
    private bool _hasModel;
    private bool _showsTokens;
    private bool _showsDuration;
    private bool _tokensDot;
    private bool _durationDot;

    static AiModelLine()
    {
        FocusableProperty.OverrideDefaultValue<AiModelLine>(false);
    }

    /// <inheritdoc cref="ModelProperty"/>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IEnumerable<AiModelProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="TokensTextProperty"/>
    public string? TokensText { get => GetValue(TokensTextProperty); set => SetValue(TokensTextProperty, value); }

    /// <inheritdoc cref="DurationTextProperty"/>
    public string? DurationText { get => GetValue(DurationTextProperty); set => SetValue(DurationTextProperty, value); }

    /// <inheritdoc cref="IsStoppedProperty"/>
    public bool IsStopped { get => GetValue(IsStoppedProperty); set => SetValue(IsStoppedProperty, value); }

    internal string? ModelName { get => _modelName; private set => SetAndRaise(ModelNameProperty, ref _modelName, value); }

    internal object? Mark { get => _mark; private set => SetAndRaise(MarkProperty, ref _mark, value); }

    internal IBrush? MarkColor { get => _markColor; private set => SetAndRaise(MarkColorProperty, ref _markColor, value); }

    internal bool HasModel { get => _hasModel; private set => SetAndRaise(HasModelProperty, ref _hasModel, value); }

    internal bool ShowsTokens { get => _showsTokens; private set => SetAndRaise(ShowsTokensProperty, ref _showsTokens, value); }

    internal bool ShowsDuration { get => _showsDuration; private set => SetAndRaise(ShowsDurationProperty, ref _showsDuration, value); }

    internal bool TokensDot { get => _tokensDot; private set => SetAndRaise(TokensDotProperty, ref _tokensDot, value); }

    internal bool DurationDot { get => _durationDot; private set => SetAndRaise(DurationDotProperty, ref _durationDot, value); }

    /// <summary>What the line reads as.</summary>
    internal string SpokenText { get; private set; } = string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty)
        {
            Observe(IsAttachedToVisualTree ? change.GetNewValue<AiModel?>() : null);
            Refresh();
        }
        else if (change.Property == ProvidersProperty || change.Property == TokensTextProperty || change.Property == DurationTextProperty
                 || change.Property == IsStoppedProperty)
        {
            Refresh();
        }
    }

    private bool IsAttachedToVisualTree => TopLevel.GetTopLevel(this) is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Model);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The model is app data that can outlive the line: it is followed only while the line is shown.
        Observe(null);
    }

    private void Observe(AiModel? model)
    {
        if (ReferenceEquals(model, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnModelPropertyChanged;
        }
        _observed = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiModel.NameProperty || e.Property == AiModel.MarkProperty || e.Property == AiModel.MarkColorProperty
            || e.Property == AiModel.ProviderIdProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var model = Model;
        ModelName = model?.Name;
        Mark = ModelMark.Resolve(model, Providers, out var color);
        MarkColor = color;
        var hasModel = !string.IsNullOrEmpty(ModelName) || Mark is not null;
        HasModel = hasModel;
        var stopped = IsStopped;
        PseudoClasses.Set(StoppedPseudoClass, stopped);
        ShowsTokens = !stopped && !string.IsNullOrEmpty(TokensText);
        ShowsDuration = !stopped && !string.IsNullOrEmpty(DurationText);
        TokensDot = ShowsTokens && hasModel;
        DurationDot = ShowsDuration && (hasModel || ShowsTokens);
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(ModelName))
        {
            parts.Add(ModelName!);
        }
        if (ShowsTokens)
        {
            parts.Add(TokensText!);
        }
        if (ShowsDuration)
        {
            parts.Add(DurationText!);
        }
        SpokenText = string.Join(", ", parts);
        AutomationProperties.SetName(this, SpokenText);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinePeer(this);

    /// <summary>One line of text named by what it shows; the parts are not read one by one.</summary>
    private sealed class LinePeer(AiModelLine owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
