namespace TKCode.Arc;

/// <summary>
/// Optional text overrides for <see cref="UsageMeter"/>. Set <see cref="UsageMeter.Labels"/> (replacing the object when
/// switching languages) to override any visible caption, badge, default legend label or screen-reader summary; any property
/// left null falls back to the active <see cref="Localization.ArcLanguage"/> resource.
/// </summary>
public sealed class UsageMeterLabels
{
    /// <summary>Legend label for the remaining allowance when <see cref="UsageMeter.FreeLabel"/> is not set.</summary>
    public string? FreeLabel { get; init; }

    /// <summary>Legend label for the overage when <see cref="UsageMeter.OverLabel"/> is not set.</summary>
    public string? OverLabel { get; init; }

    /// <summary>Caption format for the total summary: <c>{0}</c> = unit, <c>{1}</c> = formatted limit.</summary>
    public string? TotalCaptionFormat { get; init; }

    /// <summary>Caption format when previewing the remaining allowance: <c>{0}</c> = unit.</summary>
    public string? FreeCaptionFormat { get; init; }

    /// <summary>Caption format when previewing the overage: <c>{0}</c> = unit.</summary>
    public string? OverCaptionFormat { get; init; }

    /// <summary>Caption format when previewing a category: <c>{0}</c> = unit, <c>{1}</c> = category label, <c>{2}</c> = share percentage.</summary>
    public string? SegmentCaptionFormat { get; init; }

    /// <summary>Status badge format while under the warning threshold: <c>{0}</c> = formatted remaining allowance.</summary>
    public string? FreeBadgeFormat { get; init; }

    /// <summary>Status badge text while at or past <see cref="UsageMeter.WarnThreshold"/>.</summary>
    public string? NearBadgeText { get; init; }

    /// <summary>Status badge format while past the limit: <c>{0}</c> = formatted overage.</summary>
    public string? OverBadgeFormat { get; init; }

    /// <summary>Accessible name format for the bar: <c>{0}</c> = label, <c>{1}</c> = formatted total, <c>{2}</c> = formatted limit, <c>{3}</c> = segment list, <c>{4}</c> = tail.</summary>
    public string? BarSummaryFormat { get; init; }

    /// <summary>Accessible bar tail while under the warning threshold: <c>{0}</c> = formatted remaining allowance.</summary>
    public string? BarFreeTailFormat { get; init; }

    /// <summary>Accessible bar tail while near the limit: <c>{0}</c> = formatted remaining allowance.</summary>
    public string? BarNearTailFormat { get; init; }

    /// <summary>Accessible bar tail while past the limit: <c>{0}</c> = formatted overage.</summary>
    public string? BarOverTailFormat { get; init; }

    /// <summary>Live status announcement when returning under the limit: <c>{0}</c> = formatted remaining allowance.</summary>
    public string? StatusOkFormat { get; init; }

    /// <summary>Live status announcement when entering the near-limit state: <c>{0}</c> = formatted remaining allowance.</summary>
    public string? StatusNearFormat { get; init; }

    /// <summary>Live status announcement when past the limit: <c>{0}</c> = formatted overage.</summary>
    public string? StatusOverFormat { get; init; }
}
