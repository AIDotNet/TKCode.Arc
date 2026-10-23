using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A citation pill inside the answer text: the cited number on a muted pill (18 high, at least 18 wide, 5px padding, 2px
/// left margin), a real control embedded in the text so it takes keyboard focus in reading order, the pointer, and an
/// accessible name. Lit (pointed at, focused, or its source card is) it turns darker: the muted fill under 18% of the
/// accent and the number in the foreground color. Without a source it is a plain number that does nothing.
/// The owning <see cref="AiAnswerDocument"/> handles what it means: lighting, the preview card and invoking the source.
/// </summary>
[PseudoClasses(LitPseudoClass, PlainPseudoClass)]
internal sealed class AiAnswerCitation : AvaloniaButton
{
    public const string LitPseudoClass = ":lit";
    public const string PlainPseudoClass = ":plain";

    /// <summary>Where the text baseline is, from the top of the pill: its middle sits on the middle of the lowercase letters of 16px text.</summary>
    public const double PillBaseline = 13;

    /// <summary>The number shown.</summary>
    public static readonly DirectProperty<AiAnswerCitation, string> LabelProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCitation, string>(nameof(Label), o => o.Label);

    private string _label = string.Empty;
    private bool _isLit;
    private bool _hasSource = true;

    static AiAnswerCitation()
    {
        FocusAdornerProperty.OverrideDefaultValue<AiAnswerCitation>(null);
        TextBlock.BaselineOffsetProperty.OverrideDefaultValue<AiAnswerCitation>(PillBaseline);
    }

    public AiAnswerCitation(AiAnswerDocument owner, int number)
    {
        Owner = owner;
        Number = number;
        Label = number.ToString(CultureInfo.InvariantCulture);
        Content = Label;
        // The pill draws its own ring (a local value, so an app-wide focus style does not add one).
        FocusAdorner = null;
    }

    /// <summary>The document it belongs to.</summary>
    public AiAnswerDocument Owner { get; }

    /// <summary>The cited number.</summary>
    public int Number { get; }

    /// <summary>The text it sits in (set by the text when it embeds the pill).</summary>
    internal AiAnswerText? Host { get; set; }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => _label; private set => SetAndRaise(LabelProperty, ref _label, value); }

    /// <summary>Whether the pill is lit (its sentence or its source card is active).</summary>
    public bool IsLit
    {
        get => _isLit;
        set
        {
            if (_isLit != value)
            {
                _isLit = value;
                PseudoClasses.Set(LitPseudoClass, value);
            }
        }
    }

    /// <summary>Whether the answer has the cited source; without one the pill is a plain number, not a stop or a link.</summary>
    public bool HasSource
    {
        get => _hasSource;
        set
        {
            if (_hasSource != value)
            {
                _hasSource = value;
                PseudoClasses.Set(PlainPseudoClass, !value);
                Focusable = value;
                IsHitTestVisible = value;
            }
        }
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerCitation);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            Owner.OnCitationPointer(this, entered: true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Owner.OnCitationPointer(this, entered: false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner.OnCitationFocus(this, focused: true, keyboard: e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner.OnCitationFocus(this, focused: false, keyboard: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Owner.CloseCardFrom(this))
        {
            // The preview closes; focus and lighting stay on the pill.
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space)
        {
            // A citation is a link: Enter opens it, Space does not.
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnClick()
    {
        base.OnClick();
        Owner.InvokeCitation(this);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CitationPeer(this);

    /// <summary>A link to the source (Arc: an <c>a</c> element named "Source n: title, site").</summary>
    private sealed class CitationPeer(AiAnswerCitation owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string GetClassNameCore() => nameof(AiAnswerCitation);
    }
}
