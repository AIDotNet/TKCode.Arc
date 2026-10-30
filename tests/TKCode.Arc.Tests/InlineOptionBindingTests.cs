using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests;

/// <summary>
/// Issue #32: the "one item per line" option types written inline in XAML are AvaloniaObjects, so their properties take
/// bindings, and the controls hosting them follow a value that arrives late or changes later.
/// </summary>
public class InlineOptionBindingTests
{
    private const string Namespaces =
        "xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
        "xmlns:arc=\"https://github.com/AIDotNet/TKCode.Arc\" xmlns:generic=\"using:System.Collections.Generic\"";

    /// <summary>A view model whose properties raise change notifications.</summary>
    public sealed class Model : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object?> _values = new()
        {
            [nameof(Text)] = "Bound text",
            [nameof(Value)] = "bound-value",
            [nameof(Flag)] = true,
            [nameof(Number)] = 42d,
            [nameof(Glyph)] = ArcIcons.Heart,
            [nameof(Content)] = "Bound content",
            [nameof(GaugeTone)] = TKCode.Arc.GaugeTone.Warning,
            [nameof(SwapTone)] = ActionSwapTone.Success,
            [nameof(Hold)] = TimeSpan.FromSeconds(3),
            [nameof(Action)] = new AnnouncementAction { Label = "Read more" },
            [nameof(Countdown)] = new AnnouncementCountdown { Label = "Ends in", To = DateTimeOffset.UtcNow.AddDays(1) }
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Text { get => Get<string>(); set => Set(value); }
        public string Value { get => Get<string>(); set => Set(value); }
        public bool Flag { get => Get<bool>(); set => Set(value); }
        public double Number { get => Get<double>(); set => Set(value); }
        public Geometry Glyph { get => Get<Geometry>(); set => Set(value); }
        public object Content { get => Get<object>(); set => Set(value); }
        public GaugeTone GaugeTone { get => Get<GaugeTone>(); set => Set(value); }
        public ActionSwapTone SwapTone { get => Get<ActionSwapTone>(); set => Set(value); }
        public TimeSpan Hold { get => Get<TimeSpan>(); set => Set(value); }
        public AnnouncementAction Action { get => Get<AnnouncementAction>(); set => Set(value); }
        public AnnouncementCountdown Countdown { get => Get<AnnouncementCountdown>(); set => Set(value); }

        /// <summary>The model value a bound option property reads.</summary>
        public object? this[string name] => _values[name];

        private T Get<T>([CallerMemberName] string name = "") => (T)_values[name]!;

