using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Toast"/>: the toast's size limits, padding and the gaps around the stack.</summary>
// The desktop TKCode.Arc package declares a ToastToken too, and identities are unique per application.
[ControlDesignToken(Identity = "MobileToast")]
internal sealed class ToastToken : AbstractControlDesignToken
{
    /// <summary>The smallest height of a toast, a touch target: 44.</summary>
    public double BodyMinHeight { get; set; }

    /// <summary>The widest a toast gets: 360.</summary>
    public double BodyMaxWidth { get; set; }

    /// <summary>Padding inside a toast: 16 at the sides, 10 above and below.</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>Gap between two toasts of a stack: 8.</summary>
    public double StackGap { get; set; }

    /// <summary>Space kept free at the left and right edges of the page: 16.</summary>
    public Thickness SideInset { get; set; }

    /// <summary>Size of the optional icon: 18.</summary>
    public double GlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BodyMinHeight = 44;
        BodyMaxWidth = 360;
        BodyPadding = new Thickness(16, 10);
        StackGap = 8;
        SideInset = new Thickness(16, 0);
        GlyphSize = 18;
    }
}
