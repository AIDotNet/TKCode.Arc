// Ported from vscode (src/vs/base/test/common/keybindings.test.ts, commit b57d5c9e), MIT license.
// getHashCode() is GetHashCodeString() here (Keybindings.cs).

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class KeybindingsTests
{
    [Fact]
    public void Issue_173325_Wrong_Interpretations_Of_Special_Keys()
    {
        // e.g. [Equal] is mistaken for V
        var a = new KeyCodeChord(true, false, false, false, KeyCode.KeyV);
        var b = new ScanCodeChord(true, false, false, false, ScanCode.Equal);
        (a.GetHashCodeString() == b.GetHashCodeString()).ShouldBeFalse();
    }
}
