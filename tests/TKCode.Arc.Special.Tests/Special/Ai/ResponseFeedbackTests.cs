using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// Response feedback: the thumbs and their rating, the reasons panel and the What was good row, details sends with success,
/// failure, retry and cancellation, the keyboard model, the narrow layout and the reply card around it.
/// </summary>
public class ResponseFeedbackTests
{
    private static void Click(Control button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static (FeedbackActions Actions, Window Window) Show(FeedbackActions? actions = null, double width = 520)
    {
        actions ??= new FeedbackActions();
        actions.Width = width;
        var window = TestHelpers.Show(actions);
        window.Width = width + 40;
        Dispatcher.UIThread.RunJobs();
        return (actions, window);
    }

    [Fact]
    public void Defaults_Match_The_Spec()
    {
        var actions = new FeedbackActions();
        actions.Rating.ShouldBe(FeedbackRating.None);
        actions.NegativeReasons.ShouldBe(["Inaccurate", "Not helpful", "Too long", "Unsafe", "Other"]);
        actions.PositiveReasons.ShouldBe(["Accurate", "Easy to follow", "Good examples", "Saved me time"]);
        actions.AllowComment.ShouldBeTrue();
        actions.CommentPlaceholder.ShouldBe("Add details (optional)");
        actions.CommentMaxLength.ShouldBe(1000);
        actions.PrivacyNote.ShouldBe("Shares this reply for review");
        actions.ThanksMessage.ShouldBeNull();
        actions.ThanksDuration.ShouldBe(TimeSpan.FromMilliseconds(1600));
        actions.IsRatingEnabled.ShouldBeTrue();
        actions.IsPanelOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Thumbs_Are_Named_Toggle_Buttons()
    {
        var (actions, window) = Show();
        try
        {
            AutomationProperties.GetName(actions.UpThumb!).ShouldBe("Good response");
            AutomationProperties.GetName(actions.DownThumb!).ShouldBe("Bad response");
            actions.UpThumb.ShouldBeAssignableTo<ToggleButton>();
            actions.UpThumb!.IsChecked.ShouldBe(false);
            actions.PanelBox!.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbs_Down_Opens_The_Reasons_Panel_And_Sends_The_Rating()
    {
        var (actions, window) = Show(new FeedbackActions { MessageId = "msg-1" });
        var changes = new List<FeedbackRatingChangedEventArgs>();
        var sent = new List<FeedbackSubmittedEventArgs>();
        actions.RatingChanged += (_, e) => changes.Add(e);
        actions.FeedbackSubmitted += (_, e) => sent.Add(e);
        try
        {
            Click(actions.DownThumb!);
            actions.Rating.ShouldBe(FeedbackRating.Down);
            actions.DownThumb!.IsChecked.ShouldBe(true);
            changes.Single().OldRating.ShouldBe(FeedbackRating.None);
            changes.Single().NewRating.ShouldBe(FeedbackRating.Down);
            var rating = sent.Single();
            rating.Submission.Stage.ShouldBe(FeedbackStage.Rating);
            rating.Submission.Rating.ShouldBe(FeedbackRating.Down);
            rating.Submission.MessageId.ShouldBe("msg-1");
            rating.IsSimulated.ShouldBeTrue();

            actions.IsPanelOpen.ShouldBeTrue();
            actions.PanelBox!.IsMounted.ShouldBeTrue();
            actions.Form.Title.ShouldBe("What went wrong?");
            actions.Form.Chips.Select(chip => chip.Content).ShouldBe(["Inaccurate", "Not helpful", "Too long", "Unsafe", "Other"]);
            actions.Form.NoteBox!.IsVisible.ShouldBeTrue();
            actions.Form.NoteBox.Placeholder.ShouldBe("Add details (optional)");
            actions.Form.Primary!.Content.ShouldBe("Submit");
            actions.Form.Secondary!.Content.ShouldBe("Cancel");
            actions.Form.FootnoteBlock!.IsVisible.ShouldBeTrue();
            actions.Form.FootnoteBlock.Text.ShouldBe("Shares this reply for review");
            actions.Announcement!.ShouldContain("Rated as a bad response");
            actions.Announcement!.ShouldContain("Nothing was sent");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_The_Active_Thumb_Clears_The_Rating_And_Closes_The_Panel()
    {
        var (actions, window) = Show();
        var sent = new List<FeedbackSubmission>();
        actions.FeedbackSubmitted += (_, e) => sent.Add(e.Submission);
        try
        {
            Click(actions.DownThumb!);
            Click(actions.DownThumb!);
            actions.Rating.ShouldBe(FeedbackRating.None);
            actions.IsPanelOpen.ShouldBeFalse();
            actions.PanelBox!.IsMounted.ShouldBeFalse();
            sent.Select(s => s.Rating).ShouldBe([FeedbackRating.Down, FeedbackRating.None]);
            actions.Announcement!.ShouldContain("Rating cleared");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbs_Up_Asks_What_Was_Good_Unless_There_Are_No_Positive_Reasons()
    {
        var (actions, window) = Show();
        try
        {
            Click(actions.UpThumb!);
            actions.Rating.ShouldBe(FeedbackRating.Up);
            actions.IsPanelOpen.ShouldBeTrue();
            actions.Form.Title.ShouldBe("What was good?");
            actions.Form.Chips.Count.ShouldBe(4);
            actions.Form.NoteBox!.IsVisible.ShouldBeFalse();
            actions.Form.Primary!.Content.ShouldBe("Send");
            actions.Form.Secondary!.Content.ShouldBe("Skip");
            actions.Form.FootnoteBlock!.IsVisible.ShouldBeFalse();

            // Switching to thumbs down swaps the panel for the reasons.
            Click(actions.DownThumb!);
            actions.Rating.ShouldBe(FeedbackRating.Down);
            actions.Form.Title.ShouldBe("What went wrong?");
            actions.UpThumb!.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }

        var (bare, bareWindow) = Show(new FeedbackActions { PositiveReasons = [] });
        try
        {
            Click(bare.UpThumb!);
            bare.Rating.ShouldBe(FeedbackRating.Up);
            bare.IsPanelOpen.ShouldBeFalse();
        }
        finally
        {
            bareWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Submit_Needs_A_Reason_Or_A_Note()
    {
        var (actions, window) = Show();
        try
        {
            Click(actions.DownThumb!);
            actions.Form.Primary!.IsEnabled.ShouldBeFalse();
            actions.Form.Chips[0].IsChecked = true;
            actions.Form.Primary.IsEnabled.ShouldBeTrue();
            actions.Form.Chips[0].IsChecked = false;
            actions.Form.NoteBox!.Text = "   ";
            actions.Form.Primary.IsEnabled.ShouldBeFalse();
            actions.Form.NoteBox.Text = "Wrong version";
            actions.Form.Primary.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Other_Changes_The_Note_Placeholder()
    {
        var (actions, window) = Show();
        try
        {
            Click(actions.DownThumb!);
            actions.Form.Chips.Single(chip => (string?)chip.Content == "Other").IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            actions.Form.NoteBox!.Placeholder.ShouldBe("Tell us what went wrong");
            actions.Form.NoteBox.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Details_Are_Sent_Then_The_Thanks_Hold_And_The_Panel_Folds()
    {
        var calls = new List<FeedbackSubmission>();
        var actions = new FeedbackActions
        {
            MessageId = "m2",
            ThanksDuration = TimeSpan.FromMilliseconds(40),
            FeedbackHandler = (submission, _) =>
            {
                calls.Add(submission);
                return Task.CompletedTask;
            }
        };
        var (_, window) = Show(actions);
        try
        {
            Click(actions.DownThumb!);
            actions.Form.Chips[0].IsChecked = true;
            actions.Form.Chips[2].IsChecked = true;
            actions.Form.NoteBox!.Text = "  Uses a removed API.  ";
            Click(actions.Form.Primary!);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();

            calls.Count.ShouldBe(2);
            calls[0].Stage.ShouldBe(FeedbackStage.Rating);
            var details = calls[1];
            details.Stage.ShouldBe(FeedbackStage.Details);
            details.Rating.ShouldBe(FeedbackRating.Down);
            details.MessageId.ShouldBe("m2");
            details.Reasons.ShouldBe(["Inaccurate", "Too long"]);
            details.Comment.ShouldBe("Uses a removed API.");

            actions.IsShowingThanks.ShouldBeTrue();
            actions.Thanks.Title.ShouldBe("Thanks for your feedback");
            actions.Thanks.Message.ShouldBe("It helps improve future replies.");
            actions.Announcement!.ShouldContain("Thanks for your feedback");

            TestHelpers.PumpUntil(() => !actions.IsPanelOpen).ShouldBeTrue();
            actions.Rating.ShouldBe(FeedbackRating.Down);
            actions.PanelBox!.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulated_Send_Says_Nothing_Was_Sent()
    {
        var before = FeedbackActions.SimulatedDelay;
        FeedbackActions.SimulatedDelay = TimeSpan.FromMilliseconds(400);
        var (actions, window) = Show(new FeedbackActions { ThanksDuration = TimeSpan.FromSeconds(30) });
        try
        {
            Click(actions.UpThumb!);
            actions.Form.Chips[1].IsChecked = true;
            Click(actions.Form.Primary!);
            actions.IsSending.ShouldBeTrue();
            actions.Form.Primary!.IsLoading.ShouldBeTrue();
            TestHelpers.PumpUntil(() => actions.IsShowingThanks).ShouldBeTrue();
            actions.Thanks.Message.ShouldBe("Preview only: nothing was sent.");
        }
        finally
        {
            window.Close();
            FeedbackActions.SimulatedDelay = before;
        }
    }

    [AvaloniaFact]
    public async Task A_Failed_Send_Shows_The_Error_And_Try_Again()
    {
        var fail = true;
        var actions = new FeedbackActions
        {
            ThanksDuration = TimeSpan.FromSeconds(30),
            FeedbackHandler = (submission, _) => submission.Stage == FeedbackStage.Details && fail
                ? Task.FromException(new InvalidOperationException("offline"))
                : Task.CompletedTask
        };
        var (_, window) = Show(actions);
        try
        {
            Click(actions.DownThumb!);
            actions.Form.Chips[3].IsChecked = true;
            Click(actions.Form.Primary!);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();

            actions.IsShowingThanks.ShouldBeFalse();
            actions.Form.ErrorText.ShouldBe("Couldn't send feedback.");
            actions.Form.ErrorBlock!.Text.ShouldBe("Couldn't send feedback.");
            actions.Form.FootnoteBlock!.IsVisible.ShouldBeFalse();
            actions.Form.Primary!.Content.ShouldBe("Try again");
            actions.Announcement.ShouldBe("Couldn't send feedback.");

            fail = false;
            Click(actions.Form.Primary);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            actions.IsShowingThanks.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_The_Panel_Aborts_The_Details_Send()
    {
        CancellationToken token = default;
        var actions = new FeedbackActions
        {
            FeedbackHandler = (submission, cancellation) =>
            {
                if (submission.Stage == FeedbackStage.Rating)
                {
                    return Task.CompletedTask;
                }
                token = cancellation;
                return Task.Delay(Timeout.Infinite, cancellation);
            }
        };
        var (_, window) = Show(actions);
        try
        {
            Click(actions.DownThumb!);
            actions.Form.Chips[0].IsChecked = true;
            Click(actions.Form.Primary!);
            token.CanBeCanceled.ShouldBeTrue();
            token.IsCancellationRequested.ShouldBeFalse();
            actions.ClosePanel();
            token.IsCancellationRequested.ShouldBeTrue();
            actions.Rating.ShouldBe(FeedbackRating.Down);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Rating_Aborts_The_Previous_Rating_Send()
    {
        var tokens = new List<CancellationToken>();
        var actions = new FeedbackActions
        {
            PositiveReasons = [],
            FeedbackHandler = (_, cancellation) =>
            {
                tokens.Add(cancellation);
                return Task.Delay(Timeout.Infinite, cancellation);
            }
        };
        var (_, window) = Show(actions);
        try
        {
            Click(actions.UpThumb!);
            Click(actions.UpThumb!);
            tokens.Count.ShouldBe(2);
            tokens[0].IsCancellationRequested.ShouldBeTrue();
            tokens[1].IsCancellationRequested.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failed_Rating_Shows_Try_Again_In_The_Row()
    {
        var attempts = 0;
        var actions = new FeedbackActions
        {
            PositiveReasons = [],
            FeedbackHandler = (_, _) => ++attempts == 1 ? Task.FromException(new IOException("down")) : Task.CompletedTask
        };
        var (_, window) = Show(actions);
        try
        {
            Click(actions.UpThumb!);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            actions.IsRatingErrorShown.ShouldBeTrue();
            var row = TestHelpers.Descendant<Grid>(actions, FeedbackActions.RowErrorPart);
            row.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(actions, FeedbackActions.RowErrorTextPart).Text.ShouldBe("Couldn't save rating.");
            var retry = TestHelpers.Descendant<Avalonia.Controls.Button>(actions, FeedbackActions.RowRetryPart);
            retry.Content.ShouldBe("Try again");
            Click(retry);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            attempts.ShouldBe(2);
            actions.IsRatingErrorShown.ShouldBeFalse();
            row.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Press_Focuses_The_First_Reason_And_Escape_Returns_To_The_Thumb()
    {
        var (actions, window) = Show();
        try
        {
            actions.DownThumb!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            actions.Rating.ShouldBe(FeedbackRating.Down);
            actions.IsPanelOpen.ShouldBeTrue();
            actions.Form.Chips[0].IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            actions.IsPanelOpen.ShouldBeFalse();
            actions.Rating.ShouldBe(FeedbackRating.Down);
            actions.DownThumb.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Press_Leaves_Focus_Where_It_Is()
    {
        var (actions, window) = Show();
        try
        {
            Click(actions.DownThumb!);
            Dispatcher.UIThread.RunJobs();
            actions.IsPanelOpen.ShouldBeTrue();
            actions.Form.Chips[0].IsFocused.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Ctrl_Enter_In_The_Note_Submits_And_Focus_Lands_On_The_Thanks()
    {
        var calls = new List<FeedbackSubmission>();
        var actions = new FeedbackActions
        {
            ThanksDuration = TimeSpan.FromMilliseconds(40),
            FeedbackHandler = (submission, _) =>
            {
                calls.Add(submission);
                return Task.CompletedTask;
            }
        };
        var (_, window) = Show(actions);
        try
        {
            actions.DownThumb!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var note = actions.Form.NoteBox!;
            note.Text = "Missing a step";
            note.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();

            calls.Last().Stage.ShouldBe(FeedbackStage.Details);
            calls.Last().Comment.ShouldBe("Missing a step");
            note.Text.ShouldBe("Missing a step");
            actions.IsShowingThanks.ShouldBeTrue();
            actions.Thanks.IsFocused.ShouldBeTrue();

            TestHelpers.PumpUntil(() => !actions.IsPanelOpen).ShouldBeTrue();
            actions.DownThumb.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_And_Close_Keep_The_Rating()
    {
        var (actions, window) = Show();
        try
        {
            Click(actions.DownThumb!);
            Click(actions.Form.Secondary!);
            actions.IsPanelOpen.ShouldBeFalse();
            actions.Rating.ShouldBe(FeedbackRating.Down);

            Click(actions.UpThumb!);
            actions.IsPanelOpen.ShouldBeTrue();
            Click(actions.Form.CloseButton!);
            actions.IsPanelOpen.ShouldBeFalse();
            actions.Rating.ShouldBe(FeedbackRating.Up);
            AutomationProperties.GetName(actions.Form.CloseButton!).ShouldBe("Close");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Below_400px_The_Privacy_Note_And_Cancel_Give_Way()
    {
        var (actions, window) = Show(width: 360);
        try
        {
            Click(actions.DownThumb!);
            actions.Classes.Contains(FeedbackActions.NarrowPseudoClass).ShouldBeTrue();
            actions.Form.FootnoteBlock!.IsVisible.ShouldBeFalse();
            actions.Form.Secondary!.IsVisible.ShouldBeFalse();
            actions.Form.CloseButton!.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Rating_Turns_Off_The_Thumbs()
    {
        var (actions, window) = Show(new FeedbackActions { IsRatingEnabled = false });
        try
        {
            actions.UpThumb!.IsEnabled.ShouldBeFalse();
            actions.DownThumb!.IsEnabled.ShouldBeFalse();
            actions.IsRatingEnabled = true;
            actions.UpThumb.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Rating_Set_From_Outside_Checks_The_Thumb_Without_Opening_Or_Sending()
    {
        var sent = 0;
        var (actions, window) = Show(new FeedbackActions { Rating = FeedbackRating.Up });
        actions.FeedbackSubmitted += (_, _) => sent++;
        try
        {
            actions.UpThumb!.IsChecked.ShouldBe(true);
            actions.IsPanelOpen.ShouldBeFalse();
            Click(actions.DownThumb!);
            actions.IsPanelOpen.ShouldBeTrue();
            actions.Rating = FeedbackRating.None;
            actions.IsPanelOpen.ShouldBeFalse();
            actions.DownThumb!.IsChecked.ShouldBe(false);
            sent.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var labels = new ResponseFeedbackLabels
        {
            GoodResponse = "Bonne réponse",
            BadResponse = "Mauvaise réponse",
            NegativeTitle = "Qu'est-ce qui n'allait pas ?",
            Submit = "Envoyer",
        };
        var (actions, window) = Show(new FeedbackActions { Labels = labels });
        try
        {
            AutomationProperties.GetName(actions.UpThumb!).ShouldBe("Bonne réponse");
            AutomationProperties.GetName(actions.DownThumb!).ShouldBe("Mauvaise réponse");
            Click(actions.DownThumb!);
            actions.Form.Title.ShouldBe("Qu'est-ce qui n'allait pas ?");
            actions.Form.Primary!.Content.ShouldBe("Envoyer");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_At_Full_Height_And_Only_Fades()
    {
        var actions = new FeedbackActions { Width = 520 };
        var window = TestHelpers.Show(actions, Motion.ArcMotionLevel.Reduced);
        try
        {
            Click(actions.DownThumb!);
            Dispatcher.UIThread.RunJobs();
            var box = actions.PanelBox!;
            box.IsMounted.ShouldBeTrue();
            box.NaturalHeight.ShouldBeGreaterThan(0);
            box.DisplayedHeight.ShouldBe(box.NaturalHeight, 0.5);
            ((FeedbackThumb)actions.DownThumb!).PressScale.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Without_Data_Shows_The_Frame_Alone()
    {
        var card = new ResponseFeedback { Width = 560 };
        var window = TestHelpers.Show(card);
        window.Width = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            card.ModelName.ShouldBeNull();
            TestHelpers.Descendant<Border>(card, ResponseFeedback.PromptPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<ContentPresenter>(card, ResponseFeedback.BodyPart).Content.ShouldBeNull();
            TestHelpers.Descendant<CopyButton>(card, ResponseFeedback.CopyPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<ArcGlyph>(card, ResponseFeedback.MarkPart).Data.ShouldBe(ArcIcons.Sparkles);
            card.Actions.ShouldNotBeNull();
            card.Actions!.UpThumb!.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Shows_The_Reply_It_Is_Given()
    {
        var card = new ResponseFeedback
        {
            Width = 560,
            ModelName = "Model X",
            Prompt = "Why is my query slow?",
            Reply = new TextBlock { Text = "Run EXPLAIN ANALYZE, then add an index.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            CopyText = "Run EXPLAIN ANALYZE, then add an index.",
        };
        var window = TestHelpers.Show(card);
        window.Width = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            card.ModelName.ShouldBe("Model X");
            TestHelpers.Descendant<Border>(card, ResponseFeedback.PromptPart).IsVisible.ShouldBeTrue();
            var copy = TestHelpers.Descendant<CopyButton>(card, ResponseFeedback.CopyPart);
            copy.IsVisible.ShouldBeTrue();
            copy.Value!.ShouldContain("EXPLAIN ANALYZE");
            AutomationProperties.GetName(copy).ShouldBe("Copy reply");
            TestHelpers.Descendant<Avalonia.Controls.Button>(card, ResponseFeedback.RegeneratePart).IsVisible.ShouldBeFalse();
            card.Actions.ShouldNotBeNull();
            card.Classes.Contains(ResponseFeedback.CompactPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Shows_Its_Own_Reply_Prompt_And_Actions()
    {
        var regenerated = 0;
        var card = new ResponseFeedback
        {
            Reply = "Use a partial index.",
            Prompt = null,
            AllowsRegenerate = true,
            MessageId = "abc",
        };
        card.RegenerateRequested += (_, _) => regenerated++;
        var ratings = new List<FeedbackRating>();
        card.RatingChanged += (_, e) => ratings.Add(e.NewRating);
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.Descendant<Border>(card, ResponseFeedback.PromptPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<CopyButton>(card, ResponseFeedback.CopyPart).IsVisible.ShouldBeFalse();
            card.CopyText = "Use a partial index.";
            TestHelpers.Descendant<CopyButton>(card, ResponseFeedback.CopyPart).IsVisible.ShouldBeTrue();

            var regenerate = TestHelpers.Descendant<Avalonia.Controls.Button>(card, ResponseFeedback.RegeneratePart);
            regenerate.IsVisible.ShouldBeTrue();
            Click(regenerate);
            regenerated.ShouldBe(1);

            card.Actions!.MessageId.ShouldBe("abc");
            Click(card.Actions.UpThumb!);
            card.Rating.ShouldBe(FeedbackRating.Up);
            ratings.ShouldBe([FeedbackRating.Up]);
            card.Rating = FeedbackRating.Down;
            card.Actions.DownThumb!.IsChecked.ShouldBe(true);

            // The test window is 400px wide: the card pads 16px.
            card.Classes.Contains(ResponseFeedback.CompactPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Stops_The_Thanks_Timer()
    {
        var actions = new FeedbackActions { ThanksDuration = TimeSpan.FromSeconds(30), FeedbackHandler = (_, _) => Task.CompletedTask };
        var (_, window) = Show(actions);
        Click(actions.DownThumb!);
        actions.Form.Chips[0].IsChecked = true;
        Click(actions.Form.Primary!);
        Dispatcher.UIThread.RunJobs();
        TestHelpers.PumpUntil(() => actions.IsShowingThanks).ShouldBeTrue();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        actions.IsPanelOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Note_Is_Two_Lines_Tall()
    {
        var (actions, window) = Show();
        try
        {
            Click(actions.DownThumb!);
            var note = actions.Form.NoteBox!;
            note.FrameMinHeight.ShouldBe(66);
            note.GetVisualDescendants().OfType<FieldFrame>().First().Bounds.Height.ShouldBe(66, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
