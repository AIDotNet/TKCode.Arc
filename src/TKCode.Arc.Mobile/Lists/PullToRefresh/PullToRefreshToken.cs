
namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="PullToRefresh"/>: the indicator's height and the size of its arrow and spinner.</summary>
[ControlDesignToken]
internal sealed class PullToRefreshToken : AbstractControlDesignToken
{
    /// <summary>Height of the indicator, which is also how far the content rests below the top while refreshing: 56.</summary>
    public double IndicatorExtent { get; set; }

    /// <summary>Size of the arrow shown while pulling: 20.</summary>
    public double ArrowSize { get; set; }

    /// <summary>Size of the spinner shown while refreshing: 22.</summary>
    public double SpinnerSize { get; set; }

    /// <summary>Stroke width of the spinner ring: 2.</summary>
    public double SpinnerThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        IndicatorExtent = 56;
        ArrowSize = 20;
        SpinnerSize = 22;
        SpinnerThickness = 2;
    }
}
