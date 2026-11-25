// Ported from vscode (src/vs/editor/test/common/model/textModel.test.ts, commit b57d5c9e), MIT license.
// This file: the 'TextModel.createSnapshot' suite. 'issue #119632: invalid range' is left out: it passes undefined line
// numbers, which TextRange (ints) cannot hold.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelCreateSnapshotTests
{
    private const string Utf8BomCharacter = "﻿";

    [Fact]
    public void Empty_File()
    {
        var model = TestTextModel.Create("");
        var snapshot = model.CreateSnapshot();
        snapshot.Read().ShouldBeNull();
        model.Dispose();
    }

    [Fact]
    public void File_With_BOM()
    {
        var model = TestTextModel.Create(Utf8BomCharacter + "Hello");
        model.GetLineContent(1).ShouldBe("Hello");
        var snapshot = model.CreateSnapshot(true);
        snapshot.Read().ShouldBe(Utf8BomCharacter + "Hello");
        snapshot.Read().ShouldBeNull();
        model.Dispose();
    }

    [Fact]
    public void Regular_File()
    {
        var model = TestTextModel.Create("My First Line\n\t\tMy Second Line\n    Third Line\n\n1");
        var snapshot = model.CreateSnapshot();
        snapshot.Read().ShouldBe("My First Line\n\t\tMy Second Line\n    Third Line\n\n1");
        snapshot.Read().ShouldBeNull();
        model.Dispose();
    }

    [Fact]
    public void Large_File()
    {
        var lines = new string[1000];
        for (var i = 0; i < 1000; i++)
        {
            lines[i] = "Just some text that is a bit long such that it can consume some memory";
        }

        var text = string.Join('\n', lines);

        var model = TestTextModel.Create(text);
        var snapshot = model.CreateSnapshot();
        var actual = "";

        // 70999 length => at most 2 read calls are necessary
        var tmp1 = snapshot.Read();
        tmp1.ShouldNotBeNullOrEmpty();
        actual += tmp1;

        var tmp2 = snapshot.Read();
        if (tmp2 is null)
        {
            // all good
        }
        else
        {
            actual += tmp2;
            snapshot.Read().ShouldBeNull();
        }

        actual.ShouldBe(text);

        model.Dispose();
    }
}
