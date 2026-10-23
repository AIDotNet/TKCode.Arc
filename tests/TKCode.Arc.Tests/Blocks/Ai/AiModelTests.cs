using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The shared model data of the AI blocks: effort snapping, names and number formatting.</summary>
public class AiModelTests
{
    private static readonly ModelEffort[] ThreeLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];
    private static readonly ModelEffort[] FiveLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];

    [Fact]
    public void An_Offered_Level_Is_Kept()
    {
        ModelEffortSnap.Snap(ModelEffort.Medium, ThreeLevels).ShouldBe(ModelEffort.Medium);
        ModelEffortSnap.Snap(ModelEffort.Max, FiveLevels).ShouldBe(ModelEffort.Max);
    }

    [Fact]
    public void A_Level_The_Model_Lacks_Snaps_To_The_Nearest_One()
    {
        ModelEffortSnap.Snap(ModelEffort.Max, ThreeLevels).ShouldBe(ModelEffort.High);
        ModelEffortSnap.Snap(ModelEffort.ExtraHigh, ThreeLevels).ShouldBe(ModelEffort.High);
        ModelEffortSnap.Snap(ModelEffort.Low, [ModelEffort.High, ModelEffort.Max]).ShouldBe(ModelEffort.High);
    }

    [Fact]
    public void A_Tie_Takes_The_Lower_Level_Whatever_The_Order_Of_The_List()
    {
        // Medium is one away from both Low and High.
        ModelEffortSnap.Snap(ModelEffort.Medium, [ModelEffort.High, ModelEffort.Low]).ShouldBe(ModelEffort.Low);
        ModelEffortSnap.Snap(ModelEffort.Medium, [ModelEffort.Low, ModelEffort.High]).ShouldBe(ModelEffort.Low);
    }

    [Fact]
    public void No_Current_Level_Uses_The_Default_Then_The_Middle_Level()
    {
        ModelEffortSnap.Snap(null, ThreeLevels, ModelEffort.High).ShouldBe(ModelEffort.High);
        ModelEffortSnap.Snap(null, FiveLevels, ModelEffort.Max).ShouldBe(ModelEffort.Max);
        // A default the model does not offer is ignored.
        ModelEffortSnap.Snap(null, ThreeLevels, ModelEffort.Max).ShouldBe(ModelEffort.Medium);
        ModelEffortSnap.Snap(null, FiveLevels).ShouldBe(ModelEffort.High);
        ModelEffortSnap.Snap(null, [ModelEffort.Low]).ShouldBe(ModelEffort.Low);
    }

    [Fact]
    public void A_Model_Without_Levels_Has_No_Effort()
    {
        ModelEffortSnap.Snap(ModelEffort.High, null).ShouldBeNull();
        ModelEffortSnap.Snap(ModelEffort.High, []).ShouldBeNull();
        ModelEffortSnap.Snap(null, [], ModelEffort.Low).ShouldBeNull();
    }

    [Fact]
    public void Duplicate_And_Unordered_Levels_Are_Handled()
    {
        ModelEffortSnap.Snap(ModelEffort.Max, [ModelEffort.Medium, ModelEffort.Low, ModelEffort.Medium]).ShouldBe(ModelEffort.Medium);
    }

    [Fact]
    public void Model_Helpers_Follow_Their_Properties()
    {
        var model = new AiModel { Name = "Aurora Prime 3", EffortLevels = ThreeLevels, DefaultEffort = ModelEffort.Medium };
        model.DisplayName.ShouldBe("Aurora Prime 3");
        model.ShortName = "Prime 3";
        model.DisplayName.ShouldBe("Prime 3");
        model.ShortName = "";
        model.DisplayName.ShouldBe("Aurora Prime 3");
        model.SupportsEffort.ShouldBeTrue();
        model.SnapEffort(ModelEffort.Max).ShouldBe(ModelEffort.High);
        model.SnapEffort(null).ShouldBe(ModelEffort.Medium);

        var fixedModel = new AiModel { Name = "Cobalt Lite" };
        fixedModel.SupportsEffort.ShouldBeFalse();
        fixedModel.SnapEffort(ModelEffort.High).ShouldBeNull();
    }

    [Fact]
    public void Effort_Names_And_Descriptions_Cover_Every_Level()
    {
        foreach (var level in Enum.GetValues<ModelEffort>())
        {
            ModelEffortNames.Name(level).ShouldNotBeNullOrWhiteSpace();
            ModelEffortNames.Description(level).ShouldNotBeNullOrWhiteSpace();
        }
        ModelEffortNames.Name(ModelEffort.ExtraHigh).ShouldBe("Extra high");
    }

    [Theory]
    [InlineData(512L, "512")]
    [InlineData(1_000L, "1K")]
    [InlineData(128_000L, "128K")]
    [InlineData(800_000L, "800K")]
    [InlineData(1_000_000L, "1M")]
    [InlineData(1_250_000L, "1.25M")]
    [InlineData(3_000_000L, "3M")]
    [InlineData(2_000_000_000L, "2B")]
    public void Token_Counts_Use_Short_Suffixes_Without_Trailing_Zeros(long tokens, string expected)
        => AiModelFormat.Tokens(tokens).ShouldBe(expected);

    [Fact]
    public void Prices_Have_Two_Decimals_And_A_Symbol_And_Do_Not_Follow_The_Machine_Culture()
    {
        AiModelFormat.Price(4m).ShouldBe("$4.00");
        AiModelFormat.Price(0.3m).ShouldBe("$0.30");
        AiModelFormat.Price(60m, "€").ShouldBe("€60.00");
        AiModelFormat.Price(1.5m, "$", CultureInfo.GetCultureInfo("de-DE")).ShouldBe("$1,50");
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            AiModelFormat.Price(1.5m).ShouldBe("$1.50");
            AiModelFormat.Tokens(1_250_000).ShouldBe("1.25M");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

/// <summary>The mark renderer and the provider fallback of the AI model parts.</summary>
public class ModelMarkTests
{
    private static readonly Avalonia.Media.Geometry Dot = Avalonia.Media.Geometry.Parse("M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0 -16z");

    [Fact]
    public void A_Model_Uses_Its_Own_Mark_Then_Its_Providers()
    {
        var providerMark = Avalonia.Media.Geometry.Parse("M2 2h20v20H2z");
        var providers = new[]
        {
            new AiModelProvider { Id = "other", Mark = Dot },
            new AiModelProvider { Id = "aurora", Mark = providerMark, MarkColor = Avalonia.Media.Brushes.Orange }
        };
        var own = new AiModel { Id = "a", Mark = Dot, MarkColor = Avalonia.Media.Brushes.Red, ProviderId = "aurora" };
        var inherited = new AiModel { Id = "b", ProviderId = "aurora" };
        var orphan = new AiModel { Id = "c", ProviderId = "missing" };

        ModelMark.Resolve(own, providers, out var ownColor).ShouldBe(Dot);
        ownColor.ShouldBe(Avalonia.Media.Brushes.Red);
        ModelMark.Resolve(inherited, providers, out var inheritedColor).ShouldBe(providerMark);
        inheritedColor.ShouldBe(Avalonia.Media.Brushes.Orange);
        ModelMark.Resolve(orphan, providers, out var orphanColor).ShouldBeNull();
        orphanColor.ShouldBeNull();
        ModelMark.Resolve(null, providers, out _).ShouldBeNull();
        ModelMark.Resolve(inherited, null, out _).ShouldBeNull();
    }

    [AvaloniaFact]
    public void It_Measures_To_Its_Size_And_Is_Decorative()
    {
        var mark = new ModelMark { Mark = Dot, Size = 18 };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { mark } });
        try
        {
            mark.Bounds.Size.ShouldBe(new Avalonia.Size(18, 18));
            mark.IsHitTestVisible.ShouldBeFalse();
            mark.Focusable.ShouldBeFalse();
            mark.Size = 16;
            window.UpdateLayout();
            mark.Bounds.Size.ShouldBe(new Avalonia.Size(16, 16));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Geometry_Is_Scaled_From_The_24_Box_And_Takes_The_Mark_Color_Or_The_Foreground()
    {
        var mark = new ModelMark { Mark = Dot, Size = 12, Foreground = Avalonia.Media.Brushes.Gray };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { mark } });
        try
        {
            DrawingGroup Record(ModelMark m)
            {
                var group = new DrawingGroup();
                using (var context = group.Open())
                {
                    m.Render(context);
                }
                return group;
            }
            static IEnumerable<GeometryDrawing> Shapes(DrawingGroup g) =>
                g.Children.SelectMany(c => c is DrawingGroup nested ? Shapes(nested) : c is GeometryDrawing d ? [d] : []);

            var plain = Shapes(Record(mark)).Single();
            plain.Brush.ShouldBe(Avalonia.Media.Brushes.Gray);
            mark.MarkColor = Avalonia.Media.Brushes.Orange;
            Shapes(Record(mark)).Single().Brush.ShouldBe(Avalonia.Media.Brushes.Orange);
            // Nothing to draw without a mark or without any brush.
            mark.Mark = null;
            Shapes(Record(mark)).ShouldBeEmpty();
            mark.Mark = "not a mark";
            Shapes(Record(mark)).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }
}
