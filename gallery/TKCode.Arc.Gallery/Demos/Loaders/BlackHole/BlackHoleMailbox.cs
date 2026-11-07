using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using ArcButton = TKCode.Arc.Button;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The black hole demos' mailbox: a card of selectable conversations with a Delete button, Delete / Backspace on a row,
/// and an Undo pill under the card. Deleting swallows the selected rows, then their slots close on a smooth spring;
/// Undo puts them back at their places, opens the slots and releases them.
/// </summary>
public sealed class BlackHoleMailbox : UserControl
{
    private const double RowHeight = 60.2;

    private readonly List<Entry> _all = new();
    private readonly StackPanel _rows = new() { Margin = new Thickness(6) };
    private readonly ArcButton _delete;
    private readonly BlackHoleMailboxPresence _deleteHost;
    private readonly TextBlock _toastText;
    private readonly BlackHoleMailboxPresence _toast;
    private List<Entry>? _lastBatch;
    private bool _busy;

    public BlackHoleMailbox() : this(BlackHoleSamples.Title, BlackHoleSamples.Messages, BlackHoleSamples.InitiallySelected)
    {
    }

    public BlackHoleMailbox(string title, IReadOnlyList<BlackHoleSampleMessage> messages, IReadOnlyList<string> selected)
    {
        Hole = new BlackHole { Content = _rows, Height = messages.Count * RowHeight + 12, VerticalAlignment = VerticalAlignment.Top };
        foreach (var message in messages)
        {
            var row = new BlackHoleMailboxRow(message) { IsSelected = selected.Contains(message.Id) };
            var entry = new Entry(message, row, new BlackHoleMailboxSlot(row));
            row.Toggled += (_, _) => Toggle(entry);
            row.DeleteRequested += (_, _) => _ = DeleteAsync();
            _all.Add(entry);
            _rows.Children.Add(entry.Slot);
        }

        var title1 = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.Medium,
            LetterSpacing = -0.16,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(title1, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _delete = new ArcButton
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            LabelSwapMode = ContentSwapMode.Label
        };
        _delete.Click += (_, _) => _ = DeleteAsync();
        _deleteHost = new BlackHoleMailboxPresence(_delete, BlackHoleMailboxPresence.Style.Button) { HorizontalAlignment = HorizontalAlignment.Right };
        var header = new Grid
        {
            Height = 60,
            Margin = new Thickness(18, 8, 12, 8),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { title1, _deleteHost }
        };
        Grid.SetColumn(_deleteHost, 1);
        var headerLine = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom };
        ArcTokens.Bind(headerLine, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
        var headerPanel = new Panel { Height = 60, Children = { header, headerLine } };
        header.Height = double.NaN;
        header.VerticalAlignment = VerticalAlignment.Stretch;

        var cardBody = new StackPanel { Children = { headerPanel, Hole } };
        var card = new Border { BorderThickness = new Thickness(1), ClipToBounds = true, Child = cardBody }
            .BindCornerRadius(ArcDesignTokenKind.RadiusSurface);
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);

        _toastText = new TextBlock { FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_toastText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetLiveSetting(_toastText, AutomationLiveSetting.Polite);
        var undo = new ArcButton { Content = BlackHoleSamples.Undo, Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        undo.Click += (_, _) => _ = UndoAsync();
        var pill = new Border
        {
            Height = 38,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 0, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _toastText, undo } }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(pill, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(pill, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(pill, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        _toast = new BlackHoleMailboxPresence(pill, BlackHoleMailboxPresence.Style.Toast) { HorizontalAlignment = HorizontalAlignment.Center };

        Content = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 480,
            Children = { card, new Panel { Height = 40, Children = { _toast } } }
        };
        UpdateDelete(animate: false);
    }

    /// <summary>The black hole around the list (demos subscribe to its events or pass their own options).</summary>
    public BlackHole Hole { get; }

    /// <summary>Options for every swallow and release, built when they start (null: the hole opens at the targets' centre).</summary>
    public Func<BlackHoleMailbox, BlackHoleOptions?>? Options { get; set; }

    /// <summary>The Delete button (an origin for the hole, if a demo wants one).</summary>
    public ArcButton DeleteButton => _delete;

    /// <summary>A line for the demo's log: what the user did.</summary>
    public event EventHandler<string>? Logged;

    private IEnumerable<Entry> Present => _all.Where(e => e.IsPresent);

    private void Toggle(Entry entry)
    {
        if (_busy || Hole.IsRunning)
        {
            return;
        }
        entry.Row.IsSelected = !entry.Row.IsSelected;
        UpdateDelete(animate: true);
    }

    private void UpdateDelete(bool animate)
    {
        var count = Present.Count(e => e.Row.IsSelected);
        if (count > 0)
        {
            _delete.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new ArcGlyph { Data = ArcIcons.Trash2, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = BlackHoleSamples.DeleteLabel(count), VerticalAlignment = VerticalAlignment.Center }
                }
            };
            AutomationProperties.SetName(_delete, BlackHoleSamples.DeleteLabel(count));
        }
        _deleteHost.SetShown(count > 0, animate);
    }

