using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class ToneTuner
{
    /// <summary>When the tour takes its first step, and the time between steps after that.</summary>
    internal static readonly TimeSpan TourFirstStep = TimeSpan.FromSeconds(1.4);

    internal static readonly TimeSpan TourInterval = TimeSpan.FromSeconds(3.2);

    private static readonly ToneVoice[] s_voices = [ToneVoice.Neutral, ToneVoice.Friendly, ToneVoice.Confident, ToneVoice.Empathetic];

    private Avatar? _avatar;
    private TextBlock? _name;
    private TextBlock? _subject;
    private Control? _count;
    private TextBlock? _countText;
    private ToneTunerChipRow? _chips;
    private ToneTunerSliderRow? _sliders;
    private Slider? _formality;
    private Slider? _detail;
    private ToneTunerMessage? _message;
    private Control? _statusPanel;
    private ArcGlyph? _statusGlyph;
    private ToolCallText? _statusText;
    private TextBlock? _settings;
    private AvaloniaButton? _retry;
    private Switch? _changes;
    private CopyButton? _copy;

    /// <summary>How long a simulated rewrite of your own draft (no <see cref="Rewrite"/>) pretends the model takes (tests set zero).</summary>
    internal TimeSpan SimulatedLatency { get; set; } = TimeSpan.FromMilliseconds(650);

    private readonly List<ToneTunerChip> _chipViews = [];
    private readonly Dictionary<ToneSettings, string> _landed = [];
    private DispatcherTimer? _debounce;
    private CancellationTokenSource? _cancel;
    private int _requestId;
    private bool _syncing;
    private bool _stacked;
    private bool _compact;
    private bool _resume;
    private string _announcement = string.Empty;
    private string? _error;
    private LiveLoop? _tour;
    private double _tourClock;
    private int _tourStep;
    private bool _tourStopped;

    /// <summary>The message part (tests).</summary>
    internal ToneTunerMessage? MessageView => _message;

    internal IReadOnlyList<ToneTunerChip> ChipViews => _chipViews;

    internal Slider? FormalitySlider => _formality;

    internal Slider? DetailSlider => _detail;

    internal Switch? ChangesSwitch => _changes;

    internal CopyButton? CopyView => _copy;

    internal AvaloniaButton? RetryButton => _retry;

    internal TextBlock? CountText => _countText;

    internal Control? CountView => _count;

    internal TextBlock? SettingsText => _settings;

    internal ToolCallText? StatusTextView => _statusText;

    /// <summary>What the polite live region last said.</summary>
    internal string Announcement => _announcement;

    /// <summary>The status label on screen.</summary>
    internal string StatusLabel => Status switch
    {
        ToneTunerStatus.Rewriting => Labels.Rewriting(EffectiveModel),
        ToneTunerStatus.Rewritten => Labels.Rewritten,
        ToneTunerStatus.Failed => Labels.Failed,
        _ => Labels.OriginalDraft,
    };

    internal bool IsStacked => _stacked;

    internal bool IsCompact => _compact;

    internal bool IsWaitingForDebounce => _debounce?.IsEnabled == true;

    internal bool IsTouring => _tour?.IsRunning == true;

    internal bool TourStopped => _tourStopped;

    /// <summary>Runs the debounced rewrite now (tests: headless dispatcher timers only fire on pushed frames).</summary>
    internal bool FlushDebounce()
    {
        if (_debounce?.IsEnabled != true)
        {
            return false;
        }
        _debounce.Stop();
        StartRequest();
        return true;
    }

    /// <summary>Takes the tour's next step now (tests).</summary>
    internal void StepTour() => AdvanceTour();

    /// <summary>Runs the latest rewrite again (the Retry button).</summary>
    public void Retry()
    {
        if (Value == ToneSettings.Default || !CanRewrite)
        {
            ShowOriginal(announce: false);
            return;
        }
        _debounce?.Stop();
        StartRequest();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _subject = e.NameScope.Find<TextBlock>(SubjectPart);
        _count = e.NameScope.Find<Control>(CountPart);
        _countText = e.NameScope.Find<TextBlock>(CountTextPart);
        _chips = e.NameScope.Find<ToneTunerChipRow>(ChipsPart);
        _sliders = e.NameScope.Find<ToneTunerSliderRow>(SlidersPart);
        _formality = e.NameScope.Find<Slider>(FormalityPart);
        _detail = e.NameScope.Find<Slider>(DetailPart);
        _message = e.NameScope.Find<ToneTunerMessage>(MessagePart);
        _statusPanel = e.NameScope.Find<Control>(StatusPanelPart);
        _statusGlyph = e.NameScope.Find<ArcGlyph>(StatusGlyphPart);
        _statusText = e.NameScope.Find<ToolCallText>(StatusPart);
        _settings = e.NameScope.Find<TextBlock>(SettingsPart);
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        _changes = e.NameScope.Find<Switch>(ChangesPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);

        foreach (var slider in new[] { _formality, _detail })
        {
            if (slider is null)
            {
                continue;
            }
            slider.Minimum = 0;
            slider.Maximum = 2;
            slider.SmallChange = 1;
            slider.LargeChange = 1;
            slider.Marks = [0d, 1d, 2d];
            slider.ValueChanged += OnSliderChanged;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_changes is not null)
        {
            _changes.IsCheckedChanged += OnChangesToggled;
        }
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
        }
        if (_statusPanel is not null)
        {
            AutomationProperties.SetLiveSetting(_statusPanel, AutomationLiveSetting.Polite);
        }
        if (_message is not null)
        {
            _message.ShowChanges = ShowChanges;
        }
        BuildChips();
        SyncControls();
        SyncHeader();
        SyncEnabled();
        UpdateReserved();
        // The text on screen right away: the original, or the version landed for this tone (a template change keeps it).
        if (_landed.TryGetValue(Value, out var landed) && Status == ToneTunerStatus.Rewritten)
        {
            Land(landed, Value, simulated: IsSimulated, raise: false, announce: false);
        }
        else
        {
            ShowOriginal(announce: false);
            if (Value != ToneSettings.Default)
            {
                Request(immediately: true);
            }
        }
        UpdateBreakpoints(Bounds.Width);
    }

    private void Unhook()
    {
        foreach (var slider in new[] { _formality, _detail })
        {
            if (slider is not null)
            {
                slider.ValueChanged -= OnSliderChanged;
            }
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_changes is not null)
        {
            _changes.IsCheckedChanged -= OnChangesToggled;
        }
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        foreach (var chip in _chipViews)
        {
            chip.Click -= OnChipClick;
        }
        _chipViews.Clear();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var old = change.GetOldValue<ToneSettings>() ?? ToneSettings.Default;
            var value = Value;
            SyncControls();
            UpdateReserved();
            RaiseEvent(new ToneTunerValueChangedEventArgs(ValueChangedEvent, old, value));
            if (_message is not null)
            {
                Request(immediately: false);
            }
        }
        else if (change.Property == ShowChangesProperty)
        {
            if (_message is not null)
            {
                _message.ShowChanges = ShowChanges;
            }
            SyncControls();
            UpdateStatus();
        }
        else if (change.Property == TextProperty || change.Property == RewriteProperty || change.Property == SimulationProperty)
        {
            _landed.Clear();
            SyncHeader();
            SyncEnabled();
            UpdateReserved();
            if (_message is not null)
            {
                CancelRequest();
                ShowOriginal(announce: false);
                if (Value != ToneSettings.Default)
                {
                    Request(immediately: true);
                }
            }
            UpdateTour();
        }
        else if (change.Property == LabelsProperty || change.Property == PresetsProperty)
        {
            BuildChips();
            SyncControls();
            UpdateStatus();
        }
        else if (change.Property == ModelProperty)
        {
            UpdateStatus();
        }
        else if (change.Property == RecipientNameProperty || change.Property == RecipientSubjectProperty || change.Property == RecipientAvatarProperty)
        {
            SyncHeader();
        }
        else if (change.Property == AutoPlayProperty)
        {
            UpdateTour();
        }
        else if (change.Property == StackWidthProperty || change.Property == CompactWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnAnyFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        if (_resume)
        {
            _resume = false;
            Request(immediately: true);
        }
        UpdateTour();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        RemoveHandler(PointerPressedEvent, OnAnyPointerPressed);
        RemoveHandler(GotFocusEvent, OnAnyFocus);
        if (Status == ToneTunerStatus.Rewriting)
        {
            _resume = true;
        }
        CancelRequest();
        _tour?.Stop();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <summary>Under 520px the sliders stack, the chips share one row and the status drops its settings; under 400px the chip icons, the count and Copy's label drop.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        _stacked = width < StackWidth;
        _compact = width < CompactWidth;
        PseudoClasses.Set(StackedPseudoClass, _stacked);
        PseudoClasses.Set(CompactPseudoClass, _compact);
        if (_chips is not null)
        {
            _chips.IsUniform = _stacked;
        }
        if (_sliders is not null)
        {
            _sliders.IsStacked = _stacked;
        }
        foreach (var chip in _chipViews)
        {
            chip.ShowsIcon = !_compact && chip.Icon is not null;
        }
        if (_copy is not null)
        {
            _copy.IsIconOnly = _compact;
        }
        UpdateStatus();
    }

    private void BuildChips()
    {
        if (_chips is null)
        {
            return;
        }
        foreach (var chip in _chipViews)
        {
            chip.Click -= OnChipClick;
        }
        _chipViews.Clear();
        _chips.Children.Clear();
        foreach (var preset in EffectivePresets)
        {
            var label = preset.Label ?? Labels.VoiceName(preset.Voice);
            var chip = new ToneTunerChip
            {
                Preset = preset,
                Label = label,
                Icon = preset.Icon,
                ShowsIcon = !_compact && preset.Icon is not null,
            };
            AutomationProperties.SetName(chip, label);
            chip.Click += OnChipClick;
            _chipViews.Add(chip);
            _chips.Children.Add(chip);
        }
        AutomationProperties.SetName(_chips, Labels.Voice);
    }

    /// <summary>Brings the chips, sliders and switch in line with <see cref="Value"/> and <see cref="ShowChanges"/>.</summary>
    private void SyncControls()
    {
        _syncing = true;
        try
        {
            var value = Value;
            var labels = Labels;
            foreach (var chip in _chipViews)
            {
                chip.IsChecked = chip.Preset?.Voice == value.Voice && value.Voice != ToneVoice.Neutral;
            }
            if (_formality is not null)
            {
                _formality.Label = labels.Formality;
                _formality.ValueFormatter = v => Level(labels.FormalityLevels, v);
                _formality.Value = value.F;
            }
            if (_detail is not null)
            {
                _detail.Label = labels.Detail;
                _detail.ValueFormatter = v => Level(labels.DetailLevels, v);
                _detail.Value = value.D;
            }
            if (_changes is not null)
            {
                _changes.Content = labels.Changes;
                _changes.IsChecked = ShowChanges;
                AutomationProperties.SetName(_changes, labels.Changes);
            }
            if (_copy is not null)
            {
                _copy.Label = labels.Copy;
                _copy.CopiedLabel = labels.Copied;
                AutomationProperties.SetName(_copy, labels.Copy);
            }
            if (_retry is not null)
            {
                _retry.Content = labels.Retry;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private static string Level(IReadOnlyList<string> levels, double value)
    {
        var index = Math.Clamp((int)Math.Round(value), 0, 2);
        return index < levels.Count ? levels[index] : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SyncHeader()
    {
        var name = EffectiveRecipientName;
        if (_avatar is not null)
        {
            _avatar.DisplayName = name;
            _avatar.Source = EffectiveRecipientAvatar;
            _avatar.IsVisible = name is not null || EffectiveRecipientAvatar is not null;
        }
        if (_name is not null)
        {
            _name.Text = name;
            _name.IsVisible = name is not null;
        }
        if (_subject is not null)
        {
            var subject = EffectiveRecipientSubject;
            _subject.Text = subject;
            _subject.IsVisible = !string.IsNullOrEmpty(subject);
        }
    }

    /// <summary>With no draft at all (no <see cref="Text"/>, no <see cref="Simulation"/>) the dials, Changes and Copy are off.</summary>
    private void SyncEnabled()
    {
        var enabled = HasDraft;
        foreach (var part in new Control?[] { _chips, _sliders, _changes, _copy })
        {
            if (part is not null)
            {
                part.IsEnabled = enabled;
            }
        }
    }

    /// <summary>
    /// The heights the message keeps: every voice at the current formality and detail, so a voice change never resizes the
    /// card. Simulated rewrites are known ahead; with a model, the versions landed so far at this formality and detail.
    /// </summary>
    private void UpdateReserved()
    {
        if (_message is null)
        {
            return;
        }
        var value = Value;
        var original = Original;
        var texts = new List<string>();
        foreach (var voice in s_voices)
        {
            var settings = value.WithVoice(voice);
            if (settings == ToneSettings.Default)
            {
                texts.Add(original);
            }
            else if (IsSimulated)
            {
                texts.Add(ToneTunerText.Normalize(SimulatedText(settings)));
            }
            else if (_landed.TryGetValue(settings, out var landed))
            {
                texts.Add(landed);
            }
        }
        _message.Reserved = texts;
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        var level = Math.Clamp((int)Math.Round(e.NewValue), 0, 2);
        var next = sender == _formality ? Value.WithFormality(level) : Value.WithDetail(level);
        if (next != Value)
        {
            SetCurrentValue(ValueProperty, next);
        }
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToneTunerChip { Preset: { } preset })
        {
            Press(preset);
        }
    }

    /// <summary>A chip press: its voice and starting point, or back to the neutral voice when it is already on.</summary>
    private void Press(TonePreset preset)
    {
        var value = Value;
        var next = value.Voice == preset.Voice
            ? value.WithVoice(ToneVoice.Neutral)
            : new ToneSettings(Math.Clamp(preset.Formality, 0, 2), Math.Clamp(preset.Detail, 0, 2), preset.Voice);
        if (next != value)
        {
            SetCurrentValue(ValueProperty, next);
        }
        else
        {
            SyncControls();
        }
    }

    private void OnChangesToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _changes is null)
        {
            return;
        }
        SetCurrentValue(ShowChangesProperty, _changes.IsChecked == true);
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => Retry();

    private void OnCopied(object? sender, EventArgs e) => RaiseEvent(new ToneTunerCopiedEventArgs(CopiedEvent, DisplayedText));

    /// <summary>Asks for the current tone: the original at the starting tone, otherwise a debounced rewrite (the status flips to Rewriting at once).</summary>
    private void Request(bool immediately)
    {
        CancelRequest();
        if (Value == ToneSettings.Default || !CanRewrite)
        {
            ShowOriginal(announce: Status != ToneTunerStatus.Original);
            return;
        }
        _error = null;
        Status = ToneTunerStatus.Rewriting;
        UpdateStatus();
        var delay = Debounce;
        if (immediately || delay <= TimeSpan.Zero)
        {
            StartRequest();
            return;
        }
        if (_debounce is null)
        {
            _debounce = new DispatcherTimer(DispatcherPriority.Normal);
            _debounce.Tick += (_, _) =>
            {
                _debounce!.Stop();
                StartRequest();
            };
        }
        _debounce.Interval = delay;
        _debounce.Start();
    }

    /// <summary>Aborts the request in flight (its late answer is ignored) and the pending debounce.</summary>
    private void CancelRequest()
    {
        _requestId++;
        _debounce?.Stop();
        if (_cancel is not null)
        {
            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = null;
        }
    }

    private void StartRequest()
    {
        CancelRequest();
        var value = Value;
        if (value == ToneSettings.Default || !CanRewrite)
        {
            ShowOriginal(announce: value == ToneSettings.Default);
            return;
        }
        _error = null;
        Status = ToneTunerStatus.Rewriting;
        UpdateStatus();
        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        var id = _requestId;
        var request = new ToneRewriteRequest
        {
            Text = Original,
            Settings = value,
            Instructions = ToneTunerText.Instructions(value),
        };
        var simulated = IsSimulated;
        Task<string> task;
        try
        {
            task = simulated || Rewrite is not { } rewrite
                ? Simulate(SimulatedText(value), PlayingSimulation?.Latency ?? SimulatedLatency, cancel.Token)
                : rewrite(request, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Fail(id, ex.Message);
            return;
        }
        _ = Await(id, task, value, simulated, cancel.Token);
    }

    /// <summary>The simulated rewrite: the playing simulation's version, or the plain rules over your draft.</summary>
    private string SimulatedText(ToneSettings value) =>
        PlayingSimulation is { } simulation ? simulation.Rewrite(value) : ToneTunerSimulation.Rules(Original, value);

    private static Task<string> Simulate(string text, TimeSpan latency, CancellationToken token)
    {
        if (latency <= TimeSpan.Zero)
        {
            return Task.FromResult(text);
        }
        return Delayed(text, latency, token);

        static async Task<string> Delayed(string text, TimeSpan latency, CancellationToken token)
        {
            await Task.Delay(latency, token);
            return text;
        }
    }

    private async Task Await(int id, Task<string> task, ToneSettings value, bool simulated, CancellationToken token)
    {
        string result;
        try
        {
            result = await task;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || id != _requestId)
        {
            return;
        }
        catch (Exception ex)
        {
            Fail(id, ex.Message);
            return;
        }
        if (id != _requestId)
        {
            return;
        }
        _cancel?.Dispose();
        _cancel = null;
        Land(result ?? string.Empty, value, simulated, raise: true, announce: true);
    }

    private void Fail(int id, string? reason)
    {
        if (id != _requestId)
        {
            return;
        }
        _error = reason ?? string.Empty;
        Status = ToneTunerStatus.Failed;
        Announce(Labels.FailedAnnouncement(_error));
        UpdateStatus();
    }

    private void ShowOriginal(bool announce)
    {
        CancelRequest();
        _error = null;
        Show(Original, animate: true);
        Status = ToneTunerStatus.Original;
        if (announce)
        {
            Announce(Labels.BackToOriginal);
        }
        UpdateStatus();
    }

    /// <summary>A rewrite landed: changed sentences crossfade, the highlights and count follow, the status says Rewritten.</summary>
    private void Land(string text, ToneSettings value, bool simulated, bool raise, bool announce)
    {
        text = ToneTunerText.Normalize(text);
        _landed[value] = text;
        if (!simulated)
        {
            UpdateReserved();
        }
        Show(text, animate: true);
        Status = ToneTunerStatus.Rewritten;
        if (announce)
        {
            Announce(Labels.Landed(Labels.Settings(value, Labels), simulated));
        }
        UpdateStatus();
        if (raise)
        {
            RaiseEvent(new ToneTunerRewrittenEventArgs(RewrittenEvent, text, value, simulated));
        }
    }

    private void Show(string text, bool animate)
    {
        var (ranges, words) = ToneTunerText.Inserted(Original, text);
        ChangedWords = words;
        DisplayedText = text;
        _message?.Show(text, ranges, animate && IsLoaded);
        if (_copy is not null)
        {
            _copy.Value = text;
        }
    }

    private void Announce(string text)
    {
        _announcement = text;
        if (_statusPanel is not null)
        {
            AutomationProperties.SetName(_statusPanel, text);
        }
    }

    /// <summary>The status row and the header count, from the state.</summary>
    private void UpdateStatus()
    {
        var status = Status;
        PseudoClasses.Set(OriginalPseudoClass, status == ToneTunerStatus.Original);
        PseudoClasses.Set(RewritingPseudoClass, status == ToneTunerStatus.Rewriting);
        PseudoClasses.Set(RewrittenPseudoClass, status == ToneTunerStatus.Rewritten);
        PseudoClasses.Set(FailedPseudoClass, status == ToneTunerStatus.Failed);
        if (_statusText is not null)
        {
            var label = StatusLabel;
            if (_statusText.Text != label)
            {
                _statusText.Text = label;
            }
            _statusText.IsShimmering = status == ToneTunerStatus.Rewriting;
        }
        if (_statusGlyph is not null)
        {
            _statusGlyph.Data = status switch
            {
                ToneTunerStatus.Rewriting => ArcIcons.Sparkles,
                ToneTunerStatus.Rewritten => ArcIcons.Check,
                ToneTunerStatus.Failed => ArcIcons.CircleAlert,
                _ => ArcIcons.FileText,
            };
        }
        if (_settings is not null)
        {
            var show = status == ToneTunerStatus.Rewritten && !_stacked;
            _settings.Text = show ? "· " + Labels.Settings(Value, Labels) : string.Empty;
            _settings.IsVisible = show;
        }
        if (_retry is not null)
        {
            _retry.IsVisible = status == ToneTunerStatus.Failed;
        }
        if (_count is not null && _countText is not null)
        {
            var words = ChangedWords;
            _countText.Text = Labels.ChangedWords(words);
            _count.IsVisible = words > 0 && ShowChanges && !_compact && status != ToneTunerStatus.Original;
        }
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e) => StopTour();

    private void OnAnyFocus(object? sender, RoutedEventArgs e) => StopTour();

    /// <summary>The tour ends for good at the first press or focus inside the card.</summary>
    private void StopTour()
    {
        if (_tourStopped)
        {
            return;
        }
        _tourStopped = true;
        _tour?.Stop();
    }

    /// <summary>
    /// The self playing tour: every 3.2s the next voice chip (then back to the original). It runs on a
    /// <see cref="LiveLoop"/>, so it pauses off screen and under reduced motion never starts.
    /// </summary>
    private void UpdateTour()
    {
        if (!EffectiveAutoPlay || _tourStopped || VisualRoot is null)
        {
            _tour?.Stop();
            return;
        }
        _tour ??= new LiveLoop(this, dt =>
        {
            _tourClock += dt;
            var due = (_tourStep == 0 ? TourFirstStep : TourFirstStep + TourInterval * _tourStep).TotalSeconds;
            if (_tourClock >= due)
            {
                AdvanceTour();
            }
            return !_tourStopped;
        });
        _tour.Start();
    }

    private void AdvanceTour()
    {
        var presets = EffectivePresets;
        var index = _tourStep % (presets.Count + 1);
        _tourStep++;
        if (index < presets.Count)
        {
            var preset = presets[index];
            SetCurrentValue(ValueProperty, new ToneSettings(Math.Clamp(preset.Formality, 0, 2), Math.Clamp(preset.Detail, 0, 2), preset.Voice));
        }
        else
        {
            SetCurrentValue(ValueProperty, ToneSettings.Default);
        }
    }
}
