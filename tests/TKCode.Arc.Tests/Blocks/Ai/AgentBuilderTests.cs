using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AgentBuilder"/> on its invented sample (Harbor) or on given data: the draft, save, discard and publish states,
/// tools, knowledge sources and their indexing, validation, the layout by width, and the test chat's snapshot and restart.
/// Every simulated wait completes at once.
/// </summary>
public class AgentBuilderTests
{
    private static (AgentBuilder Builder, Window Window) Show(Action<AgentBuilder>? configure = null, double width = 1200)
    {
        var builder = new AgentBuilder { Delay = static (_, _) => Task.CompletedTask };
        configure?.Invoke(builder);
        var window = TestHelpers.Show(builder);
        window.Width = width;
        window.Height = 1400;
        Settle(window);
        return (builder, window);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(AvaloniaButton? button)
    {
        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void An_Empty_Builder_Shows_The_Sample_Agent_Saved_With_Unpublished_Changes()
    {
        var (builder, _) = Show();
        Assert.Equal(AgentBuilderSamples.Name, builder.AgentName);
        Assert.Equal(3, builder.Models.Count);
        Assert.Equal(5, builder.Tools.Count);
        Assert.Equal(3, builder.Sources.Count);
        Assert.Equal(AgentBuilderSamples.ModelId, builder.SelectedModelId);
        Assert.False(builder.IsDirty);
        Assert.Equal(AgentBuilderPublishState.Changed, builder.PublishState);
        Assert.Equal("Harbor", builder.TitleText!.Text);
        Assert.Equal("H", builder.InitialText!.Text);
        Assert.Equal("Unpublished changes", builder.StatusBadge!.Content);
        Assert.Equal("2 of 5 on", builder.ToolsCountText!.Text);
        Assert.Equal("3 sources", builder.KnowledgeCountText!.Text);
        Assert.Equal("All changes saved", builder.SaveStatusText);
        Assert.False(builder.SaveButton!.IsEnabled);
        Assert.False(builder.DiscardButton!.IsEnabled);
        Assert.True(builder.PublishButton!.IsEnabled);
        Assert.Equal(5, builder.ToolRows.Count);
        Assert.Equal(3, builder.SourceRows.Count);
        Assert.Equal("Order lookup", AutomationProperties.GetName(builder.ToolRows[0].Toggle!));
        Assert.Contains(":failed", builder.SourceRows[2].Classes);
        Assert.Equal("File · 58 KB · The file is password protected.", builder.SourceRows[2].DetailText);
        Assert.Equal("File · 412 KB · Ready · 86 passages", builder.SourceRows[0].DetailText);
    }

    [AvaloniaFact]
    public void Given_Data_Keeps_The_Sample_Out_And_ShowsSampleData_False_Starts_Empty()
    {
        var (given, _) = Show(b => b.AgentName = "Scout");
        Assert.Equal("Scout", given.AgentName);
        Assert.Empty(given.Tools);
        Assert.Equal(AgentBuilderPublishState.Draft, given.PublishState);
        Assert.Equal("Draft", given.StatusBadge!.Content);

        var (empty, _) = Show(b => b.ShowsSampleData = false);
        Assert.Equal(string.Empty, empty.AgentName);
        Assert.Equal("Untitled agent", empty.TitleText!.Text);
        Assert.Equal("?", empty.InitialText!.Text);
        Assert.True(empty.NoSourcesText!.IsVisible);
        Assert.Equal("A name is required to save.", empty.NameField!.ErrorMessage);
        Assert.False(empty.PublishButton!.IsEnabled);
    }

    [AvaloniaFact]
    public void Typing_Makes_A_Draft_That_Raises_ConfigChanged_And_Enables_Save()
    {
        var (builder, _) = Show();
        var changes = new List<AgentBuilderConfigChangedEventArgs>();
        builder.ConfigChanged += (_, e) => changes.Add(e);
        builder.NameField!.Text = "Harbor Plus";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Harbor Plus", builder.AgentName);
        Assert.True(builder.IsDirty);
        Assert.Equal(AgentBuilderField.Name, changes[^1].Field);
        Assert.True(changes[^1].IsDirty);
        Assert.Equal("Harbor Plus", builder.Config.Name);
        Assert.Equal("Unsaved changes", builder.SaveStatusText);
        Assert.True(builder.SaveButton!.IsEnabled);
        Assert.True(builder.DiscardButton!.IsEnabled);
        Assert.Contains(":dirty", builder.Classes);

        // Code edits reach the field; returning to the saved text is no longer dirty.
        builder.AgentName = "Harbor";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Harbor", builder.NameField.Text);
        Assert.False(builder.IsDirty);
    }

    [AvaloniaFact]
    public void Instructions_Count_Characters_And_Tokens_And_Refuse_Saving_Past_The_Limit()
    {
        var (builder, _) = Show();
        builder.Instructions = new string('a', 41);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("41 characters · about 11 tokens", builder.InstructionsCountText!.Text);
        Assert.Null(builder.InstructionsField!.ErrorMessage);
        Assert.True(builder.SaveButton!.IsEnabled);

        builder.InstructionsMaxLength = 40;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Keep the instructions under 40 characters.", builder.InstructionsField.ErrorMessage);
        Assert.False(builder.SaveButton.IsEnabled);
    }

    [AvaloniaFact]
    public async Task Save_Stores_The_Draft_And_A_Failed_Save_Keeps_It()
    {
        var (builder, _) = Show();
        var saved = new List<AgentBuilderConfig>();
        builder.Saved += (_, e) => saved.Add(e.Config);
        var fail = true;
        builder.SaveHandler = (_, _) => fail ? Task.FromException(new InvalidOperationException("Server busy")) : Task.CompletedTask;
        builder.Instructions = "Be brief.";

        Assert.False(await builder.SaveAsync());
        Assert.True(builder.IsDirty);
        Assert.Equal("Server busy", builder.SaveError);
        Assert.Equal("Not saved: Server busy", builder.SaveStatusText);
        Assert.Empty(saved);

        fail = false;
        Assert.True(await builder.SaveAsync());
        Assert.False(builder.IsDirty);
        Assert.Null(builder.SaveError);
        Assert.Equal("Be brief.", builder.SavedConfig!.Instructions);
        Assert.Single(saved);
        Assert.Equal("Changes saved", builder.LastAnnouncement);
        Assert.Equal("All changes saved", builder.SaveStatusText);
    }

    [AvaloniaFact]
    public void Ctrl_S_Saves()
    {
        var (builder, window) = Show();
        builder.AgentDescription = "Shorter";
        builder.NameField!.Focus();
        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(TestHelpers.PumpUntil(() => !builder.IsDirty));
        Assert.Equal("Shorter", builder.SavedConfig!.Description);
    }

    [AvaloniaFact]
    public void Discard_Brings_Back_Fields_Tools_And_Sources()
    {
        var (builder, _) = Show();
        var removed = builder.Sources[1];
        builder.AgentName = "Other";
        builder.Tools[2].IsOn = true;
        builder.RemoveSource(removed);
        Dispatcher.UIThread.RunJobs();
        Assert.True(builder.IsDirty);
        Assert.Equal(2, builder.Sources.Count);

        Click(builder.DiscardButton);
        Assert.False(builder.IsDirty);
        Assert.Equal("Harbor", builder.AgentName);
        Assert.Equal("Harbor", builder.NameField!.Text);
        Assert.False(builder.Tools[2].IsOn);
        Assert.Same(removed, builder.Sources[1]);
        Assert.Equal(3, builder.SourceRows.Count);
        Assert.Equal("Changes discarded", builder.LastAnnouncement);
    }

    [AvaloniaFact]
    public async Task Publish_Saves_First_And_Marks_The_Agent_Published()
    {
        var (builder, _) = Show();
        var published = new List<AgentBuilderConfig>();
        builder.Published += (_, e) => published.Add(e.Config);
        builder.Tools[4].IsOn = true;

        Assert.True(await builder.PublishAsync());
        Assert.False(builder.IsDirty);
        Assert.Equal(AgentBuilderPublishState.Published, builder.PublishState);
        Assert.True(builder.PublishedConfig!.HasTool("code"));
        Assert.Single(published);
        Assert.Equal("Published", builder.StatusBadge!.Content);
        Assert.Equal(BadgeTone.Success, builder.StatusBadge.Tone);
        Assert.Equal("Up to date", builder.PublishButton!.Content);
        Assert.False(builder.PublishButton.IsEnabled);
        Assert.Equal("Harbor published", builder.LastAnnouncement);

        // An edit after publishing is a change again.
        builder.Tools[4].IsOn = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(AgentBuilderPublishState.Changed, builder.PublishState);
    }

    [AvaloniaFact]
    public void A_Press_On_A_Tool_Row_Flips_The_Switch_And_Is_Announced()
    {
        var (builder, _) = Show();
        var row = builder.ToolRows[2];
        Assert.False(row.Tool.IsOn);
        row.Toggle!.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(builder.Tools[2].IsOn);
        Assert.Contains(":on", row.Classes);
        Assert.Equal("Web search on", builder.LastAnnouncement);
        Assert.Equal("3 of 5 on", builder.ToolsCountText!.Text);
        Assert.Equal(["orders", "handoff", "web"], builder.Config.ToolIds);

        builder.Tools[2].IsOn = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(row.Toggle.IsChecked);
        Assert.Equal("Web search off", builder.LastAnnouncement);
    }

    [AvaloniaFact]
    public void Links_Are_Checked_Then_Indexed()
    {
        var (builder, _) = Show();
        var indexed = new List<AgentBuilderSource>();
        builder.SourceIndexed += (_, e) => indexed.Add(e.Item);

        Assert.False(builder.AddLink("not a link"));
        Assert.Equal("Enter a full link that starts with https://", builder.LinkField!.ErrorMessage);
        Assert.False(builder.AddLink("ftp://files.example/x"));
        Assert.False(builder.AddLink("https://help.tidewater.example/fitting"));
        Assert.Equal("That page is already a source.", builder.LinkField.ErrorMessage);

        builder.LinkField.Text = "https://docs.tidewater.example/warranty/";
        Click(builder.AddLinkButton);
        Assert.Null(builder.LinkField.ErrorMessage);
        Assert.Equal(string.Empty, builder.LinkField.Text);
        var source = builder.Sources[^1];
        Assert.Equal("docs.tidewater.example/warranty", source.Name);
        Assert.Equal(AgentBuilderSourceKind.Website, source.Kind);
        Assert.True(TestHelpers.PumpUntil(() => source.Status == AgentBuilderSourceStatus.Ready));
        Assert.Equal(1, source.Progress);
        Assert.NotNull(source.ChunkCount);
        Assert.Contains(source, indexed);
        Assert.True(builder.IsDirty);
        Assert.Equal("4 sources", builder.KnowledgeCountText!.Text);
    }

    [AvaloniaFact]
    public void An_Index_Handler_Reports_Progress_And_Failures_And_Retry_Runs_Again()
    {
        var gate = new TaskCompletionSource<int?>();
        var calls = 0;
        var (builder, _) = Show(b => { });
        builder.IndexHandler = async (_, progress, cancellation) =>
        {
            calls++;
            progress.Report(0.5);
            if (calls == 1)
            {
                await gate.Task.WaitAsync(cancellation);
                throw new InvalidOperationException("Sign-in required");
            }
            return 7;
        };
        Assert.True(builder.AddLink("https://intranet.example/handbook"));
        var source = builder.Sources[^1];
        Assert.True(TestHelpers.PumpUntil(() => Math.Abs(source.Progress - 0.5) < 1e-9));
        Assert.Contains(":indexing", builder.SourceRows[^1].Classes);
        Assert.Equal("Website · Indexing 50%", builder.SourceRows[^1].DetailText);

        gate.SetResult(null);
        Assert.True(TestHelpers.PumpUntil(() => source.Status == AgentBuilderSourceStatus.Failed));
        Assert.Equal("Sign-in required", source.ErrorMessage);
        Assert.True(builder.SourceRows[^1].RetryButton!.IsVisible);
        Assert.Equal("intranet.example/handbook could not be indexed: Sign-in required", builder.LastAnnouncement);

        Click(builder.SourceRows[^1].RetryButton);
        Assert.True(TestHelpers.PumpUntil(() => source.Status == AgentBuilderSourceStatus.Ready));
        Assert.Equal(7, source.ChunkCount);
        Assert.Equal(2, calls);
    }

    [AvaloniaFact]
    public void Remove_Takes_The_Source_Out_And_Names_It()
    {
        var (builder, _) = Show();
        var row = builder.SourceRows[0];
        Assert.Equal("Remove returns-and-repairs.pdf", AutomationProperties.GetName(row.RemoveButton!));
        Click(row.RemoveButton);
        Assert.Equal(2, builder.Sources.Count);
        Assert.Equal("Removed returns-and-repairs.pdf", builder.LastAnnouncement);
        Assert.True(builder.IsDirty);
    }

    [AvaloniaFact]
    public void The_Chat_Sits_Beside_The_Settings_And_Moves_Under_Them_When_Narrow()
    {
        var (wide, _) = Show(width: 1200);
        Assert.False(wide.IsStacked);
        Assert.DoesNotContain(":stacked", wide.Classes);

        var (narrow, _) = Show(width: 600);
        Assert.True(narrow.IsStacked);
        Assert.Contains(":stacked", narrow.Classes);
        Assert.DoesNotContain(":compact", narrow.Classes);

        var (compact, _) = Show(width: 420);
        Assert.Contains(":compact", compact.Classes);
    }

    [AvaloniaFact]
    public void The_Model_Pill_And_The_Chat_Follow_The_Selected_Model()
    {
        var (builder, _) = Show();
        Assert.Equal("lumen-pro-2", builder.ModelField!.SelectedModelId);
        Assert.Equal("lumen-pro-2", builder.Chat!.SelectedModel!.Id);
        builder.ModelField.SelectedModelId = "quill-deep-1";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("quill-deep-1", builder.SelectedModelId);
        Assert.Equal("quill-deep-1", builder.Chat.SelectedModel!.Id);
        Assert.True(builder.IsDirty);
    }

    [AvaloniaFact]
    public void Load_Shows_A_Config_As_Saved()
    {
        var (builder, _) = Show();
        var config = new AgentBuilderConfig("Atlas", "Plans trips", "Plan carefully.", "lumen-lite-2", ["web"], [builder.Sources[0]]);
        builder.Load(config);
        Dispatcher.UIThread.RunJobs();
        Assert.False(builder.IsDirty);
        Assert.Equal("Atlas", builder.TitleText!.Text);
        Assert.Equal(["web"], builder.Config.ToolIds);
        Assert.Single(builder.Sources);
        Assert.Equal("lumen-lite-2", builder.ModelField!.SelectedModelId);
    }

    [AvaloniaFact]
    public void The_Test_Chat_Answers_With_The_Settings_It_Started_With_Until_Restarted()
    {
        var responder = new RecordingResponder();
        var (builder, _) = Show(b => b.Responder = responder);
        builder.Chat!.Send("Where is order 4417?");
        Assert.True(TestHelpers.PumpUntil(() => responder.Requests.Count == 1));
        var first = responder.Requests[0];
        Assert.Equal("Harbor", first.Config.Name);
        Assert.Equal("lumen-pro-2", first.Model!.Id);
        Assert.Equal(["orders", "handoff"], first.Tools.Select(t => t.Id));
        Assert.Equal("Where is order 4417?", first.Chat.Messages[^1].Content);
        Assert.NotNull(builder.TestConfig);
        Assert.False(builder.IsTestStale);

        builder.Instructions = "Answer in French.";
        Dispatcher.UIThread.RunJobs();
        Assert.True(builder.IsTestStale);
        Assert.True(builder.StaleHost!.IsPresent);
        Assert.Equal("Settings changed. Restart the test chat to try them.", builder.LastAnnouncement);

        Assert.True(TestHelpers.PumpUntil(() => !builder.Chat.IsBusy));
        builder.Chat.Send("And order 5000?");
        Assert.True(TestHelpers.PumpUntil(() => responder.Requests.Count == 2));
        Assert.Equal(AgentBuilderSamples.Instructions, responder.Requests[1].Config.Instructions);

        Assert.True(TestHelpers.PumpUntil(() => !builder.Chat.IsBusy));
        Click(builder.RestartButton);
        Assert.Null(builder.TestConfig);
        Assert.False(builder.IsTestStale);
        Assert.False(builder.StaleHost.IsPresent);
        Assert.Null(builder.Chat.CurrentConversation);
        Assert.Equal("Test chat restarted with the current settings", builder.LastAnnouncement);

        builder.Chat.Send("Bonjour");
        Assert.True(TestHelpers.PumpUntil(() => responder.Requests.Count == 3));
        Assert.Equal("Answer in French.", responder.Requests[2].Config.Instructions);
    }

    [AvaloniaFact]
    public async Task The_Simulated_Responder_Searches_Ready_Sources_And_Runs_A_Matching_Tool()
    {
        var tools = AgentBuilderSamples.Tools().Where(t => t.IsOn).ToList();
        Assert.Equal("orders", AgentBuilderSimulatedResponder.PickTool("Where is my ORDER?", tools)!.Id);
        Assert.Null(AgentBuilderSimulatedResponder.PickTool("Hello there", tools));

        var sources = AgentBuilderSamples.Sources();
        var config = new AgentBuilderConfig("Harbor", "", "Be kind.", "x", ["orders"], sources);
        var conversation = new AiChatConversation();
        var request = new AgentBuilderTestRequest(config, null, tools.Take(1).ToList(),
            new AiChatRequest(conversation, null, [new AiChatRequestMessage(AiChatRole.User, "Track order 12", [])]));
        var events = new List<AiChatStreamEvent>();
        await foreach (var item in new AgentBuilderSimulatedResponder(static (_, _) => Task.CompletedTask).RespondAsync(request, CancellationToken.None))
        {
            events.Add(item);
        }
        var search = events.OfType<AiChatToolUpdate>().Last(u => u.Id == "knowledge");
        Assert.Equal(AiChatToolStatus.Done, search.Status);
        Assert.Equal("2 sources", search.Summary);
        Assert.Contains(events.OfType<AiChatToolUpdate>(), u => u.Id == "tool-orders" && u.Status == AiChatToolStatus.Done);
        var text = string.Concat(events.OfType<AiChatTextDelta>().Select(d => d.Text));
        Assert.Contains("**Harbor**", text);
        Assert.Contains("Order lookup", text);
        Assert.Contains("2 ready sources [1]", text);
    }

    [Fact]
    public void Configs_Compare_By_Value_Including_Lists()
    {
        var source = new AgentBuilderSource { Id = "a" };
        var one = new AgentBuilderConfig("N", "D", "I", "m", ["x", "y"], [source]);
        var two = new AgentBuilderConfig("N", "D", "I", "m", new List<string> { "x", "y" }, new List<AgentBuilderSource> { source });
        Assert.Equal(one, two);
        Assert.NotEqual(one, two with { ToolIds = ["y", "x"] });
        Assert.NotEqual(one, two with { Sources = [] });
        Assert.Equal(11, AgentBuilder.EstimateTokens(new string('a', 41)));
        Assert.InRange(AgentBuilder.ToneOf("Harbor"), 0, 3);
        Assert.Equal(AgentBuilder.ToneOf("harbor"), AgentBuilder.ToneOf(" HARBOR "));
    }

    private sealed class RecordingResponder : IAgentBuilderResponder
    {
        public List<AgentBuilderTestRequest> Requests { get; } = new();

        public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AgentBuilderTestRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await Task.Yield();
            yield return new AiChatTextDelta("Done.");
        }
    }
}
