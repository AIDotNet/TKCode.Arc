using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The right side of a document extraction's footer (Arc document extraction): the model's mark (14px), its name, then the
/// cost and the time of the run, "· "-separated: 12 / 16.8, the name in the secondary text color, the figures in the muted
/// one with tabular figures so a running time does not jitter, 7px between the parts, the dots 3px in the muted color at
/// 55%. The owner formats the figures ("$0.0003", "3.2 s"); an empty one is left out with its dot. A narrow footer hides
/// the name (<see cref="ShowsModel"/>) and keeps the mark. It reads as one line of text, named by
/// <see cref="AutomationName"/> or, when that is empty, by what it shows ("Lumen Extract S, $0.0003, 3.2 s").
/// </summary>
[TemplatePart(NamePart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class AiRunMeta : TemplatedControl
{
    public const string NamePart = "PART_Name";

    /// <summary>Set while the name is hidden.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>The model's mark: a geometry in a 24 × 24 box or an image (see <see cref="ModelMark"/>); null shows none.</summary>
    public static readonly StyledProperty<object?> ModelMarkProperty =
        AvaloniaProperty.Register<AiRunMeta, object?>(nameof(ModelMark));

    /// <summary>The model's name.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<AiRunMeta, string?>(nameof(ModelName));

    /// <summary>The cost as shown ("$0.0003"); null or empty leaves it out.</summary>
    public static readonly StyledProperty<string?> CostTextProperty =
        AvaloniaProperty.Register<AiRunMeta, string?>(nameof(CostText));

    /// <summary>The time as shown ("3.2 s"); null or empty leaves it out.</summary>
    public static readonly StyledProperty<string?> ElapsedTextProperty =
        AvaloniaProperty.Register<AiRunMeta, string?>(nameof(ElapsedText));

    /// <summary>Whether the model's name shows (false in a narrow footer; the mark stays).</summary>
    public static readonly StyledProperty<bool> ShowsModelProperty =
        AvaloniaProperty.Register<AiRunMeta, bool>(nameof(ShowsModel), true);

    /// <summary>What screen readers hear; null or empty reads the parts that show, comma-separated.</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<AiRunMeta, string?>(nameof(AutomationName));

    /// <summary>Whether the name shows (template use).</summary>
    internal static readonly DirectProperty<AiRunMeta, bool> ShowsNameProperty =
        AvaloniaProperty.RegisterDirect<AiRunMeta, bool>(nameof(ShowsName), o => o.ShowsName);

    /// <summary>Whether the cost shows (template use).</summary>
    internal static readonly DirectProperty<AiRunMeta, bool> ShowsCostProperty =
        AvaloniaProperty.RegisterDirect<AiRunMeta, bool>(nameof(ShowsCost), o => o.ShowsCost);

    /// <summary>Whether the time shows (template use).</summary>
    internal static readonly DirectProperty<AiRunMeta, bool> ShowsElapsedProperty =
        AvaloniaProperty.RegisterDirect<AiRunMeta, bool>(nameof(ShowsElapsed), o => o.ShowsElapsed);

    /// <summary>Whether a dot leads the cost (the name shows before it; template use).</summary>
    internal static readonly DirectProperty<AiRunMeta, bool> CostDotProperty =
        AvaloniaProperty.RegisterDirect<AiRunMeta, bool>(nameof(CostDot), o => o.CostDot);

    /// <summary>Whether a dot leads the time (a name or a cost shows before it; template use).</summary>
    internal static readonly DirectProperty<AiRunMeta, bool> ElapsedDotProperty =
        AvaloniaProperty.RegisterDirect<AiRunMeta, bool>(nameof(ElapsedDot), o => o.ElapsedDot);

    private bool _showsName;
    private bool _showsCost;
    private bool _showsElapsed;
    private bool _costDot;
    private bool _elapsedDot;

    static AiRunMeta()
    {
        FocusableProperty.OverrideDefaultValue<AiRunMeta>(false);
        IsTabStopProperty.OverrideDefaultValue<AiRunMeta>(false);
    }

    public AiRunMeta()
    {
        Refresh();
    }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public object? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="CostTextProperty"/>
    public string? CostText { get => GetValue(CostTextProperty); set => SetValue(CostTextProperty, value); }

    /// <inheritdoc cref="ElapsedTextProperty"/>
    public string? ElapsedText { get => GetValue(ElapsedTextProperty); set => SetValue(ElapsedTextProperty, value); }

    /// <inheritdoc cref="ShowsModelProperty"/>
    public bool ShowsModel { get => GetValue(ShowsModelProperty); set => SetValue(ShowsModelProperty, value); }

    /// <inheritdoc cref="AutomationNameProperty"/>
    public string? AutomationName { get => GetValue(AutomationNameProperty); set => SetValue(AutomationNameProperty, value); }

    internal bool ShowsName { get => _showsName; private set => SetAndRaise(ShowsNameProperty, ref _showsName, value); }

    internal bool ShowsCost { get => _showsCost; private set => SetAndRaise(ShowsCostProperty, ref _showsCost, value); }

    internal bool ShowsElapsed { get => _showsElapsed; private set => SetAndRaise(ShowsElapsedProperty, ref _showsElapsed, value); }

    internal bool CostDot { get => _costDot; private set => SetAndRaise(CostDotProperty, ref _costDot, value); }

    internal bool ElapsedDot { get => _elapsedDot; private set => SetAndRaise(ElapsedDotProperty, ref _elapsedDot, value); }

    /// <summary>What the line reads as.</summary>
    internal string SpokenText { get; private set; } = string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelNameProperty || change.Property == CostTextProperty || change.Property == ElapsedTextProperty
            || change.Property == ShowsModelProperty || change.Property == AutomationNameProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var name = ModelName;
        var cost = CostText;
        var elapsed = ElapsedText;
        ShowsName = ShowsModel && !string.IsNullOrEmpty(name);
        ShowsCost = !string.IsNullOrEmpty(cost);
        ShowsElapsed = !string.IsNullOrEmpty(elapsed);
        // The dots separate figures from what is before them; the mark is not separated by a dot (Arc's narrow footer).
        CostDot = ShowsCost && ShowsName;
        ElapsedDot = ShowsElapsed && (ShowsName || ShowsCost);
        PseudoClasses.Set(CompactPseudoClass, !ShowsModel);

        var spoken = AutomationName;
        if (string.IsNullOrEmpty(spoken))
        {
            spoken = Join(Join(ShowsName ? name : null, ShowsCost ? cost : null), ShowsElapsed ? elapsed : null);
        }
        SpokenText = spoken;
        AutomationProperties.SetName(this, spoken);
    }

    private static string Join(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first))
        {
            return second ?? string.Empty;
        }
        return string.IsNullOrEmpty(second) ? first : first + ", " + second;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MetaPeer(this);

    /// <summary>One line of text named by what it shows; the parts are not read one by one.</summary>
    private sealed class MetaPeer(AiRunMeta owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((AiRunMeta)Owner).SpokenText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
