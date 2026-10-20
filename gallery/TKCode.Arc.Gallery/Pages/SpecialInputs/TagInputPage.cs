using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class TagInputPage
{
    private const double FieldWidth = 420;

    public static Control Create()
    {
        var basic = Field(new TagInput { Label = "Tags", Value = ["design", "motion", "react"] });

        var empty = Field(new TagInput { Label = "Topics" });

        var described = Field(new TagInput
        {
            Label = "Keywords",
            Description = "Press Enter or a comma to add a keyword. Backspace picks the last one.",
            Value = ["onboarding"]
        });

        var limited = Field(new TagInput
        {
            Label = "Skills",
            Description = "Up to 5 skills. A sixth is refused with a shake and an announcement.",
            MaxTags = 5,
            Value = ["Figma", "Avalonia", "C#", "Motion"]
        });

        var wrapping = Field(new TagInput
        {
            Label = "Recipients",
            Value =
            [
                "ada@example.com", "grace@example.com", "linus@example.com", "margaret@example.com",
                "ken@example.com", "barbara@example.com", "dennis@example.com"
            ]
        });

        var paste = Field(new TagInput
        {
            Label = "Colors",
            Description = "Paste a list separated by commas, new lines or tabs: every piece becomes a tag and the last piece stays in the draft."
        });
        var copySample = new Button { Content = "Copy “red, green, blue, teal”", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        copySample.Click += async (_, _) => await ArcClipboard.CopyTextAsync(copySample, "red, green, blue, teal");

        var invalid = Field(new TagInput { Label = "Labels", ErrorMessage = "Add at least one label." });

        var disabled = Field(new TagInput { Label = "Locked tags", Value = ["archived", "read-only"], IsEnabled = false });

        var keyboard = Field(new TagInput { Label = "Try the keys", Value = ["alpha", "beta", "gamma"] });

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "Type and press Enter or a comma. A new tag blurs in where it was typed; the field never scrolls sideways.", basic),
            GalleryUi.Card("Empty with placeholder", "The placeholder rises in whenever the field has no tags and no draft.", empty),
            GalleryUi.Card("Description", "Helper copy under the field also describes the editor for screen readers.", described),
            GalleryUi.Card("Limit with MaxTags", "MaxTags refuses adds past the limit: the content shakes and the limit is announced.", limited),
            GalleryUi.Card("Wrapping rows", "Tags wrap onto new rows and the shell height follows on the smooth spring. Remove one to watch the rest glide in.", wrapping),
            GalleryUi.Card("Paste a list", "Copy the sample, then paste into the field.", GalleryUi.Column(16, paste, Left(copySample))),
            GalleryUi.Card("Keyboard", "Backspace at the start picks the last tag, Backspace or Delete removes the pick, ArrowLeft / ArrowRight move the ring, Escape clears it. Clicking a tag picks it too.", keyboard),
            GalleryUi.Card("Error", "A non-empty ErrorMessage shows the danger border and the error row.", invalid),
            GalleryUi.Card("Disabled", "Disabled fields fade to 0.5 on SurfaceMuted.", disabled),
            GalleryUi.Card("Two-way value and events", "Value is a two-way IReadOnlyList<string>; the copy below is bound to it. ValueChanged reports what was added and removed.", Bound()));
    }

    private static Control Bound()
    {
        var source = Field(new TagInput { Label = "Labels", Value = ["bug", "ui"] });
        var copy = Field(new TagInput { Label = "Bound copy", Description = "Bound two-way to the field above." });
        copy[!!TagInput.ValueProperty] = source[!!TagInput.ValueProperty];
        var value = GalleryUi.Mono("Value: bug, ui");
        var last = GalleryUi.Mono("Last event: none", ArcDesignTokenKind.TextMuted);
        source.ValueChanged += (_, e) =>
        {
            value.Text = "Value: " + (e.NewValue.Count == 0 ? "(empty)" : string.Join(", ", e.NewValue));
            var parts = new List<string>(2);
            if (e.Added.Count > 0)
            {
                parts.Add("added " + string.Join(", ", e.Added));
            }
            if (e.Removed.Count > 0)
            {
                parts.Add("removed " + string.Join(", ", e.Removed));
            }
            last.Text = "Last event: " + string.Join("; ", parts);
        };
        var reset = new Button { Content = "Reset in code", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        reset.Click += (_, _) => source.Value = ["bug", "ui"];
        return GalleryUi.Column(16, source, copy, value, last, Left(reset));
    }

    private static TagInput Field(TagInput input)
    {
        input.Width = FieldWidth;
        input.HorizontalAlignment = HorizontalAlignment.Left;
        return input;
    }

    private static Control Left(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Left;
        return control;
    }
}
