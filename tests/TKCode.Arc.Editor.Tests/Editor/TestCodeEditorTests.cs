// Not from vscode: checks of the test editor itself, so that the ported cursor tests that rely on wrapping or on a
// configuration really run with it.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TestCodeEditorTests
{
    [Fact]
    public void Word_Wrap_Column_Wraps_View_Lines()
    {
        // the model line of upstream's issue #36740, which upstream says becomes four view lines at column 16
        using var editor = TestCodeEditor.Create(
            ["Lorem ipsum dolor sit amet consectetur adipiscing elit"],
            new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 16 });

        editor.Model.GetLineCount().ShouldBe(1);
        editor.ViewModel.GetLineCount().ShouldBe(4);
    }

    [Fact]
    public void Without_Word_Wrap_View_Lines_Are_Model_Lines()
    {
        using var editor = TestCodeEditor.Create(["Lorem ipsum dolor sit amet consectetur adipiscing elit"]);

        editor.ViewModel.GetLineCount().ShouldBe(1);
    }
}
