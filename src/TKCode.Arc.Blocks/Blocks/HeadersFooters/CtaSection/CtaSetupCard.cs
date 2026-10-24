// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): SetupVisual (cta-section.tsx) and the
// .setup rules of cta-section.module.css.

using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// One row of the setup card: the step mark, the name (one line, ellipsis) and, on the last row, the teammates' faces that
/// slide in (opacity 0 to 1 in 240 ms, x -6 to 0 and scale .9 to 1 over 580 ms on the CSS spring curve, 60 ms apart) once the
/// step is the current or a finished one. The theme styles <c>:active</c> (a muted fill) and <c>:done</c>.
/// </summary>
[TemplatePart(FacesPart, typeof(SiteFaceStack))]
[PseudoClasses(ActivePseudoClass, DonePseudoClass)]
internal sealed class CtaSetupStep : TemplatedControl
{
    public const string FacesPart = "PART_Faces";
    public const string ActivePseudoClass = ":active";
    public const string DonePseudoClass = ":done";

    private const double FaceFrom = -6;
    private const double ScaleFrom = 0.9;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CtaSetupStep, string?>(nameof(Label));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CtaSetupStep, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<CtaSetupStep, bool>(nameof(IsDone));

    public static readonly StyledProperty<IList<IImage>?> FacesProperty =
        AvaloniaProperty.Register<CtaSetupStep, IList<IImage>?>(nameof(Faces));

    private SiteFaceStack? _stack;
    private MotionValue<double>[] _fade = [];
    private MotionValue<double>[] _slide = [];
    private bool _revealed;
    // The faces belong to the card's host: followed only while the row is in a visual tree.
    private INotifyCollectionChanged? _observed;
    private bool _watching;

    static CtaSetupStep()
    {
        FocusableProperty.OverrideDefaultValue<CtaSetupStep>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<CtaSetupStep>(false);
    }

    /// <summary>The step's name.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The step in progress (the one after the last finished).</summary>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <summary>The step is finished.</summary>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <summary>The faces on this row (the last one only).</summary>
    public IList<IImage>? Faces { get => GetValue(FacesProperty); set => SetValue(FacesProperty, value); }

    /// <summary>True once the faces were asked to show.</summary>
    internal bool FacesRevealed => _revealed;

    /// <summary>How face <paramref name="index"/> is drawn right now: opacity, x offset and scale (test hook).</summary>
    internal (double Opacity, double Offset, double Scale) FaceState(int index)
    {
        var slide = _slide[index].Value;
        return (Math.Clamp(_fade[index].Value, 0, 1), FaceFrom * (1 - slide), ScaleFrom + (1 - ScaleFrom) * slide);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stack = e.NameScope.Find<SiteFaceStack>(FacesPart);
        if (_stack is not null)
        {
            _stack.Images = Faces;
        }
        BuildFaceMotion();
        ApplyFacesNow();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty || change.Property == IsDoneProperty)
        {
            PseudoClasses.Set(ActivePseudoClass, IsActive);
            PseudoClasses.Set(DonePseudoClass, IsDone);
            UpdateReveal();
        }
        else if (change.Property == FacesProperty)
        {
            WatchFaces();
            if (_stack is not null)
            {
                _stack.Images = Faces;
            }
            BuildFaceMotion();
            ApplyFacesNow();
        }
    }

    private void OnFacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        BuildFaceMotion();
        ApplyFacesNow();
    }

    private void WatchFaces()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnFacesChanged;
        }
        _observed = _watching ? Faces as INotifyCollectionChanged : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnFacesChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchFaces();
        // Faces added or removed while the row was away (the stack draws whichever pictures are there).
        if (_fade.Length != FaceCount)
        {
            BuildFaceMotion();
            ApplyFacesNow();
        }
    }

    private int FaceCount => Faces?.Count ?? 0;

    private void BuildFaceMotion()
    {
        foreach (var value in _fade)
        {
            value.Dispose();
        }
        foreach (var value in _slide)
        {
            value.Dispose();
        }
        var count = FaceCount;
        if (_stack is not null)
        {
            _stack.IsVisible = count > 0;
        }
        _fade = new MotionValue<double>[count];
        _slide = new MotionValue<double>[count];
        for (var i = 0; i < count; i++)
        {
            var index = i;
            _fade[i] = new MotionValue<double>(this, 0, MotionVectors.Double);
            _slide[i] = new MotionValue<double>(this, 0, MotionVectors.Double);
            _fade[i].Changed += _ => Push(index);
            _slide[i].Changed += _ => Push(index);
        }
    }

    private void Push(int index)
    {
        if (_stack is null || index >= _fade.Length)
        {
            return;
        }
        var slide = _slide[index].Value;
        _stack.SetReveal(index, Math.Clamp(_fade[index].Value, 0, 1), FaceFrom * (1 - slide), ScaleFrom + (1 - ScaleFrom) * slide);
    }

    /// <summary>Shows or hides the faces where they stand, without motion.</summary>
    private void ApplyFacesNow()
    {
        var shown = IsActive || IsDone;
        _revealed = shown;
        for (var i = 0; i < _fade.Length; i++)
        {
            _fade[i].Jump(shown ? 1 : 0);
            _slide[i].Jump(shown ? 1 : 0);
            Push(i);
        }
    }

    private void UpdateReveal()
    {
        var shown = IsActive || IsDone;
        if (shown == _revealed)
        {
            return;
        }
        _revealed = shown;
        var full = ArcMotion.IsFull(this) && IsLoaded;
        for (var i = 0; i < _fade.Length; i++)
        {
            var target = shown ? 1 : 0;
            if (!full)
            {
                _fade[i].Jump(target);
                _slide[i].Jump(target);
                Push(i);
                continue;
            }
            // Per face, 60 ms apart: opacity on the standard tween, position and scale on the 580 ms CSS spring curve.
            // Code-driven because the faces are painted by one control; a ControlTheme has no per-face handle.
            var delay = TimeSpan.FromMilliseconds(i * 60);
            _ = _fade[i].AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, delay));
            _ = _slide[i].AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring, delay));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchFaces();
        foreach (var value in _fade)
        {
            value.Stop();
        }
        foreach (var value in _slide)
        {
            value.Stop();
        }
    }
}

