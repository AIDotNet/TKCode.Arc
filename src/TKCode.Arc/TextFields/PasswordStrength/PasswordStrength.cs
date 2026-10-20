using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PasswordStrength.StrengthChanged"/>.</summary>
public sealed class PasswordStrengthChangedEventArgs : RoutedEventArgs
{
    public PasswordStrengthChangedEventArgs(RoutedEvent routedEvent, PasswordStrengthScore score, string label)
        : base(routedEvent)
    {
        Score = score;
        Label = label;
    }

    /// <summary>The new score.</summary>
    public PasswordStrengthScore Score { get; }

    /// <summary>The strength word shown beside the meter ("Fair"), empty for an empty field.</summary>
    public string Label { get; }

    /// <summary>Level 0 (empty) to 4 (strong).</summary>
    public int Level => Score.Level;
}

/// <summary>
/// Arc password strength: a new-password field that shows how strong the password is while it is typed. Four
/// segments fill in a staggered wave and change tone, the strength word rises when strength improves and drops
/// when it falls, each requirement checks off with a drawn tick (unmet length rules show a rolling "4 more"), and
/// a new <see cref="FieldTextBox.ErrorMessage"/> shakes the field once. Scoring runs on the device through a
/// pluggable <see cref="Scorer"/> (default: <see cref="PasswordStrengthEstimator"/>, synchronous and cheap).
/// </summary>
[TemplatePart(MeterPart, typeof(Control))]
[TemplatePart(WordPart, typeof(Control))]
[TemplatePart(RulesPart, typeof(Panel))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(":level-0", ":level-1", ":level-2", ":level-3", ":level-4")]
public class PasswordStrength : PasswordField
{
    public const string MeterPart = "PART_Meter";
    public const string WordPart = "PART_Word";
    public const string RulesPart = "PART_Rules";
    public const string StatusPart = "PART_Status";

    /// <summary>Requirements to check. Strength is the share of rules met, spread over four steps.</summary>
    public static readonly StyledProperty<IReadOnlyList<PasswordRule>> RulesProperty =
        AvaloniaProperty.Register<PasswordStrength, IReadOnlyList<PasswordRule>>(nameof(Rules), PasswordRules.Default);

    /// <summary>Scores the password; null uses <see cref="PasswordStrengthEstimator"/> with <see cref="ShortLength"/>.</summary>
    public static readonly StyledProperty<IPasswordStrengthScorer?> ScorerProperty =
        AvaloniaProperty.Register<PasswordStrength, IPasswordStrengthScorer?>(nameof(Scorer));

    /// <summary>Below this many characters the meter holds at its first step and reads "Too short". Default 8.</summary>
    public static readonly StyledProperty<int> ShortLengthProperty =
        AvaloniaProperty.Register<PasswordStrength, int>(nameof(ShortLength), 8);

    /// <summary>How characters are counted for <see cref="ShortLength"/>; null counts code points.</summary>
    public static readonly StyledProperty<Func<string, int>?> CharacterCounterProperty =
        AvaloniaProperty.Register<PasswordStrength, Func<string, int>?>(nameof(CharacterCounter));

    /// <summary>Every user-facing string (English defaults).</summary>
    public static readonly StyledProperty<PasswordStrengthMessages> MessagesProperty =
        AvaloniaProperty.Register<PasswordStrength, PasswordStrengthMessages>(nameof(Messages), new PasswordStrengthMessages());

    /// <summary>The current level, 0 (empty) to 4 (strong).</summary>
    public static readonly DirectProperty<PasswordStrength, int> LevelProperty =
        AvaloniaProperty.RegisterDirect<PasswordStrength, int>(nameof(Level), o => o.Level);

    /// <summary>The current strength word.</summary>
    public static readonly DirectProperty<PasswordStrength, string> StrengthLabelProperty =
        AvaloniaProperty.RegisterDirect<PasswordStrength, string>(nameof(StrengthLabel), o => o.StrengthLabel);

    /// <summary>Raised after every scored change of the password.</summary>
    public static readonly RoutedEvent<PasswordStrengthChangedEventArgs> StrengthChangedEvent =
        RoutedEvent.Register<PasswordStrength, PasswordStrengthChangedEventArgs>(nameof(StrengthChanged), RoutingStrategies.Bubble);

    public IReadOnlyList<PasswordRule> Rules { get => GetValue(RulesProperty); set => SetValue(RulesProperty, value); }
    public IPasswordStrengthScorer? Scorer { get => GetValue(ScorerProperty); set => SetValue(ScorerProperty, value); }
    public int ShortLength { get => GetValue(ShortLengthProperty); set => SetValue(ShortLengthProperty, value); }
    public Func<string, int>? CharacterCounter { get => GetValue(CharacterCounterProperty); set => SetValue(CharacterCounterProperty, value); }
    public PasswordStrengthMessages Messages { get => GetValue(MessagesProperty); set => SetValue(MessagesProperty, value); }

    private int _level;

