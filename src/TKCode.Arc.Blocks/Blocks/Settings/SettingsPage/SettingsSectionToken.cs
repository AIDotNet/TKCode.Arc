using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of a <see cref="SettingsSection"/> shown in its page.</summary>
[ControlDesignToken]
internal sealed class SettingsSectionToken : AbstractControlDesignToken
{
    /// <summary>Gap above the description.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Gap above the section content.</summary>
    public Thickness ContentMargin { get; set; }

    /// <summary>Line height of the description (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DescriptionMargin = new Thickness(0, 4, 0, 0);
        ContentMargin = new Thickness(0, 24, 0, 0);
        BodyLineHeight = 14 * 1.4;
    }
}
