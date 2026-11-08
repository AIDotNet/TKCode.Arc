using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where an invite row's resend button is.</summary>
internal enum SeatManagerResendState
{
    Idle,
    Sending,
    Sent
}

/// <summary>
/// One pending invite of <see cref="SeatManager"/> (Arc Seat manager, design reference; notes 3, 6): a 56px row with a
/// mail glyph, the address and a muted note, and Resend / Revoke. Revoke crossfades the row (old 100 ms out, new 160 ms
/// in rising 2px) into the revoked row: the address struck through, a short line and Undo. When the undo window closes
/// the row fades in about 120 ms while its height closes on <c>SpringSpec.FromVisual(0.24, 0)</c>; a row that comes back
/// (reset, or a failed revoke) opens on the same spring. Resend shows the button's spinner, then a check and "Sent"
/// (the label's width follows on <see cref="ArcSpring.Smooth"/>).
/// </summary>
/// <remarks>The height and the fade are code-driven: the target is the row's measured height, and a reopening row
/// keeps the spring's velocity.</remarks>
internal sealed class SeatManagerInviteRow : Decorator
{
    internal static readonly SpringSpec HeightSpring = SpringSpec.FromVisual(0.24, 0);
    internal static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(120);

    private readonly SeatManager _owner;
    private readonly Border _frame;
    private readonly ArcContentSwap _swap;
    private readonly Grid _normal;
    private readonly Grid _revoked;
    private readonly TextBlock _email;
    private readonly TextBlock _note;
    private readonly TextBlock _revokedEmail;
    private readonly TextBlock _revokedNote;
    private readonly Button _resend;
    private readonly Button _revoke;
    private readonly Button _undo;
    private readonly TextBlock _resendLabel;
    private readonly StackPanel _sentLabel;
    private readonly TextBlock _sentText;
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private double _heightValue = 1;
    private SeatManagerResendState _resendState;
    private bool _isRevoked;

