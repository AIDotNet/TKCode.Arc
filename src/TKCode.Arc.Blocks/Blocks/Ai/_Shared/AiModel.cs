using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A model a person can choose in the AI blocks: the model picker, the model pill and menu in a composer, a
/// chat header or a prompt studio. Data only: an <see cref="AvaloniaObject"/> so the properties bind and a change
/// (a new description, a price update) reaches what is on screen.
/// </summary>
public class AiModel : AvaloniaObject
{
    /// <summary>Stable id reported in events and bindings (the model's API id).</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiModel, string>(nameof(Id), string.Empty);

    /// <summary>The full display name, such as in a menu row.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AiModel, string>(nameof(Name), string.Empty);

    /// <summary>A shorter name for tight places (a toolbar pill); <see cref="DisplayName"/> falls back to <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> ShortNameProperty =
        AvaloniaProperty.Register<AiModel, string?>(nameof(ShortName));

    /// <summary>The id of the provider this model belongs to (<see cref="AiModelProvider"/>); groups the "More models" list and supplies a missing mark.</summary>
    public static readonly StyledProperty<string?> ProviderIdProperty =
        AvaloniaProperty.Register<AiModel, string?>(nameof(ProviderId));

    /// <summary>One line under the name in a featured menu row.</summary>
    public static readonly StyledProperty<string?> BlurbProperty =
        AvaloniaProperty.Register<AiModel, string?>(nameof(Blurb));

    /// <summary>A longer sentence for the summary below the picker.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AiModel, string?>(nameof(Description));

    /// <summary>A short note after the name in a simple menu row, such as the vendor.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<AiModel, string?>(nameof(Note));

    /// <summary>A small label at the end of a compact row, such as "Fast" or "Open weights".</summary>
    public static readonly StyledProperty<string?> TagProperty =
        AvaloniaProperty.Register<AiModel, string?>(nameof(Tag));

    /// <summary>
    /// The model's mark: a <see cref="Geometry"/> in a 24 × 24 box (filled with <see cref="MarkColor"/> or the text
    /// color) or an <see cref="IImage"/> such as a <see cref="DrawingImage"/> for a multicolor mark. Null falls back to
    /// the provider's mark.
    /// </summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiModel, object?>(nameof(Mark));

    /// <summary>The fill of a <see cref="Geometry"/> mark; null uses the text color (it flips with the theme).</summary>
    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<AiModel, IBrush?>(nameof(MarkColor));

    /// <summary>The context window in tokens.</summary>
    public static readonly StyledProperty<long?> ContextTokensProperty =
        AvaloniaProperty.Register<AiModel, long?>(nameof(ContextTokens));

    /// <summary>The largest reply in tokens.</summary>
    public static readonly StyledProperty<long?> MaxOutputTokensProperty =
        AvaloniaProperty.Register<AiModel, long?>(nameof(MaxOutputTokens));

    /// <summary>Price of one million input tokens (in the currency you show; the format adds the symbol).</summary>
    public static readonly StyledProperty<decimal?> InputPriceProperty =
        AvaloniaProperty.Register<AiModel, decimal?>(nameof(InputPrice));

    /// <summary>Price of one million output tokens.</summary>
    public static readonly StyledProperty<decimal?> OutputPriceProperty =
        AvaloniaProperty.Register<AiModel, decimal?>(nameof(OutputPrice));

    /// <summary>True when the prices are placeholders; parts that print a price note it.</summary>
    public static readonly StyledProperty<bool> IsSamplePriceProperty =
        AvaloniaProperty.Register<AiModel, bool>(nameof(IsSamplePrice));

    /// <summary>The effort levels the model offers; null or empty means the model has no effort setting.</summary>
    public static readonly StyledProperty<IReadOnlyList<ModelEffort>?> EffortLevelsProperty =
        AvaloniaProperty.Register<AiModel, IReadOnlyList<ModelEffort>?>(nameof(EffortLevels));

    /// <summary>The level used when nothing is chosen yet; must be one of <see cref="EffortLevels"/>.</summary>
    public static readonly StyledProperty<ModelEffort?> DefaultEffortProperty =
        AvaloniaProperty.Register<AiModel, ModelEffort?>(nameof(DefaultEffort));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ShortNameProperty"/>
    public string? ShortName { get => GetValue(ShortNameProperty); set => SetValue(ShortNameProperty, value); }

    /// <inheritdoc cref="ProviderIdProperty"/>
    public string? ProviderId { get => GetValue(ProviderIdProperty); set => SetValue(ProviderIdProperty, value); }

    /// <inheritdoc cref="BlurbProperty"/>
    public string? Blurb { get => GetValue(BlurbProperty); set => SetValue(BlurbProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public string? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkColorProperty"/>
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }

    /// <inheritdoc cref="ContextTokensProperty"/>
    public long? ContextTokens { get => GetValue(ContextTokensProperty); set => SetValue(ContextTokensProperty, value); }

    /// <inheritdoc cref="MaxOutputTokensProperty"/>
    public long? MaxOutputTokens { get => GetValue(MaxOutputTokensProperty); set => SetValue(MaxOutputTokensProperty, value); }

    /// <inheritdoc cref="InputPriceProperty"/>
    public decimal? InputPrice { get => GetValue(InputPriceProperty); set => SetValue(InputPriceProperty, value); }

    /// <inheritdoc cref="OutputPriceProperty"/>
    public decimal? OutputPrice { get => GetValue(OutputPriceProperty); set => SetValue(OutputPriceProperty, value); }

    /// <inheritdoc cref="IsSamplePriceProperty"/>
    public bool IsSamplePrice { get => GetValue(IsSamplePriceProperty); set => SetValue(IsSamplePriceProperty, value); }

    /// <inheritdoc cref="EffortLevelsProperty"/>
    public IReadOnlyList<ModelEffort>? EffortLevels { get => GetValue(EffortLevelsProperty); set => SetValue(EffortLevelsProperty, value); }

    /// <inheritdoc cref="DefaultEffortProperty"/>
    public ModelEffort? DefaultEffort { get => GetValue(DefaultEffortProperty); set => SetValue(DefaultEffortProperty, value); }


    /// <summary><see cref="ShortName"/> when set, otherwise <see cref="Name"/>.</summary>
    public string DisplayName => string.IsNullOrEmpty(ShortName) ? Name : ShortName;

    /// <summary>True when the model offers at least one effort level.</summary>
    public bool SupportsEffort => EffortLevels is { Count: > 0 };

    /// <summary>The effort to run at when <paramref name="current"/> was chosen for another model (see <see cref="ModelEffortSnap"/>).</summary>
    /// <param name="current">The level chosen so far, or null.</param>
    public ModelEffort? SnapEffort(ModelEffort? current) => ModelEffortSnap.Snap(current, EffortLevels, DefaultEffort);
}

/// <summary>A company or project that makes models: groups the "More models" list and supplies the mark of its models that have none.</summary>
public class AiModelProvider : AvaloniaObject
{
    /// <summary>Stable id that <see cref="AiModel.ProviderId"/> refers to.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiModelProvider, string>(nameof(Id), string.Empty);

    /// <summary>The group title in the "More models" list.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AiModelProvider, string>(nameof(Name), string.Empty);

    /// <summary>The mark of models without their own; same kinds as <see cref="AiModel.Mark"/>.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiModelProvider, object?>(nameof(Mark));

    /// <summary>The fill of a <see cref="Geometry"/> mark; null uses the text color.</summary>
    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<AiModelProvider, IBrush?>(nameof(MarkColor));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkColorProperty"/>
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }
}
