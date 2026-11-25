// Tests of what the parameter hints widget computes (src/vs/editor/contrib/parameterHints/browser/
// parameterHintsWidget.ts: getParameterLabelOffsets, hasDocs, the overload counter; commit b57d5c9e). Upstream tests
// these only through the DOM; written for this port.

using Shouldly;
using TKCode.Arc.Editor.Contrib.ParameterHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ParameterHintsLabelTests
{
    private static SignatureInformation Signature(string label, params ParameterInformation[] parameters) => new(label, parameters);

    [Fact]
    public void A_string_label_is_found_as_a_whole_word()
    {
        // "a" appears in "max" but only the parameter named a matches
        var signature = Signature("max(a: number, b: number)", new ParameterInformation { Label = "a" }, new ParameterInformation { Label = "b" });
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 0).ShouldBe((4, 5));
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 1).ShouldBe((15, 16));
    }

    [Fact]
    public void Label_offsets_are_used_as_given_and_missing_labels_give_nothing()
    {
        var signature = Signature("f(x, y)", new ParameterInformation { LabelOffsets = (5, 6) }, new ParameterInformation { Label = "" }, new ParameterInformation { Label = "zz" });
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 0).ShouldBe((5, 6));
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 1).ShouldBe((0, 0));
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 2).ShouldBe((0, 0));
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 7).ShouldBe((0, 0));
    }

    [Fact]
    public void Regex_characters_in_a_label_are_literal()
    {
        var signature = Signature("g(...args: any[])", new ParameterInformation { Label = "...args: any[]" });
        ParameterHintsLabel.GetParameterLabelOffsets(signature, 0).ShouldBe((2, 16));
    }

    [Fact]
    public void The_overload_counter_is_padded_to_the_total()
    {
        var signatures = Enumerable.Range(0, 12).Select(i => Signature("s" + i)).ToList();
        ParameterHintsLabel.FormatOverloads(new SignatureHelp(signatures, 1, 0)).ShouldBe("02/12");
        ParameterHintsLabel.FormatOverloads(new SignatureHelp(signatures[..3], 2, 0)).ShouldBe("3/3");
    }

    [Fact]
    public void Docs_and_the_announced_label()
    {
        var withDocs = Signature("f(a)", new ParameterInformation { Label = "a", Documentation = new MarkdownString("the **a**") }) with { Documentation = "does f" };
        var bare = Signature("g(b)", new ParameterInformation { Label = "b", Documentation = "" });

        ParameterHintsLabel.HasDocs(withDocs, withDocs.Parameters[0]).ShouldBeTrue();
        ParameterHintsLabel.HasDocs(bare, bare.Parameters[0]).ShouldBeFalse();
        ParameterHintsLabel.GetLabelToAnnounce(withDocs, 0).ShouldBe("a, the **a**, does f");
        ParameterHintsLabel.GetLabelToAnnounce(bare, 3).ShouldBeNull();
        ParameterHintsLabel.GetActiveParameterIndex(new SignatureHelp([withDocs], 0, 2), withDocs).ShouldBe(2);
        var own = withDocs with { ActiveParameter = 0 };
        ParameterHintsLabel.GetActiveParameterIndex(new SignatureHelp([own], 0, 2), own).ShouldBe(0);
    }
}
