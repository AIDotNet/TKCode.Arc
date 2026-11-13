using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The shell's toast (Arc screen shell, design reference): an inverted pill centred over the bottom of the main card, 36
/// high, radius 12, padding 5 5 5 14, the message (13, ellipsis), an optional action (13/500, padding 5 9, r8) and a
/// dismiss cross (26 square, r7, text at 70%). It rises 8 → 0 while fading in over 200 ms, stays 5 s, and leaves by fading
/// out while sinking 4 px in 150 ms. A new toast replaces the shown one. Role status (polite).
/// </summary>
internal sealed class ScreenToast : Border
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);

    private readonly MotionValue<double> _progress;
    private readonly TranslateTransform _shift = new();
    private DispatcherTimer? _timer;
    private bool _leaving;

    public ScreenToast(ScreenPaint paint, string text, string? action, Action? onAction, string dismissName)
    {
        Height = 36;
        Padding = new Thickness(14, 5, 5, 5);
        Background = paint.InverseBrush;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Bottom;
        BoxShadow = paint.IsDark
            ? new BoxShadows(new BoxShadow { OffsetY = 22, Blur = 55, Color = Color.FromArgb(87, 0, 0, 0) })
            : new BoxShadows(new BoxShadow { OffsetY = 20, Blur = 48, Color = Color.FromArgb(27, 0, 0, 0) },
                [new BoxShadow { OffsetY = 3, Blur = 10, Color = Color.FromArgb(11, 0, 0, 0) }]);
        this.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusStep12);
        RenderTransform = _shift;
        Opacity = 0;
        var row = new ArcShrinkRow { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var message = ScreenText.Line(text, 13, paint.InverseText);
        message.Margin = new Thickness(0, 0, 6, 0);
        ArcShrinkRow.SetIsShrinkable(message, true);
        row.Children.Add(message);
        if (action is not null && onAction is not null)
        {
            var actionButton = new ScreenPressable
            {
                Height = 28,
                Radius = 8,
                Padding = new Thickness(9, 0),
                HoverFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.InverseText.Color, 0.08)),
                FocusPen = paint.FocusPen,
                Child = ScreenText.Line(action, 13, paint.InverseText, FontWeight.Medium),
            };
            AutomationProperties.SetName(actionButton, action);
            actionButton.Clicked += _ =>
            {
                onAction();
                Leave();
            };
            row.Children.Add(actionButton);
        }
        var close = new ScreenPressable
        {
            Width = 26,
            Height = 26,
            Radius = 7,
            HoverFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.InverseText.Color, 0.08)),
            FocusPen = paint.FocusPen,
            Child = new ArcGlyph
            {
                Data = ArcIcons.X,
                Size = 16,
                StrokeThickness = 1.75,
                Foreground = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.InverseText.Color, 0.7)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(close, dismissName);
        close.Clicked += _ => Leave();
        row.Children.Add(close);
        Child = row;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(this, text);
        Text = text;
        _progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        _progress.Changed += Apply;
    }

    /// <summary>The message.</summary>
    public string Text { get; }

    /// <summary>Raised once the toast has left (the host removes it).</summary>
    public event Action<ScreenToast>? Left;

    private void Apply(double p)
    {
        Opacity = Math.Clamp(p, 0, 1);
        _shift.Y = _leaving ? (1 - p) * 4 : (1 - p) * 8;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Apply(0);
        _ = _progress.AnimateTo(1, ArcMotion.IsFull(this)
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
        _timer = new DispatcherTimer { Interval = Lifetime };
        _timer.Tick += (_, _) => Leave();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        _progress.Stop();
    }

    /// <summary>Fades the toast out and raises <see cref="Left"/>.</summary>
    public async void Leave()
    {
        if (_leaving)
        {
            return;
        }
        _leaving = true;
        _timer?.Stop();
        IsHitTestVisible = false;
        await _progress.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard));
        Left?.Invoke(this);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ScreenToastPeer(this);

    private sealed class ScreenToastPeer(ScreenToast owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => "Status";
    }
}