    /// <summary>Swallows the selected conversations, then closes their slots and offers Undo.</summary>
    public async Task DeleteAsync()
    {
        var batch = Present.Where(e => e.Row.IsSelected).ToList();
        if (_busy || Hole.IsRunning || batch.Count == 0)
        {
            return;
        }
        _busy = true;
        Logged?.Invoke(this, $"Delete {batch.Count}");
        try
        {
            await Hole.SwallowAsync(batch.Select(e => (Control)e.Row).ToArray(), Options?.Invoke(this));
            foreach (var entry in batch)
            {
                entry.IsPresent = false;
                entry.Row.IsSelected = false;
                entry.Slot.Close(() => _rows.Children.Remove(entry.Slot));
            }
            _lastBatch = batch;
            _toastText.Text = BlackHoleSamples.RemovedLabel(batch.Count);
            _toast.SetShown(true, animate: true);
            UpdateDelete(animate: true);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Puts the last deleted conversations back at their places and releases them.</summary>
    public async Task UndoAsync()
    {
        if (_busy || Hole.IsRunning || _lastBatch is not { } batch)
        {
            return;
        }
        _busy = true;
        _lastBatch = null;
        Logged?.Invoke(this, "Undo");
        try
        {
            foreach (var entry in batch)
            {
                entry.IsPresent = true;
                if (!_rows.Children.Contains(entry.Slot))
                {
                    var index = _all.TakeWhile(e => e != entry).Count(e => e.IsPresent && _rows.Children.Contains(e.Slot));
                    _rows.Children.Insert(index, entry.Slot);
                }
                entry.Slot.Open();
            }
            _toast.SetShown(false, animate: true);
            await Hole.ReleaseAsync(batch.Select(e => (Control)e.Row).ToArray(), Options?.Invoke(this));
        }
        finally
        {
            _busy = false;
        }
    }

    private sealed class Entry(BlackHoleSampleMessage message, BlackHoleMailboxRow row, BlackHoleMailboxSlot slot)
    {
        public BlackHoleSampleMessage Message { get; } = message;
        public BlackHoleMailboxRow Row { get; } = row;
        public BlackHoleMailboxSlot Slot { get; } = slot;
        public bool IsPresent { get; set; } = true;
    }
}

/// <summary>A row's slot: its height follows a factor on Arc's smooth spring (0.4 s, no bounce) so the list closes and opens gaps.</summary>
public sealed class BlackHoleMailboxSlot : Decorator
{
    private MotionValue<double>? _factor;
    private int _generation;

    public BlackHoleMailboxSlot(Control child)
    {
        Child = child;
    }

    private double Factor => _factor?.Value ?? 1;

    public void Close(Action removed)
    {
        var generation = ++_generation;
        _factor ??= CreateFactor();
        _ = Run();

        async Task Run()
        {
            await _factor.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            if (generation == _generation)
            {
                removed();
            }
        }
    }

    public void Open()
    {
        _generation++;
        _factor ??= CreateFactor();
        _factor.Jump(Math.Min(_factor.Value, 0));
        _ = _factor.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private MotionValue<double> CreateFactor()
    {
        var factor = new MotionValue<double>(this, 1, MotionVectors.Double);
        factor.Changed += value =>
        {
            // Clipped only while the slot is shorter than its row, so a resting row's focus ring shows.
            ClipToBounds = value < 0.999;
            InvalidateMeasure();
        };
        return factor;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, size.Height * Math.Max(0, Factor));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// Shows and hides the Delete button (opacity, 4px blur, scale 0.96; 160 ms ease-out) or the Undo pill (opacity, 4px blur,
/// 6px rise in, 4px drop out; smooth spring). Reduced motion keeps only a short fade.
/// </summary>
public sealed class BlackHoleMailboxPresence : Decorator
{
    public enum Style
    {
        Button,
        Toast
    }

    private readonly Style _style;
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _shift = new();
    private MotionValue<double>? _progress;
    private bool _shown;

    public BlackHoleMailboxPresence(Control child, Style style)
    {
        _style = style;
        Child = child;
        child.RenderTransform = style == Style.Button ? _scale : _shift;
        IsVisible = false;
        child.Opacity = 0;
    }

    public bool IsShown => _shown;

    public void SetShown(bool shown, bool animate)
    {
        if (shown == _shown && (shown ? IsVisible : !IsVisible))
        {
            return;
        }
        _shown = shown;
        _progress ??= CreateProgress();
        if (shown)
        {
            IsVisible = true;
        }
        if (!animate || !ArcMotion.IsFull(this))
        {
            if (animate && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced)
            {
                // Reduced motion: a short fade only.
                _ = FadeAsync(shown);
                return;
            }
            _progress.Jump(shown ? 1 : 0);
            IsVisible = shown;
            return;
        }
        var spec = _style == Style.Button
            ? MotionSpec.Tween(ArcMotionPresets.Fast, new Avalonia.Animation.Easings.CubicEaseOut())
            : MotionSpec.Spring(ArcSpring.Smooth);
        _ = Run(shown, spec);
    }

    private async Task Run(bool shown, MotionSpec spec)
    {
        var done = await _progress!.AnimateTo(shown ? 1 : 0, spec);
        if (done && !_shown)
        {
            IsVisible = false;
        }
    }

    private async Task FadeAsync(bool shown)
    {
        await _progress!.AnimateTo(shown ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        if (!_shown)
        {
            IsVisible = false;
        }
    }

    private MotionValue<double> CreateProgress()
    {
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        progress.Changed += Apply;
        return progress;
    }

    private void Apply(double p)
    {
        if (Child is not { } child)
        {
            return;
        }
        var full = ArcMotion.IsFull(this);
        child.Opacity = Math.Clamp(p, 0, 1);
        child.Effect = full ? ArcBlur.Get(4 * (1 - Math.Clamp(p, 0, 1))) : null;
        if (_style == Style.Button)
        {
            var s = full ? 0.96 + 0.04 * p : 1;
            _scale.ScaleX = s;
            _scale.ScaleY = s;
        }
        else
        {
            // In from 6px below, out by dropping 4px.
            _shift.Y = full ? (_shown ? 6 : 4) * (1 - p) : 0;
        }
    }
}

/// <summary>
/// A conversation row: a selection circle, the sender's avatar, sender and subject, and the time. It acts as a checkbox
/// (click, Space or Enter toggles it); Delete or Backspace asks the mailbox to delete the selection.
/// </summary>
public sealed class BlackHoleMailboxRow : Border
{
    private readonly Border _circle;
    private readonly ArcGlyph _check;
    private readonly TextBlock _time;
    private readonly Border _focusRing;
    private bool _isSelected;
    private bool _hover;
    private IDisposable? _background;
    private IDisposable? _circleBorder;
    private IDisposable? _circleFill;

    // A token binding per state: the previous one is let go before the next is made.
    private static void Rebind(ref IDisposable? slot, StyledElement target, AvaloniaProperty property, ArcDesignTokenKind kind)
    {
        slot?.Dispose();
        slot = ArcTokens.Bind(target, property, kind, Avalonia.Data.BindingPriority.LocalValue);
    }

    public BlackHoleMailboxRow(BlackHoleSampleMessage message)
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        Padding = new Thickness(12, 10);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        AutomationProperties.SetName(this, $"{message.Sender}, {message.Subject}, {message.Time}");
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.CheckBox);

        _check = new ArcGlyph { Data = ArcIcons.Check, Size = 12, StrokeThickness = 2.5, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Surface);
        _circle = new Border { Width = 18, Height = 18, BorderThickness = new Thickness(1), Child = _check, VerticalAlignment = VerticalAlignment.Center }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);

        var avatar = BuildAvatar(message.Hues);
        var sender = new TextBlock { Text = message.Sender, FontSize = 14, LineHeight = 19.6, FontWeight = FontWeight.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(sender, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var subject = new TextBlock { Text = message.Subject, FontSize = 14, LineHeight = 19.6, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) };
        ArcTokens.Bind(subject, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { sender, subject } };
        _time = new TextBlock
        {
            Text = message.Time,
            FontSize = 12,
            LineHeight = 16.8,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") }
        };
        ArcTokens.Bind(_time, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 12, Children = { _circle, avatar, text, _time } };
        Grid.SetColumn(avatar, 1);
        Grid.SetColumn(text, 2);
        Grid.SetColumn(_time, 3);

        // The keyboard focus ring: 2px, 2px outside the row's 20px corners.
        _focusRing = new Border { BorderThickness = new Thickness(2), Margin = new Thickness(-16, -14), IsVisible = false, IsHitTestVisible = false }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep24);
        ArcTokens.Bind(_focusRing, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
        Child = new Panel { Children = { grid, _focusRing } };
        UpdateLook();
    }

    /// <summary>Raised when a click, Space or Enter toggles the row.</summary>
    public event EventHandler? Toggled;

    /// <summary>Raised when Delete or Backspace is pressed on the row.</summary>
    public event EventHandler? DeleteRequested;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            UpdateLook();
        }
    }

    private static Control BuildAvatar(IReadOnlyList<double> hues)
    {
        Color Hue(int i, double light) => new HslColor(1, hues[i % hues.Count], 0.78, light).ToRgb();
        var bottom = new Ellipse
        {
            Width = 32,
            Height = 32,
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.15, 0.1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.85, 0.95, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Hue(0, 0.62), 0), new GradientStop(Hue(1, 0.6), 1) }
            }
        };
        var glow = new Ellipse
        {
            Width = 32,
            Height = 32,
            Fill = new RadialGradientBrush
            {
                Center = new RelativePoint(0.72, 0.3, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.72, 0.3, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Hue(hues.Count > 2 ? 2 : 0, 0.66), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) }
            }
        };
        return new Panel { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center, Children = { bottom, glow } };
    }

    private void UpdateLook()
    {
        Rebind(ref _background, this, BackgroundProperty, _isSelected || _hover ? ArcDesignTokenKind.SurfaceMuted : ArcDesignTokenKind.Surface);
        Rebind(ref _circleBorder, _circle, Border.BorderBrushProperty, _isSelected ? ArcDesignTokenKind.ControlOn : ArcDesignTokenKind.BorderStrong);
        _circleFill?.Dispose();
        _circleFill = null;
        if (_isSelected)
        {
            _circleFill = ArcTokens.Bind(_circle, Border.BackgroundProperty, ArcDesignTokenKind.ControlOn, Avalonia.Data.BindingPriority.LocalValue);
        }
        else
        {
            _circle.ClearValue(Border.BackgroundProperty);
        }
        _check.IsVisible = _isSelected;
        AutomationProperties.SetItemStatus(this, _isSelected ? "Selected" : "Not selected");
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ArcMotion.IsFull(this))
        {
            Transitions = [new ArcBrushTransition { Property = BackgroundProperty }];
            _circle.Transitions = [new ArcBrushTransition { Property = Border.BackgroundProperty }, new ArcBrushTransition { Property = Border.BorderBrushProperty }];
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // The time column steps aside when the list is 352px or narrower.
        _time.IsVisible = e.NewSize.Width > 340;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hover = true;
            UpdateLook();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        UpdateLook();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            Focus(NavigationMethod.Pointer);
            Toggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                Toggled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case Key.Delete:
            case Key.Back:
                DeleteRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing.IsVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing.IsVisible = false;
    }
}
