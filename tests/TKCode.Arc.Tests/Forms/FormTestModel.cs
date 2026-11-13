using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Forms;

/// <summary>
/// A hand-written <see cref="INotifyDataErrorInfo"/> view model for the form tests: one property per value type the Arc
/// fields bind, and errors set per property (the empty name holds the entity-level errors).
/// </summary>
public sealed class FormTestModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, object?> _values = new();
    private readonly Dictionary<string, List<object>> _errors = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Values.Any(list => list.Count > 0);

    public IEnumerable GetErrors(string? propertyName)
        => _errors.TryGetValue(propertyName ?? string.Empty, out var list) ? list.ToArray() : Array.Empty<object>();

    /// <summary>Replaces the errors of <paramref name="property"/> (none clears them) and raises ErrorsChanged.</summary>
    public void SetErrors(string property, params object[] errors)
    {
        _errors[property] = errors.ToList();
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
    }

    public void ClearAllErrors()
    {
        foreach (var key in _errors.Keys.ToArray())
        {
            SetErrors(key);
        }
    }

    private T Get<T>(T fallback = default!, [CallerMemberName] string name = "")
        => _values.TryGetValue(name, out var value) ? (T)value! : fallback;

    private void Set<T>(T value, [CallerMemberName] string name = "")
    {
        _values[name] = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public string? Text { get => Get<string?>(); set => Set(value); }

    public string? Other { get => Get<string?>(); set => Set(value); }

    public int Number { get => Get<int>(); set => Set(value); }

    public object? Object { get => Get<object?>(); set => Set(value); }

    public bool? NullableBool { get => Get<bool?>(false); set => Set(value); }

    public bool Bool { get => Get<bool>(); set => Set(value); }

    public decimal Decimal { get => Get<decimal>(); set => Set(value); }

    public decimal? NullableDecimal { get => Get<decimal?>(); set => Set(value); }

    public double Double { get => Get<double>(); set => Set(value); }

    public int Index { get => Get(-1); set => Set(value); }

    public DateOnly? Date { get => Get<DateOnly?>(); set => Set(value); }

    public TimeOnly? Time { get => Get<TimeOnly?>(); set => Set(value); }

    public DateRange? Range { get => Get<DateRange?>(); set => Set(value); }

    public Color Color { get => Get(Colors.Red); set => Set(value); }

    public IList? List { get => Get<IList?>(); set => Set(value); }

    public IReadOnlyList<object?> Values { get => Get<IReadOnlyList<object?>>(Array.Empty<object?>()); set => Set(value); }

    public IReadOnlyList<string> Tags { get => Get<IReadOnlyList<string>>(Array.Empty<string>()); set => Set(value); }

    public KeyGesture? Gesture { get => Get<KeyGesture?>(); set => Set(value); }

    public string Phone { get => Get(string.Empty); set => Set(value); }
}

/// <summary>A command that counts its runs and can be switched off.</summary>
public sealed class CountingCommand : ICommand
{
    private readonly Action<object?>? _execute;
    private bool _canExecute = true;

    public CountingCommand(Action<object?>? execute = null) => _execute = execute;

    public int Count { get; private set; }

    public object? LastParameter { get; private set; }

    public bool Enabled
    {
        get => _canExecute;
        set
        {
            _canExecute = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute;

    public void Execute(object? parameter)
    {
        Count++;
        LastParameter = parameter;
        _execute?.Invoke(parameter);
    }
}

internal static class FormTestBinding
{
    /// <summary>Binds <paramref name="property"/> two-way to <paramref name="path"/> of <paramref name="model"/>.</summary>
    public static T BindTo<T>(this T control, AvaloniaProperty property, FormTestModel model, string path,
        BindingMode mode = BindingMode.TwoWay) where T : Control
    {
        control.Bind(property, new Binding(path) { Source = model, Mode = mode });
        return control;
    }
}
