using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: a simulated program, a fresh program and one past its milestone.</summary>
public partial class ReferralCardPreviewDemo : UserControl
{
    private readonly bool _ready;

    public ReferralCardPreviewDemo()
    {
        InitializeComponent();
        Show("sample");
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor body runs.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "sample");
        }
    }

    private void Show(string scenario)
    {
        // A new card each time, so the program plays again from the start. The demo gives the data: the link, the reward,
        // the milestone and either a program to play (a simulation) or the referrals themselves.
        var card = new ReferralCard
        {
            Link = ReferralCardDemoData.Link,
            Reward = ReferralCardDemoData.Reward,
            Milestone = ReferralCardDemoData.Milestone
        };
        switch (scenario)
        {
            case "fresh":
                card.Referrals = [];
                card.Simulate = true;
                card.Link = "https://app.example.com/r/sam-k";
                break;
            case "unlocked":
                card.Referrals = ReferralCardDemoData.Unlocked(DateTimeOffset.Now);
                break;
            default:
                card.Simulation = new ReferralCardSimulation
                {
                    Referrals = ReferralCardDemoData.Program(DateTimeOffset.Now),
                    Beats = ReferralCardDemoData.Beats
                };
                break;
        }
        Host.Content = card;
    }
}
