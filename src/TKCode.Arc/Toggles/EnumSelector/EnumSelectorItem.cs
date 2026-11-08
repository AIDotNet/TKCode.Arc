using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One option of an <see cref="EnumSelector"/>: an enum member and the text it shows. It is the data an
/// <see cref="EnumSelector.ItemTemplate"/> receives (bind to <see cref="Text"/>, or to <see cref="Member"/> for the rest).
/// </summary>
public sealed class EnumSelectorItem : AvaloniaObject, ITogglesValueSource
{
    /// <summary>The text shown for the member: its localized text, <c>[Description]</c> or name, as
    /// <see cref="EnumSelector.UseDisplayNames"/> says.</summary>
    public static readonly DirectProperty<EnumSelectorItem, string> TextProperty =
        AvaloniaProperty.RegisterDirect<EnumSelectorItem, string>(nameof(Text), o => o.Text);

    private string _text;

    internal EnumSelectorItem(EnumMemberInfo member)
    {
        Member = member;
        _text = member.Name;
    }

    /// <summary>The enum member.</summary>
    public EnumMemberInfo Member { get; }

    /// <summary>The boxed enum value.</summary>
    public object Value => Member.Value;

    /// <summary>The member's name in code.</summary>
    public string Name => Member.Name;

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => _text;
        internal set => SetAndRaise(TextProperty, ref _text, value);
    }

    /// <summary>The value as 64 bits.</summary>
    internal ulong Bits => Member.Bits;

    /// <summary>The text of the language catalog key, while the selector observes it.</summary>
    internal string? ResourceText { get; set; }

    object? ITogglesValueSource.ToggleValue => Member.Value;

    /// <inheritdoc />
    public override string ToString() => Text;
}
