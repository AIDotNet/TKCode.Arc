using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model menu's catalog resolution, selection sync and effort snapping (no popup involved).</summary>
public class ModelMenuDataTests
{
    private static string[] Ids(IEnumerable<AiModel> models) => models.Select(m => m.Id).ToArray();

    [AvaloniaFact]
    public void Defaults()
    {
        var menu = new ModelMenu();
        menu.IsVisible.ShouldBeFalse();
        menu.IsOpen.ShouldBeFalse();
        menu.PagedBelowWidth.ShouldBe(640);
        menu.AnchorGap.ShouldBe(8);
        menu.Labels.Menu.ShouldBe("Models");
        menu.Labels.Effort.ShouldBe("Effort");
        menu.Labels.MoreModels.ShouldBe("More models");
        menu.Labels.Default.ShouldBe("Default");
        menu.Labels.BackTo("Effort").ShouldBe("Back to featured models, Effort");
        menu.Labels.EffortName(ModelEffort.ExtraHigh).ShouldBe("Extra high");
        menu.Labels.EffortDescription(ModelEffort.Low).ShouldBe(ModelEffortNames.Description(ModelEffort.Low));
        menu.FeaturedModels.ShouldBeEmpty();
        menu.OtherModels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Without_Featured_Ids_The_First_Four_Models_Are_Featured_And_The_Rest_Grouped_By_Provider()
    {
        var menu = ModelMenuSamples.Menu();
        Ids(menu.FeaturedModels).ShouldBe(["aurora-prime-3", "aurora-swift-3", "cobalt-atlas-2", "halcyon-wide-15"]);
        Ids(menu.OtherModels).ShouldBe([
            "aurora-summit-29", "aurora-pocket-24", "cobalt-atlas-lite", "halcyon-flash-12", "basalt-4", "driftwood-coder-7",
            "ember-large-3", "fjord-reason-2", "granite-70b"
        ]);
        menu.Groups.Select(g => g.Name).ShouldBe(["Aurora Labs", "Cobalt Research", "Halcyon", "Basalt AI", "Driftwood", "Ember Systems", "Fjord", "Granite Open"]);
    }

    [AvaloniaFact]
    public void Featured_Ids_Choose_And_Order_The_Top_Models_Skipping_Missing_And_Repeated_Ids()
    {
        var menu = ModelMenuSamples.Menu(m =>
        {
            m.FeaturedIds.Add("granite-70b");
            m.FeaturedIds.Add("no-such-model");
            m.FeaturedIds.Add("aurora-prime-3");
            m.FeaturedIds.Add("granite-70b");
        });
        Ids(menu.FeaturedModels).ShouldBe(["granite-70b", "aurora-prime-3"]);
        menu.OtherModels.Count.ShouldBe(11);
        menu.OtherModels.ShouldNotContain(m => m.Id == "granite-70b" || m.Id == "aurora-prime-3");
        // The granite group is gone: its only model is featured.
        menu.Groups.Select(g => g.Id).ShouldNotContain("granite");

        menu.FeaturedIds.Clear();
        Ids(menu.FeaturedModels).ShouldBe(["aurora-prime-3", "aurora-swift-3", "cobalt-atlas-2", "halcyon-wide-15"]);
    }

    [AvaloniaFact]
    public void Listed_Providers_Order_The_Groups_And_Unlisted_Ones_Follow_In_First_Appearance_Order()
    {
        var menu = new ModelMenu();
        var providers = ModelMenuSamples.Providers();
        menu.Providers.Add(providers.First(p => p.Id == "granite"));
        menu.Providers.Add(providers.First(p => p.Id == "cobalt"));
        foreach (var model in ModelMenuSamples.Models())
        {
            menu.Models.Add(model);
        }
        menu.Models.Add(ModelMenuSamples.Model("loose", "Loose Model", null!, null, null, null, null));
        menu.Groups.Select(g => g.Id).ShouldBe(["granite", "cobalt", "aurora", "halcyon", "basalt", "driftwood", "ember", "fjord", ""]);
        // A listed provider titles its group; an unlisted one is titled by its id; models without a provider get no title.
        menu.Groups[0].Name.ShouldBe("Granite Open");
        menu.Groups[2].Name.ShouldBe("aurora");
        menu.Groups[^1].Name.ShouldBe("");
        Ids(menu.OtherModels).Take(3).ShouldBe(["granite-70b", "cobalt-atlas-lite", "aurora-summit-29"]);
    }

    [AvaloniaFact]
    public void Models_Source_Wins_Over_Inline_Models_And_Its_Changes_Are_Followed()
    {
        var menu = ModelMenuSamples.Menu();
        var source = new ObservableCollection<AiModel>(ModelMenuSamples.Models().Skip(8));
        menu.ModelsSource = source;
        Ids(menu.FeaturedModels).ShouldBe(["basalt-4", "driftwood-coder-7", "ember-large-3", "fjord-reason-2"]);
        Ids(menu.OtherModels).ShouldBe(["granite-70b"]);

        source.Insert(0, ModelMenuSamples.Model("new-one", "New One", "fjord", null, null, null, null));
        menu.FeaturedModels[0].Id.ShouldBe("new-one");
        menu.OtherModels.Select(m => m.Id).ShouldBe(["fjord-reason-2", "granite-70b"]);

        // Inline changes are ignored while a source is set.
        menu.Models.Clear();
        menu.FeaturedModels[0].Id.ShouldBe("new-one");

        menu.ModelsSource = null;
        menu.FeaturedModels.ShouldBeEmpty();
        source.Clear();
        menu.OtherModels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Selected_Model_And_Its_Id_Stay_In_Sync()
    {
        var menu = ModelMenuSamples.Menu();
        menu.SelectedModelId = "basalt-4";
        menu.SelectedModel!.Name.ShouldBe("Basalt 4");
        var swift = menu.FeaturedModels[1];
        menu.SelectedModel = swift;
        menu.SelectedModelId.ShouldBe("aurora-swift-3");
        menu.SelectedModel = null;
        menu.SelectedModelId.ShouldBeNull();
    }

    [AvaloniaFact]
    public void An_Id_The_Catalog_Does_Not_Have_Yet_Resolves_When_The_Model_Arrives()
    {
        var source = new ObservableCollection<AiModel>();
        var menu = new ModelMenu { ModelsSource = source, SelectedModelId = "basalt-4", Effort = ModelEffort.Max };
        menu.SelectedModel.ShouldBeNull();
        menu.SelectedModelId.ShouldBe("basalt-4");
        var chosen = new List<ModelMenuEffortEventArgs>();
        menu.EffortChosen += (_, e) => chosen.Add(e);
        foreach (var model in ModelMenuSamples.Models())
        {
            source.Add(model);
        }
        menu.SelectedModel!.Id.ShouldBe("basalt-4");
        // The pending effort is moved to a level the model offers, quietly (nothing was chosen).
        menu.Effort.ShouldBe(ModelEffort.High);
        chosen.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_First_Featured_Model_Is_Selected_Once_When_The_Menu_Loads()
    {
        var menu = ModelMenuSamples.Menu();
        var chosen = new List<ModelMenuEffortEventArgs>();
        menu.EffortChosen += (_, e) => chosen.Add(e);
        menu.SelectedModel.ShouldBeNull();
        var window = TestHelpers.Show(new Panel { Children = { menu } });
        try
        {
            menu.SelectedModel.ShouldBe(menu.FeaturedModels[0]);
            menu.SelectedModelId.ShouldBe("aurora-prime-3");
            menu.Effort.ShouldBe(ModelEffort.Medium);
            chosen.ShouldBeEmpty();

            // Only once: clearing the selection later keeps it clear.
            menu.SelectedModel = null;
            menu.Models.Add(ModelMenuSamples.Model("late", "Late", "fjord", null, null, null, null));
            menu.SelectedModel.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Menu_That_Loads_Empty_Selects_The_First_Featured_Model_When_Models_Arrive()
    {
        var source = new ObservableCollection<AiModel>();
        var menu = new ModelMenu { ModelsSource = source };
        var window = TestHelpers.Show(new Panel { Children = { menu } });
        try
        {
            menu.SelectedModel.ShouldBeNull();
            source.Add(ModelMenuSamples.Models()[2]);
            menu.SelectedModelId.ShouldBe("cobalt-atlas-2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Given_Selection_Is_Kept_When_The_Menu_Loads()
    {
        var menu = ModelMenuSamples.Menu(m => m.SelectedModelId = "granite-70b");
        var window = TestHelpers.Show(new Panel { Children = { menu } });
        try
        {
            menu.SelectedModel!.Id.ShouldBe("granite-70b");
            menu.Effort.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Model_Snaps_The_Effort_And_Reports_The_Snap()
    {
        var menu = ModelMenuSamples.Menu(m => m.SelectedModelId = "aurora-prime-3");
        var chosen = new List<ModelMenuEffortEventArgs>();
        menu.EffortChosen += (_, e) => chosen.Add(e);
        // Before the menu is in a tree, values are its starting state: no event.
        menu.SelectedModelId = "aurora-swift-3";
        chosen.ShouldBeEmpty();
        menu.SelectedModelId = "aurora-prime-3";
        menu.Effort = ModelEffort.Max;
        var window = TestHelpers.Show(new Panel { Children = { menu } });
        try
        {
            Snaps(menu, chosen);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Snaps(ModelMenu menu, List<ModelMenuEffortEventArgs> chosen)
    {

        menu.SelectedModelId = "cobalt-atlas-2";
        menu.Effort.ShouldBe(ModelEffort.High);
        chosen.Count.ShouldBe(1);
        chosen[0].IsSnap.ShouldBeTrue();
        chosen[0].PreviousEffort.ShouldBe(ModelEffort.Max);
        chosen[0].Effort.ShouldBe(ModelEffort.High);

        // A model without levels has no effort.
        menu.SelectedModelId = "cobalt-atlas-lite";
        menu.Effort.ShouldBeNull();
        chosen.Count.ShouldBe(2);
        chosen[1].Effort.ShouldBeNull();

        // From no effort, a model starts at its own default.
        menu.SelectedModelId = "halcyon-flash-12";
        menu.Effort.ShouldBe(ModelEffort.Low);
        chosen.Count.ShouldBe(3);

        // Same level offered: no change, no event.
        menu.SelectedModelId = "aurora-prime-3";
        menu.Effort.ShouldBe(ModelEffort.Low);
        chosen.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Effort_Set_By_Code_Is_Kept_On_An_Offered_Level_Without_An_Event()
    {
        var menu = ModelMenuSamples.Menu(m => m.SelectedModelId = "cobalt-atlas-2");
        var chosen = new List<ModelMenuEffortEventArgs>();
        menu.EffortChosen += (_, e) => chosen.Add(e);
        menu.Effort.ShouldBe(ModelEffort.Medium);
        menu.Effort = ModelEffort.Low;
        menu.Effort.ShouldBe(ModelEffort.Low);
        menu.Effort = ModelEffort.Max;
        menu.Effort.ShouldBe(ModelEffort.High);
        menu.Effort = null;
        menu.Effort.ShouldBe(ModelEffort.Medium);
        chosen.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void New_Levels_On_The_Selected_Model_Move_The_Effort()
    {
        var menu = ModelMenuSamples.Menu(m => m.SelectedModelId = "aurora-prime-3");
        menu.Effort = ModelEffort.Max;
        menu.SelectedModel!.EffortLevels = [ModelEffort.Low, ModelEffort.Medium];
        menu.Effort.ShouldBe(ModelEffort.Medium);
        menu.SelectedModel.EffortLevels = null;
        menu.Effort.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Renamed_Id_Or_Provider_Regroups_The_Catalog()
    {
        var menu = ModelMenuSamples.Menu();
        var basalt = menu.OtherModels.First(m => m.Id == "basalt-4");
        basalt.ProviderId = "aurora";
        menu.Groups[0].Models.ShouldContain(basalt);
        menu.Groups.Select(g => g.Id).ShouldNotContain("basalt");
        menu.FeaturedIds.Add("basalt-renamed");
        menu.FeaturedModels.ShouldBeEmpty();
        basalt.Id = "basalt-renamed";
        menu.FeaturedModels.ShouldBe([basalt]);
    }

    [AvaloniaFact]
    public void A_Catalog_That_Outlives_The_Menu_Does_Not_Keep_It_Alive()
    {
        var source = new ObservableCollection<AiModel>(ModelMenuSamples.Models());
        var reference = MenuOverSource(source);
        for (var i = 0; i < 4 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }
        reference.IsAlive.ShouldBeFalse();
        // The catalog still works (the dead menu's links drop themselves).
        source.Add(ModelMenuSamples.Model("after", "After", "fjord", null, null, null, null));
        source[0].Name = "Renamed";
        GC.KeepAlive(source);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference MenuOverSource(ObservableCollection<AiModel> source)
    {
        var menu = new ModelMenu { ModelsSource = source };
        var host = ModelMenuHost.Show(menu);
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(Avalonia.Input.PhysicalKey.End);
        host.Key(Avalonia.Input.PhysicalKey.ArrowRight);
        host.Menu.Close();
        host.Pump();
        host.Window.Content = null;
        host.Dispose();
        host.Pump();
        return new WeakReference(menu);
    }
}
