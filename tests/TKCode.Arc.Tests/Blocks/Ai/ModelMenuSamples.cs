using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// An invented model catalog for the model menu's tests and snapshots: made-up providers with geometric marks (24 grid),
/// made-up models, blurbs and tags. Nothing here names a real vendor or model.
/// </summary>
internal static class ModelMenuSamples
{
    public static readonly ModelEffort[] ThreeLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];

    public static readonly ModelEffort[] FiveLevels =
        [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];

    public static AiModelProvider[] Providers() =>
    [
        Provider("aurora", "Aurora Labs", "#D9822B",
            "M4 4A16 16 0 0 1 20 20L17 20A13 13 0 0 0 4 7Z M4 9A11 11 0 0 1 15 20L12 20A8 8 0 0 0 4 12Z M4 14A6 6 0 0 1 10 20L4 20Z"),
        Provider("cobalt", "Cobalt Research", "#3E63DD",
            "M12 2L22 12L12 22L2 12Z M12 5.2L5.2 12L12 18.8L18.8 12Z M9.5 12A2.5 2.5 0 1 0 14.5 12A2.5 2.5 0 1 0 9.5 12Z"),
        Provider("halcyon", "Halcyon", "#8E4EC6",
            "M3 12A6 6 0 1 1 15 12A6 6 0 1 1 3 12Z M9 12A6 6 0 1 1 21 12A6 6 0 1 1 9 12Z"),
        Provider("basalt", "Basalt AI", null, "M12 2L21 7L21 17L12 22L3 17L3 7Z M12 5L6 8.5L6 15.5L12 19L18 15.5L18 8.5Z"),
        Provider("driftwood", "Driftwood", "#12A594",
            "M2 9Q7 5 12 9T22 9L22 12Q17 8 12 12T2 12Z M2 15Q7 11 12 15T22 15L22 18Q17 14 12 18T2 18Z"),
        Provider("ember", "Ember Systems", "#E5484D", "M12 3.5L21 19.5L3 19.5Z"),
        Provider("fjord", "Fjord", null, "M4 10H8V20H4Z M10 4H14V20H10Z M16 13H20V20H16Z"),
        Provider("granite", "Granite Open", null, "M12 2A10 10 0 1 1 11.99 2Z M4.5 16.5L16.5 4.5L19.5 7.5L7.5 19.5Z")
    ];

    /// <summary>Four featured models first (the default featured set), then the rest of the catalog.</summary>
    public static AiModel[] Models() =>
    [
        Model("aurora-prime-3", "Aurora Prime 3", "aurora", "Deep work: refactors, research, long tasks", null, FiveLevels, ModelEffort.Medium),
        Model("aurora-swift-3", "Aurora Swift 3", "aurora", "Quick and capable for daily chats", null, FiveLevels, ModelEffort.High),
        Model("cobalt-atlas-2", "Cobalt Atlas 2", "cobalt", "Reads charts, screenshots and PDFs", null, ThreeLevels, ModelEffort.Medium),
        Model("halcyon-wide-15", "Halcyon Wide 1.5", "halcyon", "Huge context for whole codebases", null, ThreeLevels, ModelEffort.Medium),
        Model("aurora-summit-29", "Aurora Summit 2.9", "aurora", null, "Strongest", FiveLevels, ModelEffort.High),
        Model("aurora-pocket-24", "Aurora Pocket 2.4", "aurora", null, "Fast", FiveLevels, ModelEffort.Medium),
        Model("cobalt-atlas-lite", "Cobalt Atlas Lite", "cobalt", null, "Fast", null, null),
        Model("halcyon-flash-12", "Halcyon Flash 1.2", "halcyon", null, "Fast", ThreeLevels, ModelEffort.Low),
        Model("basalt-4", "Basalt 4", "basalt", null, null, ThreeLevels, ModelEffort.Medium),
        Model("driftwood-coder-7", "Driftwood Coder 7", "driftwood", null, "Code", ThreeLevels, ModelEffort.Medium),
        Model("ember-large-3", "Ember Large 3", "ember", null, null, null, null),
        Model("fjord-reason-2", "Fjord Reason 2", "fjord", null, "New", ThreeLevels, ModelEffort.High),
        Model("granite-70b", "Granite 70B", "granite", null, "Open weights", null, null)
    ];

    /// <summary>
    /// The composer's five models (invented): short names for the toolbar chip, one line each for the menu; two offer five
    /// effort levels, two offer three, one has no effort setting.
    /// </summary>
    public static AiModel[] ComposerModels()
    {
        AiModel[] models =
        [
            Model("aurora-prime-3", "Aurora Prime 3", "aurora", "Deep work: refactors, research, long tasks", null, FiveLevels, ModelEffort.Medium),
            Model("aurora-swift-3", "Aurora Swift 3", "aurora", "Quick and capable for daily chats", null, FiveLevels, ModelEffort.High),
            Model("cobalt-atlas-2", "Cobalt Atlas 2", "cobalt", "Reads charts, screenshots and PDFs", null, ThreeLevels, ModelEffort.Medium),
            Model("halcyon-wide-15", "Halcyon Wide 1.5", "halcyon", "Huge context for whole codebases", null, ThreeLevels, ModelEffort.Medium),
            Model("granite-70b", "Granite 70B", "granite", "Open weights on your own servers", null, null, null)
        ];
        string[] shortNames = ["Prime 3", "Swift 3", "Atlas 2", "Wide 1.5", "Granite"];
        for (var i = 0; i < models.Length; i++)
        {
            models[i].ShortName = shortNames[i];
        }
        return models;
    }

    /// <summary>The composer's model menu: every model in one list, the effort as a segmented row, above its chip.</summary>
    public static ModelMenu ComposerMenu(Action<ModelMenu>? configure = null)
    {
        var menu = new ModelMenu
        {
            EffortLayout = ModelMenuEffortLayout.Inline,
            ShowMoreModels = false,
            Placement = Avalonia.Controls.PlacementMode.TopEdgeAlignedRight
        };
        foreach (var provider in Providers())
        {
            menu.Providers.Add(provider);
        }
        foreach (var model in ComposerModels())
        {
            menu.Models.Add(model);
        }
        configure?.Invoke(menu);
        return menu;
    }

    /// <summary>A menu with the whole catalog and its providers.</summary>
    public static ModelMenu Menu(Action<ModelMenu>? configure = null)
    {
        var menu = new ModelMenu();
        foreach (var provider in Providers())
        {
            menu.Providers.Add(provider);
        }
        foreach (var model in Models())
        {
            menu.Models.Add(model);
        }
        configure?.Invoke(menu);
        return menu;
    }

    public static AiModel Model(string id, string name, string provider, string? blurb, string? tag,
                                IReadOnlyList<ModelEffort>? levels, ModelEffort? defaultEffort) => new()
    {
        Id = id,
        Name = name,
        ProviderId = provider,
        Blurb = blurb,
        Tag = tag,
        EffortLevels = levels,
        DefaultEffort = defaultEffort
    };

    private static AiModelProvider Provider(string id, string name, string? color, string mark) => new()
    {
        Id = id,
        Name = name,
        Mark = Geometry.Parse(mark),
        MarkColor = color is null ? null : new ImmutableSolidColorBrush(Color.Parse(color))
    };
}
