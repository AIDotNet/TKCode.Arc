using System.Diagnostics;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One event of a <see cref="Timeline"/> (Arc's <c>TimelineRow</c>): a marker on the line (portrait or tone icon), the
/// actor and title with context and a relative time, and a detail that opens in place. Rows are reused by the
/// virtualized list, so every piece of motion is code-driven with an explicit "jump" for reuse: the marker pops and the
/// line draws once per event when it scrolls into view, fresh rows grow in, the chevron turns, the detail springs open.
/// </summary>
[TemplatePart(GrowPart, typeof(TimelineGrowHost))]
[TemplatePart(TriggerPart, typeof(TimelineTrigger))]
[TemplatePart(DetailPart, typeof(TimelineGrowHost))]
[PseudoClasses(ExpandablePseudoClass, LastPseudoClass, AvatarPseudoClass, SuccessPseudoClass, DangerPseudoClass, NarrowPseudoClass)]
internal sealed class TimelineRow : TemplatedControl
{
    public const string GrowPart = "PART_Grow";
    public const string SegmentPart = "PART_Segment";
    public const string MarkerPart = "PART_Marker";
    public const string MarkerContentPart = "PART_MarkerContent";
    public const string ContentPart = "PART_Content";
    public const string TriggerPart = "PART_Trigger";
    public const string TitlePart = "PART_Title";
    public const string MetaPart = "PART_Meta";
    public const string TimePart = "PART_Time";
    public const string ChevronPart = "PART_Chevron";
    public const string DetailPart = "PART_Detail";
    public const string DetailBodyPart = "PART_DetailBody";
    public const string DetailHostPart = "PART_DetailHost";

    public const string ExpandablePseudoClass = ":expandable";
    public const string LastPseudoClass = ":last";
    public const string AvatarPseudoClass = ":avatar";
    public const string SuccessPseudoClass = ":success";
    public const string DangerPseudoClass = ":danger";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The actor's color (Foreground), set by the theme.</summary>
    public static readonly StyledProperty<IBrush?> ActorBrushProperty =
        AvaloniaProperty.Register<TimelineRow, IBrush?>(nameof(ActorBrush));

    public IBrush? ActorBrush { get => GetValue(ActorBrushProperty); set => SetValue(ActorBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(TimelineRow);

    private static readonly double s_enterTicks = Stopwatch.Frequency * 0.6;
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private TimelineGrowHost? _grow;
    private Border? _segment;
    private Panel? _marker;
    private Decorator? _markerContent;
    private Panel? _content;
    private TimelineTrigger? _trigger;
    private TextBlock? _title;
    private TextBlock? _meta;
    private ArcContentSwap? _time;
    private ArcGlyph? _chevron;
    private TimelineGrowHost? _detail;
    private Control? _detailBody;
    private Decorator? _detailHost;

    private readonly ScaleTransform _markerScale = new(0.4, 0.4);
    private readonly ScaleTransform _segmentScale = new(1, 0);
    private readonly TranslateTransform _contentShift = new();
    private readonly TranslateTransform _detailShift = new();
    private readonly RotateTransform _chevronTurn = new();

    private MotionValue<double>? _markerPop;
    private MotionValue<double>? _markerFade;
    private MotionValue<double>? _lineDraw;
    private MotionValue<double>? _dropY;
    private MotionValue<double>? _dropFade;
    private MotionValue<double>? _detailFade;
    private MotionValue<double>? _turn;
    private double _dropBlur;
    private double _detailBlur;

    private Avatar? _avatar;
    private ArcGlyph? _icon;
    private TextBlock? _detailText;
    private Run? _actorRun;
    private readonly Run _spaceRun = new(" ");
    private readonly Run _titleRun = new();
    private string _timeText = string.Empty;
    private string _timeFull = string.Empty;
    private bool _expanded;
    private int _revealGeneration;

    public TimelineEventModel? Model { get; private set; }

    public Timeline? Owner { get; set; }

    public bool IsExpanded => _expanded;

    public TimelineTrigger? Trigger => _trigger;

    public TimelineGrowHost? Grow => _grow;

    public TimelineGrowHost? Detail => _detail;

    /// <summary>The time label shown (short relative or clock time).</summary>
    public string TimeText => _timeText;

    /// <summary>The full date read aloud and shown on hover.</summary>
    public string TimeFull => _timeFull;

    /// <summary>Marker presence (0 hidden, 1 shown) for tests.</summary>
    public double MarkerProgress => _markerFade?.Value ?? 1;

    /// <summary>Marker scale for tests.</summary>
    public double MarkerScale => _markerScale.ScaleX;

    /// <summary>Line draw progress for tests.</summary>
    public double LineProgress => _segmentScale.ScaleY;

    /// <summary>Content drop offset (fresh rows) for tests.</summary>
    public double ContentOffset => _contentShift.Y;

    /// <summary>Chevron angle for tests.</summary>
    public double ChevronAngle => _chevronTurn.Angle;

    public TimelineRow()
    {
        _titleRun.Text = string.Empty;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.Row = null;
        }
        _grow = e.NameScope.Find<TimelineGrowHost>(GrowPart);
        _segment = e.NameScope.Find<Border>(SegmentPart);
        _marker = e.NameScope.Find<Panel>(MarkerPart);
        _markerContent = e.NameScope.Find<Decorator>(MarkerContentPart);
        _content = e.NameScope.Find<Panel>(ContentPart);
        _trigger = e.NameScope.Find<TimelineTrigger>(TriggerPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _meta = e.NameScope.Find<TextBlock>(MetaPart);
        _time = e.NameScope.Find<ArcContentSwap>(TimePart);
        _chevron = e.NameScope.Find<ArcGlyph>(ChevronPart);
        _detail = e.NameScope.Find<TimelineGrowHost>(DetailPart);
        _detailBody = e.NameScope.Find<Control>(DetailBodyPart);
        _detailHost = e.NameScope.Find<Decorator>(DetailHostPart);

        if (_marker is not null)
        {
            _marker.RenderTransformOrigin = RelativePoint.Center;
            _marker.RenderTransform = _markerScale;
        }
        if (_segment is not null)
        {
            _segment.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
            _segment.RenderTransform = _segmentScale;
        }
        if (_content is not null)
        {
            _content.RenderTransform = _contentShift;
        }
        if (_detailBody is not null)
        {
            _detailBody.RenderTransform = _detailShift;
        }
        if (_time is not null)
        {
            // Times keep a steady column: "9m" becoming "10m" never rewraps the title beside it.
            TextElement.SetFontFeatures(_time, s_tabular);
        }
        if (_chevron is not null)
        {
            _chevron.RenderTransformOrigin = RelativePoint.Center;
            _chevron.RenderTransform = _chevronTurn;
        }
        if (_trigger is not null)
        {
            _trigger.Row = this;
            _trigger.Click += OnTriggerClick;
        }
        if (_grow is not null)
        {
            _grow.Closed += OnGrowClosed;
        }
        if (Model is { } model)
        {
            ApplyModel(model);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActorBrushProperty && _actorRun is not null)
        {
            _actorRun.Foreground = ActorBrush;
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (Model is { } model && model.Item.HasDetail)
        {
            e.Handled = true;
            Owner?.Toggle(model.Item.Id);
        }
    }

    internal void RequestExpanded(bool expanded)
    {
        if (Model is { } model && model.Item.HasDetail)
        {
            if (expanded)
            {
                Owner?.Expand(model.Item.Id);
            }
            else
            {
                Owner?.Collapse(model.Item.Id);
            }
        }
    }

    /// <summary>Binds the row to an event (a new or reused container): everything jumps to the event's state.</summary>
    public void Prepare(TimelineEventModel model)
    {
        Model = model;
        IsHitTestVisible = true;
        ApplyModel(model);
    }

    /// <summary>Releases the event (the container goes back to the pool).</summary>
    public void Clear()
    {
        _revealGeneration++;
        if (_detailHost is not null)
        {
            _detailHost.Child = null;
        }
        Model = null;
    }

    private void ApplyModel(TimelineEventModel model)
    {
        if (_trigger is null)
        {
            return;
        }
        var item = model.Item;
        var owner = Owner;
        var expandable = item.HasDetail;
        PseudoClasses.Set(ExpandablePseudoClass, expandable);
        PseudoClasses.Set(LastPseudoClass, model.IsLastOfDay);
        var hasAvatar = !string.IsNullOrWhiteSpace(item.Avatar);
        PseudoClasses.Set(AvatarPseudoClass, hasAvatar);
        PseudoClasses.Set(SuccessPseudoClass, !hasAvatar && item.Tone == TimelineTone.Success);
        PseudoClasses.Set(DangerPseudoClass, !hasAvatar && item.Tone == TimelineTone.Danger);
        PseudoClasses.Set(NarrowPseudoClass, owner?.IsNarrow == true);
        _trigger.IsInteractive = expandable;
        _segment!.IsVisible = !model.IsLastOfDay;
        Padding = new Thickness(0, 0, 0, model.IsLastOfDay ? owner?.DayGap ?? 12 : 0);

        UpdateTitle(item);
        UpdateMarker(item, hasAvatar);
        if (owner is not null)
        {
            var (label, full) = owner.TimeLabels(model);
            SetTime(label, full, animate: false);
        }

        // Expansion follows the timeline's set; a reused row jumps to it.
        _expanded = expandable && owner?.IsExpanded(item.Id) == true;
        SetDetailContent(expandable ? item.Detail : null);
        _detail!.Set(_expanded, null);
        TurnChevron(animate: false);
        if (_detailBody is not null)
        {
            _detailFade?.Jump(1);
            _detailBody.Opacity = 1;
            _detailBody.Effect = null;
            _detailShift.Y = 0;
        }
        UpdateAccessibleName();

        // Entrance: a fresh row grows in once, right after it was added; anything else shows at rest.
        var level = owner is null ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(owner);
        var entering = model.Fresh && level != ArcMotionLevel.Off && model.EnterUntil > Stopwatch.GetTimestamp();
        model.EnterUntil = 0;
        if (entering)
        {
            PlayEnter(level);
        }
        else
        {
            _grow!.Set(true, null);
            ResetDrop();
        }

        // Reveal: once per event, the first time it scrolls into view.
        _revealGeneration++;
        if (model.Revealed || level == ArcMotionLevel.Off)
        {
            model.Revealed = true;
            ShowRevealed();
        }
        else
        {
            HideForReveal();
        }
    }

    private void UpdateTitle(TimelineEvent item)
    {
        var inlines = _title!.Inlines ??= new InlineCollection();
        inlines.Clear();
        if (!string.IsNullOrEmpty(item.Actor))
        {
            _actorRun ??= new Run { FontWeight = FontWeight.Medium };
            _actorRun.Text = item.Actor;
            _actorRun.Foreground = ActorBrush;
            inlines.Add(_actorRun);
            inlines.Add(_spaceRun);
        }
        _titleRun.Text = item.Title;
        inlines.Add(_titleRun);
        _meta!.Text = item.Meta;
        _meta.IsVisible = !string.IsNullOrEmpty(item.Meta);
    }

    private void UpdateMarker(TimelineEvent item, bool hasAvatar)
    {
        if (hasAvatar)
        {
            if (_avatar is null)
            {
                _avatar = ActivityAvatar.Create(28, item.Actor, item.Avatar);
            }
            else
            {
                ActivityAvatar.Update(_avatar, item.Actor, item.Avatar);
            }
            _markerContent!.Child = _avatar;
            _avatar.Width = _avatar.Height = Owner?.MarkerSize ?? 28;
        }
        else if (item.Icon is { } icon)
        {
            _icon ??= new ArcGlyph { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _icon.Data = icon;
            _icon.Size = Owner?.MarkerGlyphSize ?? 14;
            _icon.StrokeThickness = Owner?.MarkerGlyphStroke ?? 2;
            _markerContent!.Child = _icon;
        }
        else
        {
            _markerContent!.Child = null;
        }
    }

    private void SetDetailContent(object? detail)
    {
        if (_detailHost is null)
        {
            return;
        }
        switch (detail)
        {
            case null:
                _detailHost.Child = null;
                break;
            case Control control:
                if (control.Parent is Decorator previous && !ReferenceEquals(previous, _detailHost))
                {
                    previous.Child = null;
                }
                _detailHost.Child = control;
                break;
            default:
                _detailText ??= new TextBlock { TextWrapping = TextWrapping.Wrap };
                _detailText.Text = detail.ToString();
                _detailHost.Child = _detailText;
                break;
        }
    }

    /// <summary>Refreshes the time label; when <paramref name="animate"/> the new text rises in from a blur.</summary>
    public void SetTime(string label, string full, bool animate)
    {
        _timeFull = full;
        if (_time is null)
        {
            _timeText = label;
            return;
        }
        if (!string.Equals(_timeText, label, StringComparison.Ordinal) || _time.Content is null)
        {
            _timeText = label;
            if (animate)
            {
                _time.Content = label;
            }
            else
            {
                TimelineMotion.Silently(_time, () => _time.Content = label);
            }
        }
        Tooltip.SetTip(_time, full);
        UpdateAccessibleName();
    }

    /// <summary>Re-reads layout-affecting flags (last of day, narrow) after the rows changed.</summary>
    public void Refresh()
    {
        if (Model is not { } model || _trigger is null)
        {
            return;
        }
        PseudoClasses.Set(LastPseudoClass, model.IsLastOfDay);
        PseudoClasses.Set(NarrowPseudoClass, Owner?.IsNarrow == true);
        _segment!.IsVisible = !model.IsLastOfDay;
        Padding = new Thickness(0, 0, 0, model.IsLastOfDay ? Owner?.DayGap ?? 12 : 0);
        if (Owner is { } owner)
        {
            var (label, full) = owner.TimeLabels(model);
            SetTime(label, full, animate: true);
        }
    }

    private void UpdateAccessibleName()
    {
        if (_trigger is null || Model is not { } model)
        {
            return;
        }
        var item = model.Item;
        var title = string.IsNullOrEmpty(item.Actor) ? item.Title : $"{item.Actor} {item.Title}";
        var name = string.IsNullOrEmpty(item.Meta) ? $"{title}, {_timeFull}" : $"{title}, {item.Meta}, {_timeFull}";
        AutomationProperties.SetName(_trigger, name);
    }

    #region Expansion

    /// <summary>Opens or closes the detail (Arc: smooth spring, the inner content fades up from a blur after 60ms).</summary>
    public void SetExpanded(bool expanded, bool animate)
    {
        if (Model is not { } model || !model.Item.HasDetail || _detail is null)
        {
            return;
        }
        if (_expanded == expanded)
        {
            return;
        }
        var old = _expanded;
        _expanded = expanded;
        var level = Owner is null ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(Owner);
        if (!animate || level == ArcMotionLevel.Off || !IsLoaded)
        {
            _detail.Set(expanded, null);
            TurnChevron(animate: false);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _detail.Set(expanded, null);
            TurnChevron(animate: false);
            if (expanded)
            {
                FadeDetail(MotionSpec.Tween(TimelineMotion.ReducedFade, ArcEasings.Enter), blur: 0, shift: 0);
            }
        }
        else
        {
            _detail.Set(expanded, MotionSpec.Spring(expanded ? ArcMotionPresets.Smooth : TimelineMotion.DetailClose));
            TurnChevron(animate: true);
            if (expanded)
            {
                FadeDetail(MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(60)), blur: 4, shift: -4);
            }
            else
            {
                _detailFade ??= CreateDetailFade();
                _ = _detailFade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
        }
        _trigger?.RaiseExpandCollapseChanged(old, expanded);
    }

    private void FadeDetail(MotionSpec spec, double blur, double shift)
    {
        _detailFade ??= CreateDetailFade();
        _detailBlur = blur;
        _detailShiftFrom = shift;
        _detailFade.Jump(0);
        ApplyDetailFade(0);
        _ = _detailFade.AnimateTo(1, spec);
    }

    private double _detailShiftFrom;

    private MotionValue<double> CreateDetailFade()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplyDetailFade;
        return value;
    }

    private void ApplyDetailFade(double p)
    {
        if (_detailBody is null)
        {
            return;
        }
        var t = Math.Clamp(p, 0, 1);
        _detailBody.Opacity = t;
        _detailShift.Y = _detailShiftFrom * (1 - t);
        _detailBody.Effect = ArcBlur.Get(_detailBlur * (1 - t));
    }

    private void TurnChevron(bool animate)
    {
        var target = _expanded ? 180 : 0;
        _turn ??= CreateValue(target, v => _chevronTurn.Angle = v);
        if (animate)
        {
            _ = _turn.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _turn.Jump(target);
            _chevronTurn.Angle = target;
        }
    }

    #endregion

    #region Entrance, exit and reveal

    /// <summary>A fresh row: its height grows on the smooth spring while the content drops in from a blur.</summary>
    private void PlayEnter(ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Reduced)
        {
            _grow!.Set(true, null);
            _dropFade ??= CreateValue(1, ApplyDrop);
            _dropY ??= CreateValue(0, ApplyDrop);
            _dropY.Jump(0);
            _dropBlur = 0;
            _dropFade.Jump(0);
            ApplyDrop(0);
            _ = _dropFade.AnimateTo(1, MotionSpec.Tween(TimelineMotion.ReducedFade, ArcEasings.Enter));
            return;
        }
        _grow!.Set(true, MotionSpec.Spring(ArcSpring.Smooth), from: 0);
        _dropFade ??= CreateValue(1, ApplyDrop);
        _dropY ??= CreateValue(0, ApplyDrop);
        _dropBlur = 4;
        _dropY.Jump(-10);
        _dropFade.Jump(0);
        ApplyDrop(0);
        var delay = TimeSpan.FromMilliseconds(50);
        _ = _dropY.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        _ = _dropFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
    }

    private void ResetDrop()
    {
        _dropY?.Jump(0);
        _dropFade?.Jump(1);
        _dropBlur = 0;
        ApplyDrop(0);
    }

    private void ApplyDrop(double _)
    {
        if (_content is null)
        {
            return;
        }
        var fade = Math.Clamp(_dropFade?.Value ?? 1, 0, 1);
        _content.Opacity = fade;
        _contentShift.Y = _dropY?.Value ?? 0;
        _content.Effect = ArcBlur.Get(_dropBlur * (1 - fade));
    }

    /// <summary>Removed from the data: the row's height closes while it fades (Arc: smooth .3s, opacity .12s).</summary>
    public void PlayExit(ArcMotionLevel level)
    {
        if (_grow is null)
        {
            return;
        }
        _dropFade ??= CreateValue(1, ApplyDrop);
        IsHitTestVisible = false;
        if (_trigger is not null)
        {
            _trigger.Focusable = false;
        }
        if (level == ArcMotionLevel.Full)
        {
            _grow.Set(false, MotionSpec.Spring(TimelineMotion.RowExit));
            _ = _dropFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
        }
        else
        {
            _grow.Set(false, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
            _ = _dropFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
        }
    }

    private void OnGrowClosed(object? sender, EventArgs e)
    {
        if (Model is { Leaving: true } model)
        {
            Owner?.RemoveLeaving(model);
        }
    }

    private void HideForReveal()
    {
        _markerPop ??= CreateValue(0.4, ApplyMarker);
        _markerFade ??= CreateValue(0, ApplyMarker);
        _lineDraw ??= CreateValue(0, ApplyLine);
        _markerPop.Jump(0.4);
        _markerFade.Jump(0);
        _lineDraw.Jump(0);
        ApplyMarker(0);
        ApplyLine(0);
    }

    private void ShowRevealed()
    {
        _markerPop?.Jump(1);
        _markerFade?.Jump(1);
        _lineDraw?.Jump(1);
        _markerScale.ScaleX = _markerScale.ScaleY = 1;
        if (_marker is not null)
        {
            _marker.Opacity = 1;
        }
        _segmentScale.ScaleY = 1;
    }

    /// <summary>
    /// Reveals the marker and line after <paramref name="delay"/> (Arc: the marker pops on a bouncy spring while it
    /// fades in fast; the line draws downward 0.1s later). Reduced fades the marker only; Off shows both at once.
    /// </summary>
    public void Reveal(TimeSpan delay)
    {
        if (Model is not { Revealed: false } model)
        {
            return;
        }
        model.Revealed = true;
        var level = Owner is null ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(Owner);
        if (level == ArcMotionLevel.Off)
        {
            ShowRevealed();
            return;
        }
        _markerPop ??= CreateValue(0.4, ApplyMarker);
        _markerFade ??= CreateValue(0, ApplyMarker);
        _lineDraw ??= CreateValue(0, ApplyLine);
        if (level == ArcMotionLevel.Reduced)
        {
            _markerPop.Jump(1);
            _lineDraw.Jump(1);
            ApplyLine(1);
            _ = _markerFade.AnimateTo(1, MotionSpec.Tween(TimelineMotion.ReducedFade, ArcEasings.Enter));
            return;
        }
        if (model.Fresh)
        {
            delay += TimeSpan.FromMilliseconds(120);
        }
        _ = _markerPop.AnimateTo(1, MotionSpec.Spring(TimelineMotion.MarkerPop, delay));
        _ = _markerFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, delay));
        _ = _lineDraw.AnimateTo(1, MotionSpec.Spring(TimelineMotion.LineDraw, delay + TimeSpan.FromMilliseconds(100)));
    }

    private void ApplyMarker(double _)
    {
        var scale = _markerPop?.Value ?? 1;
        _markerScale.ScaleX = _markerScale.ScaleY = scale;
        if (_marker is not null)
        {
            _marker.Opacity = Math.Clamp(_markerFade?.Value ?? 1, 0, 1);
        }
    }

    private void ApplyLine(double _)
    {
        _segmentScale.ScaleY = Math.Max(0, _lineDraw?.Value ?? 1);
    }

    private MotionValue<double> CreateValue(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    #endregion

    internal static long EnterWindow => (long)s_enterTicks;
}
