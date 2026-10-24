// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): the four steps of login-centered.tsx and their CSS.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>The geometry of the card's views (login-centered.module.css); <c>--space-N</c> is N × 4px.</summary>
internal static class LoginCenteredMetrics
{
    /// <summary>A step's padding: 32, 24 when narrow (container ≤ 520), 20 at the sides when tiny (≤ 400).</summary>
    public static readonly Thickness StepPadding = new(32);
    public static readonly Thickness StepPaddingNarrow = new(24);
    public static readonly Thickness StepPaddingTiny = new(20, 24);

    /// <summary>The heading: 8 between title and sentence, 24 under it.</summary>
    public const double HeadingGap = 8;
    public static readonly Thickness HeadingMargin = new(0, 0, 0, 24);

    /// <summary>The two wide buttons of the first view are 12 apart; a form's rows 16.</summary>
    public const double ActionsGap = 12;
    public const double FormGap = 16;

    /// <summary>The "or" divider: 20 above and below, 12 between the word and its lines.</summary>
    public static readonly Thickness DividerMargin = new(0, 20);
    public const double DividerGap = 12;

    /// <summary>The provider buttons: three equal columns 8 apart; 8 at their sides (4 when tiny), 8 between mark and name (6).</summary>
    public const double SsoGap = 8;
    public static readonly Thickness SsoPadding = new(8, 0);
    public static readonly Thickness SsoPaddingTiny = new(4, 0);
    public const double SsoContentGap = 8;
    public const double SsoContentGapTiny = 6;

    /// <summary>A text button sits 16 under the form and reaches 8 into the step's bottom padding.</summary>
    public static readonly Thickness TextButtonMargin = new(0, 16, 0, -8);

    /// <summary>The success mark is 48 square with 24 under it.</summary>
    public static readonly Thickness SuccessMargin = new(0, 0, 0, 24);

    /// <summary>The details list pulls 8 up into the heading's margin and keeps 24 under it; each row has 12 above and below.</summary>
    public static readonly Thickness DetailsMargin = new(0, -8, 0, 24);
    public static readonly Thickness DetailsRowPadding = new(0, 12);

    /// <summary>Button content: a 16px glyph 8 before its label.</summary>
    public const double ButtonContentGap = 8;

    /// <summary>The resend label: .3em between the words and the time.</summary>
    public const double ResendGap = 14 * 0.3;

    /// <summary>The rolling characters move .7em (of the 14px label).</summary>
    public const double RollDistance = 14 * 0.7;
}

/// <summary>A step's title: display face, 36px (28 when tiny), line height 1.1, tracking −0.03em, regular weight.</summary>
internal sealed class LoginCenteredHeading : LoginCenteredBalancedText
{
    public LoginCenteredHeading()
    {
        FocusAdorner = null;
        ArcTokens.Bind(this, FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(this, FontWeightProperty, ArcDesignTokenKind.WeightRegular);
        ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.Foreground);
        SetSize(false);
    }

    /// <summary>36px, or 28px in a tiny container.</summary>
    public void SetSize(bool tiny)
    {
        _size?.Dispose();
        _size = ArcTokens.Bind(this, FontSizeProperty, tiny ? ArcDesignTokenKind.Type2XL : ArcDesignTokenKind.Type3XL);
    }

    private IDisposable? _size;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty)
        {
            LineHeight = FontSize * 1.1;
            LetterSpacing = FontSize * -0.03;
        }
    }
}

/// <summary>
/// A title with Arc's <c>text-wrap: balance</c>: it wraps at the narrowest width that keeps its line count, so two lines
/// come out even. The text sits at its start, which is what the browser shows for start-aligned text.
/// </summary>
internal class LoginCenteredBalancedText : TextBlock
{
    public LoginCenteredBalancedText()
    {
        TextWrapping = TextWrapping.Wrap;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (double.IsInfinity(availableSize.Width) || availableSize.Width <= 0)
        {
            return size;
        }
        var lines = TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return size;
        }
        // A dozen halvings is well under a pixel.
        double low = 0, high = availableSize.Width;
        for (var i = 0; i < 12; i++)
        {
            var mid = (low + high) / 2;
            base.MeasureOverride(availableSize.WithWidth(mid));
            if (TextLayout.TextLines.Count == lines)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }
        return base.MeasureOverride(availableSize.WithWidth(Math.Ceiling(high)));
    }
}

