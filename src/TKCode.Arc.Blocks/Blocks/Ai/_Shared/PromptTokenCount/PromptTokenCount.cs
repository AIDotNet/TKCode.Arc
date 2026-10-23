using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The token estimate at the top right of the prompt editor ("112 tokens", 12 / 16.8 in muted text): the number's digits
/// roll like a counter when <see cref="Count"/> changes (Core <see cref="RollingDigits"/> on the quick spring, cut at the
/// line box like Arc's digit strips) and the words around it come from <see cref="Format"/>. The number keeps a cell of
/// the same width for every digit, so the line moves only when a digit is added or removed. Reduced motion and Off jump.
/// Screen readers get the whole formatted text as the name.
/// </summary>
/// <remarks>
/// <see cref="Format"/> formats the whole line; the first run of digits in its result (with the group separators between
/// them) is the number that rolls, the text before and after it is shown as it is, with a 4px gap in place of the space
/// next to the number. When that run does not read like <see cref="RollingDigits"/> formats the count (another
/// culture's grouping, words for numbers) the line is shown as plain text and does not roll. Without a format only the
/// number is shown.
/// </remarks>
[TemplatePart(PrefixPart, typeof(TextBlock))]
[TemplatePart(DigitsPart, typeof(RollingDigits))]
[TemplatePart(WordsPart, typeof(TextBlock))]
internal class PromptTokenCount : TemplatedControl
{
    public const string PrefixPart = "PART_Prefix";
    public const string DigitsPart = "PART_Digits";
    public const string WordsPart = "PART_Words";

    /// <summary>The gap between the number and the words next to it (Arc's flex gap).</summary>
    internal const double WordGap = 4;

    /// <summary>The estimated token count.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<PromptTokenCount, int>(nameof(Count));

    /// <summary>Formats the whole line for a count ("112 tokens"); null shows the number alone.</summary>
    public static readonly StyledProperty<Func<int, string>?> FormatProperty =
        AvaloniaProperty.Register<PromptTokenCount, Func<int, string>?>(nameof(Format));

    private static readonly CultureInfo s_rollingCulture = CultureInfo.GetCultureInfo("en-US");

    private TextBlock? _prefix;
    private RollingDigits? _digits;
    private TextBlock? _words;
    private string _displayText = "0";
    private bool _isRolling = true;

    static PromptTokenCount()
    {
        FocusableProperty.OverrideDefaultValue<PromptTokenCount>(false);
        IsTabStopProperty.OverrideDefaultValue<PromptTokenCount>(false);
    }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public Func<int, string>? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>The whole line as text (the accessible name).</summary>
    internal string DisplayText => _displayText;

    /// <summary>Whether the number rolls (false when the format's number could not be matched).</summary>
    internal bool IsRolling => _isRolling;

    /// <summary>The rolling number (template part).</summary>
    internal RollingDigits? Digits => _digits;

    /// <summary>The text before the number (template part).</summary>
    internal TextBlock? PrefixBlock => _prefix;

    /// <summary>The text after the number (template part).</summary>
    internal TextBlock? WordsBlock => _words;

    /// <summary>
    /// Splits a formatted line into the text before its number, the number (the first run of digits, with single
    /// separators between digits) and the text after it. A line without digits is all prefix.
    /// </summary>
    internal static (string Prefix, string Number, string Suffix) Split(string text)
    {
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                start = i;
                break;
            }
        }
        if (start < 0)
        {
            return (text, string.Empty, string.Empty);
        }
        var end = start;
        while (end < text.Length)
        {
            if (char.IsAsciiDigit(text[end]))
            {
                end++;
            }
            else if (IsSeparator(text[end]) && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1]))
            {
                end += 2;
            }
            else
            {
                break;
            }
        }
        return (text[..start], text[start..end], text[end..]);
    }

    private static bool IsSeparator(char c) => c is ',' or '.' or '\'' or '’' or ' ' or ' ' or ' ';

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _prefix = e.NameScope.Find<TextBlock>(PrefixPart);
        _digits = e.NameScope.Find<RollingDigits>(DigitsPart);
        _words = e.NameScope.Find<TextBlock>(WordsPart);
        if (_digits is not null)
        {
            _digits.CultureName = s_rollingCulture.Name;
            _digits.Falloff = RollingDigitsFalloff.None;
            _digits.WheelSpring = ArcSpring.Quick;
        }
        Update();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty || change.Property == FormatProperty)
        {
            Update();
        }
    }

    /// <summary>Formats the line and hands the number to the wheels and the words to the text blocks.</summary>
    private void Update()
    {
        var count = Count;
        var text = Format?.Invoke(count) ?? count.ToString(CultureInfo.InvariantCulture);
        _displayText = text;
        AutomationProperties.SetName(this, text);
        var (prefix, number, suffix) = Split(text);
        var grouping = RollingFormat(count, number);
        _isRolling = grouping is not null;
        if (_isRolling)
        {
            SetText(_prefix, prefix.TrimEnd(), prefix.Length > 0 && char.IsWhiteSpace(prefix[^1]) ? new Thickness(0, 0, WordGap, 0) : default);
            SetText(_words, suffix.TrimStart(), suffix.Length > 0 && char.IsWhiteSpace(suffix[0]) ? new Thickness(WordGap, 0, 0, 0) : default);
            if (_digits is not null)
            {
                _digits.UseGrouping = grouping!.Value;
                _digits.Value = count;
                _digits.IsVisible = true;
            }
        }
        else
        {
            // Not a number the wheels can show as written: the whole line as plain text.
            SetText(_prefix, text, default);
            SetText(_words, string.Empty, default);
            if (_digits is not null)
            {
                _digits.IsVisible = false;
            }
        }
    }

    /// <summary>
    /// Whether the wheels can show <paramref name="number"/> for <paramref name="count"/>: true with en-US grouping,
    /// false without grouping, null when neither reads the same.
    /// </summary>
    internal static bool? RollingFormat(int count, string number)
    {
        if (count < 0 || number.Length == 0)
        {
            return null;
        }
        if (number == count.ToString(CultureInfo.InvariantCulture))
        {
            return false;
        }
        return number == count.ToString("N0", s_rollingCulture) ? true : null;
    }

    private static void SetText(TextBlock? block, string text, Thickness margin)
    {
        if (block is null)
        {
            return;
        }
        block.Text = text;
        block.Margin = margin;
        block.IsVisible = text.Length > 0;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CountPeer(this);

    /// <summary>Reads as one piece of text: the whole formatted line, not the wheels.</summary>
    private sealed class CountPeer(PromptTokenCount owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override string? GetNameCore() => ((PromptTokenCount)Owner).DisplayText;
    }
}
