using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Tests.Theme;

public class CompactDensityTests
{
    [AvaloniaFact]
    public void Compact_shrinks_core_controls_and_restores_on_default()
    {
        var button = new Button { Content = "Save" };
        var input = new Input { Placeholder = "Name" };
        var combobox = new Combobox { Placeholder = "Pick", ItemsSource = new[] { "Alpha", "Beta" } };
        var select = new Select { Placeholder = "Choose", ItemsSource = new[] { "One", "Two" } };
        var card = new ExpandableCard { Header = "Settings", Description = "Details", Content = new TextBlock { Text = "Body" } };
        var toggle = new Switch();
        var stack = new StackPanel
        {
            Width = 360,
            Spacing = 8,
            Children = { button, input, combobox, select, card, toggle }
        };
        var window = TestHelpers.Show(stack);
        try
        {
            var defaultButtonH = button.Bounds.Height;
            var defaultComboboxFrameH = combobox.GetVisualDescendants().OfType<Border>().First(b => b.Name == Combobox.FramePart).Bounds.Height;
            var defaultCardTriggerH = card.GetVisualDescendants().OfType<ExpandableCardTrigger>().First().Bounds.Height;
            var defaultInputFrameH = input.GetVisualDescendants().OfType<FieldFrame>().First().Bounds.Height;

            defaultButtonH.ShouldBe(44, 1.0);
            defaultComboboxFrameH.ShouldBe(44, 1.0);
            defaultInputFrameH.ShouldBe(44, 1.0);
            defaultCardTriggerH.ShouldBeGreaterThanOrEqualTo(64);

            ArcTheme.Apply(t => t.Compact());
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var compactButtonH = button.Bounds.Height;
            var compactComboboxFrameH = combobox.GetVisualDescendants().OfType<Border>().First(b => b.Name == Combobox.FramePart).Bounds.Height;
            var compactCardTriggerH = card.GetVisualDescendants().OfType<ExpandableCardTrigger>().First().Bounds.Height;
            var compactInputFrameH = input.GetVisualDescendants().OfType<FieldFrame>().First().Bounds.Height;

            compactButtonH.ShouldBe(34, 1.0);
            compactComboboxFrameH.ShouldBe(34, 1.0);
            compactInputFrameH.ShouldBe(34, 1.0);
            compactCardTriggerH.ShouldBeLessThan(defaultCardTriggerH);
            compactCardTriggerH.ShouldBeGreaterThanOrEqualTo(44);

            // Switch compact geometry invariant: ThumbOnOffset = TrackWidth - 2*TrackInset - ThumbSize.
            static T ReadSwitch<T>(Control host, SwitchTokenKind kind)
            {
                host.TryFindResource(SwitchTokens.Key(kind), host.ActualThemeVariant, out var value).ShouldBeTrue();
                return (T)value!;
            }
            var trackW = ReadSwitch<double>(toggle, SwitchTokenKind.TrackWidth);
            var trackInset = ReadSwitch<Thickness>(toggle, SwitchTokenKind.TrackInset);
            var thumbSize = ReadSwitch<double>(toggle, SwitchTokenKind.ThumbSize);
            var thumbStretched = ReadSwitch<double>(toggle, SwitchTokenKind.ThumbStretchedWidth);
            var thumbOnOffset = ReadSwitch<double>(toggle, SwitchTokenKind.ThumbOnOffset);
            var thumbPressedOffset = ReadSwitch<double>(toggle, SwitchTokenKind.ThumbOnPressedOffset);

            thumbOnOffset.ShouldBe(trackW - trackInset.Left - trackInset.Right - thumbSize);
            thumbPressedOffset.ShouldBe(trackW - trackInset.Left - trackInset.Right - thumbStretched);

            // Restore to Default.
            ArcTheme.Apply(t => t.Density(ArcDensity.Default));
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            button.Bounds.Height.ShouldBe(defaultButtonH, 1.0);
            card.GetVisualDescendants().OfType<ExpandableCardTrigger>().First().Bounds.Height.ShouldBe(defaultCardTriggerH, 1.0);
        }
        finally
        {
            window.Close();
            ArcTheme.Apply(t => t.Density(ArcDensity.Default));
        }
    }

    [AvaloniaFact]
    public void Density_scope_renders_compact_and_default_side_by_side()
    {
        var normalButton = new Button { Content = "Default" };
        var compactButton = new Button { Content = "Compact" };
        var compactScope = new Border { Child = compactButton };
        ArcThemeScope.SetDensity(compactScope, ArcDensity.Compact);

        var window = TestHelpers.Show(new StackPanel { Spacing = 8, Children = { normalButton, compactScope } });
        try
        {
            normalButton.Bounds.Height.ShouldBe(44, 1.0);
            compactButton.Bounds.Height.ShouldBe(34, 1.0);
        }
        finally
        {
            window.Close();
        }
    }
}
