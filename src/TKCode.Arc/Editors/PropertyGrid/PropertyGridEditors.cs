using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The built-in editors of a <see cref="PropertyGrid"/>: Arc's own fields at the small size. Each template recycles its
/// control; a binder follows the control's data context (the row) and keeps the two in step without bindings.
/// </summary>
internal static class PropertyGridEditors
{
    public static readonly IDataTemplate Text = Create<TextBlock>(static () => new TextBinder().Control);
    public static readonly IDataTemplate String = Create<Input>(static () => new StringBinder().Control);
    public static readonly IDataTemplate Number = Create<NumberField>(static () => new NumberBinder().Control);
    public static readonly IDataTemplate Boolean = Create<Switch>(static () => new BooleanBinder().Control);
    public static readonly IDataTemplate Choice = Create<Select>(static () => new ChoiceBinder().Control);
    public static readonly IDataTemplate Color = Create<ColorPicker>(static () => new ColorBinder().Control);
    public static readonly IDataTemplate Date = Create<DatePicker>(static () => new DateBinder().Control);

    public static IDataTemplate For(PropertyGridEditorKind kind) => kind switch
    {
        PropertyGridEditorKind.String => String,
        PropertyGridEditorKind.Number => Number,
        PropertyGridEditorKind.Boolean => Boolean,
        PropertyGridEditorKind.Choice => Choice,
        PropertyGridEditorKind.Color => Color,
        PropertyGridEditorKind.Date => Date,
        _ => Text
    };

    private static IDataTemplate Create<TControl>(Func<Control> build) where TControl : Control
        => new FuncDataTemplate<PropertyGridRow>((_, _) => build(), supportsRecycling: true);

    /// <summary>Follows the control's data context and pushes the row into the control.</summary>
    private abstract class Binder<TControl> where TControl : Control
    {
        private PropertyGridRow? _row;
        private bool _pushing;

        protected Binder(TControl control)
        {
            Control = control;
            control.DataContextChanged += (_, _) => Attach(control.DataContext as PropertyGridRow);
        }

        public TControl Control { get; }

        protected PropertyGridRow? Row => _row;

        /// <summary>True while the binder writes the control, so the control's change events are not written back.</summary>
        protected bool IsPushing => _pushing;

        private void Attach(PropertyGridRow? row)
        {
            if (ReferenceEquals(row, _row))
            {
                return;
            }
            if (_row is not null)
            {
                _row.PropertyChanged -= OnRowChanged;
            }
            _row = row;
            if (row is null)
            {
                return;
            }
            row.PropertyChanged += OnRowChanged;
            AutomationProperties.SetName(Control, row.DisplayName);
            Push(row, configure: true);
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_row is { } row && e.PropertyName is nameof(PropertyGridRow.Value) or nameof(PropertyGridRow.IsMixed)
                    or nameof(PropertyGridRow.IsReadOnly) or nameof(PropertyGridRow.DisplayText) or nameof(PropertyGridRow.ErrorMessage))
            {
                Push(row, configure: false);
            }
        }

        private void Push(PropertyGridRow row, bool configure)
        {
            _pushing = true;
            try
            {
                if (configure)
                {
                    Configure(row);
                }
                Update(row);
            }
            finally
            {
                _pushing = false;
            }
        }

        /// <summary>Sets what depends on the property (limits, choices); called when the row changes.</summary>
        protected virtual void Configure(PropertyGridRow row)
        {
        }

        /// <summary>Shows the row's current value and state.</summary>
        protected abstract void Update(PropertyGridRow row);

