using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The "Memory updated" pill of the agent memory: its two states, its fixed width, its press and the paused notice.</summary>
public class MemoryCapturedPillTests
{
    // Invented sample data (spec section 10).
    private const string Title = "Memory updated";
    private const string Note = "Noted: you run builds with the dotnet CLI, not the IDE";
    private const string Saved = "Saved to Preferences";
    private const string Paused = "Saved memories are paused. Orbit will not note or use new ones until you turn them back on.";

    private static readonly string[] Candidates = ["Saved to About you", "Saved to Preferences", "Saved to Projects", "Saved to People"];

    private static MemoryCapturedPill Create() => new()
    {
        Title = Title,
        Note = Note,
        SizingTexts = Candidates,
        AccessibleName = "Memory updated. Noted: you run builds with the dotnet CLI, not the IDE. File under Preferences",
        PausedText = Paused
    };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 700)
    {
        var host = new Border { Width = width, Padding = new Thickness(16), Child = content, HorizontalAlignment = HorizontalAlignment.Left };
        var window = TestHelpers.Show(host, level);
        window.Width = Math.Max(400, width + 40);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ArcContentSwap Swap(MemoryCapturedPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, MemoryCapturedPill.ContentPart);

    private static Border Frame(MemoryCapturedPill pill) => TestHelpers.Descendant<Border>(pill, "PART_Frame");

    private static Border FocusRing(MemoryCapturedPill pill) => TestHelpers.Descendant<Border>(pill, "PART_FocusRing");

    private static DockPanel Notice(MemoryCapturedPill pill) => TestHelpers.Descendant<DockPanel>(pill, "PART_Paused");

    /// <summary>Layers in the content swap: 1 at rest, 2 while the old state fades out.</summary>
    private static int Layers(MemoryCapturedPill pill) => Swap(pill).GetVisualChildren().Count();

    private static T Part<T>(Control content, string name) where T : Control => TestHelpers.Descendant<T>(content, name);

    private static Rect BoundsIn(Visual visual, Visual root) =>
        new(visual.TranslatePoint(default, root)!.Value, visual.Bounds.Size);

    private static Point CenterOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs posted jobs until <paramref name="done"/> holds (continuations come back from the thread pool).</summary>
    private static bool WaitFor(Func<bool> done)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (done())
            {
                return true;
            }
            Thread.Sleep(5);
        }
        return false;
    }

    /// <summary>The width a row of glyph + 7 + text would take, measured with a plain text block of the same font.</summary>
    private static double NaturalWidth(TextBlock text)
    {
        var probe = new TextBlock
        {
            Text = text.Text,
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontWeight = text.FontWeight,
            LetterSpacing = text.LetterSpacing
        };
        probe.Measure(Size.Infinity);
        return probe.DesiredSize.Width;
    }

    #region Look

    [AvaloniaFact]
    public void Is_A_34px_Bordered_Pill_On_The_Surface()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            pill.Bounds.Height.ShouldBe(34);
            pill.Padding.ShouldBe(new Thickness(10, 0, 12, 0));
            pill.BorderThickness.ShouldBe(new Thickness(1));
            pill.CornerRadius.TopLeft.ShouldBeGreaterThanOrEqualTo(17);
            pill.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
            pill.Focusable.ShouldBeTrue();
            pill.ClipToBounds.ShouldBeFalse();
            pill.FocusAdorner.ShouldBeNull();
            TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Surface));
            TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Border));
            FocusRing(pill).IsVisible.ShouldBeFalse();
            Notice(pill).IsVisible.ShouldBeFalse();
            pill.Classes.ShouldNotContain(MemoryCapturedPill.SavedPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Note_Row_Shows_The_Bookmark_The_Title_The_Note_And_The_Arrow()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            var content = pill.CurrentContent!;
            content.IsSaved.ShouldBeFalse();
            var bookmark = Part<ArcGlyph>(content, "PART_Bookmark");
            var title = Part<TextBlock>(content, "PART_Title");
            var note = Part<TextBlock>(content, "PART_Note");
            var arrow = Part<ArcGlyph>(content, "PART_Arrow");
            Part<DockPanel>(content, "PART_SavedRow").IsVisible.ShouldBeFalse();
            title.Text.ShouldBe(Title);
            note.Text.ShouldBe(Note);
            bookmark.Size.ShouldBe(15);
            arrow.Size.ShouldBe(15);
            bookmark.StrokeThickness.ShouldBe(2);
            title.FontSize.ShouldBe(14);
            title.LineHeight.ShouldBe(19.6, 0.01);
            title.FontWeight.ShouldBe(FontWeight.Medium);
            note.FontWeight.ShouldBe(FontWeight.Normal);
            note.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            TestHelpers.ColorOf(bookmark.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(title.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(note.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(arrow.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextMuted));
            // 7px between the parts, in one row, starting at the 10px padding (inside the 1px border).
            var b = BoundsIn(bookmark, pill);
            var t = BoundsIn(title, pill);
            var n = BoundsIn(note, pill);
            var a = BoundsIn(arrow, pill);
            b.Left.ShouldBe(11, 0.01);
            (t.Left - b.Right).ShouldBe(7, 0.01);
            (n.Left - t.Right).ShouldBe(7, 0.01);
            (a.Left - n.Right).ShouldBe(7, 0.01);
            // The note row is the widest text here: the arrow ends at the right padding.
            (pill.Bounds.Width - a.Right).ShouldBe(13, 0.01);
            (b.Center.Y).ShouldBe(17, 0.5);
            (a.Center.Y).ShouldBe(17, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Saved_State_Shows_A_Check_And_The_Text()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            pill.SavedText = Saved;
            pill.Classes.ShouldContain(MemoryCapturedPill.SavedPseudoClass);
            var content = pill.CurrentContent!;
            content.IsSaved.ShouldBeTrue();
            window.UpdateLayout();
            Part<DockPanel>(content, "PART_NoteRow").IsVisible.ShouldBeFalse();
            var check = Part<ArcGlyph>(content, "PART_Check");
            var text = Part<TextBlock>(content, "PART_SavedText");
            text.Text.ShouldBe(Saved);
            check.Size.ShouldBe(15);
            check.Data.ShouldBe(ArcIcons.Check);
            TestHelpers.ColorOf(check.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Success));
            TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Foreground));
            text.FontWeight.ShouldBe(FontWeight.Normal);
            // Left-aligned in the (wider) pill, no arrow.
            BoundsIn(check, pill).Left.ShouldBe(11, 0.01);
            (BoundsIn(text, pill).Left - BoundsIn(check, pill).Right).ShouldBe(7, 0.01);

            pill.SavedText = null;
            pill.Classes.ShouldNotContain(MemoryCapturedPill.SavedPseudoClass);
            pill.CurrentContent!.IsSaved.ShouldBeFalse();
            pill.CurrentContent.Note.ShouldBe(Note);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Width

    [AvaloniaFact]
    public void The_Width_Is_The_Same_In_The_Note_And_The_Saved_State()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            var width = pill.Bounds.Width;
            width.ShouldBeGreaterThan(200);
            pill.SavedText = Saved;
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(width);
            pill.SavedText = "Saved to People";
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(width);
            pill.SavedText = null;
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Widest_Possible_Text_Sets_The_Width()
    {
        var pill = new MemoryCapturedPill { Title = "Saved", Note = "Short" };
        var window = Show(pill);
        try
        {
            var noteOnly = pill.Bounds.Width;
            // A longer saved text that may come later widens the pill now, so it will not grow when it shows.
            pill.SizingTexts = ["Saved to a group with a rather long name indeed"];
            window.UpdateLayout();
            var widened = pill.Bounds.Width;
            widened.ShouldBeGreaterThan(noteOnly + 50);
            pill.SavedText = "Saved to a group with a rather long name indeed";
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(widened);
            // The note row stays left-aligned in the wider pill (the arrow follows the note).
            pill.SavedText = null;
            window.UpdateLayout();
            var arrow = Part<ArcGlyph>(pill.CurrentContent!, "PART_Arrow");
            BoundsIn(arrow, pill).Right.ShouldBeLessThan(widened - 13 - 20);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Growing_List_Of_Texts_Is_Followed()
    {
        var texts = new ObservableCollection<string> { "Saved to A" };
        var pill = new MemoryCapturedPill { Title = "Saved", Note = "Short", SizingTexts = texts };
        var window = Show(pill);
        try
        {
            var before = pill.Bounds.Width;
            texts.Add("Saved to a group with a rather long name indeed");
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBeGreaterThan(before + 50);
            pill.Sizer!.Children.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void In_A_Narrow_Container_The_Note_Is_Trimmed()
    {
        var pill = Create();
        // A 288 card with the compact pill row padding (16 left... 12 / 16) leaves about 260.
        var window = Show(pill, width: 288);
        try
        {
            pill.Bounds.Width.ShouldBe(256, 0.01);
            var content = pill.CurrentContent!;
            var note = Part<TextBlock>(content, "PART_Note");
            var arrow = Part<ArcGlyph>(content, "PART_Arrow");
            // Only the note gives way (the ellipsis itself shows in the Skia snapshots; headless text does not draw it).
            note.Bounds.Width.ShouldBeLessThan(NaturalWidth(note) - 20);
            note.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            // The arrow stays inside the pill, at the right padding.
            BoundsIn(arrow, pill).Right.ShouldBe(256 - 13, 0.5);
            var title = Part<TextBlock>(content, "PART_Title");
            title.Bounds.Width.ShouldBe(NaturalWidth(title), 0.5);
            pill.SavedText = Saved;
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(256, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Press

    [AvaloniaFact]
    public void A_Press_Clicks_And_Runs_The_Command()
    {
        var pill = Create();
        var clicks = 0;
        var commands = 0;
        pill.Click += (_, _) => clicks++;
        pill.Command = new RelayCommand(() => commands++);
        var window = Show(pill);
        try
        {
            Settle(window);
            var point = CenterOf(pill, window);
            window.MouseDown(point, MouseButton.Left);
            clicks.ShouldBe(0);
            window.MouseUp(point, MouseButton.Left);
            clicks.ShouldBe(1);
            commands.ShouldBe(1);
            // A pointer press focuses without the ring.
            Settle(window);
            FocusRing(pill).IsVisible.ShouldBeFalse();

            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            clicks.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            clicks.ShouldBe(3);
            commands.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Ring_2px_Outside()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            pill.Focus(NavigationMethod.Tab);
            Settle(window);
            var ring = FocusRing(pill);
            ring.IsVisible.ShouldBeTrue();
            ring.BorderThickness.ShouldBe(new Thickness(2));
            ring.Margin.ShouldBe(new Thickness(-4));
            TestHelpers.ColorOf(ring.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.FocusRing));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Tints_The_Pill_And_Touch_Leaves_No_Hover()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            Settle(window);
            var point = CenterOf(pill, window);
            window.MouseMove(point, RawInputModifiers.None);
            pill.Classes.ShouldContain(MemoryCapturedPill.HoverPseudoClass);
            TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted));
            TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.BorderStrong));
            window.MouseMove(new Point(1, 1), RawInputModifiers.None);
            pill.Classes.ShouldNotContain(MemoryCapturedPill.HoverPseudoClass);
            TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Surface));

            var touch = window.TouchBegin(point);
            window.TouchEnd(touch, point);
            pill.Classes.ShouldNotContain(MemoryCapturedPill.HoverPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Colors_Ease_Over_160ms_At_Full_And_120ms_At_Reduced()
    {
        var pill = Create();
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            Settle(window);
            pill.Classes.ShouldContain(":motion-full");
            var transitions = pill.Transitions!;
            transitions.OfType<ArcBrushTransition>().Select(t => t.Property).ShouldBe([Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty], ignoreOrder: true);
            transitions.OfType<ArcBrushTransition>().ShouldAllBe(t => t.Duration == TimeSpan.FromMilliseconds(160));
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Settle(window);
            pill.Transitions!.OfType<ArcBrushTransition>().ShouldAllBe(t => t.Duration == TimeSpan.FromMilliseconds(120));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Crossfade

    [AvaloniaFact]
    public void Filing_Crossfades_With_A_2px_Lift_And_Keeps_The_Width()
    {
        var pill = Create();
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var swap = Swap(pill);
            swap.Mode.ShouldBe(ContentSwapMode.Lift);
            swap.EnterOffset.ShouldBe(2);
            Layers(pill).ShouldBe(1);
            var width = pill.Bounds.Width;
            var old = pill.CurrentContent;
            pill.SavedText = Saved;
            Layers(pill).ShouldBe(2);
            pill.CurrentContent.ShouldNotBeSameAs(old);
            CoreFrames.Run(clock, 4);
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(width);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            WaitFor(() => Layers(pill) == 1).ShouldBeTrue();
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(width);
            pill.CurrentContent!.Opacity.ShouldBe(1);
            // The same words again bring no new layer.
            pill.SavedText = Saved;
            Layers(pill).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_In_One_Short_Fade()
    {
        var pill = Create();
        var window = Show(pill, ArcMotionLevel.Reduced);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            pill.SavedText = Saved;
            Layers(pill).ShouldBe(2);
            clock.Advance(TimeSpan.FromMilliseconds(140));
            WaitFor(() => Layers(pill) == 1).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Swaps_At_Once()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            pill.SavedText = Saved;
            Layers(pill).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Paused

    [AvaloniaFact]
    public void The_Paused_Notice_Replaces_The_Pill_And_Is_Plain_Text()
    {
        var pill = Create();
        var clicks = 0;
        pill.Click += (_, _) => clicks++;
        var window = Show(pill, width: 600);
        try
        {
            pill.IsPaused = true;
            Settle(window);
            pill.Classes.ShouldContain(MemoryCapturedPill.PausedPseudoClass);
            pill.Focusable.ShouldBeFalse();
            TestHelpers.Descendant<Panel>(pill, "PART_Pill").IsVisible.ShouldBeFalse();
            var notice = Notice(pill);
            notice.IsVisible.ShouldBeTrue();
            var glyph = TestHelpers.Descendant<ArcGlyph>(pill, "PART_PauseGlyph");
            var text = TestHelpers.Descendant<TextBlock>(pill, "PART_PausedText");
            glyph.Size.ShouldBe(16);
            glyph.Data.ShouldBe(MemoryCapturedPillGlyphs.CirclePause);
            TestHelpers.ColorOf(glyph.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextMuted));
            text.Text.ShouldBe(Paused);
            text.FontSize.ShouldBe(14);
            text.LineHeight.ShouldBe(19.6, 0.01);
            text.TextWrapping.ShouldBe(TextWrapping.Wrap);
            TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextSecondary));
            // Circle-pause 2px down, 8px before the sentence, top-aligned; the sentence wraps in a narrow row.
            BoundsIn(glyph, pill).Top.ShouldBe(2, 0.01);
            (BoundsIn(text, pill).Left - BoundsIn(glyph, pill).Right).ShouldBe(8, 0.01);
            text.TextLayout.TextLines.Count.ShouldBeGreaterThan(1);

            // Not focusable, no click with the mouse, no hover look.
            pill.Focus(NavigationMethod.Tab).ShouldBeFalse();
            var point = CenterOf(text, window);
            window.MouseMove(point, RawInputModifiers.None);
            pill.Classes.ShouldNotContain(MemoryCapturedPill.HoverPseudoClass);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            clicks.ShouldBe(0);
            pill.Classes.ShouldNotContain(":pressed");

            pill.IsPaused = false;
            Settle(window);
            pill.Focusable.ShouldBeTrue();
            notice.IsVisible.ShouldBeFalse();
            pill.Bounds.Height.ShouldBe(34);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Skips_The_Paused_Notice()
    {
        var before = new Avalonia.Controls.Button { Content = "Before" };
        var pill = Create();
        pill.IsPaused = true;
        var after = new Avalonia.Controls.Button { Content = "After" };
        var window = Show(new StackPanel { Children = { before, pill, after } });
        try
        {
            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            after.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void Reads_As_A_Button_Named_By_The_Owner_And_Invokes_A_Click()
    {
        var pill = Create();
        var clicks = 0;
        pill.Click += (_, _) => clicks++;
        var window = Show(pill);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(pill);
            peer.GetName().ShouldBe(pill.AccessibleName);
            AutomationProperties.GetName(pill).ShouldBe(pill.AccessibleName);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetChildren().ShouldBeEmpty();
            peer.IsKeyboardFocusable().ShouldBeTrue();
            peer.GetProvider<IInvokeProvider>()!.Invoke();
            clicks.ShouldBe(1);

            pill.AccessibleName = "Memory updated. Saved to Preferences";
            peer.GetName().ShouldBe("Memory updated. Saved to Preferences");

            pill.IsPaused = true;
            Settle(window);
            peer.GetName().ShouldBe(Paused);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            peer.IsKeyboardFocusable().ShouldBeFalse();
            peer.GetProvider<IInvokeProvider>()!.Invoke();
            clicks.ShouldBe(1);

            pill.IsPaused = false;
            peer.GetName().ShouldBe("Memory updated. Saved to Preferences");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sizing_Layer_Is_Invisible_To_Input_And_Screen_Readers()
    {
        var pill = Create();
        var window = Show(pill);
        try
        {
            var sizer = pill.Sizer!;
            sizer.Opacity.ShouldBe(0);
            sizer.IsHitTestVisible.ShouldBeFalse();
            AutomationProperties.GetAccessibilityView(sizer).ShouldBe(AccessibilityView.Raw);
            // Note row, saved row (hidden until saved) and the four candidates.
            sizer.Children.Count.ShouldBe(6);
            sizer.Children.OfType<MemoryCapturedPillContent>().Count(c => c.IsSaved && c.IsVisible).ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    private sealed class RelayCommand(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
