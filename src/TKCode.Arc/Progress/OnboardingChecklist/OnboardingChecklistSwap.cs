// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): the rise, roll, fade and iconSwap variants.
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Which of the source's variants an <see cref="OnboardingChecklistSwap"/> plays.</summary>
internal enum OnboardingChecklistSwapKind
{
    /// <summary>A label: rises 0.3em out of a soft blur (<c>rise</c>).</summary>
    Rise,

    /// <summary>A digit: turns 0.45em in the direction the count moved, on the snappy spring (<c>roll</c>).</summary>
    Roll,

    /// <summary>A glyph: grows from 0.6 on the snappy spring (<c>iconSwap</c>).</summary>
    Icon
}

/// <summary>
/// One slot whose content changes with the source's exact motion. The old content leaves out of layout while the new one
/// enters; reduced motion only crossfades; the first content never animates. Code-driven because each layer runs a spring
/// and a tween together on content that comes and goes, and ArcContentSwap's fixed BlurSwap, Roll and Pop timings differ
/// from these (0.22 and 0.15s at 0.3em; 0.14s at 0.45em; snappy and 0.12s).
/// </summary>
internal sealed class OnboardingChecklistSwap : Control
{
    private static readonly TimeSpan s_riseEnter = TimeSpan.FromSeconds(0.22);
    private static readonly TimeSpan s_riseExit = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan s_rollExit = TimeSpan.FromSeconds(0.14);
    private static readonly TimeSpan s_iconExit = TimeSpan.FromSeconds(0.12);
    private static readonly TimeSpan s_reducedEnter = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan s_reducedExit = TimeSpan.FromSeconds(0.1);
    private const double BlurSoft = 4;
    private const double BlurSubtle = 2;
    private const double IconScale = 0.6;

    private sealed class Layer
    {
        public required Control Content { get; init; }
        public MotionValue<double>? Fade;
        public MotionValue<double>? Move;
    }

    private Layer? _current;
    private readonly List<Layer> _leaving = new();

    public OnboardingChecklistSwapKind Kind { get; set; }

    public Control? Current => _current?.Content;

    /// <summary>The number of layers still on their way out.</summary>
    internal int LeavingCount => _leaving.Count;

    /// <summary>Replaces the content; <paramref name="direction"/> is 1 when a count rose and -1 when it fell.</summary>
    public void Show(Control? content, int direction, bool animate)
    {
        var level = animate && this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var outgoing = _current;
        _current = null;
        if (outgoing is not null)
        {
            if (level == ArcMotionLevel.Off)
            {
                Remove(outgoing);
            }
            else
            {
                _leaving.Add(outgoing);
                Exit(outgoing, level, direction);
            }
        }
        if (content is not null)
        {
            var incoming = new Layer { Content = content };
            LogicalChildren.Add(content);
            VisualChildren.Add(content);
            _current = incoming;
            if (level != ArcMotionLevel.Off)
            {
                Enter(incoming, level, direction);
            }
        }
        InvalidateMeasure();
    }

    private double Em => TextElement.GetFontSize(this);

    private void Enter(Layer layer, ArcMotionLevel level, int direction)
    {
        var content = layer.Content;
        content.Opacity = 0;
        if (level != ArcMotionLevel.Full)
        {
            layer.Fade = Value(0, v => content.Opacity = v);
            _ = layer.Fade.AnimateTo(1, MotionSpec.Tween(s_reducedEnter, ArcEasings.Standard));
            return;
        }
        var blur = Kind == OnboardingChecklistSwapKind.Rise ? BlurSoft : BlurSubtle;
        Blur(content, blur);
        layer.Fade = Value(0, v =>
        {
            content.Opacity = v;
            Blur(content, blur * (1 - v));
        });
        switch (Kind)
        {
            case OnboardingChecklistSwapKind.Rise:
                var rise = Translate(content, 0.3 * Em * direction);
                layer.Move = Value(rise.Y, v => rise.Y = v);
                _ = layer.Fade.AnimateTo(1, MotionSpec.Tween(s_riseEnter, ArcEasings.Enter));
                _ = layer.Move.AnimateTo(0, MotionSpec.Tween(s_riseEnter, ArcEasings.Enter));
                break;
            case OnboardingChecklistSwapKind.Roll:
                var roll = Translate(content, 0.45 * Em * direction);
                layer.Move = Value(roll.Y, v => roll.Y = v);
                _ = layer.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                _ = layer.Move.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
                break;
            default:
                var scale = Scale(content, IconScale);
                layer.Move = Value(IconScale, v =>
                {
                    scale.ScaleX = v;
                    scale.ScaleY = v;
                });
                _ = layer.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
                _ = layer.Move.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
                break;
        }
    }

