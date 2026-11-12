using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The buttons of <see cref="ImportWizard"/> (Arc's block buttons, not Arc's Button component): 36 tall, padding 0 16, a
/// capsule, 15px icons 7 from the 14px label, pressed scale 0.97 (160ms). Primary: accent fill and on-accent text with no
/// hover change, 0.3 opacity when disabled. Ghost: surface fill, 1px border, muted surface on hover. Styled by its
/// ControlTheme in <c>ImportWizardTheme.axaml</c>.
/// </summary>
[PseudoClasses(PrimaryPseudoClass, GhostPseudoClass)]
internal sealed class ImportWizardButton : AvaloniaButton
{
    public const string PrimaryPseudoClass = ":primary";
    public const string GhostPseudoClass = ":ghost";

    private readonly StackPanel _row;
    private readonly TextBlock _label;
    private ArcGlyph? _start;
    private ArcGlyph? _end;

    public ImportWizardButton(bool primary, string text, Geometry? iconStart = null, Geometry? iconEnd = null)
    {
        IsPrimary = primary;
        PseudoClasses.Set(PrimaryPseudoClass, primary);
        PseudoClasses.Set(GhostPseudoClass, !primary);
        _label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, LineHeight = 19.6 };
        _row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        Content = _row;
        SetIcons(iconStart, iconEnd);
        _row.Children.Add(_label);
        if (_end is not null)
        {
            _row.Children.Remove(_end);
            _row.Children.Add(_end);
        }
        AutomationProperties.SetName(this, text);
    }

    /// <summary>True for the accent button.</summary>
    public bool IsPrimary { get; }

    /// <summary>The label text block (a view can put rolling numbers next to it).</summary>
    public TextBlock Label => _label;

    /// <summary>The row of icon, label and icon (a view can replace the label).</summary>
    public StackPanel Row => _row;

    /// <summary>The label.</summary>
    public string Text
    {
        get => _label.Text ?? string.Empty;
        set
        {
            _label.Text = value;
            AutomationProperties.SetName(this, value);
        }
    }

    /// <summary>Sets the leading and trailing icons (Lucide 24-unit geometry, drawn at 15px).</summary>
    public void SetIcons(Geometry? start, Geometry? end)
    {
        if (_start is not null)
        {
            _row.Children.Remove(_start);
        }
        if (_end is not null)
        {
            _row.Children.Remove(_end);
        }
        _start = start is null ? null : Glyph(start);
        _end = end is null ? null : Glyph(end);
        if (_start is not null)
        {
            _row.Children.Insert(0, _start);
        }
        if (_end is not null)
        {
            _row.Children.Add(_end);
        }
    }

    private ArcGlyph Glyph(Geometry data)
    {
        // Follows the button's foreground (on-accent or text).
        var glyph = new ArcGlyph { Data = data, Size = 15, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        glyph.Bind(ArcGlyph.ForegroundProperty, this.GetObservable(ForegroundProperty));
        return glyph;
    }

    protected override Type StyleKeyOverride => typeof(ImportWizardButton);
}
