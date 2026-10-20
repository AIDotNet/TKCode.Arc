using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
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
/// The "Appearance" popover: light or dark, one of Arc's eight accents, the motion level and the language. The
/// gallery's whole job is to show components under those settings, so they live one click away from every page.
/// </summary>
public sealed class GalleryAppearancePanel : Border
{
    private readonly GalleryTheme _theme;
    private readonly Control _owner;
    // Language names are endonyms: each reads the same in every language, so they are not translated.
    private static readonly (string Tag, string Name)[] s_languages = [("en-US", "English"), ("zh-CN", "简体中文"), ("zh-TW", "繁體中文")];

    private readonly GallerySegmented _mode = new(GalleryShellLangResourceKind.Light, GalleryShellLangResourceKind.Dark);
    private readonly GallerySegmented _motion = new(GalleryShellLangResourceKind.MotionFull, GalleryShellLangResourceKind.MotionReduced, GalleryShellLangResourceKind.MotionOff);
    private readonly GallerySegmented _language = new(s_languages.Select(l => l.Name).ToArray());
    private readonly Dictionary<ArcAccentPreset, (Button Button, Ellipse Dot)> _swatches = new();
    private readonly Dictionary<ArcAccentPreset, IDisposable?> _rings = new();

    public GalleryAppearancePanel(GalleryTheme theme, Control owner)
    {
        _theme = theme;
        _owner = owner;

        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(Section(GalleryShellLangResourceKind.Mode, _mode));
        stack.Children.Add(Section(GalleryShellLangResourceKind.Accent, BuildAccents()));
        stack.Children.Add(Section(GalleryShellLangResourceKind.Motion, _motion));
        stack.Children.Add(Section(GalleryShellLangResourceKind.Language, _language));

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
        _language.SelectionChanged += index => GalleryText.SetLanguage(s_languages[index].Tag);
        _theme.Changed += Sync;
        Sync();
        // The language can also change from outside (a host, a test); follow it while the panel is open.
        GalleryText.Track(this, SyncLanguage);
    }

    private static Control Section(GalleryShellLangResourceKind label, Control content)
    {
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, label);
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(title);
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
            var name = AccentName(preset);
            GalleryText.Bind(swatch, Avalonia.Automation.AutomationProperties.NameProperty, name);
            GalleryText.Bind(swatch, Tooltip.TipProperty, name);
            var captured = preset;
            swatch.Click += async (_, _) => await _theme.SetAccentAsync(captured);
            _swatches[preset] = (swatch, dot);
            _rings[preset] = null;
            grid.Children.Add(swatch);
        }
        return grid;
    }

    private static GalleryShellLangResourceKind AccentName(ArcAccentPreset preset) => preset switch
    {
        ArcAccentPreset.Neutral => GalleryShellLangResourceKind.AccentNeutral,
        ArcAccentPreset.Violet => GalleryShellLangResourceKind.AccentViolet,
        ArcAccentPreset.Blue => GalleryShellLangResourceKind.AccentBlue,
        ArcAccentPreset.Green => GalleryShellLangResourceKind.AccentGreen,
        ArcAccentPreset.Amber => GalleryShellLangResourceKind.AccentAmber,
        ArcAccentPreset.Orange => GalleryShellLangResourceKind.AccentOrange,
        ArcAccentPreset.Coral => GalleryShellLangResourceKind.AccentCoral,
        _ => GalleryShellLangResourceKind.AccentRose
    };

    private void SyncLanguage()
    {
        var index = Array.FindIndex(s_languages, l => string.Equals(l.Tag, GalleryText.CurrentLanguage, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _language.SelectedIndex = index;
        }
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
