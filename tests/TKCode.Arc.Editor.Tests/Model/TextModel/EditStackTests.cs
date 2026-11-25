// Ported from vscode (src/vs/editor/test/common/model/editStack.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class EditStackTests
{
    [Fact]
    public void Issue_118041_Unicode_Character_Undo_Bug()
    {
        var stackData = new SingleModelEditStackData(
            1,
            2,
            EndOfLineSequence.LF,
            EndOfLineSequence.LF,
            [new TextSelection(10, 2, 10, 2)],
            [new TextSelection(10, 1, 10, 1)],
            [new TextChange(428, "﻿", 428, string.Empty)]);

        var buff = stackData.Serialize();
        var actual = SingleModelEditStackData.Deserialize(buff);

        actual.BeforeVersionId.ShouldBe(stackData.BeforeVersionId);
        actual.AfterVersionId.ShouldBe(stackData.AfterVersionId);
        actual.BeforeEOL.ShouldBe(stackData.BeforeEOL);
        actual.AfterEOL.ShouldBe(stackData.AfterEOL);
        actual.BeforeCursorState.ShouldBe(stackData.BeforeCursorState);
        actual.AfterCursorState.ShouldBe(stackData.AfterCursorState);
        actual.Changes.ShouldBe(stackData.Changes);
    }
}