/// <summary>Text helpers for the card's views (all colors and sizes from ArcDesign tokens).</summary>
internal static class LoginCenteredText
{
    /// <summary>A plain label for button content.</summary>
    public static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>A 16px Lucide glyph followed by a label, 8 apart (Arc's button children).</summary>
    public static StackPanel IconLabel(Control icon, string text, double gap = LoginCenteredMetrics.ButtonContentGap)
        => new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = gap,
            Children = { icon, Label(text) }
        };

    public static ArcGlyph Glyph(Geometry data, double strokeThickness = 1.75, double size = 16)
        => new() { Data = data, Size = size, StrokeThickness = strokeThickness, VerticalAlignment = VerticalAlignment.Center };
}

/// <summary>
/// One view of the card (Arc's <c>.step</c>): a padded column. Built in code because the views come and go as the step
/// changes (each change mounts a new view, as Arc's keyed children do).
/// </summary>
internal abstract class LoginCenteredStepView : Border
{
    protected LoginCenteredStepView(LoginCenteredStep step, string key)
    {
        Step = step;
        Key = key;
        Body = new StackPanel();
        Child = Body;
        Padding = LoginCenteredMetrics.StepPadding;
    }

    /// <summary>The step this view shows.</summary>
    public LoginCenteredStep Step { get; }

    /// <summary>Arc's key of the view: <c>email-sign-in</c> and <c>email-sign-up</c> are different views.</summary>
    public string Key { get; }

    protected StackPanel Body { get; }

    private readonly List<LoginCenteredHeading> _headings = new();

    protected bool IsTiny { get; private set; }

    /// <summary>The element that takes focus when the view arrives (Arc's <c>focusNext</c>), if any.</summary>
    public virtual Control? FocusTarget => null;

    /// <summary>Applies the container's width class: narrow (≤ 520) and tiny (≤ 400).</summary>
    public void SetWidthClass(bool narrow, bool tiny)
    {
        IsTiny = tiny;
        Padding = tiny ? LoginCenteredMetrics.StepPaddingTiny : narrow ? LoginCenteredMetrics.StepPaddingNarrow : LoginCenteredMetrics.StepPadding;
        OnWidthClassChanged();
    }

    protected virtual void OnWidthClassChanged()
    {
        foreach (var heading in _headings)
        {
            heading.SetSize(IsTiny);
        }
    }

    /// <summary>Title and sentence, 8 apart, 24 above the rest.</summary>
    protected StackPanel AddHeading(LoginCenteredHeading title, Control description)
    {
        _headings.Add(title);
        var heading = new StackPanel { Spacing = LoginCenteredMetrics.HeadingGap, Margin = LoginCenteredMetrics.HeadingMargin, Children = { title, description } };
        Body.Children.Add(heading);
        return heading;
    }

    protected static LoginCenteredPillButton TextButton()
    {
        var button = new LoginCenteredPillButton { HorizontalAlignment = HorizontalAlignment.Center, Margin = LoginCenteredMetrics.TextButtonMargin };
        button.Classes.Add("text");
        return button;
    }
}

/// <summary>
/// The first view: "Sign in to Arc", the passkey button and "Use email instead" (Cancel while the device is asked), an
/// "or" divider and the three provider buttons.
/// </summary>
internal sealed class LoginCenteredPasskeyView : LoginCenteredStepView
{
    private readonly LoginCenteredHeading _title = new();
    private readonly LoginCenteredFlow _description = new();
    private readonly TextBlock _divider = new();
    private readonly Grid _sso;
    private readonly IDisposable?[] _ssoFont = new IDisposable?[3];
    private LoginCenteredPasskeyPhase? _shownPhase;
    private LoginCenteredLabels? _shownLabels;

