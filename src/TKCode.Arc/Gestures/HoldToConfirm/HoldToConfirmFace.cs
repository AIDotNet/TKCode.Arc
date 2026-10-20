using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Icon and label of a <see cref="HoldToConfirm"/>. The button renders it twice, once on the surface and once inside
/// the fill, and drives both with the same calls so they move as one and the text changes color exactly at the edge.
/// </summary>
internal sealed class HoldToConfirmFace : StackPanel
{
    private static readonly Geometry DoneCheck = ArcIcons.Parse("M4 12.5l5 5L20 6.5");

    private readonly ButtonsGlyphSwap _icon;
    private readonly ArcContentSwap _label;
    private Geometry? _shownIcon;
    private bool? _shownDone;

    public HoldToConfirmFace()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        _icon = new ButtonsGlyphSwap { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
        _label = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, VerticalAlignment = VerticalAlignment.Center };
        // The frame springs to the new label's width; its clip leaves room for the rise and the soft blur.
        var frame = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Spring = ArcSpring.Morph, Child = _label, VerticalAlignment = VerticalAlignment.Center };
        Children.Add(_icon);
        Children.Add(frame);
    }

    public void Show(string text, Geometry? icon, bool done, bool animate)
    {
        if (!Equals(_label.Content, text))
        {
            _label.Content = text;
        }
        if (_shownDone == done && ReferenceEquals(_shownIcon, icon))
        {
            return;
        }
        _shownDone = done;
        _shownIcon = icon;
        Control? glyph;
        if (done)
        {
            var check = new StrokeDrawGlyph { Data = DoneCheck, Size = 18, StrokeThickness = 2 };
            if (animate && IsLoaded && ArcMotion.IsFull(this))
            {
                // The tick draws itself from its short stroke.
                check.Progress = 0;
                check.AttachedToVisualTree += (_, _) => _ = check.Play(TimeSpan.FromSeconds(0.32), ArcEasings.Enter, TimeSpan.FromSeconds(0.08));
            }
            glyph = check;
        }
        else
        {
            glyph = icon is null ? null : new ArcGlyph { Data = icon, Size = 18, StrokeThickness = 1.75 };
        }
        _icon.Show(glyph, ButtonsGlyphPose.Pop, ButtonsGlyphPose.Pop);
    }
}