        protected void Commit(object? value)
        {
            if (!_pushing && _row is { IsReadOnly: false } row)
            {
                row.Value = value;
            }
        }
    }

    private sealed class TextBinder : Binder<TextBlock>
    {
        public TextBinder() : base(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        })
        {
            ArcTokens.Bind(Control, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            ArcTokens.Bind(Control, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        }

        protected override void Update(PropertyGridRow row)
        {
            Control.Text = row.DisplayText;
            ToolTip.SetTip(Control, string.IsNullOrEmpty(row.DisplayText) ? null : row.DisplayText);
        }
    }

    /// <summary>Text commits on Enter or when focus leaves; Escape puts the value back.</summary>
    private sealed class StringBinder : Binder<Input>
    {
        private string _shown = string.Empty;

        public StringBinder() : base(new Input { Size = ControlSize.Small, IsLabelVisible = false })
        {
            Control.LostFocus += (_, _) => CommitText();
            Control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && !string.Equals(Control.Text, _shown, StringComparison.Ordinal))
            {
                Control.Text = _shown;
                Control.IsInvalid = false;
                e.Handled = true;
            }
        }

        private void CommitText()
        {
            var text = Control.Text ?? string.Empty;
            if (Row is null || string.Equals(text, _shown, StringComparison.Ordinal))
            {
                return;
            }
            Commit(text);
        }

        protected override void Update(PropertyGridRow row)
        {
            _shown = row.IsMixed ? string.Empty : PropertyGridValues.Format(row.Value, row.Property);
            Control.Label = row.DisplayName;
            Control.Text = _shown;
            Control.Placeholder = row.IsMixed ? row.Grid.MixedValueText : null;
            Control.IsReadOnly = row.IsReadOnly;
            Control.IsInvalid = row.ErrorMessage is not null;
            ToolTip.SetTip(Control, row.ErrorMessage);
        }
    }

    private sealed class NumberBinder : Binder<NumberField>
    {
        public NumberBinder() : base(new NumberField { Size = ControlSize.Small, IsLabelVisible = false, UseGrouping = false })
        {
            Control.ValueChanged += (_, e) =>
            {
                if (Row is { } row && !IsPushing)
                {
                    Commit(e.NewValue);
                }
            };
        }

        protected override void Configure(PropertyGridRow row)
        {
            var type = row.PropertyType;
            var (minimum, maximum) = PropertyGridValues.Limits(row.Property, type);
            var digits = PropertyGridValues.FractionDigits(row.Property, type);
            Control.Minimum = minimum;
            Control.Maximum = maximum;
            Control.Step = PropertyGridValues.Step(row.Property, type);
            Control.LargeStep = PropertyGridValues.IsWholeNumber(type) ? null : Math.Max(1m, Control.Step);
            Control.MinimumFractionDigits = digits;
            Control.MaximumFractionDigits = digits;
        }

        protected override void Update(PropertyGridRow row)
        {
            Control.Label = row.DisplayName;
            Control.Value = Math.Clamp(PropertyGridValues.NumberOf(row.Value), Control.Minimum, Control.Maximum);
            Control.IsReadOnly = row.IsReadOnly;
            Control.Placeholder = row.IsMixed ? row.Grid.MixedValueText : null;
            if (row.IsMixed)
            {
                Control.Text = string.Empty;
            }
            Control.IsInvalid = row.ErrorMessage is not null;
            ToolTip.SetTip(Control, row.ErrorMessage);
        }
    }

    private sealed class BooleanBinder : Binder<Switch>
    {
        public BooleanBinder() : base(new Switch
        {
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        })
        {
            Control.IsCheckedChanged += (_, _) =>
            {
                if (Control.IsChecked is { } value)
                {
                    Commit(value);
                }
            };
        }

        protected override void Update(PropertyGridRow row)
        {
            Control.IsChecked = row.IsMixed ? null : row.Value is true;
            Control.IsEnabled = !row.IsReadOnly;
        }
    }

    private sealed class ChoiceBinder : Binder<Select>
    {
        public ChoiceBinder() : base(new Select
        {
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextSelector = static item => (item as PropertyGridChoice)?.Text
        })
        {
            Control.SelectionChanged += (_, _) =>
            {
                if (Control.SelectedItem is PropertyGridChoice choice)
                {
                    Commit(choice.Value);
                }
            };
        }

        protected override void Configure(PropertyGridRow row)
        {
            Control.SelectedItem = null;
            Control.ItemsSource = row.Property.Choices ?? PropertyGridValues.EnumChoices(row.PropertyType) ?? [];
        }

        protected override void Update(PropertyGridRow row)
        {
            PropertyGridChoice? selected = null;
            if (!row.IsMixed && Control.ItemsSource is IReadOnlyList<PropertyGridChoice> choices)
            {
                foreach (var choice in choices)
                {
                    if (PropertyGridValues.Same(choice.Value, row.Value))
                    {
                        selected = choice;
                        break;
                    }
                }
            }
            Control.SelectedItem = selected;
            Control.Placeholder = row.IsMixed ? row.Grid.MixedValueText : row.DisplayText;
            Control.IsEnabled = !row.IsReadOnly;
        }
    }

    private sealed class ColorBinder : Binder<ColorPicker>
    {
        public ColorBinder() : base(new ColorPicker { Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Center })
        {
            Control.ColorChanged += (_, e) => Commit(e.NewColor);
        }

        protected override void Update(PropertyGridRow row)
        {
            Control.Label = row.DisplayName;
            if (row.Value is Avalonia.Media.Color color)
            {
                Control.Color = color;
            }
            Control.IsEnabled = !row.IsReadOnly;
        }
    }

    private sealed class DateBinder : Binder<DatePicker>
    {
        public DateBinder() : base(new DatePicker { Size = ControlSize.Small, IsLabelVisible = false })
        {
            Control.SelectedDateChanged += (_, _) =>
            {
                if (Row is { } row && !IsPushing && Control.SelectedDate is { } date
                    && PropertyGridValues.TryConvert(date, row.PropertyType, out var value))
                {
                    Commit(value);
                }
            };
        }

        protected override void Update(PropertyGridRow row)
        {
            Control.Label = row.DisplayName;
            Control.SelectedDate = row.IsMixed ? null : row.Value switch
            {
                DateOnly date => date,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                DateTimeOffset offset => DateOnly.FromDateTime(offset.Date),
                _ => null
            };
            if (row.IsMixed)
            {
                Control.Placeholder = row.Grid.MixedValueText;
            }
            else
            {
                Control.ClearValue(FieldTextBox.PlaceholderProperty);
            }
            Control.IsReadOnly = row.IsReadOnly;
        }
    }
}