    /// <summary>The current level, 0 (empty) to 4 (strong).</summary>
    public int Level
    {
        get => _level;
        private set => SetAndRaise(LevelProperty, ref _level, value);
    }

    private string _strengthLabel = string.Empty;

    /// <summary>The current strength word.</summary>
    public string StrengthLabel
    {
        get => _strengthLabel;
        private set => SetAndRaise(StrengthLabelProperty, ref _strengthLabel, value);
    }

    /// <summary>The last score.</summary>
    public PasswordStrengthScore Score { get; private set; } = new(0, Array.Empty<string>(), false);

    /// <summary>Raised after every scored change of the password.</summary>
    public event EventHandler<PasswordStrengthChangedEventArgs>? StrengthChanged
    {
        add => AddHandler(StrengthChangedEvent, value);
        remove => RemoveHandler(StrengthChangedEvent, value);
    }

    private PasswordStrengthMeter? _meter;
    private FieldSwapText? _word;
    private Panel? _rulesPanel;
    private TextBlock? _status;
    private readonly List<RuleRow> _rows = new();
    private CancellationTokenSource? _scoring;
    private double _rank;
    private int _trackLevel;
    private int _previousLevel;
    private int _direction = 1;
    private HashSet<string> _met = new(StringComparer.Ordinal);
    private MotionValue<double>? _shake;
    private readonly TranslateTransform _shakeTransform = new();
    private string? _lastError;

    private sealed class RuleRow
    {
        public required PasswordRule Rule { get; init; }
        public required Grid Root { get; init; }
        public required PasswordRuleMark Mark { get; init; }
        public required TextBlock Label { get; init; }
        public required PresenceHost RemainingPresence { get; init; }
        public required RollingDigits Remaining { get; init; }
        public IDisposable? LabelBrush { get; set; }
        public bool? ShownMet { get; set; }
        public Transitions? LabelTransitions { get; set; }
    }

    protected override string ToggleName => Messages.ShowPassword;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _meter = e.NameScope.Find<PasswordStrengthMeter>(MeterPart);
        _word = e.NameScope.Find<FieldSwapText>(WordPart);
        _rulesPanel = e.NameScope.Find<Panel>(RulesPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (Frame is { } frame)
        {
            frame.RenderTransform = _shakeTransform;
        }
        if (_meter is not null)
        {
            AutomationProperties.SetName(_meter, Messages.MeterLabel);
        }
        BuildRules();
        ApplyScore(Score, initial: true);
        _ = ScoreAsync(Text ?? string.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == ScorerProperty ||
            change.Property == ShortLengthProperty || change.Property == CharacterCounterProperty)
        {
            _ = ScoreAsync(Text ?? string.Empty);
        }
        else if (change.Property == RulesProperty)
        {
            BuildRules();
            _met.Clear();
            _ = ScoreAsync(Text ?? string.Empty);
        }
        else if (change.Property == MessagesProperty)
        {
            if (_meter is not null)
            {
                AutomationProperties.SetName(_meter, Messages.MeterLabel);
            }
            BuildRules();
            ApplyScore(Score, initial: true);
        }
        else if (change.Property == EffectiveErrorMessageProperty)
        {
            Shake(EffectiveErrorMessage);
        }
    }

    private IPasswordStrengthScorer EffectiveScorer => Scorer ?? (ShortLength == 8 && CharacterCounter is null
        ? PasswordStrengthEstimator.Default
        : new PasswordStrengthEstimator { ShortLength = ShortLength, CountCharacters = CharacterCounter ?? PasswordRules.CountCodePoints });

