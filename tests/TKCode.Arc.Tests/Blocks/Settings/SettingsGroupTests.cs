using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// The shared Settings parts: rows stack when narrow, label clicks toggle, hairlines between rows only, columns (fixed,
/// responsive, spans), the row layout a group hands down to its rows, and the row's leading and details slots.
/// </summary>
public class SettingsGroupTests
{
    private static SettingsGroup Group(out SettingsRow first, out SettingsRow second, out Switch toggle)
    {
        toggle = new Switch();
        first = new SettingsRow { Header = "Mentions", Description = "When someone mentions you.", Content = toggle };
        second = new SettingsRow { Header = "Digest", Content = new Button { Content = "Edit" } };
        return new SettingsGroup { Header = "Notifications", Width = 640, Items = { first, second } };
    }

    [AvaloniaFact]
    public void A_Narrow_Row_Moves_Its_Control_Under_The_Text()
    {
        var group = Group(out var first, out _, out _);
        var window = TestHelpers.Show(group);
        try
        {
            first.IsStacked.ShouldBeFalse();
            first.Classes.Contains(SettingsRow.StackedPseudoClass).ShouldBeFalse();

            group.Width = 360;
            Dispatcher.UIThread.RunJobs();
            first.IsStacked.ShouldBeTrue();
            var presenter = TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(first, "PART_ContentPresenter");
            Grid.GetRow(presenter).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Label_Toggles_A_Toggle_Control()
    {
        var group = Group(out var first, out _, out var toggle);
        var window = TestHelpers.Show(group);
        try
        {
            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var label = TestHelpers.Descendant<StackPanel>(first, SettingsRow.LabelPart);
            var point = label.TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            toggle.IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Rows_After_The_First_Have_A_Hairline()
    {
        var group = Group(out var first, out var second, out _);
        var window = TestHelpers.Show(group);
        try
        {
            TestHelpers.Descendant<Border>(first, "PART_Divider").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(second, "PART_Divider").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Generated_From_ItemsSource_Have_No_Hairline_Above_The_First()
    {
        // Issue #106: a templated row was built after the panel picked the first line, so it kept its hairline.
        var group = new SettingsGroup
        {
            Header = "Notifications",
            Width = 640,
            ItemsSource = new[] { "Mentions", "Digest", "Replies" },
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((name, _) => new SettingsRow { Header = name })
        };
        var window = TestHelpers.Show(group);
        try
        {
            var rows = group.GetRealizedContainers()
                .Select(container => container as SettingsRow ?? (container as ContentPresenter)?.Child as SettingsRow)
                .ToList();
            rows.Count.ShouldBe(3);
            TestHelpers.Descendant<Border>(rows[0]!, "PART_Divider").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(rows[1]!, "PART_Divider").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Border>(rows[2]!, "PART_Divider").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Control_Takes_The_Header_And_Description_Unless_It_Has_Its_Own()
    {
        var group = Group(out var first, out var second, out var toggle);
        var window = TestHelpers.Show(group);
        try
        {
            AutomationProperties.GetName(toggle).ShouldBe("Mentions");
            AutomationProperties.GetHelpText(toggle).ShouldBe("When someone mentions you.");
            first.Header = "Replies";
            AutomationProperties.GetName(toggle).ShouldBe("Replies");

            var named = new Button { Content = "Edit" };
            AutomationProperties.SetName(named, "Edit digest");
            second.Content = named;
            AutomationProperties.GetName(named).ShouldBe("Edit digest");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Plain_Group_Has_No_Frame()
    {
        var group = Group(out _, out _, out _);
        group.Appearance = SettingsGroupAppearance.Plain;
        var window = TestHelpers.Show(group);
        try
        {
            group.Classes.Contains(SettingsGroup.PlainPseudoClass).ShouldBeTrue();
            group.BorderThickness.ShouldBe(new Thickness(0));
        }
        finally
        {
            window.Close();
        }
    }

    private static SettingsGroup Rows(int count, double width)
    {
        var group = new SettingsGroup { Width = width };
        for (var i = 0; i < count; i++)
        {
            group.Items.Add(new SettingsRow { Header = $"Setting {i}", Content = new Switch() });
        }
        return group;
    }

    private static SettingsRow Row(SettingsGroup group, int index) => (SettingsRow)group.Items[index]!;

    private static ContentPresenter ControlOf(SettingsRow row) =>
        TestHelpers.Descendant<ContentPresenter>(row, SettingsRow.ContentPart);

    private static double Left(Visual visual, Visual root) => visual.TranslatePoint(new Point(0, 0), root)!.Value.X;

    [AvaloniaFact]
    public void By_Default_The_Rows_Sit_In_One_Column_Like_A_Stack()
    {
        var group = Rows(3, 640);
        var window = TestHelpers.Show(group);
        try
        {
            group.Columns.ShouldBe(1);
            group.ActualColumns.ShouldBe(1);
            group.ColumnSpacing.ShouldBe(32);
            group.RowSpacing.ShouldBe(0);
            var first = Row(group, 0);
            var second = Row(group, 1);
            var third = Row(group, 2);
            second.Bounds.X.ShouldBe(first.Bounds.X);
            second.Bounds.Width.ShouldBe(first.Bounds.Width);
            second.Bounds.Y.ShouldBe(first.Bounds.Bottom);
            third.Bounds.Y.ShouldBe(second.Bounds.Bottom);
            first.Classes.Contains(SettingsRow.FirstLinePseudoClass).ShouldBeTrue();
            second.Classes.Contains(SettingsRow.FirstLinePseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<Border>(second, "PART_Divider").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_Columns_Put_Rows_Side_By_Side_And_Only_The_Second_Line_Has_Hairlines()
    {
        var group = Rows(3, 680);
        group.Columns = 2;
        group.ColumnSpacing = 20;
        group.RowSpacing = 8;
        var window = TestHelpers.Show(group);
        try
        {
            group.ActualColumns.ShouldBe(2);
            var first = Row(group, 0);
            var second = Row(group, 1);
            var third = Row(group, 2);
            second.Bounds.Y.ShouldBe(first.Bounds.Y);
            second.Bounds.X.ShouldBe(first.Bounds.Right + 20, 0.01);
            second.Bounds.Width.ShouldBe(first.Bounds.Width, 0.01);
            third.Bounds.X.ShouldBe(first.Bounds.X);
            third.Bounds.Y.ShouldBe(first.Bounds.Bottom + 8, 0.01);
            TestHelpers.Descendant<Border>(second, "PART_Divider").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(third, "PART_Divider").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Row_With_Column_Span_Zero_Takes_The_Whole_Line()
    {
        var group = Rows(4, 680);
        group.Columns = 2;
        SettingsGroup.SetColumnSpan(Row(group, 1), 0);
        var window = TestHelpers.Show(group);
        try
        {
            var first = Row(group, 0);
            var wide = Row(group, 1);
            var third = Row(group, 2);
            var fourth = Row(group, 3);
            // The wide row does not fit beside the first, so it starts the next line and fills it.
            wide.Bounds.X.ShouldBe(first.Bounds.X);
            wide.Bounds.Y.ShouldBe(first.Bounds.Bottom, 0.01);
            wide.Bounds.Width.ShouldBe(first.Bounds.Width * 2 + group.ColumnSpacing, 0.01);
            third.Bounds.Y.ShouldBe(wide.Bounds.Bottom, 0.01);
            fourth.Bounds.Y.ShouldBe(third.Bounds.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Min_Column_Width_Drops_Columns_When_The_Group_Narrows()
    {
        var group = Rows(4, 1000);
        group.Appearance = SettingsGroupAppearance.Plain;
        group.Columns = 0;
        group.MinColumnWidth = 220;
        group.ColumnSpacing = 20;
        var window = TestHelpers.Show(group);
        try
        {
            // (1000 + 20) / (220 + 20) = 4.25, and there are four rows.
            group.ActualColumns.ShouldBe(4);

            group.Width = 500;
            Dispatcher.UIThread.RunJobs();
            group.ActualColumns.ShouldBe(2);
            Row(group, 1).Bounds.Y.ShouldBe(Row(group, 0).Bounds.Y);

            group.Width = 300;
            Dispatcher.UIThread.RunJobs();
            group.ActualColumns.ShouldBe(1);
            Row(group, 1).Bounds.Y.ShouldBe(Row(group, 0).Bounds.Bottom);

            // A fixed number of columns caps the responsive count.
            group.Width = 1000;
            group.Columns = 2;
            Dispatcher.UIThread.RunJobs();
            group.ActualColumns.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Inherit_The_Label_Width_So_Controls_Line_Up_And_A_Row_Can_Override_It()
    {
        var group = Rows(3, 640);
        group.Appearance = SettingsGroupAppearance.Plain;
        group.LabelWidth = 240;
        var withIcon = Row(group, 1);
        withIcon.Icon = ArcIcons.Bell;
        var own = Row(group, 2);
        own.LabelWidth = 320;
        var window = TestHelpers.Show(group);
        try
        {
            var first = Row(group, 0);
            first.LabelWidth.ShouldBe(240);
            first.ContentAlignment = HorizontalAlignment.Left;
            withIcon.ContentAlignment = HorizontalAlignment.Left;
            own.ContentAlignment = HorizontalAlignment.Left;
            Dispatcher.UIThread.RunJobs();

            // The control starts after the label width plus the spacing, icon or not.
            Left(ControlOf(first), first).ShouldBe(240 + 16, 0.5);
            Left(ControlOf(withIcon), withIcon).ShouldBe(240 + 16, 0.5);
            TestHelpers.Descendant<StackPanel>(first, SettingsRow.LabelPart).Bounds.Width.ShouldBe(240, 0.5);
            Left(ControlOf(own), own).ShouldBe(320 + 16, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Alignment_And_Spacing_Come_From_The_Group()
    {
        var group = Rows(2, 640);
        group.Appearance = SettingsGroupAppearance.Plain;
        group.LabelWidth = 200;
        group.ContentAlignment = HorizontalAlignment.Stretch;
        group.ContentSpacing = 24;
        var window = TestHelpers.Show(group);
        try
        {
            var first = Row(group, 0);
            var presenter = ControlOf(first);
            presenter.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
            presenter.Margin.ShouldBe(new Thickness(24, 0, 0, 0));
            presenter.Bounds.Width.ShouldBe(640 - 200 - 24, 0.5);

            // Under the text the spacing goes above the control, and Stretch still fills the row.
            group.Width = 360;
            Dispatcher.UIThread.RunJobs();
            first.IsStacked.ShouldBeTrue();
            presenter.Margin.ShouldBe(new Thickness(0, 24, 0, 0));
            presenter.Bounds.Width.ShouldBe(360, 0.5);

            // A row's own value wins; clearing the group's brings the theme's spacing back.
            var second = Row(group, 1);
            second.ContentSpacing = 4;
            group.ContentSpacing = double.NaN;
            Dispatcher.UIThread.RunJobs();
            ControlOf(second).Margin.ShouldBe(new Thickness(0, 4, 0, 0));
            presenter.Margin.ShouldBe(new Thickness(0, 12, 0, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Default_Rows_Keep_The_Control_At_The_Right()
    {
        var group = Rows(1, 640);
        group.Appearance = SettingsGroupAppearance.Plain;
        var window = TestHelpers.Show(group);
        try
        {
            var row = Row(group, 0);
            var presenter = ControlOf(row);
            presenter.HorizontalAlignment.ShouldBe(HorizontalAlignment.Right);
            presenter.Margin.ShouldBe(new Thickness(16, 0, 0, 0));
            presenter.Bounds.Right.ShouldBe(640, 0.5);
            double.IsNaN(row.LabelWidth).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Details_Sit_Under_The_Description_And_Are_Built_Only_When_Set()
    {
        var group = Group(out var first, out var second, out _);
        var window = TestHelpers.Show(group);
        try
        {
            first.Details = "Last synced 2 minutes ago";
            Dispatcher.UIThread.RunJobs();
            first.Classes.Contains(SettingsRow.DetailsPseudoClass).ShouldBeTrue();
            var details = TestHelpers.Descendant<ContentPresenter>(first, SettingsRow.DetailsPart);
            details.IsVisible.ShouldBeTrue();
            details.Content.ShouldBe("Last synced 2 minutes ago");
            var description = TestHelpers.Descendant<TextBlock>(first, "PART_Description");
            details.TranslatePoint(default, first)!.Value.Y.ShouldBeGreaterThan(description.TranslatePoint(default, first)!.Value.Y);
            details.Margin.ShouldBe(new Thickness(0, 4, 0, 0));

            // Any content works, such as an error line with its own color.
            var error = new TextBlock { Text = "The key expired." };
            first.Details = error;
            Dispatcher.UIThread.RunJobs();
            error.GetVisualParent().ShouldBe(details);

            first.Details = null;
            Dispatcher.UIThread.RunJobs();
            details.IsVisible.ShouldBeFalse();
            first.Classes.Contains(SettingsRow.DetailsPseudoClass).ShouldBeFalse();

            // A row without details has no presenter for them.
            second.GetVisualDescendants().OfType<ContentPresenter>().Any(p => p.Name == SettingsRow.DetailsPart).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leading_Content_Replaces_The_Icon_Tile()
    {
        var group = Group(out var first, out _, out _);
        first.Icon = ArcIcons.Bell;
        var window = TestHelpers.Show(group);
        try
        {
            var tile = TestHelpers.Descendant<Border>(first, SettingsRow.IconTilePart);
            tile.IsVisible.ShouldBeTrue();
            first.Classes.Contains(SettingsRow.IconPseudoClass).ShouldBeTrue();

            var avatar = new Border { Width = 40, Height = 40 };
            first.Leading = avatar;
            Dispatcher.UIThread.RunJobs();
            tile.IsVisible.ShouldBeFalse();
            first.Classes.Contains(SettingsRow.IconPseudoClass).ShouldBeFalse();
            first.Classes.Contains(SettingsRow.LeadingPseudoClass).ShouldBeTrue();
            var leading = TestHelpers.Descendant<ContentPresenter>(first, SettingsRow.LeadingPart);
            avatar.GetVisualParent().ShouldBe(leading);
            // The text starts after the leading content and the icon tile's gap.
            var label = TestHelpers.Descendant<StackPanel>(first, SettingsRow.LabelPart);
            Left(label, first).ShouldBe(40 + 16, 0.5);

            first.Leading = null;
            Dispatcher.UIThread.RunJobs();
            leading.IsVisible.ShouldBeFalse();
            tile.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Label_Width_Includes_The_Leading_Content()
    {
        var group = Rows(2, 640);
        group.Appearance = SettingsGroupAppearance.Plain;
        group.LabelWidth = 240;
        group.ContentAlignment = HorizontalAlignment.Left;
        var withLeading = Row(group, 1);
        withLeading.Leading = new Border { Width = 40, Height = 40 };
        var window = TestHelpers.Show(group);
        try
        {
            // The controls line up whether the row has leading content or not.
            Left(ControlOf(Row(group, 0)), Row(group, 0)).ShouldBe(240 + 16, 0.5);
            Left(ControlOf(withLeading), withLeading).ShouldBe(240 + 16, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
