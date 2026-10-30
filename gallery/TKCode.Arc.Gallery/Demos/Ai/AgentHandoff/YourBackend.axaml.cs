using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The card driven by the app: stages, messages, summary and person set from code, events answered.</summary>
public partial class AgentHandoffYourBackendDemo : UserControl
{
    private readonly bool _ready;

    public AgentHandoffYourBackendDemo()
    {
        InitializeComponent();
        Chat.Summary = new AgentHandoffSummary(
            "Desk arrived with a cracked leg. The customer wants a full refund.",
            new AgentHandoffField("Order", "NW-48213"),
            new AgentHandoffField("Asks for", "Full refund"));
        Chat.Agent = new AgentHandoffAgent("Jordan Reyes", "Support specialist", "14:05");
        _ready = true;
        Show(AgentHandoffStage.Assistant);
    }

    private void OnStageChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready && Enum.TryParse<AgentHandoffStage>(StagePicker.SelectedValue as string, out var stage))
        {
            Show(stage);
        }
    }

    /// <summary>Rebuilds the conversation as it would be at <paramref name="stage"/>.</summary>
    private void Show(AgentHandoffStage stage)
    {
        Chat.Typing = AgentHandoffTyping.None;
        Chat.Messages.Clear();
        Chat.AgentMessages.Clear();
        Chat.Messages.Add(new AgentHandoffMessage(AgentHandoffSender.Customer, "My desk arrived with a cracked leg.", "14:02"));
        Chat.Messages.Add(new AgentHandoffMessage(AgentHandoffSender.Assistant,
            "Sorry about that. I can ship a replacement leg, which usually arrives in two days.", "14:02"));
        if (stage != AgentHandoffStage.Assistant)
        {
            Chat.Messages.Add(new AgentHandoffMessage(AgentHandoffSender.Customer, "Talk to a person", "14:03"));
            Chat.AgentMessages.Add(new AgentHandoffMessage(AgentHandoffSender.Customer, "I'd like a full refund, please.", "14:03"));
        }
        if (stage is AgentHandoffStage.Connected or AgentHandoffStage.Resolved)
        {
            Chat.AgentMessages.Add(new AgentHandoffMessage(AgentHandoffSender.Agent,
                "Hi, I'm Jordan. I've read the summary and issued a full refund of $649.", "14:05"));
        }
        if (stage == AgentHandoffStage.Connected)
        {
            Chat.Typing = AgentHandoffTyping.Agent;
        }
        Chat.Stage = stage;
        Chat.Notice = stage == AgentHandoffStage.Resolved ? "Refund issued." : null;
    }

    private void Pick(AgentHandoffStage stage) => StagePicker.SelectedValue = stage.ToString();

    private void OnHumanRequested(object? sender, RoutedEventArgs e) => Pick(AgentHandoffStage.Transferring);

    private void OnCancel(object? sender, RoutedEventArgs e) => Pick(AgentHandoffStage.Assistant);

    private void OnRetry(object? sender, RoutedEventArgs e) => Pick(AgentHandoffStage.Connected);

    private void OnSend(object? sender, AgentHandoffSendEventArgs e)
    {
        var message = new AgentHandoffMessage(AgentHandoffSender.Customer, e.Text);
        if (Chat.Stage == AgentHandoffStage.Assistant)
        {
            Chat.Messages.Add(message);
        }
        else
        {
            Chat.AgentMessages.Add(message);
        }
    }
}