    public SeatManagerInviteRow(SeatManager owner, SeatManagerInvite invite)
    {
        _owner = owner;
        Invite = invite;
        ClipToBounds = true;

        _email = Line(14, ArcDesignTokenKind.Foreground);
        _note = Line(13, ArcDesignTokenKind.TextMuted);
        _revokedEmail = Line(14, ArcDesignTokenKind.TextMuted);
        _revokedEmail.TextDecorations = TextDecorations.Strikethrough;
        _revokedNote = Line(13, ArcDesignTokenKind.TextSecondary);

        _resendLabel = new TextBlock();
        _sentText = new TextBlock();
        var check = new ArcGlyph { Data = ArcIcons.CircleCheck, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        check.Bind(ArcGlyph.ForegroundProperty, owner.GetObservable(SeatManager.SuccessBrushProperty));
        _sentLabel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { check, _sentText } };

        _resend = ActionButton();
        _resend.Content = _resendLabel;
        _resend.Click += (_, _) => ResendClicked?.Invoke(this);
        _revoke = ActionButton();
        _revoke.Click += (_, _) => RevokeClicked?.Invoke(this);
        _undo = ActionButton();
        _undo.Click += (_, _) => UndoClicked?.Invoke(this);

        _normal = RowGrid(Text(_email, _note), new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _resend, _revoke }
        });
        _revoked = RowGrid(Text(_revokedEmail, _revokedNote), _undo);

        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Crossfade,
            EnterOffset = 2,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = _normal
        };
        _frame = new Border { Child = _swap };
        _frame.Bind(Border.HeightProperty, owner.GetObservable(SeatManager.InviteRowHeightProperty));
        ArcTokens.Bind(_frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        Child = _frame;
        Refresh();
    }

    public SeatManagerInvite Invite { get; }

    /// <summary>The second and later rows draw a 1px line above them.</summary>
    public bool HasDivider
    {
        get => _frame.BorderThickness.Top > 0;
        set => _frame.BorderThickness = new Thickness(0, value ? 1 : 0, 0, 0);
    }

    /// <summary>True while the row shows its revoked state.</summary>
    public bool IsRevoked
    {
        get => _isRevoked;
        set
        {
            if (_isRevoked == value)
            {
                return;
            }
            _isRevoked = value;
            _swap.Content = value ? _revoked : _normal;
        }
    }

    /// <summary>True once the undo window closed and the row is on its way out.</summary>
    public bool IsLeaving { get; set; }

    public SeatManagerResendState ResendState
    {
        get => _resendState;
        set
        {
            _resendState = value;
            Refresh();
        }
    }

    internal Button ResendButton => _resend;
    internal Button RevokeButton => _revoke;
    internal Button UndoButton => _undo;
    internal double HeightFactor => _heightValue;

    public event Action<SeatManagerInviteRow>? ResendClicked;
    public event Action<SeatManagerInviteRow>? RevokeClicked;
    public event Action<SeatManagerInviteRow>? UndoClicked;

    /// <summary>Re-reads the invite and the labels.</summary>
    public void Refresh()
    {
        var labels = _owner.Labels;
        var email = Invite.Email;
        _email.Text = email;
        _revokedEmail.Text = email;
        var note = labels.InviteNote(Invite.InvitedBy, Invite.Note);
        _note.Text = note;
        _note.IsVisible = !string.IsNullOrEmpty(note);
        _revokedNote.Text = labels.Revoked;
        _resendLabel.Text = labels.Resend;
        _sentText.Text = labels.Sent;
        _revoke.Content = labels.Revoke;
        _undo.Content = labels.Undo;
        AutomationProperties.SetName(_revoke, labels.RevokeName(email));
        AutomationProperties.SetName(_undo, labels.UndoName(email));
        switch (_resendState)
        {
            case SeatManagerResendState.Sent:
                _resend.IsLoading = false;
                _resend.Content = _sentLabel;
                _resend.IsEnabled = false;
                _revoke.IsEnabled = true;
                AutomationProperties.SetName(_resend, labels.SentName(email));
                break;
            case SeatManagerResendState.Sending:
                _resend.IsLoading = true;
                _resend.IsEnabled = true;
                _revoke.IsEnabled = false;
                AutomationProperties.SetName(_resend, labels.ResendName(email));
                break;
            default:
                _resend.IsLoading = false;
                _resend.Content = _resendLabel;
                _resend.IsEnabled = true;
                _revoke.IsEnabled = true;
                AutomationProperties.SetName(_resend, labels.ResendName(email));
                break;
        }
    }

    /// <summary>Opens the row from zero height (a row that appears after the first layout).</summary>
    public void Enter()
    {
        if (!_owner.IsLoaded || !ArcMotion.IsFull(_owner))
        {
            return;
        }
        HeightMotion().Jump(0);
        Fade().Jump(1);
        _ = HeightMotion().AnimateTo(1, MotionSpec.Spring(HeightSpring));
    }

    /// <summary>Brings a leaving row back: fades in and reopens from where it is.</summary>
    public void Reopen()
    {
        IsLeaving = false;
        if (!_owner.IsLoaded || !ArcMotion.IsFull(_owner))
        {
            HeightMotion().Jump(1);
            Fade().Jump(1);
            return;
        }
        _ = Fade().AnimateTo(1, MotionSpec.Tween(FadeDuration, ArcEasings.Standard));
        _ = HeightMotion().AnimateTo(1, MotionSpec.Spring(HeightSpring));
    }

    /// <summary>Fades the row and closes its height; completes when it is closed.</summary>
    public async Task CollapseAsync()
    {
        IsLeaving = true;
        if (!_owner.IsLoaded || !ArcMotion.IsFull(_owner))
        {
            Fade().Jump(0);
            HeightMotion().Jump(0);
            return;
        }
        _ = Fade().AnimateTo(0, MotionSpec.Tween(FadeDuration, ArcEasings.Standard));
        await HeightMotion().AnimateTo(0, MotionSpec.Spring(HeightSpring));
    }

    private MotionValue<double> HeightMotion()
    {
        if (_height is null)
        {
            _height = new MotionValue<double>(this, _heightValue, MotionVectors.Double);
            _height.Changed += v =>
            {
                _heightValue = Math.Max(0, v);
                InvalidateMeasure();
            };
        }
        return _height;
    }

    private MotionValue<double> Fade()
    {
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, Opacity, MotionVectors.Double);
            _fade.Changed += v => _frame.Opacity = Math.Clamp(v, 0, 1);
        }
        return _fade;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, size.Height * _heightValue);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is not null)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }

    private static TextBlock Line(double size, ArcDesignTokenKind brush)
    {
        var block = new TextBlock
        {
            FontSize = size,
            LineHeight = 20,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, brush);
        return block;
    }

    private static StackPanel Text(TextBlock first, TextBlock second) => new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Children = { first, second }
    };

    private Grid RowGrid(Control text, Control actions)
    {
        var icon = new ArcGlyph { Data = ArcIcons.Mail, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(actions, 2);
        actions.Bind(MarginProperty, _owner.GetObservable(SeatManager.InviteActionsMarginProperty));
        text.Margin = new Thickness(12, 0);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Children = { icon, text, actions }
        };
    }

    private static Button ActionButton() => new()
    {
        Variant = ButtonVariant.Ghost,
        Size = ControlSize.Small,
        LabelSwapMode = ContentSwapMode.Label,
        LabelSpring = ArcSpring.Smooth,
        VerticalAlignment = VerticalAlignment.Center
    };
}
