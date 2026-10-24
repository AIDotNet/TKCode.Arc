using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Builders for the text and lines of the <see cref="LoginSplit"/> steps; every color, size and font is a design token.</summary>
internal static class LoginSplitUi
{
    private static readonly Avalonia.Animation.Easings.Easing s_easeOut = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);

    /// <summary>Body text: 14 / 19.6 by default, tracking −0.01em.</summary>
    public static TextBlock Text(string? text, ArcDesignTokenKind color, bool small = false, bool medium = false)
    {
        var block = new TextBlock
        {
            Text = text,
            LineHeight = small ? LoginSplitMetrics.SmallLineHeight : LoginSplitMetrics.BodyLineHeight,
            LetterSpacing = small ? LoginSplitMetrics.SmallLetterSpacing : LoginSplitMetrics.BodyLetterSpacing,
            TextWrapping = TextWrapping.Wrap
        };
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, small ? ArcDesignTokenKind.TypeXS : ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(block, TextBlock.FontWeightProperty, medium ? ArcDesignTokenKind.WeightMedium : ArcDesignTokenKind.WeightRegular);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }

    /// <summary>
    /// Text that takes its color and weight from its button (hover, unavailable): body face, 14 / 19.6 or 12 / 16.8, one line.
    /// </summary>
    public static TextBlock Plain(string? text, bool small = false)
    {
        var block = new TextBlock
        {
            Text = text,
            LineHeight = small ? LoginSplitMetrics.SmallLineHeight : LoginSplitMetrics.BodyLineHeight,
            LetterSpacing = small ? LoginSplitMetrics.SmallLetterSpacing : LoginSplitMetrics.BodyLetterSpacing,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, small ? ArcDesignTokenKind.TypeXS : ArcDesignTokenKind.TypeSM);
        return block;
    }

    /// <summary>A step heading: the display face at 36 / 39.6, tracking −0.03em.</summary>
    public static TextBlock Heading(string? text)
    {
        var block = new TextBlock
        {
            Text = text,
            LineHeight = LoginSplitMetrics.HeadingLineHeight,
            LetterSpacing = LoginSplitMetrics.HeadingLetterSpacing,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetHeadingLevel(block, 2);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.Type3XL);
        ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightRegular);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        return block;
    }

    /// <summary>
    /// A line that swaps in place (Arc's swap): the new text rises 6px out of a 4px blur in 240 ms on the enter curve, the
    /// old one lifts 4px and blurs to 2px over 160 ms; reduced motion fades the new text in 120 ms and drops the old one.
    /// </summary>
    public static SiteSwap Swap(HorizontalAlignment alignment = HorizontalAlignment.Left) =>
        Configure(new SiteSwap { HorizontalContentAlignment = alignment });

    /// <summary>Gives <paramref name="swap"/> Arc's swap values (see <see cref="Swap"/>).</summary>
    public static SiteSwap Configure(SiteSwap swap)
    {
        swap.EnterOffset = 6;
        swap.EnterBlur = 4;
        swap.EnterDuration = TKCode.Arc.Motion.ArcMotionPresets.Standard;
        swap.ExitOffset = -4;
        swap.ExitBlur = 2;
        swap.ExitDuration = TKCode.Arc.Motion.ArcMotionPresets.Fast;
        swap.ExitEasing = s_easeOut;
        swap.ReducedEnterDuration = TKCode.Arc.Motion.ArcMotionPresets.Instant;
        swap.ReducedExitDuration = TimeSpan.Zero;
        return swap;
    }

    /// <summary>A 1px hairline in the border color.</summary>
    public static Border Hairline()
    {
        var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(line, Border.BackgroundProperty, ArcDesignTokenKind.Border);
        return line;
    }

    /// <summary>A heading block: the heading and the line under it, 8 apart, 24 above what follows.</summary>
    public static StackPanel HeadingBlock(Control title, Control below) => new()
    {
        Spacing = LoginSplitMetrics.HeadingGap,
        Margin = new Thickness(0, 0, 0, LoginSplitMetrics.HeadingMargin),
        Children = { title, below }
    };
}

