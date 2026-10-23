using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CustomerGalaxyYourDataDemo : UserControl
{
    private static readonly string[] Clouds = ["Fjord Cloud", "Basalt Hosting", "Own servers"];
    private static readonly string[] Teams = ["Atlas", "Beacon", "Cinder", "Delta", "Ember", "Flint", "Grove", "Harbor"];

    public CustomerGalaxyYourDataDemo()
    {
        InitializeComponent();
        // Three states of your own instead of the four defaults.
        Galaxy.HealthStates.Clear();
        Galaxy.HealthStates.Add(new GalaxyHealthState { Key = "expanding", Label = "Expanding", Tone = GalaxyTone.Success, Layer = 1, IsGrowth = true });
        Galaxy.HealthStates.Add(new GalaxyHealthState { Key = "stable", Label = "Stable", Tone = GalaxyTone.Neutral, DotOpacity = 0.72, BarOpacity = 0.5 });
        Galaxy.HealthStates.Add(new GalaxyHealthState { Key = "churning", Label = "Churning", Tone = GalaxyTone.Danger, Layer = 2, IsRisk = true });
        Galaxy.ItemsSource = CreateWorkspaces();
        Galaxy.AccountSelected += (_, e) => Log.Text = e.Account is { } account
            ? $"{account.Name}: {account.Mrr:N0} € a month, {account.Owner}"
            : "Click a dot to select a workspace.";
    }

    /// <summary>420 invented workspaces from a fixed seed.</summary>
    private static List<GalaxyAccount> CreateWorkspaces()
    {
        var random = new Random(42);
        var list = new List<GalaxyAccount>();
        for (var i = 0; i < 420; i++)
        {
            var roll = random.NextDouble();
            var (tier, mrr) = roll < 0.6 ? ("hobby", 9 + random.Next(0, 40))
                : roll < 0.92 ? ("pro", 120 + random.Next(0, 900))
                : ("scale", 2_000 + random.Next(0, 14_000));
            var health = random.NextDouble() switch { < 0.12 => "expanding", < 0.9 => "stable", _ => "churning" };
            var team = Teams[random.Next(Teams.Length)];
            var account = new GalaxyAccount
            {
                Id = $"ws-{i:D3}",
                Name = $"{team} workspace {i + 1}",
                Mrr = mrr,
                Health = health,
                Owner = tier == "scale" ? "Account team" : "Self-serve",
                Note = health == "churning" ? "Seats removed last week" : null
            };
            account.Groups["tier"] = tier;
            account.Groups["cloud"] = Clouds[random.Next(Clouds.Length)];
            list.Add(account);
        }
        return list;
    }
}
