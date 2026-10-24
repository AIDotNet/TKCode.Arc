// Ported from Arc's free otp-input block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which characters an <see cref="OtpInput"/> accepts (Arc's <c>inputMode</c>).</summary>
public enum OtpInputMode
{
    /// <summary>Digits only: anything else is dropped, and touch keyboards show the number pad.</summary>
    Numeric,

    /// <summary>Any character (letter and digit codes).</summary>
    Text
}

/// <summary>What changed the code of an <see cref="OtpInput"/>.</summary>
public enum OtpInputChangeSource
{
    /// <summary>Code set <see cref="OtpInput.Value"/> (a reset after a wrong code, a binding).</summary>
    Programmatic,

    /// <summary>The person typed a character, or an input method or autofill inserted several.</summary>
    Typing,

    /// <summary>The person pasted a code.</summary>
    Paste,

    /// <summary>The person cleared a slot with Backspace or Delete.</summary>
    Deletion
}

/// <summary>Arguments of <see cref="OtpInput.ValueChanged"/>.</summary>
public sealed class OtpInputValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public OtpInputValueChangedEventArgs(RoutedEvent routedEvent, string oldValue, string newValue, OtpInputChangeSource changeSource)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
        ChangeSource = changeSource;
    }

    /// <summary>The code before the change.</summary>
    public string OldValue { get; }

    /// <summary>The code after the change.</summary>
    public string NewValue { get; }

    /// <summary>Whether the person edited the code (typing, paste, deletion) or code set it.</summary>
    public OtpInputChangeSource ChangeSource { get; }

    /// <summary>True when the person edited the code (Arc's <c>onChange</c>).</summary>
    public bool IsUserEdit => ChangeSource != OtpInputChangeSource.Programmatic;
}

/// <summary>Arguments of <see cref="OtpInput.Completed"/>.</summary>
public sealed class OtpInputCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public OtpInputCompletedEventArgs(RoutedEvent routedEvent, string code)
        : base(routedEvent)
    {
        Code = code;
    }

    /// <summary>The complete code, one character per slot.</summary>
    public string Code { get; }
}
