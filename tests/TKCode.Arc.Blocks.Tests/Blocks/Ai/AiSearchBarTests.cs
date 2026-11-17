using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiSearchBar"/>: suggestions, the sample answers with citations, sources and results, failure, cancel, clear and the keyboard.</summary>
public class AiSearchBarTests
{
    private static (AiSearchBar Bar, Window Window) Show(Action<AiSearchBar>? configure = null)
    {
        var bar = new AiSearchBar { Delay = static (_, _) => Task.CompletedTask };
        configure?.Invoke(bar);
        var window = TestHelpers.Show(bar);
        Dispatcher.UIThread.RunJobs();
        return (bar, window);
    }

    [AvaloniaFact]
    public void Idle_shows_the_suggested_questions()
    {
        var (bar, window) = Show();
        try
        {
            bar.State.ShouldBe(AiSearchBarState.Idle);
            bar.SuggestionsPanel.ShouldNotBeNull().Children.Count.ShouldBe(3);
            AutomationProperties.GetName(bar.SuggestionsPanel!.Children[0]).ShouldBe(AiSearchBarSamples.Questions()[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_question_shows_the_answer_with_citations_sources_and_results()
    {
        var (bar, window) = Show();
        string? asked = null;
        bar.QuerySubmitted += (_, e) => asked = e.Query;
        try
        {
            await bar.SearchAsync("How many vacation days do new hires get?");
            asked.ShouldBe("How many vacation days do new hires get?");
            bar.State.ShouldBe(AiSearchBarState.Answered);
            bar.Classes.Contains(":answered").ShouldBeTrue();
            bar.SourcesPanel!.Children.Count.ShouldBe(2);
            bar.ResultsPanel!.Children.Count.ShouldBeGreaterThanOrEqualTo(3);
            bar.FollowUpsPanel!.Children.Count.ShouldBe(2);
            var citations = bar.AnswerTextBlock!.Inlines!.OfType<InlineUIContainer>().ToArray();
            citations.Length.ShouldBe(2);
            AutomationProperties.GetName(citations[1].Child).ShouldBe("Source 2: Public holidays by office");
            bar.Announcement.ShouldStartWith("Answer ready");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Sources_and_results_raise_their_events()
    {
        var (bar, window) = Show();
        AiSearchSourceEventArgs? source = null;
        AiSearchResultEventArgs? result = null;
        bar.SourceInvoked += (_, e) => source = e;
        bar.ResultInvoked += (_, e) => result = e;
        try
        {
            await bar.SearchAsync("What can I expense on a work trip?");
            ((Avalonia.Controls.Button)bar.SourcesPanel!.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            source.ShouldNotBeNull().Number.ShouldBe(2);
            source.SearchSource.Title.ShouldBe("Daily meal rates");
            ((Avalonia.Controls.Button)bar.ResultsPanel!.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            result.ShouldNotBeNull().Result.Title.ShouldBe("Travel and expenses");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_failing_provider_shows_the_error_and_retry_runs_again()
    {
        var calls = 0;
        var (bar, window) = Show(b => b.AnswerProvider = (query, _) =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<AiSearchAnswer>(new InvalidOperationException("Index offline"))
                : Task.FromResult(new AiSearchAnswer { Text = "Fine [1]", Sources = [new AiSearchSource { Title = "Doc" }] });
        });
        try
        {
            await bar.SearchAsync("anything");
            bar.State.ShouldBe(AiSearchBarState.Failed);
            bar.ErrorText.ShouldBe("Index offline");
            TestHelpers.Descendant<Button>(bar, "PART_Retry").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TestHelpers.PumpUntil(() => bar.State == AiSearchBarState.Answered).ShouldBeTrue();
            calls.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_asks_and_Escape_cancels_then_clears()
    {
        var pending = new TaskCompletionSource<AiSearchAnswer>();
        var (bar, window) = Show(b => b.AnswerProvider = (_, token) =>
        {
            token.Register(() => pending.TrySetCanceled());
            return pending.Task;
        });
        try
        {
            bar.Query = "Who approves a new laptop?";
            bar.Field!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            bar.State.ShouldBe(AiSearchBarState.Searching);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            bar.State.ShouldBe(AiSearchBarState.Idle);
            bar.Query.ShouldBe("Who approves a new laptop?");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            bar.Query.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Citations_split_text_and_numbers()
    {
        var pieces = AiSearchBarCitations.Split("A [1] b [2].[x]");
        pieces.Select(p => p.Text ?? $"#{p.Number}").ShouldBe(["A ", "#1", " b ", "#2", ".[x]"]);
    }
}
