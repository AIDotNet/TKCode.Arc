using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Localization;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The surface of <see cref="TextEditMenu"/>: a raised pill holding one <see cref="TextEditMenuItem"/> per action. It
/// builds its four items once and only shows, hides and relabels them when the menu opens.
/// </summary>
[TemplatePart(ItemsPart, typeof(Panel))]
internal sealed class TextEditMenuPresenter : TemplatedControl
{
    internal const string ItemsPart = "PART_Items";

    private readonly TextEditMenu _owner;
    private readonly TextEditMenuItem[] _items;
    private Panel? _panel;

    public TextEditMenuPresenter(TextEditMenu owner)
    {
        _owner = owner;
        _items =
        [
            new TextEditMenuItem(TextEditMenuAction.Cut),
            new TextEditMenuItem(TextEditMenuAction.Copy),
            new TextEditMenuItem(TextEditMenuAction.Paste),
            new TextEditMenuItem(TextEditMenuAction.SelectAll),
        ];
        foreach (var item in _items)
        {
            item.Click += OnItemClick;
        }
    }

    /// <summary>The items in display order, hidden ones included.</summary>
    internal IReadOnlyList<TextEditMenuItem> Items => _items;

    /// <summary>
    /// Shows the actions that apply to <paramref name="box"/> now, in the current Arc language; returns false when none
    /// does, so the menu does not open.
    /// </summary>
    internal bool Update(TextBox box)
    {
        var text = box.Text ?? string.Empty;
        var allSelected = text.Length > 0 && Math.Min(box.SelectionStart, box.SelectionEnd) == 0
                          && Math.Max(box.SelectionStart, box.SelectionEnd) >= text.Length;
        var leading = true;
        var any = false;
        foreach (var item in _items)
        {
            var visible = item.Action switch
            {
                TextEditMenuAction.Cut => box.CanCut,
                TextEditMenuAction.Copy => box.CanCopy,
                TextEditMenuAction.Paste => box.CanPaste,
                _ => text.Length > 0 && !allSelected,
            };
            item.IsVisible = visible;
            item.SetLeading(visible && leading);
            if (visible)
            {
                leading = false;
                any = true;
                var label = TextEditMenuLangResources.Get(item.Kind);
                item.Content = label;
                AutomationProperties.SetName(item, label);
            }
        }
        return any;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<Panel>(ItemsPart);
        _panel?.Children.AddRange(_items);
    }

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is TextEditMenuItem item)
        {
            e.Handled = true;
            _owner.Invoke(item.Action);
        }
    }
}

/// <summary>One action of <see cref="TextEditMenu"/>. It never takes focus, so the text box keeps the keyboard.</summary>
[PseudoClasses(LeadingPseudoClass)]
internal sealed class TextEditMenuItem : AvaloniaButton
{
    internal const string LeadingPseudoClass = ":leading";

    public TextEditMenuItem(TextEditMenuAction action)
    {
        Action = action;
        Focusable = false;
    }

    public TextEditMenuAction Action { get; }

    public TextEditMenuLangResourceKind Kind => Action switch
    {
        TextEditMenuAction.Cut => TextEditMenuLangResourceKind.Cut,
        TextEditMenuAction.Copy => TextEditMenuLangResourceKind.Copy,
        TextEditMenuAction.Paste => TextEditMenuLangResourceKind.Paste,
        _ => TextEditMenuLangResourceKind.SelectAll,
    };

    protected override Type StyleKeyOverride => typeof(TextEditMenuItem);

    /// <summary>The first shown item has no hairline before it.</summary>
    internal void SetLeading(bool leading) => PseudoClasses.Set(LeadingPseudoClass, leading);
}
