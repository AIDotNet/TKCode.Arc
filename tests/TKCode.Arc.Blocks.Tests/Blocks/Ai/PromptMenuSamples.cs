using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented data for the prompt studio's header pills and menus: the "Refund reply" versions of the spec's sample (section
/// 10), five models with made-up names, geometric marks and sample prices, and the English words the studio would pass.
/// </summary>
internal static class PromptMenuSamples
{
    /// <summary>The moment relative times are measured from.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly ModelEffort[] AllLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
    private static readonly ModelEffort[] ThreeLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];

    // Invented marks on the 24 grid: a circle, a diamond, a triangle, a hexagon and a four-point star.
    private static readonly Geometry Circle = Geometry.Parse("M12 3 A9 9 0 1 1 11.99 3 Z");
    private static readonly Geometry Diamond = Geometry.Parse("M12 2 L22 12 L12 22 L2 12 Z");
    private static readonly Geometry Triangle = Geometry.Parse("M12 3 L22 20 L2 20 Z");
    private static readonly Geometry Hexagon = Geometry.Parse("M12 2 L21 7 L21 17 L12 22 L3 17 L3 7 Z");
    private static readonly Geometry Star = Geometry.Parse("M12 2 L14.5 9.5 L22 12 L14.5 14.5 L12 22 L9.5 14.5 L2 12 L9.5 9.5 Z");

    public static PromptAuthor Rui() => new() { Name = "Rui Tanaka" };

    public static PromptVersion[] Versions()
    {
        var rui = Rui();
        return
        [
            new() { Id = "v4", Label = "v4", Message = "Name the rule before the apology", Author = rui, CreatedAt = Now.AddDays(-2) },
            new() { Id = "v3", Label = "v3", Message = "Trim to one paragraph", Author = new PromptAuthor { Name = "Lena Brooks" }, CreatedAt = Now.AddDays(-9) },
            new() { Id = "v2", Label = "v2", Message = "Add sign-off variable", Author = new PromptAuthor { Name = "Omar Haddad" }, CreatedAt = Now.AddDays(-16) },
            new() { Id = "v1", Label = "v1", Message = "Initial draft from help macros", Author = rui, CreatedAt = Now.AddDays(-29) }
        ];
    }

    public static AiModel[] Models() =>
    [
        new()
        {
            Id = "atlas-prime-2", Name = "Atlas Prime 2", ShortName = "Prime 2", Mark = Circle, MarkColor = new SolidColorBrush(Color.Parse("#C2410C")),
            InputPrice = 6m, OutputPrice = 30m, IsSamplePrice = true, EffortLevels = AllLevels, DefaultEffort = ModelEffort.High
        },
        new()
        {
            Id = "atlas-core-2", Name = "Atlas Core 2", ShortName = "Core 2", Mark = Diamond, MarkColor = new SolidColorBrush(Color.Parse("#C2410C")),
            InputPrice = 1.5m, OutputPrice = 7m, IsSamplePrice = true, EffortLevels = AllLevels, DefaultEffort = ModelEffort.High
        },
        new()
        {
            Id = "atlas-lite-2", Name = "Atlas Lite 2", ShortName = "Lite 2", Mark = Triangle, MarkColor = new SolidColorBrush(Color.Parse("#C2410C")),
            InputPrice = 0.2m, OutputPrice = 0.8m, IsSamplePrice = true, EffortLevels = ThreeLevels, DefaultEffort = ModelEffort.Medium
        },
        new()
        {
            Id = "comet-pro", Name = "Comet Pro", Mark = Hexagon, InputPrice = 2.5m, OutputPrice = 12m, IsSamplePrice = true,
            EffortLevels = ThreeLevels, DefaultEffort = ModelEffort.Low
        },
        new()
        {
            Id = "nova-flash", Name = "Nova Flash", Mark = Star, MarkColor = new SolidColorBrush(Color.Parse("#2563EB")),
            InputPrice = 0.1m, OutputPrice = 0.4m, IsSamplePrice = true
        }
    ];

    /// <summary>The short words of the effort cells.</summary>
    public static string Cell(ModelEffort effort) => effort switch
    {
        ModelEffort.Low => "Low",
        ModelEffort.Medium => "Med",
        ModelEffort.High => "High",
        ModelEffort.ExtraHigh => "XHigh",
        _ => "Max"
    };

    public static string DaysAgo(int days) => days switch
    {
        0 => "today",
        1 => "yesterday",
        _ => $"{days} days ago"
    };

    public static string WeeksAgo(int weeks) => $"{weeks} weeks ago";

    /// <summary>A version pill with the studio's words.</summary>
    public static PromptVersionPill VersionPill(string label = "v4", bool production = true) => new()
    {
        Label = label,
        IsProduction = production,
        ProductionText = "Production",
        LatestText = "Latest"
    };

    /// <summary>A version menu with the studio's words, the sample versions, v4 open and in production.</summary>
    public static PromptVersionMenu VersionMenu(PromptVersion[]? versions = null)
    {
        versions ??= Versions();
        return new PromptVersionMenu
        {
            Versions = versions,
            OpenVersion = versions[0],
            ProductionVersion = versions[0],
            Now = Now,
            DialogName = "Versions",
            GroupName = "Pick a version to compare or promote",
            ProductionText = "Production",
            CompareText = label => $"Compare with {label}",
            PromoteText = "Promote to production",
            ProductionServesText = label => $"Production serves {label}",
            InProductionText = label => $"{label} is in production",
            UndoText = "Undo",
            MetaFormat = (label, author, when) => $"{label} · {author} · {when}",
            DaysAgoText = DaysAgo,
            LastWeekText = "last week",
            WeeksAgoText = WeeksAgo
        };
    }

    /// <summary>A model menu with the studio's words and the sample models, the second one selected at High.</summary>
    public static PromptModelMenu ModelMenu(AiModel[]? models = null)
    {
        models ??= Models();
        return new PromptModelMenu
        {
            Models = models,
            SelectedModel = models[1],
            Effort = ModelEffort.High,
            DialogName = "Model and effort",
            ModelsName = "Model",
            EffortTitle = "Effort",
            EffortCellText = Cell,
            EffortName = ModelEffortNames.Name,
            PriceNote = "Input · output per 1M tokens. Every price here is a sample.",
            PriceName = (input, output, sample) => $"{input} input, {output} output per million tokens{(sample ? ", sample price" : "")}"
        };
    }

    /// <summary>A model pill showing what <paramref name="menu"/> has selected, following its choices.</summary>
    public static PromptModelPill ModelPill(PromptModelMenu menu)
    {
        var pill = new PromptModelPill { Model = menu.SelectedModel, Effort = menu.Effort, EffortText = ModelEffortNames.Name };
        menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == PromptModelMenu.SelectedModelProperty || e.Property == PromptModelMenu.EffortProperty)
            {
                pill.Model = menu.SelectedModel;
                pill.Effort = menu.Effort;
            }
        };
        return pill;
    }

    /// <summary>
    /// Wires a pill to its menu as the studio would: the pill toggles the menu, the menu's open state shows on the pill,
    /// and (for the version menu) promote and undo move production, the pill saying whether the open version is in it.
    /// </summary>
    public static void Wire(PromptHeaderPill pill, PromptPopoverMenu menu)
    {
        menu.PlacementTarget = pill;
        pill.OpenRequested += (_, e) => menu.Toggle(e.Focus);
        menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == PromptPopoverMenu.IsOpenProperty)
            {
                pill.IsOpen = menu.IsOpen;
            }
        };
        if (menu is PromptVersionMenu versions && pill is PromptVersionPill versionPill)
        {
            var history = new Stack<PromptVersion?>();
            versions.PromoteRequested += (_, e) =>
            {
                history.Push(versions.ProductionVersion);
                versions.ProductionVersion = e.Version;
            };
            versions.UndoPromoteRequested += (_, _) =>
            {
                if (history.Count > 0)
                {
                    versions.ProductionVersion = history.Pop();
                }
            };
            versions.PropertyChanged += (_, e) =>
            {
                if (e.Property == PromptVersionMenu.ProductionVersionProperty || e.Property == PromptVersionMenu.OpenVersionProperty)
                {
                    versionPill.IsProduction = ReferenceEquals(versions.ProductionVersion, versions.OpenVersion);
                }
            };
        }
    }
}