/// <summary>
/// Arc's self-completing setup card: a workspace header whose status reads "Setting up" and then "Ready", a progress line and
/// a list of steps. <see cref="Start"/> (called once the card is half in view) completes step i after 520 + i × 620 ms; the
/// current step is the number finished. Under Reduced motion every step shows finished. Decorative: hidden from screen readers.
/// </summary>
[TemplatePart(StepsPart, typeof(Panel))]
[TemplatePart(SettingPart, typeof(TextBlock))]
[TemplatePart(ReadyPart, typeof(TextBlock))]
[PseudoClasses(DonePseudoClass)]
internal sealed class CtaSetupCard : TemplatedControl
{
    public const string StepsPart = "PART_Steps";
    public const string SettingPart = "PART_Setting";
    public const string ReadyPart = "PART_Ready";
    public const string DonePseudoClass = ":done";

    internal static readonly string[] DefaultSteps = ["Create workspace", "Import 214 issues", "Invite 4 teammates"];

    public static readonly StyledProperty<string> WorkspaceProperty =
        AvaloniaProperty.Register<CtaSetupCard, string>(nameof(Workspace), "Northwind");

    public static readonly StyledProperty<IList<string>?> StepsProperty =
        AvaloniaProperty.Register<CtaSetupCard, IList<string>?>(nameof(Steps));

    public static readonly StyledProperty<IList<IImage>?> FacesProperty =
        AvaloniaProperty.Register<CtaSetupCard, IList<IImage>?>(nameof(Faces));

    public static readonly StyledProperty<int> CompletedProperty =
        AvaloniaProperty.Register<CtaSetupCard, int>(nameof(Completed));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<CtaSetupCard, double>(nameof(Progress));

    internal static readonly DirectProperty<CtaSetupCard, string> InitialProperty =
        AvaloniaProperty.RegisterDirect<CtaSetupCard, string>(nameof(Initial), o => o.Initial);

    private Panel? _steps;
    private TextBlock? _setting;
    private TextBlock? _ready;
    private readonly List<CtaSetupStep> _rows = new();
    private readonly List<DispatcherTimer> _timers = new();
    // The step names belong to the host: followed only while the card is in a visual tree.
    private INotifyCollectionChanged? _observedSteps;
    private bool _watching;
    private string _initial = "N";
    private bool _started;

    static CtaSetupCard()
    {
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<CtaSetupCard>(AccessibilityView.Raw);
        FocusableProperty.OverrideDefaultValue<CtaSetupCard>(false);
    }

