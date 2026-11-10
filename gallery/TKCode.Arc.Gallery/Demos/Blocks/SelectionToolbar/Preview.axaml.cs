using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SelectionToolbarPreviewDemo : UserControl
{
    private static readonly string[] s_files =
    [
        "Q3 board deck.pdf", "Pricing notes.md", "Logo sketches.fig", "Onboarding script.docx", "Churn by cohort.csv",
        "Field interviews.m4a", "Roadmap 2027.md", "Brand colors.ase", "Offsite photos.zip", "Release checklist.md"
    ];

    private readonly List<Checkbox> _checks = new();
    private bool _bulk;

    public SelectionToolbarPreviewDemo()
    {
        InitializeComponent();
        foreach (var name in s_files)
        {
            var check = new Checkbox { Content = name, Margin = new Thickness(8, 6) };
            check.IsCheckedChanged += (_, _) => Recount();
            _checks.Add(check);
            Rows.Children.Add(check);
        }
        Toolbar.TotalCount = _checks.Count;
        _checks[1].IsChecked = true;
        _checks[4].IsChecked = true;
    }

    private void Recount()
    {
        if (!_bulk)
        {
            Toolbar.SelectedCount = _checks.Count(c => c.IsChecked == true);
        }
    }

    private void SetAll(bool value)
    {
        _bulk = true;
        foreach (var check in _checks)
        {
            check.IsChecked = value;
        }
        _bulk = false;
        Recount();
    }

    private void OnSelectAll(object? sender, RoutedEventArgs e) => SetAll(true);

    private void OnCleared(object? sender, RoutedEventArgs e) => SetAll(false);

    private void OnActionInvoked(object? sender, SelectionToolbarActionEventArgs e)
    {
        Toast.Show(this, $"{e.Action.Label}: {e.SelectedCount} files");
        SetAll(false);
    }
}
