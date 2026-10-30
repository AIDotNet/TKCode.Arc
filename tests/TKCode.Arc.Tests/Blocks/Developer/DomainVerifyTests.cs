using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// <see cref="DomainVerify"/>: the pure domain rules and scripted check, then the card on invented records (the flow, an
/// invalid record, failures and retries, cancelling, removing, the simulated flow and its autoplay, the compact layout,
/// names for screen readers and motion levels).
/// </summary>
public class DomainVerifyTests
{
    private static readonly DomainRecord[] Records =
    [
        new("route", "CNAME", "shop", "edge.example.net", "Routes traffic"),
        new("owner", "TXT", "_verify", "verify=0123456789abcdef", "Proves you own the domain"),
        new("caa", "CAA", "shop", "0 issue \"letsencrypt.org\"", "Lets the certificate be issued"),
    ];

    private static DomainVerifyResult Result(int regions, params DomainRecordStatus[] statuses) =>
        new(Records.Zip(statuses, (r, s) => s == DomainRecordStatus.Invalid
                ? new DomainRecordCheck(r.Id, s, Found: "0 issue \"other.example\"", Fix: "Use the value shown above.")
                : new DomainRecordCheck(r.Id, s)).ToList(),
            new DomainRegions(regions, 6));

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 700, ArcMotionLevel level = ArcMotionLevel.Off, Action<DomainVerify>? configure = null)
        {
            Block = new DomainVerify { AutoPlay = false, DefaultDomain = "shop.example.com", PollInterval = TimeSpan.Zero };
            Block.SimulationScale = 0;
            configure?.Invoke(Block);
            Block.PhaseChanged += (_, e) => Phases.Add(e.NewPhase);
            Block.Verified += (_, e) => Verified.Add(e);
            Block.Removed += (_, e) => Removed.Add(e);
            Block.Failed += (_, e) => Failures.Add(e);
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1000;
            Window.Height = 900;
            Pump();
        }

        public DomainVerify Block { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<DomainVerifyPhase> Phases { get; } = new();

        public List<DomainVerifiedEventArgs> Verified { get; } = new();

        public List<DomainRemovedEventArgs> Removed { get; } = new();

        public List<DomainVerifyFailedEventArgs> Failures { get; } = new();

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public void Await(Task task)
        {
            TestHelpers.PumpUntil(() => task.IsCompleted, 5000).ShouldBeTrue();
            task.GetAwaiter().GetResult();
            Pump();
        }

        public T Await<T>(Task<T> task)
        {
            Await((Task)task);
            return task.Result;
        }

        public void Dispose() => Window.Close();
    }

    private static Action<DomainVerify> Handlers(
        Func<DomainVerifyContext, DomainVerifyResult>? verify = null, Func<DomainCertificate>? certificate = null) => block =>
    {
        block.GetRecordsAction = (_, _) => Task.FromResult<IReadOnlyList<DomainRecord>>(Records);
        block.VerifyAction = (_, _, context, _) => Task.FromResult(
            (verify ?? (c => Result(6, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Valid)))(context));
        block.IssueCertificateAction = (_, _) =>
            Task.FromResult((certificate ?? (() => new DomainCertificate("Test CA", "in 90 days")))());
    };

    #region Domain rules

    [Theory]
    [InlineData("https://App.Example.com:8443/path?x=1#top", "app.example.com")]
    [InlineData("  shop.northwind.dev. ", "shop.northwind.dev")]
    [InlineData("http://user:pw@host.example.org/", "host.example.org")]
    [InlineData("example.com:80", "example.com")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_strips_protocol_credentials_port_path_and_trailing_dot(string? typed, string expected) =>
        DomainVerifySimulation.Normalize(typed).ShouldBe(expected);

    [Theory]
    [InlineData("app.example.com", true)]
    [InlineData("example.co", true)]
    [InlineData("a-b.example.io", true)]
    [InlineData("localhost", false)]
    [InlineData("-bad.example.com", false)]
    [InlineData("bad-.example.com", false)]
    [InlineData("exa mple.com", false)]
    [InlineData("example.c", false)]
    [InlineData("example.123", false)]
    [InlineData("a..example.com", false)]
    [InlineData("", false)]
    public void IsValid_needs_two_labels_and_a_letter_only_top_level(string host, bool valid) =>
        DomainVerifySimulation.IsValid(host).ShouldBe(valid);

    [Fact]
    public void Default_records_point_a_subdomain_with_a_CNAME_and_an_apex_with_an_A_record()
    {
        var sub = DomainVerifySimulation.DefaultRecords("app.example.com");
        sub.Select(r => r.Type).ShouldBe(["CNAME", "TXT", "CAA"]);
        sub[0].Name.ShouldBe("app");
        sub.Select(r => r.Id).Distinct().Count().ShouldBe(3);

        var apex = DomainVerifySimulation.DefaultRecords("example.com");
        apex.Select(r => r.Type).ShouldBe(["A", "TXT", "CAA"]);
        apex[0].Name.ShouldBe("@");
        DomainVerifySimulation.IsApex("example.com").ShouldBeTrue();
        DomainVerifySimulation.IsApex("app.example.com").ShouldBeFalse();
    }

    [Fact]
    public void The_ownership_token_is_stable_per_host_and_differs_between_hosts()
    {
        DomainVerifySimulation.Token("a.example.com").ShouldBe(DomainVerifySimulation.Token("a.example.com"));
        DomainVerifySimulation.Token("a.example.com").ShouldNotBe(DomainVerifySimulation.Token("b.example.com"));
        DomainVerifySimulation.Token("a.example.com").Length.ShouldBe(16);
    }

    [Fact]
    public void The_scripted_check_spreads_the_records_then_finds_a_conflict_then_passes()
    {
        var records = DomainVerifySimulation.DefaultRecords("app.example.com");

        var first = DomainVerifySimulation.Check(records, new DomainVerifyContext(1, 1));
        first.Records.Select(c => c.Status).ShouldBe([DomainRecordStatus.Valid, DomainRecordStatus.Missing, DomainRecordStatus.Missing]);

        var second = DomainVerifySimulation.Check(records, new DomainVerifyContext(2, 1));
        second.Records.Select(c => c.Status).ShouldBe([DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Invalid]);
        second.Records[2].Found.ShouldNotBeNullOrEmpty();
        second.Records[2].Fix.ShouldNotBeNullOrEmpty();

        var fixedRun = DomainVerifySimulation.Check(records, new DomainVerifyContext(1, 2));
        fixedRun.Records.ShouldAllBe(c => c.Status == DomainRecordStatus.Valid);
        fixedRun.Regions.ShouldBe(new DomainRegions(6, 6));
    }

    #endregion

    #region Card

    [AvaloniaFact]
    public void It_starts_with_the_field_placeholder_rows_and_no_footer()
    {
        using var rig = new Rig();
        var block = rig.Block;

        block.Phase.ShouldBe(DomainVerifyPhase.Enter);
        block.DomainInput!.Text.ShouldBe("shop.example.com");
        block.AddButton!.IsEffectivelyVisible.ShouldBeTrue();
        block.EmptyState!.IsEffectivelyVisible.ShouldBeTrue();
        block.Rows.Count.ShouldBe(0);
        block.RemoveButton!.IsEffectivelyVisible.ShouldBeFalse();
        block.FooterAction!.IsEffectivelyVisible.ShouldBeFalse();
        block.Classes.Contains(DomainVerify.EnterPseudoClass).ShouldBeTrue();
        block.Domain.ShouldBeNull();
    }

    [AvaloniaFact]
    public void An_untouched_field_follows_DefaultDomain_and_a_typed_one_keeps_its_text()
    {
        using var rig = new Rig();
        rig.Block.DefaultDomain = "docs.example.com";
        rig.Block.DomainInput!.Text.ShouldBe("docs.example.com");

        rig.Block.DomainInput.Text = "mine.example.com";
        rig.Block.DefaultDomain = "other.example.com";
        rig.Block.DomainInput.Text.ShouldBe("mine.example.com");
    }

    [AvaloniaFact]
    public void A_bad_domain_shows_an_error_and_stays_in_the_Enter_phase()
    {
        using var rig = new Rig(configure: Handlers());
        rig.Block.DomainInput!.Text = "not a domain";

        rig.Await(rig.Block.AddDomainAsync()).ShouldBeFalse();

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
        rig.Block.DomainInput.ErrorMessage.ShouldBe(rig.Block.Labels.DomainInvalid);
        rig.Phases.ShouldBeEmpty();

        rig.Block.DomainInput.Text = "ok.example.com";
        rig.Pump();
        rig.Block.DomainInput.ErrorMessage.ShouldBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void Adding_a_domain_lists_the_records_and_moves_to_the_footer_action()
    {
        using var rig = new Rig(configure: Handlers());
        rig.Block.DomainInput!.Focus();
        rig.Pump();

        rig.Await(rig.Block.AddDomainAsync()).ShouldBeTrue();

        var block = rig.Block;
        block.Phase.ShouldBe(DomainVerifyPhase.Records);
        block.Domain.ShouldBe("shop.example.com");
        rig.Phases.ShouldBe([DomainVerifyPhase.Adding, DomainVerifyPhase.Records]);
        block.Rows.Select(r => r.Record.Type).ShouldBe(["CNAME", "TXT", "CAA"]);
        block.Rows.ShouldAllBe(r => r.State == DomainRowState.NotChecked);
        block.AddButton!.IsEffectivelyVisible.ShouldBeFalse();
        block.RemoveButton!.IsEffectivelyVisible.ShouldBeTrue();
        block.FooterAction!.IsEffectivelyVisible.ShouldBeTrue();
        block.Classes.Contains(DomainVerify.RecordsPseudoClass).ShouldBeTrue();
        block.FooterAction!.Content.ShouldBe(block.Labels.CheckRecords);
        block.FooterText.ShouldBe(block.Labels.FooterRecords);
        rig.Window.FocusManager!.GetFocusedElement().ShouldBe(block.FooterAction);
    }

    [AvaloniaFact]
    public void Enter_in_the_field_adds_the_domain()
    {
        using var rig = new Rig(configure: Handlers());
        rig.Block.DomainInput!.Focus();
        rig.Pump();

        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        TestHelpers.PumpUntil(() => rig.Block.Phase == DomainVerifyPhase.Records, 5000).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_domain_is_reduced_to_a_host_name_before_it_goes_to_the_server()
    {
        string? asked = null;
        using var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.GetRecordsAction = (domain, _) =>
            {
                asked = domain;
                return Task.FromResult<IReadOnlyList<DomainRecord>>(Records);
            };
        });
        rig.Block.DomainInput!.Text = "HTTPS://Shop.Example.com:443/path/.";

        rig.Await(rig.Block.AddDomainAsync()).ShouldBeTrue();

        asked.ShouldBe("shop.example.com");
    }

    [AvaloniaFact]
    public void A_check_polls_while_a_record_is_missing_then_verifies_and_issues_the_certificate()
    {
        var contexts = new List<DomainVerifyContext>();
        using var rig = new Rig(configure: Handlers(c =>
        {
            contexts.Add(c);
            return c.Attempt switch
            {
                1 => Result(1, DomainRecordStatus.Valid, DomainRecordStatus.Missing, DomainRecordStatus.Missing),
                2 => Result(4, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Missing),
                _ => Result(6, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Valid),
            };
        }));
        rig.Await(rig.Block.AddDomainAsync());

        rig.Await(rig.Block.CheckAsync());

        contexts.ShouldBe([new(1, 1), new(2, 1), new(3, 1)]);
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Verified);
        rig.Block.Rows.ShouldAllBe(r => r.State == DomainRowState.Valid);
        rig.Block.Certificate.ShouldBe(new DomainCertificate("Test CA", "in 90 days"));
        rig.Phases.ShouldBe([DomainVerifyPhase.Adding, DomainVerifyPhase.Records, DomainVerifyPhase.Checking, DomainVerifyPhase.Verified]);
        rig.Verified.Count.ShouldBe(1);
        rig.Verified[0].Domain.ShouldBe("shop.example.com");
        rig.Verified[0].Certificate.Issuer.ShouldBe("Test CA");
        rig.Block.FooterAction!.Content.ShouldBe(rig.Block.Labels.AddAnother);
        rig.Block.FooterText.ShouldBe(rig.Block.Labels.FooterVerified);
    }

    [AvaloniaFact]
    public void A_missing_record_shows_as_not_found_and_a_running_check_shows_the_regions()
    {
        var gate = new TaskCompletionSource();
        using var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.VerifyAction = async (_, _, context, ct) =>
            {
                if (context.Attempt == 1)
                {
                    return Result(2, DomainRecordStatus.Valid, DomainRecordStatus.Missing, DomainRecordStatus.Missing);
                }
                await gate.Task.WaitAsync(ct);
                return Result(6, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Valid);
            };
        });
        rig.Await(rig.Block.AddDomainAsync());

        var run = rig.Block.CheckAsync();
        TestHelpers.PumpUntil(() => rig.Block.Rows[0].State == DomainRowState.Valid).ShouldBeTrue();
        rig.Pump();

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Checking);
        rig.Block.Rows[1].State.ShouldBe(DomainRowState.Checking); // polling again
        rig.Block.FooterText.ShouldBe(rig.Block.Labels.FooterChecking(2, 6));
        rig.Block.FooterAction!.IsLoading.ShouldBeTrue();

        gate.SetResult();
        rig.Await(run);
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Verified);
    }

    [AvaloniaFact]
    public void A_conflicting_record_stops_the_polling_and_shows_what_was_found_and_the_fix()
    {
        var calls = 0;
        using var rig = new Rig(configure: Handlers(c =>
        {
            calls++;
            return c.Run == 1
                ? Result(3, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Invalid)
                : Result(6, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Valid);
        }));
        rig.Await(rig.Block.AddDomainAsync());

        rig.Await(rig.Block.CheckAsync());

        calls.ShouldBe(1);
        var block = rig.Block;
        block.Phase.ShouldBe(DomainVerifyPhase.Invalid);
        block.Rows[2].State.ShouldBe(DomainRowState.Invalid);
        block.Rows[2].IsFixShown.ShouldBeTrue();
        block.Rows[0].IsFixShown.ShouldBeFalse();
        block.Rows[2].StatusText.ShouldBe(block.Labels.Misconfigured);
        block.FooterAction!.Content.ShouldBe(block.Labels.CheckAgain);
        block.FooterText.ShouldBe(block.Labels.FooterInvalid);
        block.Announcer!.History.Last().ShouldBe(block.Labels.NeedsFixAnnouncement("CAA"));

        rig.Await(block.CheckAsync());

        block.Phase.ShouldBe(DomainVerifyPhase.Verified);
        block.Rows[2].IsFixShown.ShouldBeFalse();
        calls.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_failing_GetRecordsAction_returns_to_the_field_with_the_message()
    {
        using var rig = new Rig(configure: b => b.GetRecordsAction = (_, _) => throw new InvalidOperationException("Domain is taken."));

        rig.Await(rig.Block.AddDomainAsync()).ShouldBeFalse();

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
        rig.Block.DomainInput!.ErrorMessage.ShouldBe("Domain is taken.");
        rig.Failures.Count.ShouldBe(1);
        rig.Failures[0].Domain.ShouldBe("shop.example.com");
        rig.Phases.ShouldBe([DomainVerifyPhase.Adding, DomainVerifyPhase.Enter]);
    }

    [AvaloniaFact]
    public void A_failing_check_keeps_the_records_shows_the_message_and_can_be_retried()
    {
        var fail = true;
        using var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.VerifyAction = (_, _, _, _) => fail
                ? throw new InvalidOperationException("Resolver pool is busy.")
                : Task.FromResult(Result(6, DomainRecordStatus.Valid, DomainRecordStatus.Valid, DomainRecordStatus.Valid));
        });
        rig.Await(rig.Block.AddDomainAsync());

        rig.Await(rig.Block.CheckAsync());

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Records);
        rig.Block.FooterText.ShouldBe("Resolver pool is busy.");
        rig.Block.Rows.Count.ShouldBe(3);
        rig.Failures.Count.ShouldBe(1);

        fail = false;
        rig.Await(rig.Block.CheckAsync());
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Verified);
        rig.Block.FooterText.ShouldBe(rig.Block.Labels.FooterVerified);
    }

    [AvaloniaFact]
    public void A_failing_certificate_keeps_the_records_valid_and_a_retry_issues_it()
    {
        var fail = true;
        using var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.IssueCertificateAction = (_, _) => fail
                ? throw new InvalidOperationException("CA is unreachable.")
                : Task.FromResult(new DomainCertificate("Test CA", "in 90 days"));
        });
        rig.Await(rig.Block.AddDomainAsync());

        rig.Await(rig.Block.CheckAsync());

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Records);
        rig.Block.Certificate.ShouldBeNull();
        rig.Block.FooterText.ShouldBe("CA is unreachable.");
        rig.Verified.ShouldBeEmpty();

        fail = false;
        rig.Await(rig.Block.CheckAsync());
        rig.Block.Certificate!.Issuer.ShouldBe("Test CA");
        rig.Verified.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Removing_while_a_check_runs_cancels_it_quietly_and_returns_to_the_field()
    {
        CancellationToken seen = default;
        using var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.VerifyAction = async (_, _, _, ct) =>
            {
                seen = ct;
                await Task.Delay(Timeout.Infinite, ct);
                return Result(0);
            };
        });
        rig.Await(rig.Block.AddDomainAsync());
        var run = rig.Block.CheckAsync();
        TestHelpers.PumpUntil(() => seen.CanBeCanceled).ShouldBeTrue();

        rig.Block.Remove();
        rig.Await(run);

        seen.IsCancellationRequested.ShouldBeTrue();
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
        rig.Block.Domain.ShouldBeNull();
        rig.Block.Rows.Count.ShouldBe(0);
        rig.Block.DomainInput!.Text.ShouldBeNullOrEmpty();
        rig.Failures.ShouldBeEmpty();
        rig.Removed.Count.ShouldBe(1);
        rig.Removed[0].Domain.ShouldBe("shop.example.com");
    }

    [AvaloniaFact]
    public void The_close_button_and_Add_another_remove_the_domain_and_raise_Removed()
    {
        using var rig = new Rig(configure: Handlers());
        rig.Await(rig.Block.AddDomainAsync());

        rig.Block.RemoveButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
        rig.Removed.Count.ShouldBe(1);

        rig.Block.DomainInput!.Text = "again.example.com";
        rig.Await(rig.Block.AddDomainAsync());
        rig.Await(rig.Block.CheckAsync());
        rig.Block.FooterAction!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
        rig.Removed.Count.ShouldBe(2);
        rig.Removed[1].Domain.ShouldBe("again.example.com");
    }

    [AvaloniaFact]
    public void Removing_with_no_domain_raises_nothing()
    {
        using var rig = new Rig();
        rig.Block.Remove();
        rig.Removed.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Without_actions_it_plays_a_scripted_flow_that_ends_verified_and_says_it_is_simulated()
    {
        using var rig = new Rig(configure: b => b.PollInterval = null);
        var block = rig.Block;

        rig.Await(block.AddDomainAsync(domain: "app.example.com"));
        block.Rows.Select(r => r.Record.Type).ShouldBe(["CNAME", "TXT", "CAA"]);

        rig.Await(block.CheckAsync());
        block.Phase.ShouldBe(DomainVerifyPhase.Invalid);
        block.Rows[2].IsFixShown.ShouldBeTrue();

        rig.Await(block.CheckAsync());
        block.Phase.ShouldBe(DomainVerifyPhase.Verified);
        block.Certificate!.Issuer.ShouldBe("Let's Encrypt");
        block.Announcer!.History.ShouldAllBe(a => a.EndsWith(block.Labels.SimulatedNote));
    }

    [AvaloniaFact]
    public void With_a_VerifyAction_the_announcements_do_not_claim_to_be_simulated()
    {
        using var rig = new Rig(configure: Handlers());
        rig.Await(rig.Block.AddDomainAsync());
        rig.Await(rig.Block.CheckAsync());

        rig.Block.Announcer!.History.ShouldNotBeEmpty();
        rig.Block.Announcer.History.ShouldAllBe(a => !a.Contains(rig.Block.Labels.SimulatedNote));
        rig.Block.Announcer.History.ShouldContain(rig.Block.Labels.VerifiedAnnouncement("shop.example.com"));
    }

    [AvaloniaFact]
    public void Autoplay_starts_on_screen_and_plays_the_flow_to_the_end()
    {
        using var rig = new Rig(configure: b => { b.AutoPlay = null; b.PollInterval = null; b.DefaultDomain = ""; });

        TestHelpers.PumpUntil(() => rig.Block.Phase == DomainVerifyPhase.Verified && rig.Block.Certificate is not null, 5000)
            .ShouldBeTrue();

        rig.Block.Domain.ShouldBe("app.acme.com");
        rig.Phases.ShouldContain(DomainVerifyPhase.Invalid);
    }

    [AvaloniaFact]
    public void Stopping_the_autoplay_leaves_the_card_where_it_is()
    {
        using var rig = new Rig(configure: b => { b.AutoPlay = null; b.PollInterval = null; b.StopAutoPlay(); });

        rig.Await(rig.Block.RunAutoPlayAsync());

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
    }

    [AvaloniaFact]
    public void Autoplay_stays_off_when_an_action_is_set()
    {
        using var rig = new Rig(configure: b =>
        {
            b.AutoPlay = null;
            Handlers()(b);
        });

        rig.Await(rig.Block.RunAutoPlayAsync());

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
    }

    [AvaloniaFact]
    public void A_click_inside_the_card_hands_control_to_the_person()
    {
        // A slow script (a thousand times the usual delays) is still waiting for its first step when the click lands.
        using var rig = new Rig(configure: b => { b.AutoPlay = null; b.SimulationScale = 1000; });

        rig.Window.MouseDown(new Avalonia.Point(40, 20), Avalonia.Input.MouseButton.Left);
        rig.Window.MouseUp(new Avalonia.Point(40, 20), Avalonia.Input.MouseButton.Left);
        rig.Pump();

        rig.Await(rig.Block.RunAutoPlayAsync());
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Enter);
    }

    [AvaloniaFact]
    public void Below_the_compact_width_the_records_stack_and_the_header_row_leaves()
    {
        using var rig = new Rig(width: 700, configure: Handlers());
        rig.Await(rig.Block.AddDomainAsync());
        rig.Block.IsCompact.ShouldBeFalse();
        rig.Block.ColumnHeader!.IsEffectivelyVisible.ShouldBeTrue();
        ValueRow(rig.Block.Rows[0]).ShouldBe(0);

        rig.Host.Width = 400;
        rig.Pump();

        rig.Block.IsCompact.ShouldBeTrue();
        rig.Block.Classes.Contains(DomainVerify.CompactPseudoClass).ShouldBeTrue();
        rig.Block.ColumnHeader.IsEffectivelyVisible.ShouldBeFalse();
        ValueRow(rig.Block.Rows[0]).ShouldBe(1);

        rig.Host.Width = 700;
        rig.Pump();
        rig.Block.IsCompact.ShouldBeFalse();
        ValueRow(rig.Block.Rows[0]).ShouldBe(0);
    }

    // The grid row of the cell that holds a record's value and its copy button.
    private static int ValueRow(DomainVerifyRow row) => Grid.GetRow((Control)row.CopyButton.Parent!);

    [AvaloniaFact]
    public void Rows_and_copy_buttons_have_names_for_screen_readers()
    {
        using var rig = new Rig(configure: Handlers(c => Result(2, DomainRecordStatus.Valid, DomainRecordStatus.Missing, DomainRecordStatus.Invalid)));
        rig.Await(rig.Block.AddDomainAsync());
        rig.Await(rig.Block.CheckAsync());

        AutomationProperties.GetName(rig.Block.Rows[0]).ShouldBe("CNAME shop, Valid");
        AutomationProperties.GetName(rig.Block.Rows[1]).ShouldBe("TXT _verify, Not found");
        AutomationProperties.GetName(rig.Block.Rows[2]).ShouldBe("CAA shop, Misconfigured");
        AutomationProperties.GetName(rig.Block.Rows[1].CopyButton).ShouldBe("Copy TXT value");
        rig.Block.Rows[1].CopyButton.Value.ShouldBe("verify=0123456789abcdef");
        AutomationProperties.GetName(rig.Block.RemoveButton!).ShouldBe("Remove shop.example.com");
    }

    [AvaloniaFact]
    public void Labels_reword_the_card()
    {
        using var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.Labels = new DomainVerifyLabels { AddDomain = "Connect", FooterRecords = "Create these first", Valid = "OK" };
        });

        rig.Block.AddButton!.Content.ShouldBe("Connect");
        rig.Await(rig.Block.AddDomainAsync());
        rig.Block.FooterText.ShouldBe("Create these first");
        rig.Await(rig.Block.CheckAsync());
        rig.Block.Rows[0].StatusText.ShouldBe("OK");
    }

    [AvaloniaFact]
    public void A_null_provider_hides_the_DNS_line_and_a_set_one_shows_in_the_domain_row()
    {
        using var rig = new Rig(configure: Handlers());
        rig.Block.Provider.ShouldNotBeNull();
        rig.Block.Provider!.Name.ShouldBe("Cloudflare");
        rig.Await(rig.Block.AddDomainAsync());
        rig.Block.DomainDetail!.Content.ShouldNotBeNull();

        rig.Block.Provider = null;
        rig.Pump();
        rig.Block.DomainDetail.Content.ShouldBeNull();
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Off)]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Full)]
    public void The_flow_works_at_every_motion_level_and_rows_end_fully_visible(ArcMotionLevel level)
    {
        using var rig = new Rig(level: level, configure: Handlers());
        rig.Await(rig.Block.AddDomainAsync());
        rig.Await(rig.Block.CheckAsync());

        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Verified);
        TestHelpers.PumpUntil(() => rig.Block.Rows.All(r => r.Opacity >= 1), 5000).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Leaving_the_window_cancels_a_running_step_and_returns_to_a_resting_phase()
    {
        CancellationToken seen = default;
        var rig = new Rig(configure: b =>
        {
            Handlers()(b);
            b.VerifyAction = async (_, _, _, ct) =>
            {
                seen = ct;
                await Task.Delay(Timeout.Infinite, ct);
                return Result(0);
            };
        });
        rig.Await(rig.Block.AddDomainAsync());
        var run = rig.Block.CheckAsync();
        TestHelpers.PumpUntil(() => seen.CanBeCanceled).ShouldBeTrue();

        rig.Host.Child = null;
        rig.Await(run);

        seen.IsCancellationRequested.ShouldBeTrue();
        rig.Block.Phase.ShouldBe(DomainVerifyPhase.Records);
        rig.Block.Rows.ShouldAllBe(r => r.State == DomainRowState.NotChecked);
        rig.Failures.ShouldBeEmpty();
        rig.Dispose();
    }

    #endregion
}
