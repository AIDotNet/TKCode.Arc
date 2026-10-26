using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile checkbox: a 22px rounded box with a check, and the label (the content) beside it, in a row at least 44 high
/// so the whole row is the touch target. A finger on the box shrinks it on a spring (the row itself does not move); there
/// is no hover state. The check draws itself in when it appears, and with <c>IsThreeState</c> it also has an
/// indeterminate state, shown as a dash that morphs into the check and back. With reduced motion the marks appear at once.
/// </summary>
[TemplatePart(CheckPart, typeof(StrokeDrawGlyph))]
[TemplatePart(MarkPart, typeof(MorphGlyph))]
public class Checkbox : ToggleButton
{
    private const string CheckPart = "PART_Check";
    private const string MarkPart = "PART_Mark";
    private const string CheckData = "M20 6 9 17l-5-5";
    private const string DashData = "M5 12h14";

    private StrokeDrawGlyph? _check;
    private MorphGlyph? _mark;
    private bool? _applied;
    private bool _hasApplied;

    protected override Type StyleKeyOverride => typeof(Checkbox);

    /// <summary>The glyph that draws the check in when the box goes from unchecked to checked, for tests.</summary>
    internal StrokeDrawGlyph? CheckGlyph => _check;

    /// <summary>The glyph that shows the dash, and morphs between dash and check, for tests.</summary>
    internal MorphGlyph? MarkGlyph => _mark;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _check = e.NameScope.Find<StrokeDrawGlyph>(CheckPart);
        _mark = e.NameScope.Find<MorphGlyph>(MarkPart);
        _hasApplied = false;
        Apply(IsChecked, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            Apply(change.GetNewValue<bool?>(), IsLoaded);
        }
    }

    // The marks are driven from code because one glyph pair has three states and two transitions (draw in, dash <-> check
    // morph) that a ControlTheme setter cannot sequence; colors, opacity and the press scale stay in the theme.
    private void Apply(bool? state, bool animate)
    {
        if (_check is null || _mark is null)
        {
            return;
        }
        var full = animate && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var previous = _applied;
        var hadPrevious = _hasApplied;
        _applied = state;
        _hasApplied = true;
        if (state == true)
        {
            if (hadPrevious && previous is null)
            {
                // Dash -> check: the same stroke morphs.
                _check.Opacity = 0;
                _mark.Opacity = 1;
                _ = _mark.MorphTo(CheckData, full);
            }
            else
            {
                // Unchecked -> checked: the check draws from its start to its end.
                _mark.Opacity = 0;
                _check.Opacity = 1;
                if (full)
                {
                    _check.Progress = 0;
                    _ = _check.Play(ArcMotionPresets.Standard, ArcEasings.Enter);
                }
                else
                {
                    _check.Progress = 1;
                }
            }
        }
        else if (state is null)
        {
            _check.Opacity = 0;
            _mark.Opacity = 1;
            if (hadPrevious && previous == true)
            {
                // Check -> dash: start from the check (a drawn-in check has no morph source of its own).
                _ = _mark.MorphTo(CheckData, false);
                _ = _mark.MorphTo(DashData, full);
            }
            else
            {
                _ = _mark.MorphTo(DashData, false);
            }
        }
        else
        {
            _check.Opacity = 0;
            _mark.Opacity = 0;
        }
    }
}