    private void Exit(Layer layer, ArcMotionLevel level, int direction)
    {
        var content = layer.Content;
        layer.Fade?.Stop();
        layer.Move?.Stop();
        var start = content.Opacity;
        if (level != ArcMotionLevel.Full)
        {
            layer.Fade = Value(1, v => content.Opacity = start * v);
            _ = RemoveWhenDone(layer, layer.Fade.AnimateTo(0, MotionSpec.Tween(s_reducedExit, ArcEasings.Standard)));
            return;
        }
        var spec = MotionSpec.Tween(Kind switch
        {
            OnboardingChecklistSwapKind.Rise => s_riseExit,
            OnboardingChecklistSwapKind.Roll => s_rollExit,
            _ => s_iconExit
        }, ArcEasings.Standard);
        layer.Fade = Value(1, v =>
        {
            content.Opacity = start * v;
            Blur(content, BlurSubtle * (1 - v));
        });
        if (Kind == OnboardingChecklistSwapKind.Icon)
        {
            var scale = content.RenderTransform as ScaleTransform ?? Scale(content, 1);
            layer.Move = Value(scale.ScaleX, v =>
            {
                scale.ScaleX = v;
                scale.ScaleY = v;
            });
            _ = layer.Move.AnimateTo(IconScale, spec);
        }
        else
        {
            var translate = content.RenderTransform as TranslateTransform ?? Translate(content, 0);
            layer.Move = Value(translate.Y, v => translate.Y = v);
            _ = layer.Move.AnimateTo((Kind == OnboardingChecklistSwapKind.Rise ? -0.3 : -0.45) * Em * direction, spec);
        }
        _ = RemoveWhenDone(layer, layer.Fade.AnimateTo(0, spec));
    }

    private static TranslateTransform Translate(Control content, double y)
    {
        var transform = new TranslateTransform(0, y);
        content.RenderTransform = transform;
        return transform;
    }

    private static ScaleTransform Scale(Control content, double scale)
    {
        var transform = new ScaleTransform(scale, scale);
        content.RenderTransformOrigin = RelativePoint.Center;
        content.RenderTransform = transform;
        return transform;
    }

    private static void Blur(Control content, double radius) => content.Effect = radius < 0.05 ? null : ArcBlur.Get(radius);

    private MotionValue<double> Value(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    private async Task RemoveWhenDone(Layer layer, Task<bool> exit)
    {
        // The value completes its task from the frame, on the UI thread.
        await exit;
        if (_leaving.Remove(layer))
        {
            Remove(layer);
        }
    }

    private void Remove(Layer layer)
    {
        layer.Fade?.Dispose();
        layer.Move?.Dispose();
        LogicalChildren.Remove(layer.Content);
        VisualChildren.Remove(layer.Content);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _leaving)
        {
            layer.Content.Measure(availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Content.Measure(availableSize);
        return _current.Content.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        foreach (var layer in _leaving)
        {
            layer.Content.Arrange(rect);
        }
        _current?.Content.Arrange(rect);
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _leaving)
        {
            Remove(layer);
        }
        _leaving.Clear();
        if (_current is { } current)
        {
            current.Fade?.Stop();
            current.Move?.Stop();
            current.Content.Opacity = 1;
            current.Content.Effect = null;
            current.Content.RenderTransform = null;
        }
    }
}

/// <summary>A label slot: every label it can show sits invisibly underneath, so a swap never changes its size (Arc <c>SwapText</c>).</summary>
internal sealed class OnboardingChecklistSwapText : Panel
{
    private readonly TextBlock _sizerA = NewSizer();
    private readonly TextBlock _sizerB = NewSizer();
    private readonly OnboardingChecklistSwap _swap = new() { Kind = OnboardingChecklistSwapKind.Rise, IsHitTestVisible = false };
    private string? _text;

    public OnboardingChecklistSwapText()
    {
        Children.Add(_sizerA);
        Children.Add(_sizerB);
        Children.Add(_swap);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The label being shown.</summary>
    public string? Text => _text;

    public TextWrapping TextWrapping { get; set; } = TextWrapping.Wrap;

    public void Set(string text, string optionA, string optionB, bool animate)
    {
        _sizerA.TextWrapping = TextWrapping;
        _sizerB.TextWrapping = TextWrapping;
        _sizerA.Text = optionA;
        _sizerB.Text = optionB;
        if (text == _text)
        {
            return;
        }
        _text = text;
        _swap.Show(new TextBlock { Text = text, TextWrapping = TextWrapping }, 1, animate);
    }

    private static TextBlock NewSizer() => new() { Opacity = 0, IsHitTestVisible = false };
}
