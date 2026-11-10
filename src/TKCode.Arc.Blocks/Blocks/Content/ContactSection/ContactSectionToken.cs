using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="ContactSection"/> block (contact-section.module.css).</summary>
[ControlDesignToken]
internal sealed class ContactSectionToken : AbstractControlDesignToken
{
    /// <summary>The section's content is at most 1120 wide.</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The padding of a channel in the list.</summary>
    public Thickness ChannelPadding { get; set; }

    /// <summary>The padding of a topic chip.</summary>
    public Thickness TopicPadding { get; set; }

    /// <summary>The padding of an office card.</summary>
    public Thickness OfficePadding { get; set; }

    /// <summary>The diameter of an office's status dot (7).</summary>
    public double StatusDotSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContentMaxWidth = 1120;
        ChannelPadding = new Thickness(16, 12);
        TopicPadding = new Thickness(16, 0);
        OfficePadding = new Thickness(24);
        StatusDotSize = 7;
    }
}