    private async Task ScoreAsync(string password)
    {
        _scoring?.Cancel();
        var task = EffectiveScorer.ScoreAsync(password, Rules, CancellationToken.None);
        if (task.IsCompletedSuccessfully)
        {
            ApplyScore(task.Result, initial: false);
            return;
        }
        var cts = new CancellationTokenSource();
        _scoring = cts;
        try
        {
            var score = await task;
            if (!cts.IsCancellationRequested)
            {
                ApplyScore(score, initial: false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyScore(PasswordStrengthScore score, bool initial)
    {
        var changedScore = score.Level != Score.Level || score.IsShort != Score.IsShort || !score.MetRuleIds.SequenceEqual(Score.MetRuleIds);
        Score = score;
        var messages = Messages;
        var label = score.Level > 0 ? messages.LevelText(score.Level, score.IsShort) : string.Empty;

        // The previous level and rule states tell which way the word moves and which ticks join the wave.
        var rank = score.IsShort ? 0.5 : score.Level;
        if (Math.Abs(rank - _rank) > 0.01)
        {
            _previousLevel = _trackLevel;
            _direction = rank > _rank ? 1 : -1;
            _rank = rank;
        }
        _trackLevel = score.Level;
        var met = new HashSet<string>(score.MetRuleIds, StringComparer.Ordinal);
        var changed = new List<string>();
        foreach (var row in _rows)
        {
            if (met.Contains(row.Rule.Id) != _met.Contains(row.Rule.Id))
            {
                changed.Add(row.Rule.Id);
            }
        }
        _met = met;

        Level = score.Level;
        StrengthLabel = label;
        for (var level = 0; level <= 4; level++)
        {
            PseudoClasses.Set($":level-{level}", score.Level == level);
        }
        _meter?.SetLevel(score.Level, _previousLevel, score.Level > 0 ? label : messages.NoPassword);
        if (_word is not null)
        {
            _word.Direction = _direction;
            _word.Text = label;
        }
        var password = Text ?? string.Empty;
        foreach (var row in _rows)
        {
            var ok = met.Contains(row.Rule.Id);
            var order = changed.IndexOf(row.Rule.Id);
            row.Mark.SetMet(ok, TimeSpan.FromSeconds(order > 0 ? order * 0.05 : 0));
            if (row.ShownMet != ok)
            {
                // The label darkens on the standard tween when its rule is met (full motion only).
                row.ShownMet = ok;
                row.Label.Transitions = IsLoaded && ArcMotion.IsFull(this)
                    ? row.LabelTransitions ??= new Transitions { new ArcBrushTransition { Property = TextBlock.ForegroundProperty, Tween = ArcTween.Standard } }
                    : null;
                row.LabelBrush?.Dispose();
                row.LabelBrush = ArcTokens.Bind(row.Label, TextBlock.ForegroundProperty,
                    ok ? DesignTokens.ArcDesignTokenKind.Foreground : DesignTokens.ArcDesignTokenKind.TextMuted);
            }
            AutomationProperties.SetName(row.Root, row.Rule.Label + (ok ? messages.RuleMet : messages.RuleNotMet));
            var remaining = password.Length > 0 && !ok ? row.Rule.Remaining?.Invoke(password) ?? 0 : 0;
            if (remaining > 0)
            {
                row.Remaining.Value = remaining;
            }
            row.RemainingPresence.IsPresent = remaining > 0;
        }
        if (_status is not null)
        {
            _status.Text = score.Level > 0
                ? string.Format(System.Globalization.CultureInfo.CurrentCulture, messages.SummaryFormat, label, score.MetRuleIds.Count, Rules.Count)
                : string.Empty;
        }
        if (!initial && changedScore)
        {
            RaiseEvent(new PasswordStrengthChangedEventArgs(StrengthChangedEvent, score, label));
        }
    }

    private void BuildRules()
    {
        if (_rulesPanel is null)
        {
            return;
        }
        _rulesPanel.Children.Clear();
        foreach (var old in _rows)
        {
            old.LabelBrush?.Dispose();
        }
        _rows.Clear();
        AutomationProperties.SetName(_rulesPanel, Messages.RulesLabel);
        foreach (var rule in Rules)
        {
            var mark = new PasswordRuleMark { VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(mark, PasswordRuleMark.OutlineBrushProperty, DesignTokens.ArcDesignTokenKind.BorderStrong);
            ArcTokens.Bind(mark, PasswordRuleMark.SuccessBrushProperty, DesignTokens.ArcDesignTokenKind.Success);
            var label = new TextBlock
            {
                Text = rule.Label,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 0, 0),
                LineHeight = 14 * 1.4
            };
            ArcTokens.Bind(label, TextBlock.FontFamilyProperty, DesignTokens.ArcDesignTokenKind.BodyFontFamily);
            ArcTokens.Bind(label, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
            var digits = new RollingDigits
            {
                UseGrouping = false,
                Prefix = string.IsNullOrEmpty(Messages.RemainingPrefix) ? null : Messages.RemainingPrefix + " ",
                Suffix = string.IsNullOrEmpty(Messages.RemainingSuffix) ? null : " " + Messages.RemainingSuffix
            };
            ArcTokens.Bind(digits, RollingDigits.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(digits, RollingDigits.FontFamilyProperty, DesignTokens.ArcDesignTokenKind.BodyFontFamily);
            ArcTokens.Bind(digits, RollingDigits.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
            var presence = new PresenceHost
            {
                Mode = PresenceMode.Rise,
                Offset = 4,
                IsPresent = false,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = digits
            };
            Grid.SetColumn(label, 1);
            Grid.SetColumn(presence, 2);
            var root = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                MinHeight = 20,
                Classes = { "rule" },
                Children = { mark, label, presence }
            };
            _rulesPanel.Children.Add(root);
            _rows.Add(new RuleRow { Rule = rule, Root = root, Mark = mark, Label = label, RemainingPresence = presence, Remaining = digits });
        }
    }

    /// <summary>P13: a spring released with sideways velocity rings out on its own, once per new error.</summary>
    private void Shake(string? error)
    {
        var previous = _lastError;
        _lastError = error;
        if (string.IsNullOrEmpty(error) || error == previous || !IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_shake is null)
        {
            _shake = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shake.Changed += x => _shakeTransform.X = x;
        }
        _shake.Jump(0);
        _ = _shake.AnimateTo(0, MotionSpec.Spring(SpringSpec.FromPhysical(900, 15)), -240);
    }
}
