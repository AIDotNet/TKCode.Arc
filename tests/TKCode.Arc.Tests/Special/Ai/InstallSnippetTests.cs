using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The install snippet: commands, languages and key slots; the sample card; framework and package manager tabs with their
/// keyboard model and events; Generate key (simulated, real, failing, cancelled); the app's own key; copies and done
/// steps; breakpoints; and the key wipe's motion.
/// </summary>
public class InstallSnippetTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60.0);

    [Theory]
    [InlineData(InstallSnippetPackageManager.Npm, "npm install @lumen/node")]
    [InlineData(InstallSnippetPackageManager.Pnpm, "pnpm add @lumen/node")]
    [InlineData(InstallSnippetPackageManager.Yarn, "yarn add @lumen/node")]
    [InlineData(InstallSnippetPackageManager.Bun, "bun add @lumen/node")]
    public void Install_Command_Follows_The_Package_Manager(InstallSnippetPackageManager manager, string expected) =>
        InstallSnippetText.InstallCommand(manager, "  @lumen/node ").ShouldBe(expected);

    [Fact]
    public void Install_Command_Keeps_Every_Package()
    {
        InstallSnippetText.InstallCommand(InstallSnippetPackageManager.Pnpm, "a  b\tc").ShouldBe("pnpm add a b c");
        InstallSnippetText.InstallCommand(InstallSnippetPackageManager.Npm, null).ShouldBe("npm install");
    }

    [Theory]
    [InlineData("Terminal", null, "bash")]
    [InlineData(".env", null, "bash")]
    [InlineData(".env.local", null, "bash")]
    [InlineData("index.js", null, "js")]
    [InlineData("main.tsx", null, "tsx")]
    [InlineData("app.py", null, "python")]
    [InlineData("main.go", null, "go")]
    [InlineData("index.html", null, "html")]
    [InlineData("config.toml", null, "toml")]
    [InlineData("index.html", "xml", "xml")]
    public void Language_Follows_The_File_Name_Unless_Given(string file, string? language, string expected) =>
        InstallSnippetText.LanguageOf(file, language).ShouldBe(expected);

    [Fact]
    public void Split_Puts_The_Placeholder_In_Every_Slot()
    {
        var segments = InstallSnippetText.Split("a={{key}} b={{key}}", "KEY", out var display);
        display.ShouldBe("a=KEY b=KEY");
        segments.Select(s => (s.Start, s.Length, s.IsSlot)).ShouldBe([(0, 2, false), (2, 3, true), (5, 3, false), (8, 3, true)]);
    }

    [Fact]
    public void Fill_Normalize_And_Line_Count()
    {
        InstallSnippetText.Fill("KEY={{key}}\r\n", "sk").ShouldBe("KEY=sk");
        InstallSnippetText.Normalize("a\tb\r\nc\n\n").ShouldBe("a    b\nc");
        InstallSnippetText.LineCount("one\n\nthree").ShouldBe(3);
        InstallSnippetText.LineCount(null).ShouldBe(1);
        InstallSnippetText.HasKey("x {{key}}").ShouldBeTrue();
        InstallSnippetText.HasKey("x").ShouldBeFalse();
    }

    [Fact]
    public void Simulated_Keys_Are_Obviously_Demo_Keys()
    {
        var key = InstallSnippetBuiltIns.NewKey(new Random(7), LumenKeyPrefix);
        key.ShouldStartWith("lumen_demo_");
        key.Length.ShouldBe("lumen_demo_".Length + 20);
        key[LumenKeyPrefix.Length..].ShouldAllBe(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c));
        InstallSnippetBuiltIns.NewKey(new Random(7), InstallSnippetBuiltIns.DefaultKeyPrefix).ShouldStartWith("demo_");
    }

    [Fact]
    public void Default_Labels_Format_Announcements()
    {
        var labels = InstallSnippetLabels.Default;
        labels.StepName(2, 3).ShouldBe("Step 2 of 3");
        labels.DoneTitle("Install").ShouldBe("Install, done");
        labels.FrameworkShown("Python").ShouldBe("Showing Python");
        labels.NameOf(InstallSnippetPackageManager.Bun).ShouldBe("bun");
    }

    [AvaloniaFact]
    public void Card_With_A_Simulation_Shows_Its_SDK_With_The_Placeholder()
    {
        var (card, window) = Open();
        try
        {
            card.FrameworkTabs!.Tabs.Select(t => t.Id).ShouldBe(["node", "python", "go", "html"]);
            card.FrameworkTabs.SelectedId.ShouldBe("node");
            card.FrameworkTabs.Tabs.ShouldAllBe(t => t.MarkShown && t.LabelShown);
            card.PackageManagerTabs!.IsVisible.ShouldBeTrue();
            card.PackageManagerTabs.SelectedId.ShouldBe(nameof(InstallSnippetPackageManager.Npm));
            Step(card, InstallSnippetStep.Install).CopyValue.ShouldBe("npm install @lumen/node");
            Step(card, InstallSnippetStep.Install).FileName.ShouldBe("Terminal");
            Step(card, InstallSnippetStep.Key).CopyValue.ShouldBe("LUMEN_KEY=YOUR_LUMEN_KEY");
            Step(card, InstallSnippetStep.Key).CodeView!.ShownText.ShouldBe("LUMEN_KEY=YOUR_LUMEN_KEY");
            Step(card, InstallSnippetStep.Key).CodeView!.Tint.ShouldBe(1);
            Step(card, InstallSnippetStep.Code).FileName.ShouldBe("index.js");
            Step(card, InstallSnippetStep.Code).IsLast.ShouldBeTrue();
            card.CurrentKey.ShouldBeNull();
            card.KeyState.ShouldBe(InstallSnippetKeyState.Idle);
            card.GenerateButton!.IsVisible.ShouldBeTrue();
            card.GenerateButton.Content.ShouldBe("Generate key");
            card.KeyStatusText.ShouldBe(InstallSnippetLabels.Default.SimulatedHint);
            AutomationProperties.GetName(Step(card, InstallSnippetStep.Key)).ShouldBe("Step 2 of 3");
            AutomationProperties.GetName(Step(card, InstallSnippetStep.Key).TitleBlock!).ShouldBe("Add your key");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Code_Block_Reserves_Its_Longest_Snippet_And_Lines_Reserve_The_Longest_Line()
    {
        var (card, window) = Open();
        try
        {
            // The HTML key snippet has three lines; Node's has one, so Node's view still reserves three.
            var key = Step(card, InstallSnippetStep.Key).CodeView!;
            key.LineCount.ShouldBe(1);
            key.MinLines.ShouldBe(3);
            var height = key.Bounds.Height;
            Pick(card.FrameworkTabs!, "html");
            Pump();
            Step(card, InstallSnippetStep.Key).CodeView!.Bounds.Height.ShouldBe(height);
            Step(card, InstallSnippetStep.Code).CodeView!.MinLines.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Framework_Swaps_The_Snippets_Raises_The_Event_And_Announces()
    {
        var picked = new List<string>();
        var (card, window) = Open(c => c.FrameworkChanged += (_, e) => picked.Add(e.FrameworkId));
        try
        {
            Pick(card.FrameworkTabs!, "python");
            Pump();
            picked.ShouldBe(["python"]);
            card.SelectedFramework.ShouldBe("python");
            card.PackageManagerTabs!.IsVisible.ShouldBeFalse();
            Step(card, InstallSnippetStep.Install).CopyValue.ShouldBe("pip install lumen-analytics");
            Step(card, InstallSnippetStep.Code).FileName.ShouldBe("app.py");
            Step(card, InstallSnippetStep.Code).CodeView!.Language.ShouldBe("python");
            card.Announcement.ShouldBe("Showing Python");

            // Setting the property from code follows silently.
            card.SelectedFramework = "go";
            Pump();
            picked.Count.ShouldBe(1);
            Step(card, InstallSnippetStep.Code).FileName.ShouldBe("main.go");
            card.FrameworkTabs!.SelectedId.ShouldBe("go");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_And_Select_Tabs_With_Roving_Focus()
    {
        var (card, window) = Open();
        try
        {
            var tabs = card.FrameworkTabs!.Tabs;
            tabs.Count(t => t.IsTabStop).ShouldBe(1);
            tabs[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            card.SelectedFramework.ShouldBe("python");
            tabs[1].IsFocused.ShouldBeTrue();
            tabs.Single(t => t.IsTabStop).Id.ShouldBe("python");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            card.SelectedFramework.ShouldBe("html");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            card.SelectedFramework.ShouldBe("node");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            card.SelectedFramework.ShouldBe("html");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            card.SelectedFramework.ShouldBe("node");
            tabs[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Package_Manager_Rewrites_The_Install_Command()
    {
        var picked = new List<InstallSnippetPackageManager>();
        var (card, window) = Open(c => c.PackageManagerChanged += (_, e) => picked.Add(e.PackageManager));
        try
        {
            Pick(card.PackageManagerTabs!, nameof(InstallSnippetPackageManager.Pnpm));
            Pump();
            picked.ShouldBe([InstallSnippetPackageManager.Pnpm]);
            card.PackageManager.ShouldBe(InstallSnippetPackageManager.Pnpm);
            Step(card, InstallSnippetStep.Install).CopyValue.ShouldBe("pnpm add @lumen/node");
            Step(card, InstallSnippetStep.Install).CodeView!.ShownText.ShouldBe("pnpm add @lumen/node");
            card.Announcement.ShouldBe("Using pnpm");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulated_Generate_Key_Puts_A_Demo_Key_In_Every_Snippet_And_Says_So()
    {
        var generated = new List<InstallSnippetKeyGeneratedEventArgs>();
        var (card, window) = Open(c =>
        {
            c.Delay = static (_, _) => Task.CompletedTask;
            c.KeyGenerated += (_, e) => generated.Add(e);
        });
        try
        {
            Press(window, card.GenerateButton!);
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Ready).ShouldBeTrue();
            var key = card.CurrentKey.ShouldNotBeNull();
            key.ShouldStartWith(LumenKeyPrefix);
            generated.Single().Key.ShouldBe(key);
            generated.Single().IsSimulated.ShouldBeTrue();
            Step(card, InstallSnippetStep.Key).CopyValue.ShouldBe("LUMEN_KEY=" + key);
            Step(card, InstallSnippetStep.Key).CodeView!.Key.ShouldBe(key);
            card.GenerateButton!.Content.ShouldBe("New key");
            card.KeyStatusText.ShouldBe(InstallSnippetLabels.Default.CreatedSimulated);
            card.Announcement.ShouldBe(InstallSnippetLabels.Default.SimulatedKeyAnnouncement);
            card.KeyStatus!.Classes.Contains(InstallSnippetKeyStatus.ReadyPseudoClass).ShouldBeTrue();

            // The same key follows a framework switch.
            Pick(card.FrameworkTabs!, "html");
            Pump();
            Step(card, InstallSnippetStep.Key).CodeView!.ShownText.ShouldBe($"<script>\n  lumen.init(\"{key}\");\n</script>");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Real_Generator_Gets_A_Token_And_Its_Key()
    {
        var gate = new TaskCompletionSource<string>();
        CancellationToken seen = default;
        var (card, window) = Open(c => c.KeyGenerator = token =>
        {
            seen = token;
            return gate.Task;
        });
        try
        {
            card.KeyStatusText.ShouldBe(InstallSnippetLabels.Default.KeyHint);
            _ = card.GenerateKeyAsync();
            Pump();
            card.KeyState.ShouldBe(InstallSnippetKeyState.Generating);
            ((Button)card.GenerateButton!).IsLoading.ShouldBeTrue();
            card.KeyStatusText.ShouldBe(InstallSnippetLabels.Default.Creating);
            seen.CanBeCanceled.ShouldBeTrue();
            gate.SetResult("sk_live_example");
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Ready).ShouldBeTrue();
            card.CurrentKey.ShouldBe("sk_live_example");
            card.KeyStatusText.ShouldBe(InstallSnippetLabels.Default.Created);
            ((Button)card.GenerateButton!).IsLoading.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failing_Generator_Shows_The_Error_And_Try_Again()
    {
        var (card, window) = Open(c => c.KeyGenerator = async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Key service unavailable.");
        });
        try
        {
            _ = card.GenerateKeyAsync();
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Failed).ShouldBeTrue();
            card.GenerateButton!.Content.ShouldBe("Try again");
            card.KeyStatusText.ShouldBe("Key service unavailable.");
            card.KeyStatus!.Classes.Contains(InstallSnippetKeyStatus.FailedPseudoClass).ShouldBeTrue();
            card.Announcement.ShouldBe("Key service unavailable.");
            card.CurrentKey.ShouldBeNull();
            Step(card, InstallSnippetStep.Key).CopyValue.ShouldBe("LUMEN_KEY=YOUR_LUMEN_KEY");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Request_Or_Leaving_The_Window_Cancels_The_Running_One()
    {
        var tokens = new List<CancellationToken>();
        var (card, window) = Open(c => c.KeyGenerator = token =>
        {
            tokens.Add(token);
            return new TaskCompletionSource<string>().Task;
        });
        try
        {
            _ = card.GenerateKeyAsync();
            _ = card.GenerateKeyAsync();
            Pump();
            tokens.Count.ShouldBe(2);
            tokens[0].IsCancellationRequested.ShouldBeTrue();
            tokens[1].IsCancellationRequested.ShouldBeFalse();
            card.KeyState.ShouldBe(InstallSnippetKeyState.Generating);
        }
        finally
        {
            window.Close();
        }
        Pump();
        tokens[1].IsCancellationRequested.ShouldBeTrue();
        card.KeyState.ShouldBe(InstallSnippetKeyState.Idle);
    }

    [AvaloniaFact]
    public void The_Apps_Own_Key_Fills_The_Snippets_And_Hides_Generate()
    {
        var (card, window) = Open(c => c.ApiKey = "sk_live_mine");
        try
        {
            card.CurrentKey.ShouldBe("sk_live_mine");
            card.GenerateButton!.IsVisible.ShouldBeFalse();
            card.KeyStatus!.IsVisible.ShouldBeFalse();
            var view = Step(card, InstallSnippetStep.Key).CodeView!;
            view.ShownText.ShouldBe("LUMEN_KEY=sk_live_mine");
            view.Tint.ShouldBe(0);
            Step(card, InstallSnippetStep.Key).CopyValue.ShouldBe("LUMEN_KEY=sk_live_mine");

            card.ApiKey = null;
            Pump();
            card.CurrentKey.ShouldBeNull();
            card.GenerateButton.IsVisible.ShouldBeTrue();
            Step(card, InstallSnippetStep.Key).CodeView!.ShownText.ShouldBe("LUMEN_KEY=YOUR_LUMEN_KEY");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copies_Raise_The_Event_And_Mark_Steps_Done()
    {
        var copies = new List<(InstallSnippetStep Step, string Value)>();
        var (card, window) = Open(c =>
        {
            c.Delay = static (_, _) => Task.CompletedTask;
            c.Copied += (_, e) => copies.Add((e.Step, e.Value));
        });
        try
        {
            Step(card, InstallSnippetStep.Install).NotifyCopied();
            Pump();
            copies.ShouldBe([(InstallSnippetStep.Install, "npm install @lumen/node")]);
            card.IsStepDone(InstallSnippetStep.Install).ShouldBeTrue();
            Step(card, InstallSnippetStep.Install).IsDone.ShouldBeTrue();
            AutomationProperties.GetName(Step(card, InstallSnippetStep.Install).TitleBlock!).ShouldBe("Install, done");

            // Copying the placeholder does not finish step 2; copying the real key does.
            Step(card, InstallSnippetStep.Key).NotifyCopied();
            card.IsStepDone(InstallSnippetStep.Key).ShouldBeFalse();
            _ = card.GenerateKeyAsync();
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Ready).ShouldBeTrue();
            Step(card, InstallSnippetStep.Key).NotifyCopied();
            card.IsStepDone(InstallSnippetStep.Key).ShouldBeTrue();
            copies[^1].Value.ShouldBe("LUMEN_KEY=" + card.CurrentKey);

            card.ResetProgress();
            card.IsStepDone(InstallSnippetStep.Install).ShouldBeFalse();
            Step(card, InstallSnippetStep.Install).IsDone.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Frameworks_Replace_The_Simulation_And_Unknown_Ids_Get_Text_Only_Tabs()
    {
        var (card, window) = Open(c =>
        {
            c.KeyPlaceholder = "YOUR_KEY";
            c.Frameworks.Add(new InstallSnippetFramework("ruby", "Ruby")
            {
                Install = new InstallSnippetFile("Terminal", "gem install contoso-labs"),
                Key = new InstallSnippetFile(".env", "KEY={{key}}"),
                Code = new InstallSnippetFile("app.rb", "require \"contoso\""),
            });
            c.Frameworks.Add(new InstallSnippetFramework("node", "Node") { Packages = "@contoso-labs/sdk" });
        });
        try
        {
            card.FrameworkTabs!.Tabs.Select(t => t.Id).ShouldBe(["ruby", "node"]);
            card.ActiveSimulation.ShouldBeNull();
            card.FrameworkTabs.Tabs[0].MarkShown.ShouldBeFalse();
            card.FrameworkTabs.Tabs[1].MarkShown.ShouldBeTrue();
            Step(card, InstallSnippetStep.Install).CopyValue.ShouldBe("gem install contoso-labs");
            Step(card, InstallSnippetStep.Key).CopyValue.ShouldBe("KEY=YOUR_KEY");
            Step(card, InstallSnippetStep.Code).CodeView!.Language.ShouldBe("ruby");
            Step(card, InstallSnippetStep.Install).DescriptionText.ShouldBe(InstallSnippetLabels.Default.InstallDescription);

            // A snippet edited in place follows.
            card.Frameworks[0].Code!.Code = "require \"contoso\"\nputs 1";
            Pump();
            Step(card, InstallSnippetStep.Code).CodeView!.LineCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Frameworks_Or_A_Simulation_The_Steps_Wait_Over_Empty_Snippets()
    {
        var card = new InstallSnippet { Delay = static (_, _) => Task.CompletedTask };
        var window = TestHelpers.Show(card);
        try
        {
            Pump();
            card.Version.ShouldBeNull();
            card.KeyPlaceholder.ShouldBe("YOUR_API_KEY");
            card.DisplayedFrameworks.ShouldBeEmpty();
            card.FrameworkTabs!.Tabs.ShouldBeEmpty();
            card.PackageManagerTabs!.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(card, InstallSnippet.VersionPart).IsVisible.ShouldBeFalse();
            Step(card, InstallSnippetStep.Install).CopyValue.ShouldBe(string.Empty);
            Step(card, InstallSnippetStep.Key).CopyValue.ShouldBe(string.Empty);
            Step(card, InstallSnippetStep.Code).CopyValue.ShouldBe(string.Empty);
            Step(card, InstallSnippetStep.Install).DescriptionText.ShouldBe(InstallSnippetLabels.Default.InstallDescription);
            AutomationProperties.GetName(Step(card, InstallSnippetStep.Key).TitleBlock!).ShouldBe("Add your key");

            // With no simulation an invented key still says it is a demo key.
            Press(window, card.GenerateButton!);
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Ready).ShouldBeTrue();
            card.CurrentKey.ShouldNotBeNull().ShouldStartWith("demo_");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_The_Simulation_Empties_The_Card()
    {
        var (card, window) = Open();
        try
        {
            card.FrameworkTabs!.Tabs.Count.ShouldBe(4);
            card.Simulation = null;
            Pump();
            card.FrameworkTabs.Tabs.ShouldBeEmpty();
            Step(card, InstallSnippetStep.Install).CopyValue.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Every_Step()
    {
        var (card, window) = Open(c => c.Labels = new InstallSnippetLabels
        {
            InstallTitle = "Installer",
            GenerateKey = "Générer une clé",
            StepName = static (n, count) => $"Étape {n} sur {count}",
        });
        try
        {
            Step(card, InstallSnippetStep.Install).Title.ShouldBe("Installer");
            card.GenerateButton!.Content.ShouldBe("Générer une clé");
            AutomationProperties.GetName(Step(card, InstallSnippetStep.Code)).ShouldBe("Étape 3 sur 3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Hide_Marks_Names_And_The_Version()
    {
        var host = new Border { Width = 600, Child = NewCard() };
        var window = TestHelpers.Show(host);
        try
        {
            var card = (InstallSnippet)host.Child!;
            Pump();
            card.IsNarrow.ShouldBeFalse();
            card.PackageManagerTabs!.Tabs.ShouldAllBe(t => t.LabelShown);

            host.Width = 420;
            Pump();
            card.IsNarrow.ShouldBeTrue();
            card.IsCompact.ShouldBeFalse();
            card.PackageManagerTabs.Tabs.ShouldAllBe(t => !t.LabelShown && t.MarkShown);
            AutomationProperties.GetName(card.PackageManagerTabs.Tabs[1]).ShouldBe("pnpm");
            card.FrameworkTabs!.Tabs.ShouldAllBe(t => t.MarkShown);

            host.Width = 360;
            Pump();
            card.IsCompact.ShouldBeTrue();
            card.FrameworkTabs.Tabs.ShouldAllBe(t => !t.MarkShown && t.LabelShown);
            TestHelpers.Descendant<Border>(card, InstallSnippet.VersionPart).IsVisible.ShouldBeFalse();
            card.Bounds.Width.ShouldBe(360);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Fills_Its_Container_Up_To_640()
    {
        var host = new Border { Width = 900, Child = NewCard() };
        var window = TestHelpers.Show(host);
        try
        {
            Pump();
            host.Child!.Bounds.Width.ShouldBe(640);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Key_Eases_The_Slot_While_It_Wipes_In_Then_The_Tint_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (card, window) = Open(c => c.Delay = static (_, _) => Task.CompletedTask, ArcMotionLevel.Full);
        try
        {
            var view = Step(card, InstallSnippetStep.Key).CodeView!;
            view.SlotCharacters.ShouldBe("YOUR_LUMEN_KEY".Length);
            _ = card.GenerateKeyAsync();
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Ready).ShouldBeTrue();
            ReferenceEquals(Step(card, InstallSnippetStep.Key).CodeView, view).ShouldBeTrue();
            view.WipeProgress.ShouldBe(0, 0.05);
            view.Tint.ShouldBe(1);
            Run(clock, 170);
            view.WipeProgress.ShouldBeInRange(0.2, 0.99);
            view.SlotCharacters.ShouldBeInRange(14.5, 30.5);
            view.Tint.ShouldBe(1);
            Run(clock, 220);
            view.WipeProgress.ShouldBe(1);
            view.SlotCharacters.ShouldBe(card.CurrentKey!.Length);
            Run(clock, 600);
            view.Tint.ShouldBe(0, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Drops_The_Wipe_But_Keeps_The_Tint_Fade()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (card, window) = Open(c => c.Delay = static (_, _) => Task.CompletedTask, ArcMotionLevel.Reduced);
        try
        {
            var view = Step(card, InstallSnippetStep.Key).CodeView!;
            _ = card.GenerateKeyAsync();
            TestHelpers.PumpUntil(() => card.KeyState == InstallSnippetKeyState.Ready).ShouldBeTrue();
            view.WipeProgress.ShouldBe(1);
            view.SlotCharacters.ShouldBe(card.CurrentKey!.Length);
            view.Tint.ShouldBe(1);
            Run(clock, 600);
            view.Tint.ShouldBe(0, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tab_Highlight_Glides_On_A_Bounce_Free_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var tabs = card.FrameworkTabs!;
            var start = tabs.Highlight!.CurrentRect;
            Pick(tabs, "go");
            var target = tabs.Tabs[2].Bounds;
            Run(clock, 3 * Frame.TotalMilliseconds);
            // Mid-glide, and an arrange pass (the card relayouts) does not cut it short.
            tabs.InvalidateArrange();
            Pump();
            tabs.Highlight.CurrentRect.X.ShouldBeInRange(start.X + 1, target.X - 1);
            var furthest = 0.0;
            for (var i = 0; i < 40; i++)
            {
                Run(clock, Frame.TotalMilliseconds);
                furthest = Math.Max(furthest, tabs.Highlight.CurrentRect.X);
            }
            start.X.ShouldBeLessThan(target.X);
            tabs.Highlight.CurrentRect.X.ShouldBe(target.X, 0.5);
            furthest.ShouldBeLessThanOrEqualTo(target.X + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Snippets_Are_Highlighted_With_The_Library_Palette()
    {
        var (card, window) = Open();
        try
        {
            var view = Step(card, InstallSnippetStep.Code).CodeView!;
            // Highlighting runs on the thread pool; a bounded wait turns a stall under load into a failure, not a hung run.
            await view.WhenHighlighted.WaitAsync(TimeSpan.FromSeconds(30));
            Pump();
            view.HasTokens.ShouldBeTrue();
            InstallSnippetHighlighter.TryGet(view.ShownText, "js", false, out var tokens).ShouldBeTrue();
            tokens.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The tests' own invented SDK (the Lumen analytics sample the library used to ship; the Gallery now passes this
    /// library's install steps): Node, Python, Go and HTML, its placeholder, its version and its demo key prefix.
    /// </summary>
    private static InstallSnippet NewCard() => new()
    {
        Simulation = new InstallSnippetSimulation { Frameworks = LumenFrameworks(), KeyPrefix = LumenKeyPrefix },
        KeyPlaceholder = "YOUR_LUMEN_KEY",
        Version = "v2.4.0",
    };

    private const string LumenKeyPrefix = "lumen_demo_";

    /// <summary>A fresh copy of the Lumen frameworks.</summary>
    private static InstallSnippetFramework[] LumenFrameworks() =>
    [
        new InstallSnippetFramework("node", "Node")
        {
            Packages = "@lumen/node",
            PackagesDescription = "Add the SDK to your project.",
            Key = new InstallSnippetFile(".env", "LUMEN_KEY={{key}}", "Put your key in .env. Never commit it."),
            Code = new InstallSnippetFile("index.js",
                "import { Lumen } from \"@lumen/node\";\n\nconst lumen = new Lumen(process.env.LUMEN_KEY);\n\nawait lumen.track(\"signed_up\", { plan: \"free\" });",
                "Track your first event from index.js."),
        },
        new InstallSnippetFramework("python", "Python")
        {
            Install = new InstallSnippetFile("Terminal", "pip install lumen-analytics", "Install the package with pip."),
            Key = new InstallSnippetFile(".env", "LUMEN_KEY={{key}}", "Put your key in .env. Never commit it."),
            Code = new InstallSnippetFile("app.py",
                "import os\nfrom lumen import Lumen\n\nlumen = Lumen(os.environ[\"LUMEN_KEY\"])\nlumen.track(\"signed_up\", {\"plan\": \"free\"})",
                "Track your first event from app.py."),
        },
        new InstallSnippetFramework("go", "Go")
        {
            Install = new InstallSnippetFile("Terminal", "go get lumen.example/go/v2", "Add the module to your project."),
            Key = new InstallSnippetFile(".env", "LUMEN_KEY={{key}}", "Put your key in .env. Never commit it."),
            Code = new InstallSnippetFile("main.go",
                "import \"lumen.example/go/v2\"\n\nfunc main() {\n    client := lumen.FromEnv() // reads LUMEN_KEY\n    client.Track(\"signed_up\", lumen.Props{\"plan\": \"free\"})\n}",
                "Track your first event from main.go."),
        },
        new InstallSnippetFramework("html", "HTML")
        {
            Install = new InstallSnippetFile("index.html",
                "<script src=\"https://cdn.lumen.example/v2.4.0/lumen.js\"></script>",
                "Load the script in the page's head."),
            Key = new InstallSnippetFile("index.html", "<script>\n  lumen.init(\"{{key}}\");\n</script>",
                "Start Lumen with your key, after the script."),
            Code = new InstallSnippetFile("index.html",
                "<button onclick=\"lumen.track('signed_up')\">\n  Sign up\n</button>",
                "Track your first event from any element."),
        },
    ];

    private static (InstallSnippet Card, Window Window) Open(Action<InstallSnippet>? configure = null,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var card = NewCard();
        configure?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        Pump();
        return (card, window);
    }

    private static InstallSnippetStepView Step(InstallSnippet card, InstallSnippetStep step) => card.StepView(step).ShouldNotBeNull();

    private static void Pick(InstallSnippetTabs tabs, string id) => tabs.Pick(tabs.Tabs.Single(t => t.Id == id), focus: false);

    private static void Press(Window window, Control button)
    {
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
    }

    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(ManualFrameClock clock, double milliseconds)
    {
        var frames = (int)Math.Round(milliseconds / Frame.TotalMilliseconds);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Pump();
        }
    }
}
