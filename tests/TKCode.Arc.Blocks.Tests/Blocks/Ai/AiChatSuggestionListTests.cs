using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiChatSuggestionList"/> and <see cref="AiChatEmptyState"/>: at most three 44px buttons 6px apart in a column
/// 368 wide (narrower in a narrow container), labels, icons and names that follow the data, the limit, the events (prompt
/// or label), Tab / Enter / Space, the hover colors; the empty state's layout, greeting font and its fade in and out.
/// </summary>
public class AiChatSuggestionListTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 800, double height = 600)
    {
        var window = TestHelpers.Show(content, level);
        window.Width = width;
        window.Height = height;
        Layout(window);
        return window;
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static AiChatSuggestionList List(IEnumerable<AiChatSuggestion>? items = null) =>
        new() { ItemsSource = items ?? AiChatScrollSamples.Suggestions(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };

    private static Point CenterOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window) ?? default;

    #region List

    [AvaloniaFact]
    public void Shows_At_Most_Three_44px_Buttons_6px_Apart_In_A_368_Column()
    {
        var list = List();
        var window = Show(list);
        try
        {
            Assert.Equal(3, list.Buttons.Count);
            Assert.Equal(368, list.Bounds.Width, 1);
            Assert.Equal(3 * 44 + 2 * 6, list.Bounds.Height, 1);
            for (var i = 0; i < 3; i++)
            {
                var button = list.Buttons[i];
                Assert.Equal(44, button.Bounds.Height, 1);
                Assert.Equal(368, button.Bounds.Width, 1);
                Assert.Equal(i * 50, button.Bounds.Y, 1);
            }
            Assert.Equal(new[] { "Find the slowest step in the nightly build", "Compare this week's test failures to last week", "Check disk usage on ci-runner-7" },
                list.Buttons.Select(b => b.Label).ToArray());
            Assert.Equal("Find the slowest step in the nightly build", AutomationProperties.GetName(list.Buttons[0]));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Column_Shrinks_With_A_Narrow_Container()
    {
        var list = List();
        var window = Show(new Border { Padding = new Thickness(16, 0), Child = list }, width: 300);
        try
        {
            Assert.Equal(268, list.Bounds.Width, 1);
            Assert.Equal(268, list.Buttons[0].Bounds.Width, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MaxItems_Limits_The_Buttons_And_Fewer_Items_Show_Fewer()
    {
        var items = new AvaloniaList<AiChatSuggestion>(AiChatScrollSamples.Suggestions().Take(2));
        var list = List(items);
        var window = Show(list);
        try
        {
            Assert.Equal(2, list.Buttons.Count);
            list.MaxItems = 1;
            Assert.Single(list.Buttons);
            list.MaxItems = 3;
            Assert.Equal(2, list.Buttons.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Follows_Collection_Changes_And_Reuses_Buttons()
    {
        var items = new AvaloniaList<AiChatSuggestion>(AiChatScrollSamples.Suggestions().Take(1));
        var list = List(items);
        var window = Show(list);
        try
        {
            var first = list.Buttons[0];
            var more = AiChatScrollSamples.Suggestions();
            items.Add(more[1]);
            items.Add(more[2]);
            items.Add(more[3]);
            Assert.Equal(3, list.Buttons.Count);
            Assert.Same(first, list.Buttons[0]);
            items.RemoveAt(0);
            Assert.Equal(3, list.Buttons.Count);
            Assert.Same(first, list.Buttons[0]);
            Assert.Equal(more[1].Label, first.Label);
            Assert.Equal(more[3].Label, list.Buttons[2].Label);
            items.Clear();
            Assert.Empty(list.Buttons);
            Assert.False(list.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Follows_A_Suggestions_Label_And_Icon()
    {
        var items = AiChatScrollSamples.Suggestions();
        var list = List(items);
        var window = Show(list);
        try
        {
            items[0].Label = "Find the slowest job";
            items[0].Icon = null;
            var button = list.Buttons[0];
            Assert.Equal("Find the slowest job", button.Label);
            Assert.Equal("Find the slowest job", AutomationProperties.GetName(button));
            Layout(window);
            var icon = TestHelpers.Descendant<ArcGlyph>(button, "PART_Icon");
            Assert.False(icon.IsVisible);
            var label = TestHelpers.Descendant<TextBlock>(button, "PART_Label");
            Assert.Equal(17, label.TranslatePoint(default, button)!.Value.X, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Icon_Is_16px_And_The_Label_Starts_12px_After_It()
    {
        var list = List();
        var window = Show(list);
        try
        {
            var button = list.Buttons[0];
            var icon = TestHelpers.Descendant<ArcGlyph>(button, "PART_Icon");
            var label = TestHelpers.Descendant<TextBlock>(button, "PART_Label");
            Assert.True(icon.IsVisible);
            Assert.Equal(16, icon.Bounds.Width, 1);
            Assert.Equal(1.75, icon.StrokeThickness);
            // 1px border + 16 padding.
            Assert.Equal(17, icon.TranslatePoint(default, button)!.Value.X, 1);
            Assert.Equal(17 + 16 + 12, label.TranslatePoint(default, button)!.Value.X, 1);
            Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Raises_Invoked_With_The_Prompt_Or_The_Label()
    {
        var list = List();
        var window = Show(list);
        var invoked = new List<AiChatSuggestionEventArgs>();
        list.Invoked += (_, e) => invoked.Add(e);
        try
        {
            foreach (var index in new[] { 0, 2 })
            {
                var point = CenterOf(list.Buttons[index], window);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Layout(window);
            }
            Assert.Equal(2, invoked.Count);
            Assert.Equal("Find the slowest step in the nightly build", invoked[0].Prompt);
            Assert.Same(list.ItemsSource!.First(), invoked[0].Suggestion);
            Assert.Equal("Check disk usage on ci-runner-7 and tell me what is filling it up.", invoked[1].Prompt);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Buttons_Are_Tab_Stops_And_Enter_Or_Space_Invokes()
    {
        var before = new Button { Content = "Before" };
        var list = List();
        var panel = new StackPanel { Children = { before, list } };
        var window = Show(panel);
        var invoked = new List<string>();
        list.Invoked += (_, e) => invoked.Add(e.Prompt);
        try
        {
            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Layout(window);
            Assert.True(list.Buttons[0].IsFocused);
            Assert.Contains(":focus-visible", list.Buttons[0].Classes);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Layout(window);
            Assert.True(list.Buttons[1].IsFocused);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Layout(window);
            Assert.Equal(new[] { "Compare this week's test failures to last week", "Compare this week's test failures to last week" }, invoked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Strengthens_The_Border_Tints_The_Fill_And_Darkens_The_Label()
    {
        var list = List();
        var window = Show(list);
        try
        {
            var button = list.Buttons[1];
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.Surface), TestHelpers.ColorOf(button.Background));
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.Border), TestHelpers.ColorOf(button.BorderBrush));
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.TextSecondary), TestHelpers.ColorOf(button.Foreground));
            window.MouseMove(CenterOf(button, window), RawInputModifiers.None);
            Layout(window);
            Assert.Contains(":hover", button.Classes);
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(button.Background));
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.BorderStrong), TestHelpers.ColorOf(button.BorderBrush));
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.Foreground), TestHelpers.ColorOf(button.Foreground));
            var icon = TestHelpers.Descendant<ArcGlyph>(button, "PART_Icon");
            Assert.Equal(ArcTokens.GetColor(button, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(icon.Foreground));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Empty state

    [AvaloniaFact]
    public void The_Empty_State_Centers_The_Greeting_Over_The_Suggestions_22px_Apart()
    {
        var empty = new AiChatEmptyState { Greeting = AiChatScrollSamples.Greeting, Suggestions = AiChatScrollSamples.Suggestions() };
        var window = Show(empty);
        try
        {
            var greeting = TestHelpers.Descendant<TextBlock>(empty, "PART_Greeting");
            var list = empty.List!;
            Assert.Equal(3, list.Buttons.Count);
            var top = greeting.TranslatePoint(default, empty)!.Value;
            var listTop = list.TranslatePoint(default, empty)!.Value;
            Assert.Equal(greeting.Bounds.Height + 22, listTop.Y - top.Y, 1);
            Assert.Equal(24.2, greeting.LineHeight, 3);
            Assert.Equal(-0.44, greeting.LetterSpacing, 3);
            Assert.Equal(22, greeting.FontSize, 3);
            Assert.Equal(FontWeight.Normal, greeting.FontWeight);
            Assert.Equal(TextAlignment.Center, greeting.TextAlignment);
            // Centered both ways.
            var height = greeting.Bounds.Height + 22 + list.Bounds.Height;
            Assert.True(Math.Abs((empty.Bounds.Height - height) / 2 - top.Y) <= 1);
            Assert.True(Math.Abs(empty.Bounds.Width / 2 - (listTop.X + list.Bounds.Width / 2)) <= 1);
            Assert.True(Math.Abs(empty.Bounds.Width / 2 - (top.X + greeting.Bounds.Width / 2)) <= 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Suggestions_Only_The_Greeting_Shows()
    {
        var empty = new AiChatEmptyState { Greeting = AiChatScrollSamples.Greeting };
        var window = Show(empty);
        try
        {
            Assert.False(empty.List!.IsVisible);
            var greeting = TestHelpers.Descendant<TextBlock>(empty, "PART_Greeting");
            Assert.True(Math.Abs((empty.Bounds.Height - greeting.Bounds.Height) / 2 - greeting.TranslatePoint(default, empty)!.Value.Y) <= 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Suggestion_Raises_SuggestionInvoked()
    {
        var empty = new AiChatEmptyState { Greeting = AiChatScrollSamples.Greeting, Suggestions = AiChatScrollSamples.Suggestions() };
        var window = Show(empty);
        string? prompt = null;
        var listEvents = 0;
        empty.SuggestionInvoked += (_, e) => prompt = e.Prompt;
        window.AddHandler(AiChatSuggestionList.InvokedEvent, (_, _) => listEvents++);
        try
        {
            var point = CenterOf(empty.List!.Buttons[1], window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Layout(window);
            Assert.Equal("Compare this week's test failures to last week", prompt);
            Assert.Equal(0, listEvents);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Empty_State_Fades_In_Rising_4px_In_160ms_And_Out_In_100ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var empty = new AiChatEmptyState { Greeting = AiChatScrollSamples.Greeting, Suggestions = AiChatScrollSamples.Suggestions(), IsShown = false };
        var window = Show(empty, ArcMotionLevel.Full);
        try
        {
            var root = TestHelpers.Descendant<Border>(empty, AiChatEmptyState.RootPart);
            Assert.False(root.IsVisible);
            empty.IsShown = true;
            Assert.True(root.IsVisible);
            CoreFrames.Run(clock, 1);
            Assert.True(root.Opacity < 0.5);
            var rise = ((Avalonia.Media.TranslateTransform)root.RenderTransform!).Y;
            Assert.InRange(rise, 0.5, 4);
            CoreFrames.Run(clock, 10);
            Assert.Equal(1, root.Opacity, 3);
            Assert.Equal(0, ((Avalonia.Media.TranslateTransform)root.RenderTransform!).Y, 3);

            empty.IsShown = false;
            Assert.False(root.IsHitTestVisible);
            CoreFrames.Run(clock, 3);
            Assert.InRange(root.Opacity, 0.01, 0.9);
            Assert.Equal(0, ((Avalonia.Media.TranslateTransform)root.RenderTransform!).Y, 3);
            CoreFrames.Run(clock, 4);
            Dispatcher.UIThread.RunJobs();
            Assert.False(root.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Shows_And_Hides_At_Once()
    {
        var empty = new AiChatEmptyState { Greeting = AiChatScrollSamples.Greeting };
        var window = Show(empty);
        try
        {
            var root = TestHelpers.Descendant<Border>(empty, AiChatEmptyState.RootPart);
            Assert.True(root.IsVisible);
            Assert.Equal(1, root.Opacity);
            empty.IsShown = false;
            Assert.False(root.IsVisible);
            empty.IsShown = true;
            Assert.True(root.IsVisible);
            Assert.Equal(1, root.Opacity);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
