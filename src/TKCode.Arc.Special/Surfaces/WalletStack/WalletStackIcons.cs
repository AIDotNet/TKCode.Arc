using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide icons (ISC) for wallet activity that the Core subset does not include, 24×24 stroke geometry. Each property
/// names its Lucide icon. Use them for <see cref="WalletStackTransaction.Icon"/>, next to <see cref="ArcIcons"/>.
/// </summary>
public static class WalletStackIcons
{
    /// <summary>Lucide <c>arrow-down-left</c>: money in (salary, top-ups, refunds).</summary>
    public static Geometry ArrowDownLeft => ArcIcons.Parse("M17 7 7 17 M17 17H7V7");

    /// <summary>Lucide <c>shopping-cart</c>: groceries and shops.</summary>
    public static Geometry ShoppingCart => ArcIcons.Parse(
        "m2.05 2.05 1.099-.028a1 1 0 0 1 1.008.815l2.69 14.347A1 1 0 0 0 7.83 18H18 " +
        "M4.563 5h16.435a1 1 0 0 1 .981 1.204l-1.026 6.226A2 2 0 0 1 18.962 14H6.25 " +
        "M16 20a2 2 0 1 0 4 0a2 2 0 1 0-4 0 M6 20a2 2 0 1 0 4 0a2 2 0 1 0-4 0");

    /// <summary>Lucide <c>utensils</c>: restaurants and dining.</summary>
    public static Geometry Utensils => ArcIcons.Parse(
        "M3 2v7c0 1.1.9 2 2 2h4a2 2 0 0 0 2-2V2 M7 2v20 M21 15V2a5 5 0 0 0-5 5v6c0 1.1.9 2 2 2h3Zm0 0v7");

    /// <summary>Lucide <c>train-front</c>: rail travel.</summary>
    public static Geometry TrainFront => ArcIcons.Parse(
        "M8 3.1V7a4 4 0 0 0 8 0V3.1 M9 15l-1-1 M15 15l1-1 M9 19c-2.8 0-5-2.2-5-5v-4a8 8 0 0 1 16 0v4c0 2.8-2.2 5-5 5Z " +
        "M8 19l-2 3 M16 19l2 3");

    /// <summary>Lucide <c>ship</c>: ferries.</summary>
    public static Geometry Ship => ArcIcons.Parse(
        "M12 2v2 M12 9.189V13 M19 12V6a2 2 0 00-2-2H7a2 2 0 00-2 2v6 " +
        "M19.38 19A11.6 11.6 0 0021 13l-8.188-3.639a2 2 0 00-1.624 0L3 13.001a11.6 11.6 0 002.81 7.76 " +
        "M2 20c.6.5 1.2 1 2.5 1 2.5 0 2.5-2 5-2 1.3 0 1.9.5 2.5 1s1.2 1 2.5 1c2.5 0 2.5-2 5-2 1.3 0 1.9.5 2.5 1");

    /// <summary>Lucide <c>bike</c>: bike and scooter hire.</summary>
    public static Geometry Bike => ArcIcons.Parse(
        "M15 17.5a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0 M2 17.5a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0 " +
        "M14 5a1 1 0 1 0 2 0a1 1 0 1 0-2 0 M12 17.5V14l-3-3 4-3 2 3h2");

    /// <summary>Lucide <c>coffee</c>: cafés.</summary>
    public static Geometry Coffee => ArcIcons.Parse(
        "M10 2v2 M14 2v2 M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1 M6 2v2");

    /// <summary>Lucide <c>cookie</c>: bakeries and snacks.</summary>
    public static Geometry Cookie => ArcIcons.Cookie;

    /// <summary>Lucide <c>zap</c>: utilities and power.</summary>
    public static Geometry Zap => ArcIcons.Parse(
        "M15.914 4a1.5 1.5 0 00-2.474-1.561l-9 9A1.5 1.5 0 005.5 14h4.002a.5.5 0 01.471.666L8.086 20a1.5 1.5 0 002.475 1.56l9-9A1.5 1.5 0 0018.5 10h-3.997a.5.5 0 01-.472-.667z");

    /// <summary>Lucide <c>book-open</c>: books and learning.</summary>
    public static Geometry BookOpen => ArcIcons.Parse(
        "M12 5v16 M20.001 19A2 2 0 0022 17V5a2 2 0 00-1.999-2L16 3.002A5 5 0 0012 5a5 5 0 00-4-2H4a2 2 0 00-2 2v12a2 2 0 001.999 2H8a5 5 0 014 2 5 5 0 014-2z");

    /// <summary>Lucide <c>gift</c>: gifts, bonuses and rewards.</summary>
    public static Geometry Gift => ArcIcons.Parse(
        "M12 7v14 M20 11v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-8 M7.5 7a1 1 0 0 1 0-5A4.8 8 0 0 1 12 7a4.8 8 0 0 1 4.5-5 1 1 0 0 1 0 5 " +
        "M4 7h16a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z");

    /// <summary>Lucide <c>fuel</c>: fuel and charging.</summary>
    public static Geometry Fuel => ArcIcons.Parse(
        "M14 13h2a2 2 0 0 1 2 2v2a2 2 0 0 0 4 0v-6.998a2 2 0 0 0-.59-1.42L18 5 M14 21V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v16 M2 21h13 M3 9h11");

    /// <summary>Lucide <c>dumbbell</c>: gyms and classes.</summary>
    public static Geometry Dumbbell => ArcIcons.Parse(
        "M17.596 12.768a2 2 0 1 0 2.829-2.829l-1.768-1.767a2 2 0 0 0 2.828-2.829l-2.828-2.828a2 2 0 0 0-2.829 2.828l-1.767-1.768a2 2 0 1 0-2.829 2.829z " +
        "M2.5 21.5l1.4-1.4 M20.1 3.9l1.4-1.4 " +
        "M5.343 21.485a2 2 0 1 0 2.829-2.828l1.767 1.768a2 2 0 1 0 2.829-2.829l-6.364-6.364a2 2 0 1 0-2.829 2.829l1.768 1.767a2 2 0 0 0-2.828 2.829z " +
        "M9.6 14.4l4.8-4.8");

    /// <summary>Lucide <c>ticket</c>: events and passes.</summary>
    public static Geometry Ticket => ArcIcons.Parse(
        "M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z M13 5v2 M13 17v2 M13 11v2");
}
