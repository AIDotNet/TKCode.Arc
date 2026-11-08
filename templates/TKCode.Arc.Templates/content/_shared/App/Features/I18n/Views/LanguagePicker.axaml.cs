using Avalonia.Controls;
using ArcStarterApp.Localization;
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif
using TKCode.Arc;
using TKCode.Arc.Localization;

namespace ArcStarterApp.Views;

/// <summary>Chooses the app's language (AppLanguages) and switches it with ArcLanguage.Set.</summary>
public partial class LanguagePicker : UserControl
{
    public LanguagePicker()
    {
        InitializeComponent();
        foreach (var (tag, name) in AppLanguages.All)
        {
            Choice.Items.Add(new SelectItem { Content = name, Value = tag.Value });
        }
        Choice.SelectedValue = ArcLanguage.Current.Value;
        Choice.SelectionChanged += OnChosen;
    }

    private void OnChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (Choice.SelectedValue is not string value || value == ArcLanguage.Current.Value)
        {
            return;
        }
        ArcLanguage.Set(value);
#if (UsesSettings)
        AppSettings.Current.Language = value;
        AppSettings.Current.Save();
#endif
    }
}
