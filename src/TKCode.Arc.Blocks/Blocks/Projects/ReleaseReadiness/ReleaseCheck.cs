using Avalonia;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// One check of a <see cref="ReleaseReadiness"/> gate: a title, an optional line under it and whether it is done. Every
/// required check must be done before the release can start; optional checks (<see cref="IsRequired"/> false) are shown
/// and can be ticked, but do not hold the release back.
/// </summary>
public sealed class ReleaseCheck : AvaloniaObject
{
    /// <summary>A stable id the app can match the check by (events, saved state); null when the check has none.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ReleaseCheck, string?>(nameof(Key));

    /// <summary>The check's title; also the checkbox's name for screen readers.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ReleaseCheck, string>(nameof(Title), string.Empty);

    /// <summary>The line under the title; also read as the checkbox's help text. Null shows the title alone.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ReleaseCheck, string?>(nameof(Description));

    /// <summary>Whether the check is done (two-way: ticking the checkbox writes it back).</summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<ReleaseCheck, bool>(nameof(IsDone), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The value <see cref="ReleaseReadiness.Reset"/> puts back. When it is not set, Reset puts back the value
    /// <see cref="IsDone"/> had when the block first showed the check.
    /// </summary>
    public static readonly StyledProperty<bool> IsInitiallyDoneProperty =
        AvaloniaProperty.Register<ReleaseCheck, bool>(nameof(IsInitiallyDone));

    /// <summary>Whether the release waits for this check (true); an optional check is counted out of the gate and the progress.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<ReleaseCheck, bool>(nameof(IsRequired), true);

    /// <summary>Any object the app wants to keep with the check.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<ReleaseCheck, object?>(nameof(Tag));

    /// <summary>Creates an empty check (XAML).</summary>
    public ReleaseCheck()
    {
    }

    /// <summary>Creates a check with a title, an optional description and its state.</summary>
    /// <param name="title">The title.</param>
    /// <param name="description">The line under the title.</param>
    /// <param name="isDone">Whether it is done already.</param>
    public ReleaseCheck(string title, string? description = null, bool isDone = false)
    {
        Title = title;
        Description = description;
        IsDone = isDone;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsDoneProperty"/>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <inheritdoc cref="IsInitiallyDoneProperty"/>
    public bool IsInitiallyDone { get => GetValue(IsInitiallyDoneProperty); set => SetValue(IsInitiallyDoneProperty, value); }

    /// <inheritdoc cref="IsRequiredProperty"/>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The <see cref="IsDone"/> value when a block first showed the check (Reset's fallback).</summary>
    internal bool? FirstSeenDone { get; set; }

    /// <summary>The value a reset puts back: <see cref="IsInitiallyDone"/> when set, else the first value seen.</summary>
    internal bool ResetValue => IsSet(IsInitiallyDoneProperty) ? IsInitiallyDone : FirstSeenDone ?? IsDone;

    /// <inheritdoc />
    public override string ToString() => Title;
}
