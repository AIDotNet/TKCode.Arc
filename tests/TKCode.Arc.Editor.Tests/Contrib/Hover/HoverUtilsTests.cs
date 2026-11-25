// Ported from vscode (src/vs/editor/contrib/hover/test/browser/hoverUtils.test.ts, commit b57d5c9e), MIT license. All
// 29 upstream tests; the element is its bounds (left, top, width, height) instead of a mocked HTMLElement, and the mouse
// event is its modifier keys.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Hover;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class HoverUtilsTests
{
    private static HoverModifierKeys Keys(bool ctrlKey, bool altKey, bool metaKey) => new(ctrlKey, altKey, metaKey);

    // ---- shouldShowHover

    [Fact]
    public void ShouldShowHover_Returns_True_When_Enabled_Is_On() =>
        HoverUtils.ShouldShowHover(HoverEnabled.On, MultiCursorModifier.AltKey, Keys(false, false, false)).ShouldBeTrue();

    [Fact]
    public void ShouldShowHover_Returns_False_When_Enabled_Is_Off() =>
        HoverUtils.ShouldShowHover(HoverEnabled.Off, MultiCursorModifier.AltKey, Keys(false, false, false)).ShouldBeFalse();

    [Fact]
    public void ShouldShowHover_Returns_True_With_Ctrl_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.AltKey, Keys(true, false, false)).ShouldBeTrue();

    [Fact]
    public void ShouldShowHover_Returns_False_Without_Ctrl_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.AltKey, Keys(false, false, false)).ShouldBeFalse();

    [Fact]
    public void ShouldShowHover_Returns_True_With_MetaKey_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.AltKey, Keys(false, false, true)).ShouldBeTrue();

    [Fact]
    public void ShouldShowHover_Returns_True_With_Alt_Pressed_When_MultiCursorModifier_Is_CtrlKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.CtrlKey, Keys(false, true, false)).ShouldBeTrue();

    [Fact]
    public void ShouldShowHover_Returns_False_Without_Alt_Pressed_When_MultiCursorModifier_Is_CtrlKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.CtrlKey, Keys(false, false, false)).ShouldBeFalse();

    [Fact]
    public void ShouldShowHover_Returns_True_With_Alt_Pressed_When_MultiCursorModifier_Is_MetaKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.MetaKey, Keys(false, true, false)).ShouldBeTrue();

    [Fact]
    public void ShouldShowHover_Ignores_Alt_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.AltKey, Keys(false, true, false)).ShouldBeFalse();

    [Fact]
    public void ShouldShowHover_Ignores_Ctrl_When_MultiCursorModifier_Is_CtrlKey() =>
        HoverUtils.ShouldShowHover(HoverEnabled.OnKeyboardModifier, MultiCursorModifier.CtrlKey, Keys(true, false, false)).ShouldBeFalse();

    // ---- isMousePositionWithinElement

    private static bool Within((double Left, double Top, double Width, double Height) e, double x, double y) =>
        HoverUtils.IsMousePositionWithinElement(e.Left, e.Top, e.Width, e.Height, x, y);

    [Fact]
    public void IsMousePositionWithinElement_Returns_True_When_Mouse_Is_Inside_Element_Bounds()
    {
        var element = (100d, 100d, 200d, 100d);
        Within(element, 150, 150).ShouldBeTrue();
        Within(element, 200, 150).ShouldBeTrue();
        Within(element, 250, 180).ShouldBeTrue();
    }

    [Fact]
    public void IsMousePositionWithinElement_Returns_True_When_Mouse_Is_On_Element_Edges()
    {
        var element = (100d, 100d, 200d, 100d);
        Within(element, 100, 100).ShouldBeFalse(); // top-left corner
        Within(element, 300, 100).ShouldBeFalse(); // top-right corner
        Within(element, 100, 200).ShouldBeFalse(); // bottom-left corner
        Within(element, 300, 200).ShouldBeFalse(); // bottom-right corner
    }

    [Fact]
    public void IsMousePositionWithinElement_Returns_False_When_Mouse_Is_Left_Of_Element()
    {
        var element = (100d, 100d, 200d, 100d);
        Within(element, 99, 150).ShouldBeFalse();
        Within(element, 50, 150).ShouldBeFalse();
    }

    [Fact]
    public void IsMousePositionWithinElement_Returns_False_When_Mouse_Is_Right_Of_Element()
    {
        var element = (100d, 100d, 200d, 100d);
        Within(element, 301, 150).ShouldBeFalse();
        Within(element, 400, 150).ShouldBeFalse();
    }

    [Fact]
    public void IsMousePositionWithinElement_Returns_False_When_Mouse_Is_Above_Element()
    {
        var element = (100d, 100d, 200d, 100d);
        Within(element, 200, 99).ShouldBeFalse();
        Within(element, 200, 50).ShouldBeFalse();
    }

    [Fact]
    public void IsMousePositionWithinElement_Returns_False_When_Mouse_Is_Below_Element()
    {
        var element = (100d, 100d, 200d, 100d);
        Within(element, 200, 201).ShouldBeFalse();
        Within(element, 200, 300).ShouldBeFalse();
    }

    [Fact]
    public void IsMousePositionWithinElement_Handles_Element_At_Origin()
    {
        var element = (0d, 0d, 100d, 100d);
        Within(element, 0, 0).ShouldBeFalse();
        Within(element, 50, 50).ShouldBeTrue();
        Within(element, 100, 100).ShouldBeFalse();
        Within(element, 101, 101).ShouldBeFalse();
    }

    [Fact]
    public void IsMousePositionWithinElement_Handles_Small_Elements()
    {
        var element = (100d, 100d, 1d, 1d);
        Within(element, 100, 100).ShouldBeFalse();
        Within(element, 101, 101).ShouldBeFalse();
        Within(element, 102, 102).ShouldBeFalse();
    }

    // ---- isTriggerModifierPressed

    [Fact]
    public void IsTriggerModifierPressed_Returns_True_With_Ctrl_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.AltKey, Keys(true, false, false)).ShouldBeTrue();

    [Fact]
    public void IsTriggerModifierPressed_Returns_True_With_MetaKey_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.AltKey, Keys(false, false, true)).ShouldBeTrue();

    [Fact]
    public void IsTriggerModifierPressed_Returns_True_With_Both_Ctrl_And_MetaKey_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.AltKey, Keys(true, false, true)).ShouldBeTrue();

    [Fact]
    public void IsTriggerModifierPressed_Returns_False_Without_Ctrl_Or_MetaKey_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.AltKey, Keys(false, false, false)).ShouldBeFalse();

    [Fact]
    public void IsTriggerModifierPressed_Returns_False_With_Alt_Pressed_When_MultiCursorModifier_Is_AltKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.AltKey, Keys(false, true, false)).ShouldBeFalse();

    [Fact]
    public void IsTriggerModifierPressed_Returns_True_With_Alt_Pressed_When_MultiCursorModifier_Is_CtrlKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.CtrlKey, Keys(false, true, false)).ShouldBeTrue();

    [Fact]
    public void IsTriggerModifierPressed_Returns_False_Without_Alt_Pressed_When_MultiCursorModifier_Is_CtrlKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.CtrlKey, Keys(false, false, false)).ShouldBeFalse();

    [Fact]
    public void IsTriggerModifierPressed_Returns_False_With_Ctrl_Pressed_When_MultiCursorModifier_Is_CtrlKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.CtrlKey, Keys(true, false, false)).ShouldBeFalse();

    [Fact]
    public void IsTriggerModifierPressed_Returns_True_With_Alt_Pressed_When_MultiCursorModifier_Is_MetaKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.MetaKey, Keys(false, true, false)).ShouldBeTrue();

    [Fact]
    public void IsTriggerModifierPressed_Returns_False_Without_Alt_Pressed_When_MultiCursorModifier_Is_MetaKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.MetaKey, Keys(false, false, false)).ShouldBeFalse();

    [Fact]
    public void IsTriggerModifierPressed_Returns_False_With_MetaKey_Pressed_When_MultiCursorModifier_Is_MetaKey() =>
        HoverUtils.IsTriggerModifierPressed(MultiCursorModifier.MetaKey, Keys(false, false, true)).ShouldBeFalse();

    // ---- own: the distance from the pointer to a hover (contentHoverWidget.ts)

    [Fact]
    public void ComputeDistanceFromPointToRectangle_Is_Zero_Inside_And_Euclidean_Outside()
    {
        HoverUtils.ComputeDistanceFromPointToRectangle(150, 150, 100, 100, 200, 100).ShouldBe(0);
        HoverUtils.ComputeDistanceFromPointToRectangle(90, 150, 100, 100, 200, 100).ShouldBe(10);
        HoverUtils.ComputeDistanceFromPointToRectangle(303, 204, 100, 100, 200, 100).ShouldBe(5);
    }
}