    public LoginCenteredPasskeyView() : base(LoginCenteredStep.Passkey, "passkey")
    {
        AddHeading(_title, _description);
        PasskeyButton = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
        SecondaryButton = new Button { Variant = ButtonVariant.Secondary, HorizontalAlignment = HorizontalAlignment.Stretch };
        Body.Children.Add(new StackPanel { Spacing = LoginCenteredMetrics.ActionsGap, Children = { PasskeyButton, SecondaryButton } });
        Body.Children.Add(BuildDivider());
        _sso = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = LoginCenteredMetrics.SsoGap };
        var providers = new[] { LoginCenteredProvider.Google, LoginCenteredProvider.Apple, LoginCenteredProvider.GitHub };
        ProviderButtons = new Button[providers.Length];
        for (var i = 0; i < providers.Length; i++)
        {
            var button = new Button
            {
                Variant = ButtonVariant.Secondary,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = LoginCenteredMetrics.SsoPadding,
                MinWidth = 0,
                Tag = providers[i]
            };
            Grid.SetColumn(button, i);
            ProviderButtons[i] = button;
            _sso.Children.Add(button);
        }
        Body.Children.Add(_sso);
    }

    public Button PasskeyButton { get; }

    public Button SecondaryButton { get; }

    /// <summary>Google, Apple and GitHub, in that order (each button's <c>Tag</c> is its provider).</summary>
    public Button[] ProviderButtons { get; }

    public override Control? FocusTarget => PasskeyButton;

    private Grid BuildDivider()
    {
        // Arc's line-height 1 centers the glyphs on the 12px row; the natural line box centered on it does the same.
        _divider.VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(_divider, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_divider, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Border Line()
        {
            var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(line, BackgroundProperty, ArcDesignTokenKind.Border);
            return line;
        }
        var left = Line();
        var right = Line();
        Grid.SetColumn(_divider, 1);
        Grid.SetColumn(right, 2);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
            ColumnSpacing = LoginCenteredMetrics.DividerGap,
            Margin = LoginCenteredMetrics.DividerMargin,
            Height = 12,
            Children = { left, _divider, right }
        };
    }

    /// <summary>Brings the view up to date with the block's state (Arc's render of this step).</summary>
    public void Update(LoginCenteredLabels labels, LoginCenteredPasskeyPhase phase, LoginCenteredProvider? busyProvider)
    {
        _title.Text = labels.PasskeyTitle;
        _description.SetContent([(labels.PasskeyDescription, false)]);
        _divider.Text = labels.Divider;
        AutomationProperties.SetName(_sso, labels.SingleSignOnName);
        if (_shownPhase != phase || !ReferenceEquals(_shownLabels, labels))
        {
            _shownPhase = phase;
            _shownLabels = labels;
            // A new content object makes the button swap its label with Arc's blur cross-fade.
            PasskeyButton.Content = phase switch
            {
                LoginCenteredPasskeyPhase.Waiting => LoginCenteredText.IconLabel(new LoginCenteredPulse { VerticalAlignment = VerticalAlignment.Center }, labels.PasskeyWaiting),
                LoginCenteredPasskeyPhase.Verified => LoginCenteredText.IconLabel(LoginCenteredText.Glyph(ArcIcons.Check, 2), labels.PasskeyVerified),
                _ => LoginCenteredText.IconLabel(LoginCenteredText.Glyph(ArcIcons.Fingerprint), labels.PasskeyAction)
            };
            SecondaryButton.Content = phase == LoginCenteredPasskeyPhase.Waiting
                ? LoginCenteredText.Label(labels.CancelPasskey)
                : LoginCenteredText.IconLabel(LoginCenteredText.Glyph(ArcIcons.Mail), labels.UseEmail);
            RebuildProviders(labels);
        }
        AutomationProperties.SetName(PasskeyButton, phase switch
        {
            LoginCenteredPasskeyPhase.Waiting => labels.PasskeyWaiting,
            LoginCenteredPasskeyPhase.Verified => labels.PasskeyVerified,
            _ => labels.PasskeyAction
        });
        AutomationProperties.SetName(SecondaryButton, phase == LoginCenteredPasskeyPhase.Waiting ? labels.CancelPasskey : labels.UseEmail);
        foreach (var button in ProviderButtons)
        {
            button.IsLoading = busyProvider is { } busy && Equals(button.Tag, busy);
        }
    }

    private static AuthProviderKind ProviderKind(LoginCenteredProvider provider) => provider switch
    {
        LoginCenteredProvider.Google => AuthProviderKind.Google,
        LoginCenteredProvider.Apple => AuthProviderKind.Apple,
        _ => AuthProviderKind.GitHub
    };

    private void RebuildProviders(LoginCenteredLabels labels)
    {
        foreach (var button in ProviderButtons)
        {
            var provider = (LoginCenteredProvider)button.Tag!;
            var content = LoginCenteredText.IconLabel(new AuthProviderMark { Kind = ProviderKind(provider), VerticalAlignment = VerticalAlignment.Center },
                labels.ProviderName(provider), IsTiny ? LoginCenteredMetrics.SsoContentGapTiny : LoginCenteredMetrics.SsoContentGap);
            button.Content = content;
            AutomationProperties.SetName(button, labels.ContinueWith(provider));
        }
    }

    protected override void OnWidthClassChanged()
    {
        base.OnWidthClassChanged();
        for (var i = 0; i < ProviderButtons.Length; i++)
        {
            var button = ProviderButtons[i];
            button.Padding = IsTiny ? LoginCenteredMetrics.SsoPaddingTiny : LoginCenteredMetrics.SsoPadding;
            _ssoFont[i]?.Dispose();
            _ssoFont[i] = null;
            if (IsTiny)
            {
                // Tiny containers: 12px provider names (Arc's --text-xs), above the button theme's 14px.
                _ssoFont[i] = ArcTokens.Bind(button, TemplatedControl.FontSizeProperty, ArcDesignTokenKind.TypeXS, Avalonia.Data.BindingPriority.LocalValue);
            }
            if (button.Content is StackPanel content)
            {
                content.Spacing = IsTiny ? LoginCenteredMetrics.SsoContentGapTiny : LoginCenteredMetrics.SsoContentGap;
            }
        }
    }
}

