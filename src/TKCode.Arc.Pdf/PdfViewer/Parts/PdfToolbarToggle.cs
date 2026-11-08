using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>A toolbar button with an on state, for the tools a <see cref="PdfViewer"/> adds to the toolbar.</summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class PdfToolbarToggle : Button
{
    public const string ActivePseudoClass = ":active";

    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            PseudoClasses.Set(ActivePseudoClass, value);
        }
    }
}
