// Ported from vscode (src/vs/editor/contrib/parameterHints/browser/parameterHintsWidget.ts: getParameterLabelOffsets,
// hasDocs and the overload counter of render, commit b57d5c9e), MIT license. Kept in the engine so that it can be
// tested without the widget. Differences from upstream: the regular expression runs with ECMAScript semantics (\W is
// ASCII, as in JavaScript); a label that is not found gives [0, 0] as upstream.

using System.Globalization;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Contrib.ParameterHints;

/// <summary>What the parameter hints widget computes from a signature.</summary>
internal static class ParameterHintsLabel
{
    /// <summary>The active signature's active parameter index (the signature's own, else the help's).</summary>
    public static int GetActiveParameterIndex(SignatureHelp hints, SignatureInformation signature) =>
        signature.ActiveParameter ?? hints.ActiveParameter;

    /// <summary>Where parameter <paramref name="paramIdx"/> is in the signature's label (upstream's getParameterLabelOffsets).</summary>
    public static (int Start, int End) GetParameterLabelOffsets(SignatureInformation signature, int paramIdx)
    {
        if (paramIdx < 0 || paramIdx >= signature.Parameters.Count)
        {
            return (0, 0);
        }

        var param = signature.Parameters[paramIdx];
        if (param.LabelOffsets is { } offsets)
        {
            return offsets;
        }

        var label = param.Label ?? string.Empty;
        if (label.Length == 0)
        {
            return (0, 0);
        }

        var regex = new Regex($"(\\W|^){Regex.Escape(label)}(?=\\W|$)", RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
        var match = regex.Match(signature.Label);
        if (!match.Success)
        {
            return (0, 0);
        }

        var lastIndex = match.Index + match.Length;
        var idx = lastIndex - label.Length;
        return idx >= 0 ? (idx, lastIndex) : (0, 0);
    }

    /// <summary>Whether the signature or the active parameter has documentation (upstream's hasDocs).</summary>
    public static bool HasDocs(SignatureInformation signature, ParameterInformation? activeParameter) =>
        !IsEmpty(activeParameter?.Documentation) || !IsEmpty(signature.Documentation);

    /// <summary>The overload counter, "2/10" padded as "02/10" (upstream's overloads text).</summary>
    public static string FormatOverloads(SignatureHelp hints)
    {
        var total = hints.Signatures.Count.ToString(CultureInfo.InvariantCulture);
        return (hints.ActiveSignature + 1).ToString(CultureInfo.InvariantCulture).PadLeft(total.Length, '0') + "/" + total;
    }

    /// <summary>The text a screen reader announces for the active parameter (upstream's labelToAnnounce), or null.</summary>
    public static string? GetLabelToAnnounce(SignatureInformation signature, int activeParameterIndex)
    {
        if (activeParameterIndex < 0 || activeParameterIndex >= signature.Parameters.Count)
        {
            return null;
        }

        var param = signature.Parameters[activeParameterIndex];
        var label = param.LabelOffsets is { } offsets
            ? signature.Label[Math.Clamp(offsets.Start, 0, signature.Label.Length)..Math.Clamp(offsets.End, 0, signature.Label.Length)]
            : param.Label ?? string.Empty;
        if (param.Documentation is { } doc)
        {
            label += ", " + DocumentationText(doc);
        }

        if (signature.Documentation is { } signatureDoc)
        {
            label += ", " + DocumentationText(signatureDoc);
        }

        return label;
    }

    private static string DocumentationText(StringOrMarkdown doc) => doc.Markdown is { } markdown ? markdown.Value : doc.Text ?? string.Empty;

    private static bool IsEmpty(StringOrMarkdown? doc) =>
        doc is not { } value || (value.Markdown is { } markdown ? markdown.Value.Length == 0 : string.IsNullOrEmpty(value.Text));
}
