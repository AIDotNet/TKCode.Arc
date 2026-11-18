namespace TKCode.Arc.Graphics3D;

/// <summary>Which button or contact started a pointer interaction with a camera control.</summary>
public enum PointerButton
{
    /// <summary>The primary mouse button (or a pen tip).</summary>
    Left,

    /// <summary>The wheel button.</summary>
    Middle,

    /// <summary>The secondary mouse button.</summary>
    Right,

    /// <summary>A finger on a touch screen; several can be down at once.</summary>
    Touch,
}

/// <summary>Modifier keys held during a pointer or key event.</summary>
[Flags]
public enum ControlModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Meta = 8,
}

/// <summary>The keys camera controls react to.</summary>
public enum ControlKey
{
    Left,
    Up,
    Right,
    Down,
}

/// <summary>What dragging with a mouse button does.</summary>
public enum ControlAction
{
    None,

    /// <summary>Orbit around the target.</summary>
    Rotate,

    /// <summary>Move toward or away from the target (orthographic cameras: zoom).</summary>
    Dolly,

    /// <summary>Move the target and the camera sideways.</summary>
    Pan,
}

/// <summary>What a touch gesture does.</summary>
public enum TouchAction
{
    None,

    /// <summary>One finger: orbit.</summary>
    Rotate,

    /// <summary>One finger: pan.</summary>
    Pan,

    /// <summary>Two fingers: pinch to dolly, move to pan.</summary>
    DollyPan,

    /// <summary>Two fingers: pinch to dolly, move to orbit.</summary>
    DollyRotate,
}
