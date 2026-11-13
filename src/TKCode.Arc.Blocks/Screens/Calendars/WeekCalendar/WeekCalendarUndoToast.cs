using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What the one-level undo of a <see cref="WeekCalendar"/> reverts.</summary>
internal enum WeekCalendarUndoKind
{
    /// <summary>An event was created: undo removes it.</summary>
    Created,

    /// <summary>An event was moved, resized or edited: undo puts its times and calendar back.</summary>
    Updated,

    /// <summary>An event was deleted: undo puts it back where it was in the collection.</summary>
    Deleted,
}

/// <summary>Where an edit came from (announcements and undo merging differ).</summary>
internal enum WeekCalendarEditSource
{
    Pointer,
    Keyboard,
    Details,
    Api,
}

/// <summary>
/// The one undoable change of a <see cref="WeekCalendar"/> (Arc keeps one level): the kind, the event, a copy of it from
/// before (updates), its index (deletes), the text the toast shows, and what merges with it (a run of keyboard nudges of
/// one event, or the changes made in one details popover, undo together).
/// </summary>
internal sealed class WeekCalendarUndoEntry
{
    public WeekCalendarUndoEntry(WeekCalendarUndoKind kind, WeekCalendarEvent calendarEvent, WeekCalendarEvent? previous, int index,
                                 string description, object? session)
    {
        Kind = kind;
        Event = calendarEvent;
        Previous = previous;
        Index = index;
        Description = description;
        Session = session;
    }

    public WeekCalendarUndoKind Kind { get; }

    public WeekCalendarEvent Event { get; }

    public WeekCalendarEvent? Previous { get; }

    public int Index { get; }

    public string Description { get; set; }

    public object? Session { get; }
}

/// <summary>A button on the undo toast's inverted pill (Undo, ×).</summary>
internal sealed class WeekCalendarToastButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(WeekCalendarToastButton);
}

/// <summary>
/// The undo toast (notes B §3.7): an inverted pill at the bottom of the frame with what changed, Undo and ×. It rises 12px
/// on the Smooth spring (opacity on the same curve), stays for <see cref="WeekCalendar.UndoDuration"/> (hovering does not
/// pause it, as Arc) and leaves in 160ms dropping 8px; a newer change replaces it, both moving at once. Reduced motion
/// fades only. Code-driven: its rise and its opacity share one spring, which a theme transition cannot express.
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
[TemplatePart(UndoPart, typeof(WeekCalendarToastButton))]
[TemplatePart(DismissPart, typeof(WeekCalendarToastButton))]
internal sealed class WeekCalendarUndoToast : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string UndoPart = "PART_Undo";
    public const string DismissPart = "PART_Dismiss";

    private readonly WeekCalendar _owner;
    private readonly TranslateTransform _shift = new();
    private TextBlock? _text;
    private WeekCalendarToastButton? _undo;
    private WeekCalendarToastButton? _dismiss;
    private MotionValue<double>? _progress;
    private string _message = string.Empty;

    public WeekCalendarUndoToast(WeekCalendar owner, string message)
    {
        _owner = owner;
        _message = message;
        RenderTransform = _shift;
        AutomationProperties.SetName(this, message);
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarUndoToast);

    /// <summary>The text shown.</summary>
    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            AutomationProperties.SetName(this, value);
            if (_text is not null)
            {
                _text.Text = value;
            }
        }
    }

    /// <summary>True once it started to leave.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Its rise (px below its place) and opacity now (tests).</summary>
    public double Rise => _shift.Y;

    public WeekCalendarToastButton? UndoButton => _undo;

    public WeekCalendarToastButton? DismissButton => _dismiss;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_undo is not null)
        {
            _undo.Click -= OnUndo;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismiss;
        }
        _text = e.NameScope.Find<TextBlock>(TextPart);
        _undo = e.NameScope.Find<WeekCalendarToastButton>(UndoPart);
        _dismiss = e.NameScope.Find<WeekCalendarToastButton>(DismissPart);
        var labels = _owner.Labels;
        if (_text is not null)
        {
            _text.Text = _message;
        }
        if (_undo is not null)
        {
            _undo.Content = labels.Undo;
            _undo.Click += OnUndo;
        }
        if (_dismiss is not null)
        {
            AutomationProperties.SetName(_dismiss, labels.Dismiss);
            _dismiss.Click += OnDismiss;
        }
    }

    private void OnUndo(object? sender, RoutedEventArgs e) => _owner.UndoFromToast();

    private void OnDismiss(object? sender, RoutedEventArgs e) => _owner.DismissUndo(fromToast: true);

    /// <summary>Rises in: Full motion 12px on the Smooth spring with the opacity on the same curve; Reduced fades in 160ms.</summary>
    public void Enter()
    {
        _progress ??= NewProgress();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _progress.Jump(1);
            return;
        }
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, level == ArcMotionLevel.Full
            ? MotionSpec.Spring(WeekCalendarMotion.PopoverScale)
            : WeekCalendarMotion.ToastFadeIn());
    }

    /// <summary>Leaves in 160ms (dropping 8px at Full motion); completes at once with Off.</summary>
    public async Task Exit()
    {
        IsLeaving = true;
        IsHitTestVisible = false;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || VisualRoot is null)
        {
            return;
        }
        _progress ??= NewProgress();
        _leaving = true;
        await _progress.AnimateTo(0, WeekCalendarMotion.ToastExit());
    }

    private bool _leaving;

    private MotionValue<double> NewProgress()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += Apply;
        return value;
    }

    private void Apply(double progress)
    {
        Opacity = Math.Clamp(progress, 0, 1);
        if (!ArcMotion.IsFull(this))
        {
            _shift.Y = 0;
            return;
        }
        // In: from 12 below; out: down to 8 below.
        _shift.Y = (_leaving ? WeekCalendarEditMetrics.ToastDrop : WeekCalendarEditMetrics.ToastRise) * (1 - progress);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.StatusBar);
}