    /// <summary>The workspace's name; its first letter is the mark.</summary>
    public string Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }

    /// <summary>The step names (null or empty: Arc's three).</summary>
    public IList<string>? Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    /// <summary>The faces on the last step.</summary>
    public IList<IImage>? Faces { get => GetValue(FacesProperty); set => SetValue(FacesProperty, value); }

    /// <summary>The number of finished steps shown (0 to the number of steps).</summary>
    public int Completed { get => GetValue(CompletedProperty); set => SetValue(CompletedProperty, value); }

    /// <summary>The progress line's value, 0 to 1 (the theme animates it).</summary>
    public double Progress { get => GetValue(ProgressProperty); private set => SetValue(ProgressProperty, value); }

    /// <summary>The first letter of the workspace.</summary>
    internal string Initial => _initial;

    /// <summary>The effective step names.</summary>
    internal IReadOnlyList<string> EffectiveSteps => Steps is { Count: > 0 } steps ? steps.ToArray() : DefaultSteps;

    /// <summary>The rows (test hook).</summary>
    internal IReadOnlyList<CtaSetupStep> Rows => _rows;

    /// <summary>True once the timeline was started (or Reduced motion finished the card).</summary>
    internal bool HasStarted => _started;

    /// <summary>True while the timeline's timers are pending.</summary>
    internal int PendingTimers => _timers.Count;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _steps = e.NameScope.Find<Panel>(StepsPart);
        _setting = e.NameScope.Find<TextBlock>(SettingPart);
        _ready = e.NameScope.Find<TextBlock>(ReadyPart);
        RebuildRows();
        Apply();
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced)
        {
            Finish();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WorkspaceProperty)
        {
            var name = Workspace?.Trim();
            SetAndRaise(InitialProperty, ref _initial, string.IsNullOrEmpty(name) ? string.Empty : name[..1].ToUpperInvariant());
        }
        else if (change.Property == StepsProperty)
        {
            WatchSteps();
            RebuildRows();
            Apply();
        }
        else if (change.Property == FacesProperty)
        {
            foreach (var row in _rows)
            {
                row.Faces = null;
            }
            if (_rows.Count > 0)
            {
                _rows[^1].Faces = Faces;
            }
        }
        else if (change.Property == CompletedProperty)
        {
            Apply();
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRows();
        Apply();
    }

    private void WatchSteps()
    {
        if (_observedSteps is not null)
        {
            _observedSteps.CollectionChanged -= OnStepsChanged;
        }
        _observedSteps = _watching ? Steps as INotifyCollectionChanged : null;
        if (_observedSteps is not null)
        {
            _observedSteps.CollectionChanged += OnStepsChanged;
        }
    }

    /// <summary>True when the rows show <see cref="EffectiveSteps"/> as they are now.</summary>
    private bool RowsMatchSteps()
    {
        var names = EffectiveSteps;
        if (names.Count != _rows.Count)
        {
            return false;
        }
        for (var i = 0; i < names.Count; i++)
        {
            if (!string.Equals(names[i], _rows[i].Label, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private void RebuildRows()
    {
        if (_steps is null)
        {
            return;
        }
        _steps.Children.Clear();
        _rows.Clear();
        var names = EffectiveSteps;
        for (var i = 0; i < names.Count; i++)
        {
            var row = new CtaSetupStep { Label = names[i], Faces = i == names.Count - 1 ? Faces : null };
            _rows.Add(row);
            _steps.Children.Add(row);
        }
    }

    /// <summary>Puts the rows, the status words and the progress line in line with <see cref="Completed"/>.</summary>
    private void Apply()
    {
        var total = _rows.Count;
        var shown = Math.Clamp(Completed, 0, Math.Max(total, 0));
        PseudoClasses.Set(DonePseudoClass, total > 0 && shown == total);
        for (var i = 0; i < total; i++)
        {
            _rows[i].IsDone = i < shown;
            _rows[i].IsActive = i == shown;
        }
        Progress = total == 0 ? 0 : (double)shown / total;
        _setting?.Classes.Set("shown", shown < total);
        _ready?.Classes.Set("shown", shown == total);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchSteps();
        // Steps changed while the card was away (rebuilding unchanged rows would reset their faces).
        if (_steps is not null && !RowsMatchSteps())
        {
            RebuildRows();
            Apply();
        }
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced)
        {
            Finish();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchSteps();
        CancelTimers();
    }

    /// <summary>Starts the timeline: step i finishes 520 + i × 620 ms from now. Reduced motion shows everything finished instead.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }
        _started = true;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced)
        {
            Finish();
            return;
        }
        var total = _rows.Count;
        for (var i = 0; i < total; i++)
        {
            var step = i + 1;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520 + i * 620) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _timers.Remove(timer);
                Completed = step;
            };
            _timers.Add(timer);
            timer.Start();
        }
    }

    /// <summary>Shows every step finished at once.</summary>
    public void Finish()
    {
        _started = true;
        CancelTimers();
        Completed = _rows.Count;
    }

    /// <summary>Back to the start: nothing finished, the timeline can run again (Arc re-mounts the card).</summary>
    public void Reset()
    {
        CancelTimers();
        _started = false;
        Completed = 0;
    }

    private void CancelTimers()
    {
        foreach (var timer in _timers)
        {
            timer.Stop();
        }
        _timers.Clear();
    }
}