/// <summary>
/// The email view: "Sign in with email" (or "Create your account" with a "Work email" field), the field, the submit
/// button and, while signing in, "Use a passkey instead".
/// </summary>
internal sealed class LoginCenteredEmailView : LoginCenteredStepView
{
    private readonly LoginCenteredHeading _title = new();
    private readonly LoginCenteredFlow _description = new();
    private readonly TextBlock _submitLabel = new();
    private readonly TextBlock _passkeyLabel = new();

    public LoginCenteredEmailView(bool signingUp) : base(LoginCenteredStep.Email, signingUp ? "email-sign-up" : "email-sign-in")
    {
        IsSignUp = signingUp;
        AddHeading(_title, _description);
        Input = new Input { HorizontalAlignment = HorizontalAlignment.Stretch };
        TextInputOptions.SetContentType(Input, TextInputContentType.Email);
        TextInputOptions.SetAutoCapitalization(Input, false);
        TextInputOptions.SetReturnKeyType(Input, TextInputReturnKeyType.Send);
        SubmitButton = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, Content = _submitLabel };
        Body.Children.Add(new StackPanel { Spacing = LoginCenteredMetrics.FormGap, Children = { Input, SubmitButton } });
        if (!signingUp)
        {
            PasskeyButton = TextButton();
            _passkeyLabel.VerticalAlignment = VerticalAlignment.Center;
            PasskeyButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = LoginCenteredMetrics.ButtonContentGap,
                Children = { LoginCenteredText.Glyph(ArcIcons.Fingerprint, size: 14), _passkeyLabel }
            };
            Body.Children.Add(PasskeyButton);
        }
    }

    public bool IsSignUp { get; }

    public Input Input { get; }

    public Button SubmitButton { get; }

    /// <summary>"Use a passkey instead" (null while creating an account).</summary>
    public LoginCenteredPillButton? PasskeyButton { get; }

    public override Control? FocusTarget => Input;

    public void Update(LoginCenteredLabels labels, string email, string? error, bool sending)
    {
        _title.Text = IsSignUp ? labels.SignUpTitle : labels.EmailTitle;
        _description.SetContent([(IsSignUp ? labels.SignUpDescription : labels.EmailDescription, false)]);
        Input.Label = IsSignUp ? labels.WorkEmailLabel : labels.EmailLabel;
        Input.Placeholder = labels.EmailPlaceholder;
        if (!string.Equals(Input.Text ?? string.Empty, email, StringComparison.Ordinal))
        {
            Input.Text = email;
        }
        Input.ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        Input.IsReadOnly = sending;
        _submitLabel.Text = IsSignUp ? labels.CreateAccount : labels.SendCode;
        AutomationProperties.SetName(SubmitButton, _submitLabel.Text);
        SubmitButton.IsLoading = sending;
        if (PasskeyButton is not null)
        {
            _passkeyLabel.Text = labels.UsePasskey;
            AutomationProperties.SetName(PasskeyButton, labels.UsePasskey);
        }
    }
}

