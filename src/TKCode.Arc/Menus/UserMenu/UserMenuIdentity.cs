using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The compact identity header of the user menu: a 34px face (with the presence dot when status shows), the name,
/// an optional plan badge (a long plan shrinks first) and the email, both ellipsizing instead of widening the panel.
/// Created on the first open.
/// </summary>
internal sealed class UserMenuIdentity : Grid
{
    private static readonly StyledProperty<IBrush?> ForegroundTintProperty =
        AvaloniaProperty.Register<UserMenuIdentity, IBrush?>("ForegroundTint");

    private readonly Border _plan;

    public UserMenuIdentity(UserMenu owner)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        Margin = new Thickness(8);

        var avatar = new Avatar { Width = 34, Height = 34, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        avatar.Bind(Avatar.DisplayNameProperty, owner.GetObservable(UserMenu.DisplayNameProperty));
        avatar.Bind(Avatar.SourceProperty, owner.GetObservable(UserMenu.AvatarSourceProperty));
        var dot = new UserMenuPresenceDot
        {
            DotSize = 10,
            RingThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -3, -3)
        };
        ArcTokens.Bind(dot, UserMenuPresenceDot.SurfaceBrushProperty, ArcDesignTokenKind.SurfaceRaised);
        dot.Bind(UserMenuPresenceDot.StatusProperty, owner.GetObservable(UserMenu.StatusProperty));
        dot.Bind(IsVisibleProperty, owner.GetObservable(UserMenu.IsStatusVisibleProperty));
        var face = new Panel { Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0), Children = { avatar, dot } };
        Children.Add(face);

        var name = new TextBlock
        {
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        name.Bind(TextBlock.TextProperty, owner.GetObservable(UserMenu.DisplayNameProperty));
        ArcTokens.Bind(name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);

        var planText = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        planText.Bind(TextBlock.TextProperty, owner.GetObservable(UserMenu.PlanProperty));
        ArcTokens.Bind(planText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(planText, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        _plan = new Border
        {
            Height = 17,
            Padding = new Thickness(5, 0),
            BorderThickness = new Thickness(0.5),
            Margin = new Thickness(6, 0, 0, 0),
            MaxWidth = 96,
            VerticalAlignment = VerticalAlignment.Center,
            Child = planText
        };
        _plan.BindCornerRadius(ArcDesignTokenKind.RadiusStep4);
        _plan.Bind(IsVisibleProperty, owner.GetObservable(UserMenu.PlanProperty, plan => !string.IsNullOrEmpty(plan)));

        var nameRow = new DockPanel();
        DockPanel.SetDock(_plan, Dock.Right);
        nameRow.Children.Add(_plan);
        nameRow.Children.Add(name);
        nameRow.HorizontalAlignment = HorizontalAlignment.Left;

        var email = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) };
        email.Bind(TextBlock.TextProperty, owner.GetObservable(UserMenu.EmailProperty));
        ArcTokens.Bind(email, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);

        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameRow, email } };
        SetColumn(identity, 1);
        Children.Add(identity);

        ArcTokens.Bind(this, ForegroundTintProperty, ArcDesignTokenKind.Foreground);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundTintProperty && change.NewValue is ISolidColorBrush brush)
        {
            // Plan badge: a 4% foreground wash inside a 9% hairline (accent independent).
            _plan.Background = new ImmutableSolidColorBrush(Oklch.WithAlpha(brush.Color, 0.04));
            _plan.BorderBrush = new ImmutableSolidColorBrush(Oklch.WithAlpha(brush.Color, 0.09));
        }
    }
}
