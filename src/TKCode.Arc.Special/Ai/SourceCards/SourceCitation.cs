using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An inline [n] chip for answer text, styled like the number on each card of a <see cref="SourceCards"/>. Inside the
/// cards' content it names its source for screen readers ("Source 2: Request: text() method, webref.example"), turns accent
/// while its card is highlighted, and pressing it highlights the card (opening the stack when the card is in it). Its hit
/// area reaches a few pixels past the chip without pushing the text around it. Put it in a text block with
/// <c>InlineUIContainer</c>; the regular <c>Click</c> event runs first, for example to scroll the cards into view.
/// </summary>
[PseudoClasses(HighlightedPseudoClass)]
public class SourceCitation : AvaloniaButton
{
    internal const string HighlightedPseudoClass = ":highlighted";

    /// <summary>The 1-based number of the source it cites.</summary>
    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<SourceCitation, int>(nameof(Number), 1);

    /// <summary>The number as the chip shows it.</summary>
    public static readonly DirectProperty<SourceCitation, string> NumberTextProperty =
        AvaloniaProperty.RegisterDirect<SourceCitation, string>(nameof(NumberText), o => o.NumberText);

    private string _numberText = "1";
    private SourceCards? _owner;

    /// <summary>Creates the chip.</summary>
    public SourceCitation()
    {
        AutomationProperties.SetName(this, SourceCardsLabels.Default.CitationName(1, null, null));
    }

    /// <inheritdoc cref="NumberProperty"/>
    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <inheritdoc cref="NumberTextProperty"/>
    public string NumberText => _numberText;

    /// <summary>The cards this chip belongs to, if any.</summary>
    internal SourceCards? Owner => _owner;

    /// <summary>Whether its card is highlighted now.</summary>
    internal bool IsHighlighted => PseudoClasses.Contains(HighlightedPseudoClass);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NumberProperty)
        {
            SetAndRaise(NumberTextProperty, ref _numberText, SourceCardsFormat.Number(Number));
            if (_owner is null)
            {
                AutomationProperties.SetName(this, SourceCardsLabels.Default.CitationName(Number, null, null));
            }
            _owner?.SyncCitation(this);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = this.FindAncestorOfType<SourceCards>();
        _owner?.Register(this);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner?.Unregister(this);
        _owner = null;
        SetHighlighted(false);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        base.OnClick();
        _owner?.OnCitationClicked(this);
    }

    /// <summary>Shows whether its card is highlighted.</summary>
    internal void SetHighlighted(bool highlighted) => PseudoClasses.Set(HighlightedPseudoClass, highlighted);

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(SourceCitation);
}
