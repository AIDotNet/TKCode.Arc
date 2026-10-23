using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The line under the timeline's title, two stacked layers that crossfade (the one coming in over 160 ms on the enter
/// curve, the one leaving over 100 ms): while the run replays, the status word (Running with a shimmer, Paused, Ready or
/// Live, stacked so the slot keeps the widest word's width) and the counters for time, tokens and cost; once it finishes,
/// a check and the summary (agents · time · speedup · cost). The counters update about twenty times a second and each
/// reserves the width of its final value, right aligned in tabular figures, so the line never shifts while it counts.
/// Groups wrap as wholes with their separator, so a line breaks after a dot, never before. Not a live region; its
/// accessible name is the whole line. Code-driven fades because the incoming and outgoing layers use different durations
/// and curves keyed to one state, which a ControlTheme transition cannot express.
/// </summary>
internal sealed class AgentTimelineStatusLine : Control
{
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<AgentTimelineStatusLine, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<AgentTimelineStatusLine, IBrush?>(nameof(SecondaryBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<AgentTimelineStatusLine, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<AgentTimelineStatusLine, IBrush?>(nameof(SuccessBrush));

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };
    private static readonly TimeSpan EnterFade = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ExitFade = TimeSpan.FromMilliseconds(100);

    private readonly AgentTimelineFlowPanel _counters = new();
    private readonly AgentTimelineFlowPanel _summary = new();
    private readonly MotionValue<double> _countersFade;
    private readonly MotionValue<double> _summaryFade;
    private readonly TextShimmer _running = new() { IsActive = false, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextShimmer _live = new() { IsActive = false, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _paused = Word();
    private readonly TextBlock _ready = Word();
    private readonly Control[] _words;
    private readonly MotionValue<double>[] _wordFades;
    private readonly (TextBlock Value, TextBlock Ghost) _time = Slot();
    private readonly (TextBlock Value, TextBlock Ghost) _tokens = Slot();
    private readonly (TextBlock Value, TextBlock Ghost) _cost = Slot();
    private readonly TextBlock _unit = Plain();
    private readonly List<TextBlock> _separators = new();
    private readonly ArcGlyph _check = new() { Data = ArcIcons.CircleCheck, Size = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    private readonly TextBlock _summaryAgents = Plain();
    private readonly TextBlock _summaryTime = Plain();
    private readonly TextBlock _summarySpeedup = Plain();
    private readonly TextBlock _summaryCost = Plain();
    private bool _showsSummary;
    private int _word = -1;
    private string _countersName = string.Empty;
    private string _summaryName = string.Empty;

    public AgentTimelineStatusLine()
    {
        _words = [_running, _paused, _ready, _live];
        _wordFades = new MotionValue<double>[_words.Length];
        var wordSlot = new Panel();
        for (var i = 0; i < _words.Length; i++)
        {
            var word = _words[i];
            word.Opacity = 0;
            wordSlot.Children.Add(word);
            var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
            fade.Changed += value => word.Opacity = Math.Clamp(value, 0, 1);
            _wordFades[i] = fade;
        }
        _running.FontSize = 14;
        _live.FontSize = 14;

        _counters.Children.Add(Group(wordSlot, separator: true));
        _counters.Children.Add(Group(SlotPanel(_time), separator: true));
        var tokens = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        tokens.Children.Add(SlotPanel(_tokens));
        tokens.Children.Add(_unit);
        _counters.Children.Add(Group(tokens, separator: true));
        _counters.Children.Add(Group(SlotPanel(_cost), separator: false));

        var agents = new StackPanel { Orientation = Orientation.Horizontal };
        agents.Children.Add(_check);
        agents.Children.Add(_summaryAgents);
        _summary.Children.Add(Group(agents, separator: true));
        _summary.Children.Add(Group(_summaryTime, separator: true));
        _summary.Children.Add(Group(_summarySpeedup, separator: true));
        _summary.Children.Add(Group(_summaryCost, separator: false));
        _summaryTime.FontFeatures = s_tabular;
        _summaryCost.FontFeatures = s_tabular;

        _countersFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _summaryFade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _countersFade.Changed += value => SetLayerOpacity(_counters, value);
        _summaryFade.Changed += value => SetLayerOpacity(_summary, value);
        SetLayerOpacity(_counters, 1);
        SetLayerOpacity(_summary, 0);

        VisualChildren.Add(_counters);
        LogicalChildren.Add(_counters);
        VisualChildren.Add(_summary);
        LogicalChildren.Add(_summary);
        ApplyBrushes();
    }

    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    /// <summary>True when the summary layer is the one showing.</summary>
    public bool ShowsSummary => _showsSummary;

    /// <summary>The counters as shown (tests): time, tokens, cost.</summary>
    internal (string Time, string Tokens, string Cost) Counters => (_time.Value.Text ?? "", _tokens.Value.Text ?? "", _cost.Value.Text ?? "");

    /// <summary>The reserved (final) texts of the counters (tests).</summary>
    internal (string Time, string Tokens, string Cost) Finals => (_time.Ghost.Text ?? "", _tokens.Ghost.Text ?? "", _cost.Ghost.Text ?? "");

    /// <summary>The summary layer's texts (tests).</summary>
    internal string SummaryText => string.Join(" · ", _summaryAgents.Text, _summaryTime.Text, _summarySpeedup.Text, _summaryCost.Text);

    /// <summary>The word showing: 0 Running, 1 Paused, 2 Ready, 3 Live, −1 none (tests).</summary>
    internal int WordIndex => _word;

    /// <summary>The words (Running, Paused, Ready, Live) and the token unit.</summary>
    public void SetWords(AgentTimelineLabels labels)
    {
        _running.Text = labels.Running;
        _paused.Text = labels.Paused;
        _ready.Text = labels.Ready;
        _live.Text = labels.Live;
        _unit.Text = labels.TokensUnit;
    }

    /// <summary>Shows the word of <paramref name="phase"/> (none when finished): incoming 160 ms, outgoing 100 ms.</summary>
    public void SetPhase(AgentTimelinePhase phase, bool animate)
    {
        var index = phase switch
        {
            AgentTimelinePhase.Running => 0,
            AgentTimelinePhase.Paused => 1,
            AgentTimelinePhase.Ready => 2,
            AgentTimelinePhase.Live => 3,
            _ => _word
        };
        _running.IsActive = phase == AgentTimelinePhase.Running;
        _live.IsActive = phase == AgentTimelinePhase.Live;
        if (index != _word)
        {
            _word = index;
            for (var i = 0; i < _words.Length; i++)
            {
                Fade(_wordFades[i], i == index ? 1 : 0, animate);
            }
        }
        ShowSummary(phase == AgentTimelinePhase.Finished, animate);
    }

    private void ShowSummary(bool summary, bool animate)
    {
        if (summary == _showsSummary && (_countersFade.Target == (summary ? 0 : 1)))
        {
            return;
        }
        _showsSummary = summary;
        Fade(_countersFade, summary ? 0 : 1, animate);
        Fade(_summaryFade, summary ? 1 : 0, animate);
        UpdateName();
    }

    private static void Fade(MotionValue<double> value, double target, bool animate)
    {
        if (!animate)
        {
            value.Jump(target);
            return;
        }
        if (Math.Abs(value.Target - target) < 1e-9 && !value.IsAnimating && Math.Abs(value.Value - target) < 1e-9)
        {
            return;
        }
        _ = value.AnimateTo(target, target > 0
            ? MotionSpec.Tween(EnterFade, ArcEasings.Enter)
            : MotionSpec.Tween(ExitFade, ArcEasings.Standard));
    }

    private static void SetLayerOpacity(Control layer, double value)
    {
        layer.Opacity = Math.Clamp(value, 0, 1);
        layer.IsHitTestVisible = value > 0.5;
    }

    /// <summary>The counters now (time, tokens, cost) and the sentence screen readers get for the line.</summary>
    public void SetCounters(string time, string tokens, string cost, string name)
    {
        _time.Value.Text = time;
        _tokens.Value.Text = tokens;
        _cost.Value.Text = cost;
        _countersName = name;
        UpdateName();
    }

    /// <summary>The final values whose widths the counters reserve (empty in a live run, where they are not known).</summary>
    public void SetFinals(string time, string tokens, string cost)
    {
        _time.Ghost.Text = time;
        _tokens.Ghost.Text = tokens;
        _cost.Ghost.Text = cost;
    }

    /// <summary>The summary's parts and its accessible sentence (with the "Finished:" prefix).</summary>
    public void SetSummary(string agents, string time, string speedup, string cost, string name)
    {
        _summaryAgents.Text = agents;
        _summaryTime.Text = time;
        _summarySpeedup.Text = speedup;
        _summaryCost.Text = cost;
        _summaryName = name;
        UpdateName();
    }

    private void UpdateName() => AutomationProperties.SetName(this, _showsSummary ? _summaryName : _countersName);

    protected override AutomationPeer OnCreateAutomationPeer() => new StatusPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextBrushProperty || change.Property == SecondaryBrushProperty ||
            change.Property == MutedBrushProperty || change.Property == SuccessBrushProperty)
        {
            ApplyBrushes();
        }
    }

    private void ApplyBrushes()
    {
        foreach (var word in new[] { _paused, _ready, _time.Value, _tokens.Value, _cost.Value, _summaryAgents, _summaryTime, _summarySpeedup, _summaryCost })
        {
            word.Foreground = TextBrush;
        }
        _running.Foreground = TextBrush;
        _live.Foreground = TextBrush;
        _unit.Foreground = SecondaryBrush;
        foreach (var separator in _separators)
        {
            separator.Foreground = MutedBrush;
        }
        _check.Foreground = SuccessBrush;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _counters.Measure(availableSize);
        _summary.Measure(availableSize);
        return new Size(Math.Max(_counters.DesiredSize.Width, _summary.DesiredSize.Width),
            Math.Max(_counters.DesiredSize.Height, _summary.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _counters.Arrange(new Rect(finalSize));
        _summary.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private Control Group(Control content, bool separator)
    {
        if (!separator)
        {
            return content;
        }
        var group = new StackPanel { Orientation = Orientation.Horizontal };
        group.Children.Add(content);
        var dot = Plain();
        dot.Text = "·";
        dot.Margin = new Thickness(3.5, 0, 3.8, 0);
        _separators.Add(dot);
        group.Children.Add(dot);
        return group;
    }

    private static Panel SlotPanel((TextBlock Value, TextBlock Ghost) slot)
    {
        var panel = new Panel();
        panel.Children.Add(slot.Ghost);
        panel.Children.Add(slot.Value);
        return panel;
    }

    private static (TextBlock Value, TextBlock Ghost) Slot()
    {
        var value = Plain();
        value.FontFeatures = s_tabular;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        var ghost = Plain();
        ghost.FontFeatures = s_tabular;
        ghost.Opacity = 0;
        return (value, ghost);
    }

    private static TextBlock Word()
    {
        var word = Plain();
        word.HorizontalAlignment = HorizontalAlignment.Left;
        return word;
    }

    private static TextBlock Plain() => new()
    {
        FontSize = 14,
        LineHeight = 19.6,
        VerticalAlignment = VerticalAlignment.Center
    };

    private sealed class StatusPeer : ControlAutomationPeer
    {
        public StatusPeer(AgentTimelineStatusLine owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>A line of groups that wraps between groups (never inside one), left aligned, lines stacked without a gap.</summary>
internal sealed class AgentTimelineFlowPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        double x = 0;
        double lineHeight = 0;
        double height = 0;
        double widest = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > width + 0.5)
            {
                height += lineHeight;
                widest = Math.Max(widest, x);
                x = 0;
                lineHeight = 0;
            }
            x += size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        widest = Math.Max(widest, x);
        return new Size(double.IsFinite(width) ? Math.Min(widest, width) : widest, height + lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        double y = 0;
        double lineHeight = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width + 0.5)
            {
                y += lineHeight;
                x = 0;
                lineHeight = 0;
            }
            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return finalSize;
    }
}