/// <summary>
/// The code view: "Check your email" with the address and an inline "Change", the six digit code field (helper copy shows
/// the simulated code), the submit button and the resend button whose label counts down.
/// </summary>
internal sealed class LoginCenteredCodeView : LoginCenteredStepView
{
    private readonly LoginCenteredHeading _title = new();
    private readonly LoginCenteredFlow _description = new();
    private readonly LoginCenteredPresence _resendLabel = new() { PresenceStyle = LoginCenteredMotion.Swap };
    private LoginCenteredRollingTime? _time;
    private bool? _shownVerified;
    private bool? _shownWaiting;
    private string? _shownSentence;
    private LoginCenteredLabels? _shownLabels;

    public LoginCenteredCodeView() : base(LoginCenteredStep.Code, "code")
    {
        AddHeading(_title, _description);
        ChangeButton = new LoginCenteredInlineButton();
        Otp = new OtpInput { AutoFocus = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        VerifyButton = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
        Body.Children.Add(new StackPanel { Spacing = LoginCenteredMetrics.FormGap, Children = { Otp, VerifyButton } });
        ResendButton = TextButton();
        ResendButton.Content = _resendLabel;
        Body.Children.Add(ResendButton);
    }

    public LoginCenteredInlineButton ChangeButton { get; }

    public OtpInput Otp { get; }

    public Button VerifyButton { get; }

    public LoginCenteredPillButton ResendButton { get; }

    /// <summary>The rolling time while the resend button waits (tests read it).</summary>
    internal LoginCenteredRollingTime? Time => _time;

    internal LoginCenteredPresence ResendLabel => _resendLabel;

    public void Update(LoginCenteredLabels labels, string account, bool signingUp, string? demoCode, string code, string? error,
                       bool checking, bool verified, int resendIn)
    {
        _title.Text = labels.CodeTitle;
        var sentence = labels.CodeSentTo + "\u0001" + account;
        if (sentence != _shownSentence || !ReferenceEquals(labels, _shownLabels))
        {
            _shownSentence = sentence;
            ChangeButton.Text = labels.ChangeEmail;
            AutomationProperties.SetName(ChangeButton, labels.ChangeEmailName);
            _description.SetContent([(labels.CodeSentTo + " ", false), (account, true), (". ", false)], ChangeButton);
        }
        Otp.Label = labels.CodeLabel;
        Otp.Description = labels.CodeDescription(demoCode);
        if (!string.Equals(Otp.Value, code, StringComparison.Ordinal))
        {
            Otp.Value = code;
        }
        Otp.ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        Otp.IsReadOnly = checking || verified;
        VerifyButton.IsLoading = checking;
        if (_shownVerified != verified || !ReferenceEquals(labels, _shownLabels))
        {
            _shownVerified = verified;
            VerifyButton.Content = verified
                ? LoginCenteredText.IconLabel(LoginCenteredText.Glyph(ArcIcons.Check, 2), labels.Verified)
                : LoginCenteredText.Label(signingUp ? labels.VerifyAndCreate : labels.Verify);
        }
        AutomationProperties.SetName(VerifyButton, verified ? labels.Verified : signingUp ? labels.VerifyAndCreate : labels.Verify);

        var waiting = resendIn > 0;
        ResendButton.IsInert = waiting || verified;
        AutomationProperties.SetName(ResendButton, waiting ? labels.ResendInName(resendIn) : labels.Resend);
        if (_shownWaiting != waiting || !ReferenceEquals(labels, _shownLabels))
        {
            var animate = _shownWaiting is not null;
            _shownWaiting = waiting;
            if (waiting)
            {
                _time = new LoginCenteredRollingTime();
                _time.SetSeconds(resendIn, animate: false);
                var label = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = LoginCenteredMetrics.ResendGap,
                    Children = { new TextBlock { Text = labels.ResendIn, VerticalAlignment = VerticalAlignment.Center }, _time }
                };
                _resendLabel.Show(label, animate, 6, -4);
            }
            else
            {
                _time = null;
                _resendLabel.Show(new TextBlock { Text = labels.Resend, VerticalAlignment = VerticalAlignment.Center }, animate, 6, -4);
            }
        }
        else if (waiting)
        {
            _time?.SetSeconds(resendIn, animate: true);
        }
        _shownLabels = labels;
    }