/// <summary>A step of <see cref="LoginSplit"/>: its content with 8 above and below and 12 at the sides (room for blur and focus rings).</summary>
internal abstract class LoginSplitStepView : Border
{
    protected LoginSplitStepView(LoginSplitStep step)
    {
        Step = step;
        Padding = LoginSplitMetrics.StepInset;
        ClipToBounds = false;
    }

    /// <summary>The step this view shows.</summary>
    public LoginSplitStep Step { get; }
}

/// <summary>The email step: heading, the email field, Continue, "or" and the single sign-on buttons.</summary>
internal sealed class LoginSplitEmailView : LoginSplitStepView
{
    public LoginSplitEmailView(LoginSplitLabels labels, IReadOnlyList<LoginSplitProvider> providers) : base(LoginSplitStep.Email)
    {
        EmailInput = new Input
        {
            Label = labels.EmailLabel,
            Placeholder = labels.EmailPlaceholder,
            Size = ControlSize.Medium
        };
        ContinueButton = new Button
        {
            Content = labels.Continue,
            Variant = ButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var form = new StackPanel { Spacing = LoginSplitMetrics.FormGap, Children = { EmailInput, ContinueButton } };
        var content = new StackPanel
        {
            Children =
            {
                LoginSplitUi.HeadingBlock(LoginSplitUi.Heading(labels.EmailTitle), LoginSplitUi.Text(labels.EmailSubtitle, ArcDesignTokenKind.TextSecondary)),
                form
            }
        };

        var buttons = new List<Button>();
        if (providers.Count > 0)
        {
            var or = LoginSplitUi.Text(labels.Or, ArcDesignTokenKind.TextMuted, small: true);
            or.LineHeight = 12;
            or.Margin = new Thickness(LoginSplitMetrics.DividerGap, 0);
            Grid.SetColumn(or, 1);
            var right = LoginSplitUi.Hairline();
            Grid.SetColumn(right, 2);
            Divider = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
                Margin = new Thickness(0, LoginSplitMetrics.DividerMargin),
                Children = { LoginSplitUi.Hairline(), or, right }
            };
            AutomationProperties.SetAccessibilityView(Divider, AccessibilityView.Raw);

            var grid = new Grid();
            for (var i = 0; i < providers.Count; i++)
            {
                if (i > 0)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(LoginSplitMetrics.ProviderGap, GridUnitType.Pixel));
                }
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                var button = ProviderButton(providers[i], labels);
                Grid.SetColumn(button, i * 2);
                grid.Children.Add(button);
                buttons.Add(button);
            }
            AutomationProperties.SetName(grid, labels.ProvidersGroup);
            ProviderGroup = grid;
            content.Children.Add(Divider);
            content.Children.Add(grid);
        }
        ProviderButtons = buttons;
        Child = content;
    }

    public Input EmailInput { get; }

    public Button ContinueButton { get; }

    /// <summary>The "or" divider; null without providers.</summary>
    public Grid? Divider { get; }

    /// <summary>The single sign-on group; null without providers.</summary>
    public Grid? ProviderGroup { get; }

    public IReadOnlyList<Button> ProviderButtons { get; }

    private static AuthProviderKind ProviderKind(LoginSplitProviderMark mark) => mark switch
    {
        LoginSplitProviderMark.Google => AuthProviderKind.Google,
        LoginSplitProviderMark.Apple => AuthProviderKind.Apple,
        LoginSplitProviderMark.GitHub => AuthProviderKind.GitHub,
        _ => AuthProviderKind.None
    };

    private static Button ProviderButton(LoginSplitProvider provider, LoginSplitLabels labels)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LoginSplitMetrics.ProviderGap };
        if (provider.Icon is not null || provider.Mark != LoginSplitProviderMark.None)
        {
            row.Children.Add(new AuthProviderMark
            {
                // Read at render time; a custom icon wins over the built-in mark.
                Look = () => new AuthProviderLook(provider.Icon is null ? ProviderKind(provider.Mark) : AuthProviderKind.None, provider.Icon),
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        row.Children.Add(new TextBlock { Text = provider.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        var button = new Button
        {
            Content = row,
            Tag = provider,
            Variant = ButtonVariant.Secondary,
            Padding = LoginSplitMetrics.ProviderInset,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0
        };
        AutomationProperties.SetName(button, labels.ContinueWith(provider.Name));
        return button;
    }
}

/// <summary>
/// The password step: heading, the account chip, the password field with "Forgot password?" at the end of its label row,
/// the reset notice with the resend button, and Sign in (which narrows to a check once accepted).
/// </summary>
internal sealed class LoginSplitPasswordView : LoginSplitStepView
{
    public LoginSplitPasswordView(LoginSplitLabels labels, LoginSplitAccount account, string? hint) : base(LoginSplitStep.Password)
    {
        Chip = new LoginSplitChip
        {
            DisplayName = account.Name,
            Email = account.Email,
            Picture = account.Picture,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetName(Chip, labels.ChangeEmail(account.Email));

        PasswordField = new PasswordField
        {
            Label = labels.PasswordLabel,
            Description = hint,
            Size = ControlSize.Medium
        };
        ForgotLabel = LoginSplitUi.Swap(HorizontalAlignment.Right);
        ForgotLabel.Content = LoginSplitUi.Plain(labels.ForgotPassword);
        ForgotButton = new LoginSplitTextButton
        {
            Content = ForgotLabel,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -LoginSplitMetrics.ForgotInset.Top, -LoginSplitMetrics.ForgotInset.Right, 0),
            Classes = { "forgot" }
        };
        AutomationProperties.SetName(ForgotButton, labels.ForgotPassword);
        var field = new Panel { Children = { PasswordField, ForgotButton } };

        NoticeText = LoginSplitUi.Text(null, ArcDesignTokenKind.TextSecondary, small: true);
        NoticeText.VerticalAlignment = VerticalAlignment.Center;
        ResendLabel = LoginSplitUi.Swap();
        ResendButton = new LoginSplitTextButton
        {
            Content = ResendLabel,
            Margin = new Thickness(LoginSplitMetrics.NoticeGap, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "resend" }
        };
        Notice = new LoginSplitCollapse
        {
            IsOpen = false,
            Child = new WrapPanel
            {
                Margin = new Thickness(0, LoginSplitMetrics.NoticeGap, 0, 0),
                Children = { NoticeText, ResendButton }
            }
        };

        SubmitButton = new Button
        {
            Content = labels.SignIn,
            Variant = ButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        SubmitSlot = new LoginSplitSubmitSlot { Child = SubmitButton, Margin = new Thickness(0, LoginSplitMetrics.SubmitGap, 0, 0) };

        Child = new StackPanel
        {
            Children =
            {
                LoginSplitUi.HeadingBlock(LoginSplitUi.Heading(labels.PasswordTitle), Chip),
                new StackPanel
                {
                    Spacing = LoginSplitMetrics.FormGap,
                    Children = { new StackPanel { Children = { field, Notice } }, SubmitSlot }
                }
            }
        };
    }

    public LoginSplitChip Chip { get; }

    public PasswordField PasswordField { get; }

    public LoginSplitTextButton ForgotButton { get; }

    public SiteSwap ForgotLabel { get; }

    public LoginSplitCollapse Notice { get; }

    public TextBlock NoticeText { get; }

    public LoginSplitTextButton ResendButton { get; }

    public SiteSwap ResendLabel { get; }

    /// <summary>The countdown inside the resend button while it runs.</summary>
    public LoginSplitCountdown? Countdown { get; set; }

    public LoginSplitSubmitSlot SubmitSlot { get; }

    public Button SubmitButton { get; }

    /// <summary>The reset notice for <paramref name="email"/>: the template's <c>{0}</c> becomes the email in the foreground color.</summary>
    public void SetNotice(string template, string email)
    {
        var inlines = new InlineCollection();
        var at = template.IndexOf("{0}", StringComparison.Ordinal);
        if (at < 0)
        {
            inlines.Add(new Run(template));
        }
        else
        {
            if (at > 0)
            {
                inlines.Add(new Run(template[..at]));
            }
            var strong = new Run(email);
            ArcTokens.Bind(strong, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
            inlines.Add(strong);
            if (at + 3 < template.Length)
            {
                inlines.Add(new Run(template[(at + 3)..]));
            }
        }
        NoticeText.Inlines = inlines;
    }
}

/// <summary>
/// The signing step, which becomes the signed-in step in place: the account row, a heading and sentence that swap, the
/// progress line (closes when done) and Sign out (opens when done).
/// </summary>
internal sealed class LoginSplitSessionView : LoginSplitStepView
{
    public LoginSplitSessionView(LoginSplitLabels labels, LoginSplitAccount account, string product) : base(LoginSplitStep.Signing)
    {
        Avatar = new Avatar
        {
            DisplayName = account.Name,
            Source = account.Picture,
            Size = AvatarSize.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(Avatar, account.Name);
        var name = LoginSplitUi.Text(account.Name, ArcDesignTokenKind.Foreground, medium: true);
        var email = LoginSplitUi.Text(account.Email, ArcDesignTokenKind.TextSecondary);
        name.TextWrapping = TextWrapping.NoWrap;
        email.TextWrapping = TextWrapping.NoWrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        email.TextTrimming = TextTrimming.CharacterEllipsis;
        var row = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, LoginSplitMetrics.HeadingMargin),
            Children = { Avatar, new StackPanel { Margin = new Thickness(LoginSplitMetrics.AccountGap, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { name, email } } }
        };
        DockPanel.SetDock(Avatar, Dock.Left);

        Title = LoginSplitUi.Swap();
        Title.Content = LoginSplitUi.Heading(labels.SigningTitle(product));
        Title.Focusable = true;
        Title.IsTabStop = false;
        Title.FocusAdorner = null;
        AutomationProperties.SetHeadingLevel(Title, 2);
        AutomationProperties.SetName(Title, labels.SigningTitle(product));
        Description = LoginSplitUi.Swap();
        Description.Content = LoginSplitUi.Text(labels.SigningDescription, ArcDesignTokenKind.TextSecondary);

        Progress = new LoginSplitProgressLine { Height = LoginSplitMetrics.ProgressHeight };
        ArcTokens.Bind(Progress, LoginSplitProgressLine.TrackBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(Progress, LoginSplitProgressLine.FillBrushProperty, ArcDesignTokenKind.Accent);
        ProgressRow = new LoginSplitCollapse { Child = Progress };

        SignOutButton = new Button
        {
            Content = labels.SignOut,
            Variant = ButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        SignOutRow = new LoginSplitCollapse
        {
            IsOpen = false,
            Child = new Border { Padding = new Thickness(0, LoginSplitMetrics.SignOutGap, 0, 0), Child = SignOutButton }
        };

        Child = new StackPanel
        {
            Children = { row, LoginSplitUi.HeadingBlock(Title, Description), ProgressRow, SignOutRow }
        };
    }

    public Avatar Avatar { get; }

    /// <summary>The heading; it takes focus when the step appears (no ring, not a tab stop).</summary>
    public SiteSwap Title { get; }

    public SiteSwap Description { get; }

    public LoginSplitProgressLine Progress { get; }

    public LoginSplitCollapse ProgressRow { get; }

    public LoginSplitCollapse SignOutRow { get; }

    public Button SignOutButton { get; }
}