        private void Set(object? value, [CallerMemberName] string name = "")
        {
            _values[name] = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>One option type: its host with one inline option, and which model property each option property reads.</summary>
    private sealed record Case(string Xaml, Func<Control, AvaloniaObject> Option, (AvaloniaProperty Property, string Source)[] Bindings);

    private static readonly Dictionary<string, Case> s_cases = new()
    {
        ["SegmentedOption"] = new(
            """<arc:SegmentedControl><arc:SegmentedOption Value="{Binding Value}" Label="{Binding Text}" Accessory="{Binding Content}" IsDisabled="{Binding Flag}" /></arc:SegmentedControl>""",
            root => (AvaloniaObject)Find<SegmentedControl>(root).Items[0]!,
            [(SegmentedOption.ValueProperty, "Value"), (SegmentedOption.LabelProperty, "Text"), (SegmentedOption.AccessoryProperty, "Content"), (SegmentedOption.IsDisabledProperty, "Flag")]),
        ["RadioOption"] = new(
            """<arc:RadioGroup Header="Plan"><arc:RadioOption Value="{Binding Value}" Label="{Binding Text}" Description="{Binding Text}" IsDisabled="{Binding Flag}" /></arc:RadioGroup>""",
            root => (AvaloniaObject)Find<RadioGroup>(root).Items[0]!,
            [(RadioOption.ValueProperty, "Value"), (RadioOption.LabelProperty, "Text"), (RadioOption.DescriptionProperty, "Text"), (RadioOption.IsDisabledProperty, "Flag")]),
        ["RadioCardOption"] = new(
            """<arc:RadioCards><arc:RadioCardOption Value="{Binding Value}" Label="{Binding Text}" Description="{Binding Text}" Meta="{Binding Content}" Icon="{Binding Glyph}" IsDisabled="{Binding Flag}" DisabledReason="{Binding Text}" /></arc:RadioCards>""",
            root => (AvaloniaObject)Find<RadioCards>(root).Items[0]!,
            [(RadioCardOption.ValueProperty, "Value"), (RadioCardOption.LabelProperty, "Text"), (RadioCardOption.DescriptionProperty, "Text"), (RadioCardOption.MetaProperty, "Content"),
             (RadioCardOption.IconProperty, "Glyph"), (RadioCardOption.IsDisabledProperty, "Flag"), (RadioCardOption.DisabledReasonProperty, "Text")]),
        ["BillingToggleOption"] = new(
            """<arc:BillingToggle><arc:BillingToggleOption Value="{Binding Value}" Label="{Binding Text}" Badge="{Binding Text}" ActiveBadge="{Binding Text}" /></arc:BillingToggle>""",
            root => (AvaloniaObject)Find<BillingToggle>(root).Items[0]!,
            [(BillingToggleOption.ValueProperty, "Value"), (BillingToggleOption.LabelProperty, "Text"), (BillingToggleOption.BadgeProperty, "Text"), (BillingToggleOption.ActiveBadgeProperty, "Text")]),
        ["FilterField"] = new(
            """<arc:FilterToolbar><arc:FilterField Id="{Binding Value}" Label="{Binding Text}" Icon="{Binding Glyph}"><arc:FilterOption Value="open" /></arc:FilterField></arc:FilterToolbar>""",
            root => Find<FilterToolbar>(root).Fields[0],
            [(FilterField.IdProperty, "Value"), (FilterField.LabelProperty, "Text"), (FilterField.IconProperty, "Glyph")]),
        ["FilterOption"] = new(
            """<arc:FilterToolbar><arc:FilterField Id="status" Label="Status"><arc:FilterOption Value="{Binding Value}" Label="{Binding Text}" Hint="{Binding Text}" Icon="{Binding Glyph}" /></arc:FilterField></arc:FilterToolbar>""",
            root => Find<FilterToolbar>(root).Fields[0].Options[0],
            [(FilterOption.ValueProperty, "Value"), (FilterOption.LabelProperty, "Text"), (FilterOption.HintProperty, "Text"), (FilterOption.IconProperty, "Glyph")]),
        ["ReactionOption"] = new(
            """<arc:Reactions><arc:ReactionOption Id="{Binding Value}" Label="{Binding Text}" Icon="{Binding Glyph}" /></arc:Reactions>""",
            root => (AvaloniaObject)Find<Reactions>(root).Options[0],
            [(ReactionOption.IdProperty, "Value"), (ReactionOption.LabelProperty, "Text"), (ReactionOption.IconProperty, "Glyph")]),
        ["SliderMark"] = new(
            """<arc:Slider><arc:Slider.Marks><arc:SliderMark Value="{Binding Number}" Label="{Binding Text}" /></arc:Slider.Marks></arc:Slider>""",
            root => Find<Slider>(root).Marks[0],
            [(SliderMark.ValueProperty, "Number"), (SliderMark.LabelProperty, "Text")]),
        ["ElasticSliderMark"] = new(
            """<arc:ElasticSlider><arc:ElasticSlider.Marks><arc:SliderMark Value="{Binding Number}" Label="{Binding Text}" /></arc:ElasticSlider.Marks></arc:ElasticSlider>""",
            root => Find<ElasticSlider>(root).Marks[0],
            [(SliderMark.ValueProperty, "Number"), (SliderMark.LabelProperty, "Text")]),
        ["GaugeThreshold"] = new(
            """<arc:Gauge Value="50"><arc:Gauge.Thresholds><arc:GaugeThreshold From="{Binding Number}" Tone="{Binding GaugeTone}" Label="{Binding Text}" /></arc:Gauge.Thresholds></arc:Gauge>""",
            root => Find<Gauge>(root).Thresholds[0],
            [(GaugeThreshold.FromProperty, "Number"), (GaugeThreshold.ToneProperty, "GaugeTone"), (GaugeThreshold.LabelProperty, "Text")]),
        ["ActionSwapState"] = new(
            """<arc:ActionSwap><arc:ActionSwap.States><arc:ActionSwapState Id="{Binding Value}" Label="{Binding Text}" Icon="{Binding Glyph}" Tone="{Binding SwapTone}" PendingLabel="{Binding Text}" HoldFor="{Binding Hold}" /><arc:ActionSwapState Id="other" Label="Other" /></arc:ActionSwap.States></arc:ActionSwap>""",
            root => Find<ActionSwap>(root).States[0],
            [(ActionSwapState.IdProperty, "Value"), (ActionSwapState.LabelProperty, "Text"), (ActionSwapState.IconProperty, "Glyph"), (ActionSwapState.ToneProperty, "SwapTone"),
             (ActionSwapState.PendingLabelProperty, "Text"), (ActionSwapState.HoldForProperty, "Hold")]),
        ["Announcement"] = new(
            """<arc:AnnouncementBar><arc:Announcement Id="{Binding Value}" Message="{Binding Text}" Action="{Binding Action}" Countdown="{Binding Countdown}" /></arc:AnnouncementBar>""",
            root => Find<AnnouncementBar>(root).Messages[0],
            [(Announcement.IdProperty, "Value"), (Announcement.MessageProperty, "Text"), (Announcement.ActionProperty, "Action"), (Announcement.CountdownProperty, "Countdown")]),
        ["ChatParticipant"] = new(
            """<arc:ChatThread><arc:ChatThread.Participants><generic:List x:TypeArguments="arc:ChatParticipant"><arc:ChatParticipant Id="{Binding Value}" Name="{Binding Text}" Avatar="{Binding Text}" /></generic:List></arc:ChatThread.Participants></arc:ChatThread>""",
            root => Find<ChatThread>(root).Participants!.First(),
            [(ChatParticipant.IdProperty, "Value"), (ChatParticipant.NameProperty, "Text"), (ChatParticipant.AvatarProperty, "Text")]),
        ["HoverCardStat"] = new(
            """<arc:HoverCardProfile DisplayName="Mina"><arc:HoverCardProfile.Stats><arc:HoverCardStat Label="{Binding Text}" Value="{Binding Value}" /></arc:HoverCardProfile.Stats></arc:HoverCardProfile>""",
            root => Find<HoverCardProfile>(root).Stats[0],
            [(HoverCardStat.LabelProperty, "Text"), (HoverCardStat.ValueProperty, "Value")]),
    };

    public static TheoryData<string> Cases() => new(s_cases.Keys);

    private static T Find<T>(Control root) where T : Control =>
        root as T ?? root.GetLogicalDescendants().OfType<T>().First();

    private static Border Load(string xaml) => (Border)AvaloniaRuntimeXamlLoader.Load($"<Border {Namespaces}>{xaml}</Border>");

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public void Every_Option_Property_Takes_A_Binding_From_The_Hosts_DataContext(string name)
    {
        var test = s_cases[name];
        var root = Load(test.Xaml);
        var option = test.Option(root);
        // Without a DataContext nothing is stored in place of a value (an object property used to keep the binding itself).
        foreach (var (property, _) in test.Bindings)
        {
            option.GetValue(property).ShouldNotBeAssignableTo<BindingBase>();
        }
        var model = new Model();
        // The DataContext arrives after the XAML was loaded.
        root.DataContext = model;
        var window = TestHelpers.Show(root);
        try
        {
            foreach (var (property, source) in test.Bindings)
            {
                option.GetValue(property).ShouldBe(model[source], $"{name}.{property.Name}");
            }
            model.Text = "Changed text";
            model.Value = "changed-value";
            Dispatcher.UIThread.RunJobs();
            foreach (var (property, source) in test.Bindings.Where(b => b.Source is "Text" or "Value"))
            {
                option.GetValue(property).ShouldBe(model[source], $"{name}.{property.Name} after a change");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Compiled_Binding_Path_Is_Not_Rejected_For_String_Properties()
    {
        // The issue's repro: before, the XAML compiler found no setter taking a binding for RadioOption.Label (AVLN3000),
        // and the runtime loader failed the same way. A DynamicResource takes the same path.
        var root = (Border)AvaloniaRuntimeXamlLoader.Load(
            $$"""
             <Border {{Namespaces}}>
                 <Border.Resources><x:String x:Key="PlanName">Pro plan</x:String></Border.Resources>
                 <arc:RadioGroup Header="Plan">
                     <arc:RadioOption Value="pro" Label="{DynamicResource PlanName}" />
                 </arc:RadioGroup>
             </Border>
             """);
        var window = TestHelpers.Show(root);
        try
        {
            ((RadioOption)Find<RadioGroup>(root).Items[0]!).Label.ShouldBe("Pro plan");
        }
        finally
        {
            window.Close();
        }
    }

    // --- The hosts follow bound values ---

    private static Border Host(string xaml, Model model)
    {
        var root = Load(xaml);
        root.DataContext = model;
        return root;
    }

    [AvaloniaFact]
    public void RadioGroup_Rows_Follow_Label_Description_And_IsDisabled()
    {
        var model = new Model { Flag = false };
        var root = Host("""<arc:RadioGroup Header="Plan"><arc:RadioOption Value="hobby" Label="Hobby" /><arc:RadioOption Value="{Binding Value}" Label="{Binding Text}" Description="{Binding Content}" IsDisabled="{Binding Flag}" /></arc:RadioGroup>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var group = Find<RadioGroup>(root);
            var row = group.ContainerFromIndex(1).ShouldBeOfType<RadioGroupItem>();
            row.Content.ShouldBe("Bound text");
            row.Description.ShouldBe("Bound content");
            row.IsEnabled.ShouldBeTrue();
            model.Text = "Pro";
            model.Content = "For growing teams";
            model.Flag = true;
            Dispatcher.UIThread.RunJobs();
            row.Content.ShouldBe("Pro");
            row.Description.ShouldBe("For growing teams");
            row.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedValue_Follows_A_Bound_Value_That_Changes()
    {
        var model = new Model { Value = "pro" };
        var root = Host("""<arc:RadioGroup Header="Plan"><arc:RadioOption Value="hobby" Label="Hobby" /><arc:RadioOption Value="{Binding Value}" Label="Pro" /></arc:RadioGroup>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var group = Find<RadioGroup>(root);
            group.SelectedIndex = 1;
            group.SelectedValue.ShouldBe("pro");
            model.Value = "pro-2026";
            Dispatcher.UIThread.RunJobs();
            group.SelectedIndex.ShouldBe(1);
            group.SelectedValue.ShouldBe("pro-2026");
        }
        finally
        {
            window.Close();
        }
    }

    public sealed class PlanModel : INotifyPropertyChanged
    {
        private string? _plan = "pro";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Hobby => "hobby";
        public string Pro => "pro";

        public string? Plan
        {
            get => _plan;
            set
            {
                _plan = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Plan)));
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Bound_SelectedValue_Selects_An_Option_Whose_Value_Is_Bound_Too(bool contextAfterShow)
    {
        var model = new PlanModel();
        var root = Load("""<arc:RadioGroup Header="Plan" SelectedValue="{Binding Plan}"><arc:RadioOption Value="{Binding Hobby}" Label="Hobby" /><arc:RadioOption Value="{Binding Pro}" Label="Pro" /></arc:RadioGroup>""");
        var group = Find<RadioGroup>(root);
        if (!contextAfterShow)
        {
            root.DataContext = model;
        }
        var window = TestHelpers.Show(root);
        try
        {
            if (contextAfterShow)
            {
                root.DataContext = model;
                Dispatcher.UIThread.RunJobs();
            }
            group.SelectedIndex.ShouldBe(1);
            model.Plan.ShouldBe("pro");
            group.SelectedIndex = 0;
            model.Plan.ShouldBe("hobby");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SegmentedControl_Segments_Follow_Label_Accessory_And_IsDisabled()
    {
        var model = new Model { Flag = false, Text = "Day" };
        var root = Host("""<arc:SegmentedControl><arc:SegmentedOption Value="day" Label="{Binding Text}" Accessory="{Binding Content}" IsDisabled="{Binding Flag}" /><arc:SegmentedOption Value="week" Label="Week" /></arc:SegmentedControl>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var segment = Find<SegmentedControl>(root).ContainerFromIndex(0).ShouldBeOfType<SegmentedItem>();
            segment.Content.ShouldBe("Day");
            segment.Accessory.ShouldBe("Bound content");
            model.Text = "日";
            model.Content = "3";
            model.Flag = true;
            Dispatcher.UIThread.RunJobs();
            segment.Content.ShouldBe("日");
            segment.Accessory.ShouldBe("3");
            segment.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RadioCards_Follow_Label_Meta_And_DisabledReason()
    {
        var model = new Model { Flag = false };
        var root = Host("""<arc:RadioCards><arc:RadioCardOption Value="pro" Label="{Binding Text}" Meta="{Binding Content}" Icon="{Binding Glyph}" DisabledReason="{Binding Value}" IsDisabled="{Binding Flag}" /></arc:RadioCards>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var card = Find<RadioCards>(root).ContainerFromIndex(0).ShouldBeOfType<RadioCard>();
            card.Content.ShouldBe("Bound text");
            card.Meta.ShouldBe("Bound content");
            var glyph = card.Icon.ShouldBeOfType<ArcGlyph>();
            glyph.Data.ShouldBeSameAs(ArcIcons.Heart);
            model.Text = "Scale";
            model.Content = "$99";
            model.Flag = true;
            Dispatcher.UIThread.RunJobs();
            card.Content.ShouldBe("Scale");
            card.Meta.ShouldBe("$99");
            card.DisabledReason.ShouldBe("bound-value");
            card.IsEnabled.ShouldBeFalse();
            // The icon did not change, so the card keeps the glyph it shows.
            card.Icon.ShouldBeSameAs(glyph);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BillingToggle_Options_Follow_Label_And_Badge()
    {
        var model = new Model { Text = "Yearly" };
        var root = Host("""<arc:BillingToggle><arc:BillingToggleOption Value="monthly" Label="Monthly" /><arc:BillingToggleOption Value="yearly" Label="{Binding Text}" Badge="{Binding Content}" /></arc:BillingToggle>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var item = Find<BillingToggle>(root).ContainerFromIndex(1).ShouldBeOfType<BillingToggleItem>();
            item.Content.ShouldBe("Yearly");
            item.Badge.ShouldBe("Bound content");
            model.Text = "按年";
            model.Content = "省 20%";
            Dispatcher.UIThread.RunJobs();
            item.Content.ShouldBe("按年");
            item.Badge.ShouldBe("省 20%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BillingToggle_Default_Options_Are_Not_Shared_Between_Toggles()
    {
        BillingToggle.DefaultOptions[0].ShouldNotBeSameAs(BillingToggle.DefaultOptions[0]);
        var first = new BillingToggle();
        var second = new BillingToggle();
        var window = TestHelpers.Show(new StackPanel { Children = { first, second } });
        try
        {
            first.Items[0].ShouldNotBeSameAs(second.Items[0]);
            ((BillingToggleOption)first.Items[1]!).Badge.ShouldBe("Save 20%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Open_FilterMenu_Follows_Field_And_Option_Labels()
    {
        var model = new Model { Text = "Status" };
        var root = Host("""<arc:FilterToolbar><arc:FilterField Id="status" Label="{Binding Text}"><arc:FilterOption Value="open" Label="{Binding Content}" /><arc:FilterOption Value="closed" /></arc:FilterField></arc:FilterToolbar>""", model);
        model.Content = "Open";
        var window = TestHelpers.Show(root);
        try
        {
            var menu = TestHelpers.Descendant<FilterMenu>(Find<FilterToolbar>(root));
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.CurrentList!.Rows.Select(r => r.Label).ShouldBe(["Status"]);
            model.Text = "状态";
            Dispatcher.UIThread.RunJobs();
            menu.CurrentList!.Rows.Select(r => r.Label).ShouldBe(["状态"]);
            menu.CurrentList.Chosen!(menu.CurrentList.Rows[0], false);
            Dispatcher.UIThread.RunJobs();
            menu.CurrentField!.Id.ShouldBe("status");
            menu.CurrentList!.Rows.Select(r => r.Label).ShouldBe(["Open", "closed"]);
            model.Content = "打开";
            Dispatcher.UIThread.RunJobs();
            menu.CurrentList!.Rows.Select(r => r.Label).ShouldBe(["打开", "closed"]);
            menu.Close();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reactions_Pills_Follow_The_Option_Label()
    {
        var model = new Model { Text = "Like" };
        var root = Host("""<arc:Reactions><arc:ReactionOption Id="like" Label="{Binding Text}" Icon="{x:Static arc:ArcIcons.ThumbsUp}" /></arc:Reactions>""", model);
        var reactions = Find<Reactions>(root);
        reactions.Value = [new Reaction("like", 3)];
        var window = TestHelpers.Show(root);
        try
        {
            var pill = TestHelpers.Descendant<ReactionsPill>(reactions);
            Tooltip.GetTip(pill).ShouldBe("Like");
            model.Text = "赞";
            Dispatcher.UIThread.RunJobs();
            pill = TestHelpers.Descendant<ReactionsPill>(reactions);
            Tooltip.GetTip(pill).ShouldBe("赞");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slider_Mark_Labels_Follow_A_Bound_Label()
    {
        var model = new Model { Text = "" };
        var root = Host("""<arc:Slider Width="300"><arc:Slider.Marks><arc:SliderMark Value="50" Label="{Binding Text}" /></arc:Slider.Marks></arc:Slider>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var slider = Find<Slider>(root);
            // An empty label is a bare tick: no row of labels under the track.
            slider.Classes.Contains(":marks").ShouldBeFalse();
            model.Text = "Half";
            Dispatcher.UIThread.RunJobs();
            slider.Classes.Contains(":marks").ShouldBeTrue();
            TestHelpers.Descendant<SliderMarkLabels>(slider).HasLabels.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Gauge_State_Label_Follows_A_Bound_Threshold()
    {
        var model = new Model { Text = "Healthy", Number = 0 };
        var root = Host("""<arc:Gauge Value="50" Label="Disk"><arc:Gauge.Thresholds><arc:GaugeThreshold From="{Binding Number}" Label="{Binding Text}" /><arc:GaugeThreshold From="80" Tone="Danger" Label="Critical" /></arc:Gauge.Thresholds></arc:Gauge>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var gauge = Find<Gauge>(root);
            TestHelpers.PumpUntil(() => gauge.StatusText == "Healthy").ShouldBeTrue();
            model.Text = "正常";
            Dispatcher.UIThread.RunJobs();
            gauge.StatusText.ShouldBe("正常");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ActionSwap_Follows_The_Shown_States_Label()
    {
        var model = new Model { Text = "Send invite" };
        var root = Host("""<arc:ActionSwap><arc:ActionSwap.States><arc:ActionSwapState Id="idle" Label="{Binding Text}" /><arc:ActionSwapState Id="sent" Label="Sent" /></arc:ActionSwap.States></arc:ActionSwap>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var swap = Find<ActionSwap>(root);
            var surface = TestHelpers.Descendant<ActionSwapSurface>(swap);
            surface.Text.ShouldBe("Send invite");
            model.Text = "发送邀请";
            Dispatcher.UIThread.RunJobs();
            surface.Text.ShouldBe("发送邀请");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AnnouncementBar_Shows_A_Changed_Message_In_Place()
    {
        var model = new Model { Text = "Version 2 is out" };
        var root = Host("""<arc:AnnouncementBar><arc:Announcement Id="v2" Message="{Binding Text}" /><arc:Announcement Id="sale" Message="Sale" /></arc:AnnouncementBar>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var bar = Find<AnnouncementBar>(root);
            bar.Viewport!.CurrentFace!.MessageBlock.Text.ShouldBe("Version 2 is out");
            var face = bar.Viewport.CurrentFace;
            model.Text = "第 2 版已发布";
            Dispatcher.UIThread.RunJobs();
            bar.Viewport.CurrentFace.ShouldBeSameAs(face);
            face.MessageBlock.Text.ShouldBe("第 2 版已发布");
            bar.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ChatThread_Rows_Follow_A_Participants_Name()
    {
        var model = new Model { Text = "Sam Rivera" };
        var root = Host("""<arc:ChatThread CurrentUserId="me" Height="300"><arc:ChatThread.Participants><generic:List x:TypeArguments="arc:ChatParticipant"><arc:ChatParticipant Id="sam" Name="{Binding Text}" /></generic:List></arc:ChatThread.Participants></arc:ChatThread>""", model);
        var thread = Find<ChatThread>(root);
        thread.ItemsSource = new[] { new ChatMessage { Id = "m1", AuthorId = "sam", Text = "Hi", CreatedAt = DateTimeOffset.Now } };
        var window = TestHelpers.Show(root);
        try
        {
            string? Author() => thread.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == ChatThreadMessageView.AuthorPart)?.Text;
            TestHelpers.PumpUntil(() => Author() == "Sam Rivera").ShouldBeTrue();
            model.Text = "萨姆";
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => Author() == "萨姆").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HoverCardProfile_Stats_Follow_Bound_Values()
    {
        var model = new Model { Text = "Followers", Value = "1.2k" };
        var root = Host("""<arc:HoverCardProfile DisplayName="Mina"><arc:HoverCardProfile.Stats><arc:HoverCardStat Label="{Binding Text}" Value="{Binding Value}" /></arc:HoverCardProfile.Stats></arc:HoverCardProfile>""", model);
        var window = TestHelpers.Show(root);
        try
        {
            var profile = Find<HoverCardProfile>(root);
            IEnumerable<string?> Texts() => profile.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);
            Texts().ShouldContain("Followers");
            Texts().ShouldContain("1.2k");
            model.Text = "关注者";
            model.Value = "1.3k";
            Dispatcher.UIThread.RunJobs();
            Texts().ShouldContain("关注者");
            Texts().ShouldContain("1.3k");
        }
        finally
        {
            window.Close();
        }
    }

    // --- Lifetime and unchanged types ---

    [AvaloniaFact]
    public void Options_Changed_While_The_Group_Was_Away_Show_When_It_Returns()
    {
        var option = new SegmentedOption("day", "Day");
        var control = new SegmentedControl { ItemsSource = new[] { option, new SegmentedOption("week", "Week") } };
        var host = new ContentControl { Content = control };
        var window = TestHelpers.Show(host);
        try
        {
            host.Content = null;
            Dispatcher.UIThread.RunJobs();
            option.Label = "Today";
            host.Content = control;
            Dispatcher.UIThread.RunJobs();
            control.ContainerFromIndex(0).ShouldBeOfType<SegmentedItem>().Content.ShouldBe("Today");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_View_Models_Options_Do_Not_Keep_A_Closed_View_Alive()
    {
        var options = new[] { new RadioOption("a", "A"), new RadioOption("b", "B") };
        var marks = new Avalonia.Collections.AvaloniaList<SliderMark> { new(10, "Ten") };
        var group = TestHelpers.ShowAndClose(() => new RadioGroup { ItemsSource = options });
        var slider = TestHelpers.ShowAndClose(() => new Slider { Marks = marks });
        TestHelpers.Collect(group, slider);
        GC.KeepAlive(options);
        GC.KeepAlive(marks);
    }

    [Fact]
    public void FilterChip_Keeps_Value_Equality()
    {
        var a = new FilterChip("status", "Status", "Open");
        var b = new FilterChip("status", "Status", "Open");
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(new FilterChip("status", "Status", "Closed"));
        typeof(AvaloniaObject).IsAssignableFrom(typeof(FilterChip)).ShouldBeFalse();
    }
}
