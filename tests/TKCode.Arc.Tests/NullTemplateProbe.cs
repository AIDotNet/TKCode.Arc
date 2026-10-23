using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TKCode.Arc.Tests;

/// <summary>
/// A user template that records every context it is built with, null included. A host that clears its content, or attaches a
/// template to a presenter that has no content yet, hands the template null (a presenter builds its template as soon as either
/// property changes); a real template that reads its data then throws.
/// </summary>
internal sealed class NullTemplateProbe<T> where T : class
{
    public NullTemplateProbe()
    {
        Template = new FuncDataTemplate<T>((context, _) =>
        {
            Built.Add(context);
            return new Border { Width = 10, Height = 10 };
        });
    }

    public IDataTemplate Template { get; }

    public List<T?> Built { get; } = new();

    public int NullBuilds => Built.Count(context => context is null);
}
