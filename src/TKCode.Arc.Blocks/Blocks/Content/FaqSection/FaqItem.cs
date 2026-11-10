// Ported from Arc's free faq-section block (MIT, Copyright (c) 2026 Elia Kuratli): the data shapes and search helpers of
// faq-section.tsx. The sample questions are this library's own.

using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The layout of a <see cref="FaqSection"/>.</summary>
public enum FaqSectionVariant
{
    /// <summary>One centered list of questions.</summary>
    Accordion,

    /// <summary>A topic rail beside the questions of the chosen topic.</summary>
    Columns,

    /// <summary>One list with a search field that filters and highlights as you type.</summary>
    Search
}

/// <summary>One question of a <see cref="FaqSection"/>.</summary>
public sealed class FaqItem : AvaloniaObject
{
    /// <summary>A stable id for the open state; defaults to <see cref="Question"/>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<FaqItem, string?>(nameof(Id));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The question.</summary>
    public static readonly StyledProperty<string> QuestionProperty =
        AvaloniaProperty.Register<FaqItem, string>(nameof(Question), string.Empty);

    /// <inheritdoc cref="QuestionProperty"/>
    public string Question { get => GetValue(QuestionProperty); set => SetValue(QuestionProperty, value); }

    /// <summary>The answer as plain text; it is also what search matches.</summary>
    public static readonly StyledProperty<string> AnswerProperty =
        AvaloniaProperty.Register<FaqItem, string>(nameof(Answer), string.Empty);

    /// <inheritdoc cref="AnswerProperty"/>
    public string Answer { get => GetValue(AnswerProperty); set => SetValue(AnswerProperty, value); }

    /// <summary>The topic that groups the question in the columns layout; defaults to "General".</summary>
    public static readonly StyledProperty<string?> CategoryProperty =
        AvaloniaProperty.Register<FaqItem, string?>(nameof(Category));

    /// <inheritdoc cref="CategoryProperty"/>
    public string? Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }

    internal string Key => Id ?? Question;

    internal string Topic => Category ?? "General";
}

/// <summary>The way to reach a person that a <see cref="FaqSection"/> offers when the answer isn't there.</summary>
public sealed class FaqContact : AvaloniaObject
{
    /// <summary>The row's main line.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FaqContact, string>(nameof(Label), string.Empty);

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The quieter line under the label.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FaqContact, string?>(nameof(Description));

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>An absolute http, https or mailto address, opened on a press unless a handler marks the event handled.</summary>
    public static readonly StyledProperty<string?> HrefProperty =
        AvaloniaProperty.Register<FaqContact, string?>(nameof(Href));

    /// <inheritdoc cref="HrefProperty"/>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>Runs on a press.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<FaqContact, ICommand?>(nameof(Command));

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<FaqContact, object?>(nameof(CommandParameter));

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
}

/// <summary>The accent-insensitive search of the FAQ: lower case, diacritics removed, with a map back to the original text.</summary>
internal static class FaqText
{
    /// <summary>Folds <paramref name="text"/>; <paramref name="map"/> gives the original index of each folded character.</summary>
    public static string Fold(string text, out int[] map)
    {
        var builder = new StringBuilder(text.Length);
        var indices = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var decomposed = text[i] < 128 ? text[i].ToString() : text[i].ToString().Normalize(NormalizationForm.FormD);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfoExtensions.IsMark(c))
                {
                    continue;
                }
                builder.Append(char.ToLowerInvariant(c));
                indices.Add(i);
            }
        }
        map = indices.ToArray();
        return builder.ToString();
    }

    public static string Fold(string text) => Fold(text, out _);

    public static bool Contains(string text, string query) => Fold(text).Contains(Fold(query.Trim()), StringComparison.Ordinal);

    /// <summary>Every match of <paramref name="query"/> in <paramref name="text"/>, as ranges of the original text.</summary>
    public static List<(int Start, int Length)> Matches(string text, string query)
    {
        var ranges = new List<(int, int)>();
        var needle = Fold(query.Trim());
        if (needle.Length == 0)
        {
            return ranges;
        }
        var haystack = Fold(text, out var map);
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (at >= 0)
        {
            var start = map[at];
            var end = map[at + needle.Length - 1] + 1;
            ranges.Add((start, end - start));
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }
        return ranges;
    }

    /// <summary>A short window of the answer around the first match, for a closed item that matches only in its answer.</summary>
    public static string? Snippet(string answer, string query)
    {
        var matches = Matches(answer, query);
        if (matches.Count == 0)
        {
            return null;
        }
        var at = matches[0].Start;
        var searchFrom = Math.Max(0, at - 36);
        var start = Math.Max(0, answer.LastIndexOf(' ', searchFrom) + 1);
        var end = Math.Min(answer.Length, at + query.Trim().Length + 60);
        return $"{(start > 0 ? "…" : string.Empty)}{answer[start..end].Trim()}{(end < answer.Length ? "…" : string.Empty)}";
    }

    public static FaqItem[] CreateSamples() =>
    [
        new() { Question = "Can I change plans later?", Answer = "Yes. Move up or down from the billing page at any time. The new price applies from your next invoice.", Category = "Billing" },
        new() { Question = "Do you offer refunds?", Answer = "Annual plans can be refunded within 30 days of purchase. Monthly plans simply stop at the end of the month.", Category = "Billing" },
        new() { Question = "Is there a discount for nonprofits?", Answer = "Registered nonprofits and schools get 40% off every plan. Send us your registration and we will apply it.", Category = "Billing" },
        new() { Question = "How do I bring in my existing data?", Answer = "Import a CSV file or connect your old workspace, and the assistant maps the columns for you. Most imports finish in minutes.", Category = "Getting started" },
        new() { Question = "How many people can I invite?", Answer = "Every plan includes unlimited viewers. Editors count as seats, and you can add or remove seats whenever you need.", Category = "Getting started" },
        new() { Question = "Does it work offline?", Answer = "The desktop app keeps working without a connection and syncs your changes as soon as you are back online.", Category = "Getting started" },
        new() { Question = "Where is my data stored?", Answer = "Data lives in the region you pick when you create the workspace: the United States, the European Union or Japan.", Category = "Security" },
        new() { Question = "Do you support single sign-on?", Answer = "Business plans include SAML single sign-on and automatic provisioning through SCIM.", Category = "Security" },
    ];
}

/// <summary>Unicode category checks that do not allocate.</summary>
internal static class CharUnicodeInfoExtensions
{
    public static bool IsMark(char c) => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark;
}

/// <summary>Arguments of <see cref="FaqSection.ContactInvoked"/>.</summary>
public sealed class FaqContactEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FaqContactEventArgs(RoutedEvent routedEvent, object source, FaqContact contact)
        : base(routedEvent, source)
    {
        Contact = contact;
    }

    /// <summary>The contact row that was pressed.</summary>
    public FaqContact Contact { get; }
}
