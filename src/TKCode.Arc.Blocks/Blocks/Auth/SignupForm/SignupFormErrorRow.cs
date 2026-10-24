// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the submit error row of signup-form.tsx.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The submit error above the button (Arc's <c>AnimatePresence</c> row with <c>initial={{ height: 0, opacity: 0 }}</c>): a
/// new error opens the row from zero height on the smooth spring while it fades in over 160 ms (motion's default ease-out);
/// clearing it closes the row the same way with the last text still showing, then the row leaves (<see cref="IsPresent"/>
/// turns false, so the form's gap above it goes too). Reduced motion and Off open and close at once. While shown, a new
/// text replaces the old one in place and the height follows.
/// </summary>
/// <remarks>Code-driven because the height follows a measured target on a spring and the row stays until it has closed.</remarks>
internal sealed class SignupFormErrorRow : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SignupFormErrorRow, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SignupFormErrorRow>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SignupFormErrorRow>();

    /// <summary>Line height of the text.</summary>
    public static readonly StyledProperty<double> LineHeightProperty = TextBlock.LineHeightProperty.AddOwner<SignupFormErrorRow>();

    /// <summary>Letter spacing of the text.</summary>
    public static readonly StyledProperty<double> LetterSpacingProperty = TextBlock.LetterSpacingProperty.AddOwner<SignupFormErrorRow>();

    /// <summary>True from the moment an error is set until the row has closed after it was cleared.</summary>
    public static readonly DirectProperty<SignupFormErrorRow, bool> IsPresentProperty =
        AvaloniaProperty.RegisterDirect<SignupFormErrorRow, bool>(nameof(IsPresent), o => o.IsPresent);

    private readonly TextBlock _block = new() { TextWrapping = TextWrapping.Wrap };
    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;
    private bool _open;
    private bool _present;
    private int _run;

    static SignupFormErrorRow()
    {
        AffectsMeasure<SignupFormErrorRow>(LineHeightProperty, LetterSpacingProperty);
    }

    public SignupFormErrorRow()
    {
        ClipToBounds = true;
        LogicalChildren.Add(_block);
        VisualChildren.Add(_block);
        IsVisible = false;
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="LetterSpacingProperty"/>
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    /// <inheritdoc cref="IsPresentProperty"/>
    public bool IsPresent
    {
        get => _present;
        private set
        {
            if (SetAndRaise(IsPresentProperty, ref _present, value))
            {
                IsVisible = value;
            }
        }
    }

    /// <summary>The text on screen (the last error while closing).</summary>
    public string ShownText => _block.Text ?? string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Update(change.GetNewValue<string?>());
        }
        else if (change.Property == LineHeightProperty || change.Property == LetterSpacingProperty)
        {
            _block.LineHeight = LineHeight;
            _block.LetterSpacing = LetterSpacing;
        }
    }

    private bool Animates => IsLoaded && ArcMotion.IsFull(this) && IsEffectivelyVisible && Bounds.Height > 0;

    private void Update(string? text)
    {
        var show = !string.IsNullOrEmpty(text);
        var run = ++_run;
        if (show)
        {
            _block.Text = text;
            var wasOpen = _open;
            _open = true;
            // The row itself is hidden until it is present, so ask whether its parent is on screen.
            var animate = IsLoaded && ArcMotion.IsFull(this) && Parent is Visual { IsEffectivelyVisible: true };
            IsPresent = true;
            if (wasOpen)
            {
                InvalidateMeasure();
                return;
            }
            EnsureValues();
            if (!animate)
            {
                _height!.Jump(double.NaN);
                _opacity!.Jump(1);
                InvalidateMeasure();
                return;
            }
            _height!.Jump(0);
            _opacity!.Jump(0);
            _block.Measure(new Size(Bounds.Width > 0 ? Bounds.Width : double.PositiveInfinity, double.PositiveInfinity));
            var target = _block.DesiredSize.Height;
            _ = SettleOpen(run, _height.AnimateTo(target, MotionSpec.Spring(ArcMotionPresets.Smooth)));
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, SignupFormSwap.EaseOut));
            return;
        }
        if (!_open)
        {
            return;
        }
        _open = false;
        if (!Animates)
        {
            Close();
            return;
        }
        EnsureValues();
        if (double.IsNaN(_height!.Value))
        {
            _height.Jump(Bounds.Height);
        }
        _ = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, SignupFormSwap.EaseOut));
        _ = SettleClosed(run, _height.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Smooth)));
    }

    private async Task SettleOpen(int run, Task<bool> open)
    {
        // Resumes on the UI thread. At rest the row is auto again: a new text sizes it at once.
        if (await open && run == _run && _open)
        {
            _height?.Jump(double.NaN);
            InvalidateMeasure();
        }
    }

    private async Task SettleClosed(int run, Task<bool> close)
    {
        await close;
        if (run == _run && !_open)
        {
            Close();
        }
    }

    private void Close()
    {
        _height?.Jump(double.NaN);
        _opacity?.Jump(1);
        _block.Text = null;
        IsPresent = false;
        InvalidateMeasure();
    }

    private void EnsureValues()
    {
        if (_height is null)
        {
            _height = new MotionValue<double>(this, double.NaN, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
        }
        if (_opacity is null)
        {
            _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _opacity.Changed += o => _block.Opacity = o;
        }
    }

    /// <summary>The height shown right now: the spring's value while it moves, the text's height at rest.</summary>
    internal double ShownHeight => Bounds.Height;

    protected override Size MeasureOverride(Size availableSize)
    {
        _block.Measure(availableSize.WithHeight(double.PositiveInfinity));
        if (!IsPresent)
        {
            return default;
        }
        var height = _height is { } h && !double.IsNaN(h.Value) ? Math.Max(0, h.Value) : _block.DesiredSize.Height;
        return new Size(_block.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _block.Arrange(new Rect(0, 0, finalSize.Width, _block.DesiredSize.Height));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _run++;
        _height?.Stop();
        _opacity?.Stop();
        if (!_open)
        {
            Close();
        }
        else
        {
            _height?.Jump(double.NaN);
            _opacity?.Jump(1);
        }
    }
}