    /// <summary>Puts focus on the first slot (Arc's <c>focusCode</c>).</summary>
    public void FocusCode() => Otp.FocusSlot(0);
}

/// <summary>
/// "0:29" as one column per character; a changed character rolls out and the new one rolls in (down while the timer runs,
/// up when it restarts). Digits are tabular, so the columns keep their width.
/// </summary>
internal sealed class LoginCenteredRollingTime : StackPanel
{
    private readonly List<LoginCenteredPresence> _columns = new();
    private string _text = string.Empty;
    private int _seconds = -1;

    public LoginCenteredRollingTime()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>The time on show ("0:29"; tests read it).</summary>
    public string Text => _text;

    /// <summary>Formats seconds as Arc does: minutes, a colon and two-digit seconds.</summary>
    internal static string Format(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    public void SetSeconds(int seconds, bool animate)
    {
        if (seconds == _seconds)
        {
            return;
        }
        var direction = _seconds < 0 || seconds < _seconds ? 1 : -1;
        _seconds = seconds;
        var text = Format(seconds);
        if (text.Length != _columns.Count)
        {
            Children.Clear();
            _columns.Clear();
            _text = string.Empty;
            animate = false;
            foreach (var _ in text)
            {
                var column = new LoginCenteredPresence { PresenceStyle = LoginCenteredMotion.Roll, ContentAlignment = HorizontalAlignment.Center };
                _columns.Add(column);
                Children.Add(column);
            }
        }
        for (var i = 0; i < text.Length; i++)
        {
            if (i < _text.Length && _text[i] == text[i])
            {
                continue;
            }
            var glyph = new TextBlock { Text = text[i].ToString(), FontFeatures = s_tabular };
            _columns[i].Show(glyph, animate, -LoginCenteredMetrics.RollDistance * direction, LoginCenteredMetrics.RollDistance * direction);
        }
        _text = text;
    }

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };
}

