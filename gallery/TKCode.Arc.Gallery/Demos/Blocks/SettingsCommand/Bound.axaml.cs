using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SettingsCommandBoundDemo : UserControl
{
    public SettingsCommandBoundDemo()
    {
        InitializeComponent();
        DataContext = new SettingsCommandBoundState();
    }
}

/// <summary>The app's settings: what the palette and the controls under it both bind to.</summary>
public sealed class SettingsCommandBoundState : INotifyPropertyChanged
{
    private string? _theme = "System";
    private bool _compactMode;
    private bool _reduceMotion = true;
    private string? _emailDigests = "Weekly";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Theme
    {
        get => _theme;
        set => Set(ref _theme, value);
    }

    public bool CompactMode
    {
        get => _compactMode;
        set => Set(ref _compactMode, value);
    }

    public bool ReduceMotion
    {
        get => _reduceMotion;
        set => Set(ref _reduceMotion, value);
    }

    public string? EmailDigests
    {
        get => _emailDigests;
        set => Set(ref _emailDigests, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
