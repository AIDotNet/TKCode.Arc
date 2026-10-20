using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The "Appearance" popover: light or dark, one of Arc's eight accents, and the motion level. The gallery's
/// whole job is to show components under those settings, so they live one click away from every page.
/// </summary>
public sealed class GalleryAppearancePanel : Border
{
    private readonly GalleryTheme _theme;
    private readonly Window _owner;
    private readonly GallerySegmented _mode = new("Light", "Dark");
    private readonly GallerySegmented _motion = new("Full", "Reduced", "Off");
    private readonly Dictionary<ArcAccentPreset, (Button Button, Ellipse Dot)> _swatches = new();
    private readonly Dictionary<ArcAccentPreset, IDisposable?> _rings = new();

    public GalleryAppearancePanel(GalleryTheme theme, Window owner)
    {
        _theme = theme;
        _owner = owner;

        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(Section("Mode", _mode));
        stack.Children.Add(Section("Accent", BuildAccents()));
        stack.Children.Add(Section("Motion", _motion));

        Width = 292;
        Padding = new Thickness(16);
        CornerRadius = new CornerRadius(26);
        BorderThickness = new Thickness(1);
        Child = stack;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);

        _mode.SelectionChanged += async index => await _theme.SetDarkAsync(index == 1);
        _motion.SelectionChanged += index => _theme.SetMotion(_owner, index switch
        {
            0 => ArcMotionLevel.Full,
            1 => ArcMotionLevel.Reduced,
            _ => ArcMotionLevel.Off
        });
        _theme.Changed += Sync;
        Sync();
    }

    private static Control Section(string label, Control content)
    {
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(GalleryUi.Text(label, ArcDesignTokenKind.TextMuted, 12, FontWeight.Medium));
        section.Children.Add(content);
        return section;
    }

    private Control BuildAccents()
    {
        var grid = new UniformGrid { Columns = 8 };
        foreach (var preset in Enum.GetValues<ArcAccentPreset>())
        {
            var dot = new Ellipse { Width = 18, Height = 18 };
            var swatch = new Button
            {
                Content = dot,
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                MinHeight = 28,
                MinWidth = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                IsPressScaleEnabled = true
            };
            Avalonia.Automation.AutomationProperties.SetName(swatch, preset.ToString());
            Tooltip.SetTip(swatch, preset.ToString());
            var captured = preset;
            swatch.Click += async (_, _) => await _theme.SetAccentAsync(captured);
            _swatches[preset] = (swatch, dot);
            _rings[preset] = null;
            grid.Children.Add(swatch);
        }
        return grid;
    }

    private void Sync()
    {
        _mode.SelectedIndex = _theme.IsDark ? 1 : 0;
        _motion.SelectedIndex = _theme.MotionLevel switch
        {
            ArcMotionLevel.Full => 0,
            ArcMotionLevel.Reduced => 1,
            _ => 2
        };
        foreach (var (preset, parts) in _swatches)
        {
            parts.Dot.Fill = new SolidColorBrush(ArcPalette.Resolve(ArcPalette.GetSeed(preset), _theme.IsDark).Accent);
            _rings[preset]?.Dispose();
            _rings[preset] = preset == _theme.Accent
                ? ArcTokens.Bind(parts.Button, TemplatedControl.BorderBrushProperty, ArcDesignTokenKind.Foreground, BindingPriority.LocalValue)
                : null;
        }
    }
}