/// <summary>
/// The last view: the drawn success mark, "Welcome back" (or "Welcome to Arc"), the account, two details (method, this
/// device) and Sign out.
/// </summary>
internal sealed class LoginCenteredDoneView : LoginCenteredStepView
{
    private readonly LoginCenteredHeading _title = new() { Focusable = true };
    private readonly LoginCenteredFlow _description = new();
    private readonly TextBlock _methodTerm = Detail(ArcDesignTokenKind.TextSecondary);
    private readonly TextBlock _methodValue = Detail(ArcDesignTokenKind.Foreground);
    private readonly TextBlock _deviceTerm = Detail(ArcDesignTokenKind.TextSecondary);
    private readonly TextBlock _deviceValue = Detail(ArcDesignTokenKind.Foreground);
    private LoginCenteredLabels? _shownLabels;

    public LoginCenteredDoneView() : base(LoginCenteredStep.Done, "done")
    {
        KeyboardNavigation.SetIsTabStop(_title, false);
        Mark = new LoginCenteredSuccessMark
        {
            Width = LoginCenteredSuccessMark.MarkSize,
            Height = LoginCenteredSuccessMark.MarkSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = LoginCenteredMetrics.SuccessMargin
        };
        ArcTokens.Bind(Mark, LoginCenteredSuccessMark.ForegroundProperty, ArcDesignTokenKind.Success);
        Body.Children.Add(Mark);
        AddHeading(_title, _description);
        var details = new StackPanel { Margin = LoginCenteredMetrics.DetailsMargin };
        var top = new Border { Height = 1 };
        ArcTokens.Bind(top, BackgroundProperty, ArcDesignTokenKind.Border);
        details.Children.Add(top);
        details.Children.Add(Row(_methodTerm, _methodValue));
        details.Children.Add(Row(_deviceTerm, _deviceValue));
        Body.Children.Add(details);
        SignOutButton = new Button { Variant = ButtonVariant.Secondary, HorizontalAlignment = HorizontalAlignment.Stretch };
        Body.Children.Add(SignOutButton);
    }

    public LoginCenteredSuccessMark Mark { get; }

    public Button SignOutButton { get; }

    /// <summary>The title takes focus when the view arrives (Arc's <c>h2 tabIndex={-1}</c>).</summary>
    public override Control? FocusTarget => _title;

    internal TextBlock Title => _title;

    internal string MethodText => _methodValue.Text ?? string.Empty;

    private static TextBlock Detail(ArcDesignTokenKind color)
    {
        var text = new TextBlock { LineHeight = 19.6, VerticalAlignment = VerticalAlignment.Top };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, color);
        return text;
    }

    private static Border Row(TextBlock term, TextBlock value)
    {
        value.TextAlignment = TextAlignment.Right;
        value.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(value, 1);
        var row = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = LoginCenteredMetrics.DetailsRowPadding,
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16, Children = { term, value } }
        };
        ArcTokens.Bind(row, BorderBrushProperty, ArcDesignTokenKind.Border);
        return row;
    }

    public void Update(LoginCenteredLabels labels, string account, bool signingUp, LoginCenteredMethod method)
    {
        _title.Text = signingUp ? labels.WelcomeNew : labels.WelcomeBack;
        _description.SetContent(signingUp
            ? [(labels.AccountReady + " ", false), (account, true)]
            : [(labels.SignedInAs + " ", false), (account, true), (".", false)]);
        _methodTerm.Text = labels.MethodTerm;
        _methodValue.Text = labels.MethodName(method);
        _deviceTerm.Text = labels.DeviceTerm;
        _deviceValue.Text = labels.DeviceValue;
        if (!ReferenceEquals(labels, _shownLabels))
        {
            _shownLabels = labels;
            SignOutButton.Content = LoginCenteredText.Label(labels.SignOut);
        }
        AutomationProperties.SetName(SignOutButton, labels.SignOut);
    }
}
