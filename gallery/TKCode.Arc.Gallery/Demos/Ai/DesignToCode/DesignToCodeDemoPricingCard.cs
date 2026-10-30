using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The design to code demos' preview: the drawn pricing card as working controls. Monthly and Yearly switch the price,
/// which swaps with a short crossfade on tabular numerals; the features and the call to action are plain controls. Demo
/// content only.
/// </summary>
internal sealed class DesignToCodeDemoPricingCard : UserControl
{
    private static readonly FontFeatureCollection s_tabular = new() { new FontFeature { Tag = "tnum", Value = 1 } };

    private readonly SegmentedControl _billing;
    private readonly ArcContentSwap _price;
    private bool _isYearly;

    /// <summary>Builds the card on Monthly.</summary>
    public DesignToCodeDemoPricingCard()
    {
        Focusable = false;
        var badgeText = new TextBlock { Text = DesignToCodeDemoData.Badge, FontWeight = FontWeight.SemiBold };
        ArcTokens.Bind(badgeText, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(badgeText, TextBlock.ForegroundProperty, ArcDesignTokenKind.Accent);
        var badge = new Border { HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 2), Child = badgeText };
        ArcTokens.Bind(badge, Border.BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
        ArcTokens.Bind(badge, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPill);

        var title = new TextBlock { Text = DesignToCodeDemoData.Title, Margin = new Thickness(0, 10, 0, 0), FontWeight = FontWeight.SemiBold };
        ArcTokens.Bind(title, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeLG);
        var subtitle = Muted(DesignToCodeDemoData.Subtitle);
        subtitle.Margin = new Thickness(0, 2, 0, 0);

        _billing = new SegmentedControl
        {
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items =
            {
                new SegmentedOption("monthly", DesignToCodeDemoData.Monthly),
                new SegmentedOption("yearly", DesignToCodeDemoData.Yearly),
            },
            SelectedIndex = 0,
        };
        _billing.SelectionChanged += (_, _) => IsYearly = _billing.SelectedIndex == 1;

        _price = new ArcContentSwap
        {
            Mode = ContentSwapMode.Label,
            EnterOffset = 2,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        var perMonth = Muted(DesignToCodeDemoData.PerMonth);
        perMonth.Margin = new Thickness(0, 0, 0, 7);
        perMonth.VerticalAlignment = VerticalAlignment.Bottom;
        var price = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0), Spacing = 4, Children = { _price, perMonth } };

        var features = new StackPanel { Margin = new Thickness(0, 18, 0, 0), Spacing = 8 };
        foreach (var feature in DesignToCodeDemoData.Features)
        {
            var check = new ArcGlyph { Data = ArcIcons.Check, Size = 16, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Accent);
            var text = new TextBlock { Text = feature, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
            features.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { check, text } });
        }

        var cta = new Button
        {
            Content = DesignToCodeDemoData.Cta,
            Margin = new Thickness(0, 22, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Variant = ButtonVariant.Primary,
        };

        var card = new Border
        {
            MaxWidth = 300,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(22),
            Child = new StackPanel { Children = { badge, title, subtitle, _billing, price, features, cta } },
        };
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(card, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(card, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusStep18);
        ArcTokens.Bind(card, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        Content = card;
        ShowPrice();
    }

    /// <summary>Whether the yearly price shows (the billing switch follows).</summary>
    public bool IsYearly
    {
        get => _isYearly;
        set
        {
            if (_isYearly == value)
            {
                return;
            }
            _isYearly = value;
            _billing.SelectedIndex = value ? 1 : 0;
            ShowPrice();
        }
    }

    private void ShowPrice()
    {
        var price = _isYearly ? DesignToCodeDemoData.YearlyPrice : DesignToCodeDemoData.MonthlyPrice;
        var text = new TextBlock { Text = "$" + price, FontFeatures = s_tabular, FontWeight = FontWeight.SemiBold };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.Type3XL);
        _price.Content = text;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return block;
    }
}
