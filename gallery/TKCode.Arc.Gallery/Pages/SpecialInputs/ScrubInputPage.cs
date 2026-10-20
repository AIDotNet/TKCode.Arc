using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class ScrubInputPage
{
    public static Control Create()
    {
        return GalleryUi.Column(20,
            GalleryUi.Card("Inspector",
                "Drag a label sideways to scrub (2px per step; Shift ×10 on the coarse grid, Alt ×0.1, shown in a tag above the field), or click in and type: 12*2, (100-24)/2, 1,5. W and H stop at 0 and strain past it; rotation wraps 0..360; opacity shows its track.",
                Panel(Inspector())),
            GalleryUi.Card("Glyph labels",
                "A 14px glyph can stand in for the label; give the field an accessible name. Double click the label to reset (font size 16, brightness 100%).",
                Panel(GlyphLabels())),
            GalleryUi.Card("Cycling units",
                "With several units the unit becomes a button that cycles them (px, %, em). Typing a unit, as in 50% or 2em, switches to it; an unknown unit is rejected.",
                Panel(CyclingUnits())),
            GalleryUi.Card("Sizes",
                "Small is 30px with 12px text; Medium (default) is 36px with 14px text. Arc has no large size.",
                Panel(Sizes())),
            GalleryUi.Card("States",
                "Disabled fields dim to 50% and ignore input. Type a word such as abc and press Enter: the entry is rejected, the edge turns red for 900ms with a short shake, and the old value stays.",
                Panel(States())),
            GalleryUi.Card("Keyboard and events",
                "ArrowUp / ArrowDown step (Shift coarse, Alt fine), Enter commits and selects, Escape restores the last value (or cancels a scrub back to where it started). ValueChanged fires for every change (setting Value from code reports Programmatic), ValueCommitted once a change made in the field settles.",
                EventLog()));
    }

    private static Control Inspector()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        Place(grid, new ScrubInput { Label = "X", Value = 120, Unit = "px" }, 0, 0);
        Place(grid, new ScrubInput { Label = "Y", Value = 48, Unit = "px" }, 0, 1);
        Place(grid, new ScrubInput { Label = "W", Value = 320, Minimum = 0, Unit = "px", ResetValue = 320 }, 1, 0);
        Place(grid, new ScrubInput { Label = "H", Value = 180, Minimum = 0, Unit = "px", ResetValue = 180 }, 1, 1);
        Place(grid, new ScrubInput { Label = "R", Value = 15, Minimum = 0, Maximum = 360, IsWrapping = true, IsTrackVisible = false, Unit = "°", ResetValue = 0 }, 2, 0);
        Place(grid, new ScrubInput { Label = "O", Value = 80, Minimum = 0, Maximum = 100, Unit = "%", ResetValue = 100 }, 2, 1);
        return grid;
    }

    private static Control GlyphLabels()
    {
        var size = new ScrubInput { Label = GalleryUi.Glyph(ArcIcons.Type, 14), Value = 16, Minimum = 1, Unit = "px", ResetValue = 16 };
        AutomationProperties.SetName(size, "Font size");
        var brightness = new ScrubInput { Label = GalleryUi.Glyph(ArcIcons.Sun, 14), Value = 100, Minimum = 0, Maximum = 200, Unit = "%", ResetValue = 100 };
        AutomationProperties.SetName(brightness, "Brightness");
        return GalleryUi.Column(8, size, brightness);
    }

    private static Control CyclingUnits()
    {
        var log = GalleryUi.Text("Unit: px", ArcDesignTokenKind.TextMuted, 12);
        var width = new ScrubInput { Label = "Width", Value = 240, Minimum = 0, Units = ["px", "%", "em"] };
        width.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrubInput.UnitProperty)
            {
                log.Text = "Unit: " + width.EffectiveUnit;
            }
        };
        var gap = new ScrubInput { Label = "Gap", Value = 1.5, Step = 0.1, Minimum = 0, Units = ["em", "px"] };
        return GalleryUi.Column(8, width, gap, log);
    }

    private static Control Sizes()
    {
        return GalleryUi.Column(8,
            new ScrubInput { Label = "Small", Size = ControlSize.Small, Value = 12, Unit = "px" },
            new ScrubInput { Label = "Medium", Value = 12, Unit = "px" });
    }

    private static Control States()
    {
        var hint = GalleryUi.Text("Try: abc, 12px (unknown unit), 4/0", ArcDesignTokenKind.TextMuted, 12);
        var invalid = new ScrubInput { Label = "Size", Value = 24, Minimum = 0, Maximum = 100, Unit = "%" };
        invalid.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrubInput.IsInvalidProperty)
            {
                hint.Text = invalid.IsInvalid ? "Rejected: the value stays at " + invalid.FormattedValue : "Try: abc, 12px (unknown unit), 4/0";
            }
        };
        return GalleryUi.Column(8,
            new ScrubInput { Label = "Locked", Value = 64, Unit = "px", IsEnabled = false },
            invalid,
            hint);
    }

    private static Control EventLog()
    {
        var lines = new StackPanel { Spacing = 2 };
        var input = new ScrubInput { Label = "Amount", Value = 10, Minimum = 0, Maximum = 50, Step = 1, ResetValue = 10 };
        var entries = new List<string>();
        void Log(string text)
        {
            entries.Insert(0, text);
            if (entries.Count > 6)
            {
                entries.RemoveAt(entries.Count - 1);
            }
            lines.Children.Clear();
            foreach (var entry in entries)
            {
                lines.Children.Add(GalleryUi.Mono(entry, ArcDesignTokenKind.TextSecondary, 12));
            }
        }
        input.ValueChanged += (_, e) => Log($"ValueChanged    {Num(e.OldValue)} → {Num(e.NewValue)}  ({e.ChangeSource})");
        input.ValueCommitted += (_, e) => Log($"ValueCommitted  {Num(e.OldValue)} → {Num(e.NewValue)}  ({e.ChangeSource})");
        var fromCode = new Button { Content = "Set 25 from code", Variant = ButtonVariant.Secondary, HorizontalAlignment = HorizontalAlignment.Left };
        fromCode.Click += (_, _) => input.Value = 25;
        Log("Scrub, step or type to see the events.");
        return Panel(GalleryUi.Column(12, input, fromCode, lines));
    }

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void Place(Grid grid, Control child, int row, int column)
    {
        Grid.SetRow(child, row);
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    /// <summary>An inspector-like surface: scrub fields sit on a raised panel, as they do in property panels.</summary>
    private static Border Panel(Control content)
    {
        var panel = new Border
        {
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(16, 26, 16, 16),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Child = content
        };
        ArcTokens.Bind(panel, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(panel, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return panel;
    }
}
