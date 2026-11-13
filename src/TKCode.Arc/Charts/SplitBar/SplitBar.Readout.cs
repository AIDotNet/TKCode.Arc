using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The readout beside the title: the rolling total, or the highlighted part's share and value. Switching crossfades the
// line out (a short rise and a little blur) while the next one comes up into place.
public partial class SplitBar
{
    private const double ReadoutExitSeconds = 0.15;
    private const double ReadoutFadeSeconds = 0.15;
    private const double ReadoutEnterSeconds = 0.32;
    private const double ReadoutTravel = 6;
    private const double ReadoutBlur = 2;
    private static readonly Easing s_linear = new LinearEasing();

    private readonly Control?[] _lines = new Control?[3];
    private readonly SplitBarValue[] _lineOpacity = new SplitBarValue[3];
    private readonly SplitBarValue[] _lineY = new SplitBarValue[3];
    private readonly SplitBarValue[] _lineBlur = new SplitBarValue[3];
    private SplitBarReadout? _readout;
    private RollingDigits? _sumDigits;
    private TextBlock? _ofTotal;
    private TextBlock? _totalLabel;
    private TextBlock? _unitText;
    private int _currentLine;
    private SplitBarSlot? _readoutSlot;

    /// <summary>The readout line showing now: 0 the total, 1 or 2 a highlighted part (tests).</summary>
    internal int ReadoutLine => _currentLine;

    internal SplitBarReadoutLine? ReadoutSegmentLine => _lines[_currentLine] as SplitBarReadoutLine;

    private bool IsReadoutMoving
    {
        get
        {
            for (var i = 0; i < 3; i++)
            {
                if (_lineOpacity[i].IsMoving(_wall) || _lineY[i].IsMoving(_wall) || _lineBlur[i].IsMoving(_wall))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private void ApplyReadoutTemplate(INameScope scope)
    {
        _readout = scope.Find<SplitBarReadout>("PART_Readout");
        _sumDigits = scope.Find<RollingDigits>(SumPart);
        _ofTotal = scope.Find<TextBlock>(OfTotalPart);
        _totalLabel = scope.Find<TextBlock>("PART_TotalLabel");
        _unitText = scope.Find<TextBlock>("PART_Unit");
        _lines[0] = scope.Find<Control>("PART_TotalLine");
        _lines[1] = scope.Find<SplitBarReadoutLine>("PART_LineA");
        _lines[2] = scope.Find<SplitBarReadoutLine>("PART_LineB");
        _currentLine = 0;
        _readoutSlot = null;
        for (var i = 0; i < 3; i++)
        {
            _lineOpacity[i].Jump(i == 0 ? 1 : 0);
            _lineY[i].Jump(0);
            _lineBlur[i].Jump(0);
            if (_lines[i] is { } line)
            {
                line.IsHitTestVisible = false;
                line.RenderTransform = new TranslateTransform();
            }
        }
        if (_sumDigits is not null)
        {
            _sumDigits.WheelMotion = DigitMotion;
        }
        ApplyReadoutVisuals();
    }

    private void UpdateReadout(bool animate)
    {
        if (_sumDigits is not null)
        {
            _sumDigits.Decimals = ValueDecimals;
            _sumDigits.Prefix = ValuePrefix;
            _sumDigits.Suffix = ValueSuffix;
            _sumDigits.IsInstant = !animate;
            _sumDigits.Value = _sum;
        }
        if (_totalLabel is not null)
        {
            _totalLabel.Text = TotalLabel;
            _totalLabel.IsVisible = !string.IsNullOrEmpty(TotalLabel);
        }
        if (_ofTotal is not null)
        {
            var total = Total;
            var hasTotal = double.IsFinite(total) && total > 0;
            _ofTotal.IsVisible = hasTotal;
            _ofTotal.Text = hasTotal ? "of " + FormatValue(total) : null;
        }
        if (_unitText is not null)
        {
            _unitText.Text = Unit;
            _unitText.IsVisible = !string.IsNullOrEmpty(Unit);
        }
        var highlighted = HighlightedSlot();
        if (!ReferenceEquals(highlighted, _readoutSlot))
        {
            var next = highlighted is null ? 0 : _currentLine == 1 ? 2 : 1;
            if (highlighted is not null && _lines[next] is SplitBarReadoutLine fresh)
            {
                fresh.Show(this, highlighted, instant: true);
            }
            SwitchLine(next, animate);
            _readoutSlot = highlighted;
        }
        else if (highlighted is not null && _lines[_currentLine] is SplitBarReadoutLine line)
        {
            line.Show(this, highlighted, instant: !animate);
        }
        if (_readout is not null)
        {
            _readout.Current = _currentLine;
        }
    }

    private void SwitchLine(int next, bool animate)
    {
        var previous = _currentLine;
        _currentLine = next;
        if (previous == next)
        {
            return;
        }
        if (animate)
        {
            _lineOpacity[previous].Tween(0, _wall, ReadoutExitSeconds, s_linear);
            _lineY[previous].Tween(-ReadoutTravel, _wall, ReadoutExitSeconds, s_linear);
            _lineBlur[previous].Tween(ReadoutBlur, _wall, ReadoutExitSeconds, s_linear);
            _lineOpacity[next].Jump(0);
            _lineY[next].Jump(ReadoutTravel);
            _lineBlur[next].Jump(ReadoutBlur);
            _lineOpacity[next].Tween(1, _wall, ReadoutFadeSeconds, ArcEasings.InOut);
            _lineY[next].Tween(0, _wall, ReadoutEnterSeconds, ArcEasings.Standard);
            _lineBlur[next].Tween(0, _wall, ReadoutEnterSeconds, ArcEasings.Standard);
            _loop.Start();
        }
        else
        {
            for (var i = 0; i < 3; i++)
            {
                _lineOpacity[i].Jump(i == next ? 1 : 0);
                _lineY[i].Jump(0);
                _lineBlur[i].Jump(0);
            }
        }
        ApplyReadoutVisuals();
    }

    private void AnimateReadout()
    {
        if (IsReadoutMoving || _readoutDirty)
        {
            ApplyReadoutVisuals();
        }
    }

    private bool _readoutDirty;

    private void SettleReadout()
    {
        for (var i = 0; i < 3; i++)
        {
            _lineOpacity[i].Jump(_lineOpacity[i].Target);
            _lineY[i].Jump(_lineY[i].Target);
            _lineBlur[i].Jump(_lineBlur[i].Target);
        }
        _readoutDirty = true;
    }

    private void ApplyReadoutVisuals()
    {
        _readoutDirty = false;
        for (var i = 0; i < 3; i++)
        {
            if (_lines[i] is not { } line)
            {
                continue;
            }
            var opacity = Math.Clamp(_lineOpacity[i].Value(_wall), 0, 1);
            line.Opacity = opacity;
            line.IsVisible = opacity > 0.001 || i == _currentLine;
            if (line.RenderTransform is TranslateTransform translate)
            {
                translate.Y = _lineY[i].Value(_wall);
            }
            var blur = _lineBlur[i].Value(_wall);
            if (blur > 0.05)
            {
                if (line.Effect is BlurEffect effect)
                {
                    effect.Radius = blur;
                }
                else
                {
                    line.Effect = new BlurEffect { Radius = blur };
                }
            }
            else if (line.Effect is not null)
            {
                line.Effect = null;
            }
        }
    }
}
