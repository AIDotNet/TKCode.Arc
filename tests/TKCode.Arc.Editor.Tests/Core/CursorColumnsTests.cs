// Ported from vscode (src/vs/editor/test/common/core/cursorColumns.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorColumnsTests
{
    [Fact]
    public void VisibleColumnFromColumn()
    {
        static void TestVisibleColumnFromColumn(string text, int tabSize, int column, int expected)
        {
            CursorColumns.VisibleColumnFromColumn(text, column, tabSize).ShouldBe(expected);
        }

        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 1, 0);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 2, 4);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 3, 8);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 4, 9);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 5, 10);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 6, 11);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 7, 12);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 8, 13);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 9, 14);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 10, 15);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 11, 16);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 12, 17);
        TestVisibleColumnFromColumn("\t\tvar x = 3;", 4, 13, 18);

        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 1, 0);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 2, 4);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 3, 5);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 4, 8);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 5, 9);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 6, 10);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 7, 11);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 8, 12);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 9, 13);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 10, 14);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 11, 15);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 12, 16);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 13, 17);
        TestVisibleColumnFromColumn("\t \tvar x = 3;", 4, 14, 18);

        TestVisibleColumnFromColumn("\t  \tx\t", 4, -1, 0);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 0, 0);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 1, 0);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 2, 4);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 3, 5);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 4, 6);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 5, 8);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 6, 9);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 7, 12);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 8, 12);
        TestVisibleColumnFromColumn("\t  \tx\t", 4, 9, 12);

        TestVisibleColumnFromColumn("baz", 4, 1, 0);
        TestVisibleColumnFromColumn("baz", 4, 2, 1);
        TestVisibleColumnFromColumn("baz", 4, 3, 2);
        TestVisibleColumnFromColumn("baz", 4, 4, 3);

        TestVisibleColumnFromColumn("📚az", 4, 1, 0);
        TestVisibleColumnFromColumn("📚az", 4, 2, 1);
        TestVisibleColumnFromColumn("📚az", 4, 3, 2);
        TestVisibleColumnFromColumn("📚az", 4, 4, 3);
        TestVisibleColumnFromColumn("📚az", 4, 5, 4);
    }

    [Fact]
    public void ToStatusbarColumn()
    {
        static void T(string text, int tabSize, int column, int expected)
        {
            CursorColumns.ToStatusbarColumn(text, column, tabSize).ShouldBe(expected, $"<<t('{text}', {tabSize}, {column}, {expected})>>");
        }

        T("    spaces", 4, 1, 1);
        T("    spaces", 4, 2, 2);
        T("    spaces", 4, 3, 3);
        T("    spaces", 4, 4, 4);
        T("    spaces", 4, 5, 5);
        T("    spaces", 4, 6, 6);
        T("    spaces", 4, 7, 7);
        T("    spaces", 4, 8, 8);
        T("    spaces", 4, 9, 9);
        T("    spaces", 4, 10, 10);
        T("    spaces", 4, 11, 11);

        T("\ttab", 4, 1, 1);
        T("\ttab", 4, 2, 5);
        T("\ttab", 4, 3, 6);
        T("\ttab", 4, 4, 7);
        T("\ttab", 4, 5, 8);

        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 1, 1);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 2, 2);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 3, 2);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 4, 3);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 5, 3);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 6, 4);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 7, 4);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 8, 5);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 9, 5);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 10, 6);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 11, 6);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 12, 7);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 13, 7);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 14, 8);
        T("𐌀𐌁𐌂𐌃𐌄𐌅𐌆", 4, 15, 8);

        T("🎈🎈🎈🎈", 4, 1, 1);
        T("🎈🎈🎈🎈", 4, 2, 2);
        T("🎈🎈🎈🎈", 4, 3, 2);
        T("🎈🎈🎈🎈", 4, 4, 3);
        T("🎈🎈🎈🎈", 4, 5, 3);
        T("🎈🎈🎈🎈", 4, 6, 4);
        T("🎈🎈🎈🎈", 4, 7, 4);
        T("🎈🎈🎈🎈", 4, 8, 5);
        T("🎈🎈🎈🎈", 4, 9, 5);

        T("何何何何", 4, 1, 1);
        T("何何何何", 4, 2, 2);
        T("何何何何", 4, 3, 3);
        T("何何何何", 4, 4, 4);
    }

    [Fact]
    public void ColumnFromVisibleColumn()
    {
        static void TestColumnFromVisibleColumn(string text, int tabSize, int visibleColumn, int expected)
        {
            CursorColumns.ColumnFromVisibleColumn(text, visibleColumn, tabSize).ShouldBe(expected);
        }

        // TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 0, 1);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 1, 1);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 2, 1);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 3, 2);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 4, 2);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 5, 2);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 6, 2);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 7, 3);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 8, 3);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 9, 4);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 10, 5);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 11, 6);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 12, 7);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 13, 8);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 14, 9);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 15, 10);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 16, 11);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 17, 12);
        TestColumnFromVisibleColumn("\t\tvar x = 3;", 4, 18, 13);

        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 0, 1);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 1, 1);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 2, 1);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 3, 2);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 4, 2);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 5, 3);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 6, 3);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 7, 4);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 8, 4);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 9, 5);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 10, 6);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 11, 7);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 12, 8);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 13, 9);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 14, 10);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 15, 11);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 16, 12);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 17, 13);
        TestColumnFromVisibleColumn("\t \tvar x = 3;", 4, 18, 14);

        TestColumnFromVisibleColumn("\t  \tx\t", 4, -2, 1);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, -1, 1);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 0, 1);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 1, 1);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 2, 1);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 3, 2);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 4, 2);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 5, 3);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 6, 4);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 7, 4);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 8, 5);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 9, 6);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 10, 6);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 11, 7);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 12, 7);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 13, 7);
        TestColumnFromVisibleColumn("\t  \tx\t", 4, 14, 7);

        TestColumnFromVisibleColumn("baz", 4, 0, 1);
        TestColumnFromVisibleColumn("baz", 4, 1, 2);
        TestColumnFromVisibleColumn("baz", 4, 2, 3);
        TestColumnFromVisibleColumn("baz", 4, 3, 4);

        TestColumnFromVisibleColumn("📚az", 4, 0, 1);
        TestColumnFromVisibleColumn("📚az", 4, 1, 1);
        TestColumnFromVisibleColumn("📚az", 4, 2, 3);
        TestColumnFromVisibleColumn("📚az", 4, 3, 4);
        TestColumnFromVisibleColumn("📚az", 4, 4, 5);
    }

    [Fact]
    public void NextRenderTabStop() => AssertNextTabStop(CursorColumns.NextRenderTabStop);

    [Fact]
    public void NextIndentTabStop() => AssertNextTabStop(CursorColumns.NextIndentTabStop);

    [Fact]
    public void PrevRenderTabStop() => AssertPrevTabStop(CursorColumns.PrevRenderTabStop);

    [Fact]
    public void PrevIndentTabStop() => AssertPrevTabStop(CursorColumns.PrevIndentTabStop);

    private static void AssertNextTabStop(Func<int, int, int> fn)
    {
        fn(0, 4).ShouldBe(4);
        fn(1, 4).ShouldBe(4);
        fn(2, 4).ShouldBe(4);
        fn(3, 4).ShouldBe(4);
        fn(4, 4).ShouldBe(8);
        fn(5, 4).ShouldBe(8);
        fn(6, 4).ShouldBe(8);
        fn(7, 4).ShouldBe(8);
        fn(8, 4).ShouldBe(12);

        fn(0, 2).ShouldBe(2);
        fn(1, 2).ShouldBe(2);
        fn(2, 2).ShouldBe(4);
        fn(3, 2).ShouldBe(4);
        fn(4, 2).ShouldBe(6);
        fn(5, 2).ShouldBe(6);
        fn(6, 2).ShouldBe(8);
        fn(7, 2).ShouldBe(8);
        fn(8, 2).ShouldBe(10);

        fn(0, 1).ShouldBe(1);
        fn(1, 1).ShouldBe(2);
        fn(2, 1).ShouldBe(3);
        fn(3, 1).ShouldBe(4);
        fn(4, 1).ShouldBe(5);
        fn(5, 1).ShouldBe(6);
        fn(6, 1).ShouldBe(7);
        fn(7, 1).ShouldBe(8);
        fn(8, 1).ShouldBe(9);
    }

    private static void AssertPrevTabStop(Func<int, int, int> fn)
    {
        fn(0, 4).ShouldBe(0);
        fn(1, 4).ShouldBe(0);
        fn(2, 4).ShouldBe(0);
        fn(3, 4).ShouldBe(0);
        fn(4, 4).ShouldBe(0);
        fn(5, 4).ShouldBe(4);
        fn(6, 4).ShouldBe(4);
        fn(7, 4).ShouldBe(4);
        fn(8, 4).ShouldBe(4);
        fn(9, 4).ShouldBe(8);

        fn(0, 2).ShouldBe(0);
        fn(1, 2).ShouldBe(0);
        fn(2, 2).ShouldBe(0);
        fn(3, 2).ShouldBe(2);
        fn(4, 2).ShouldBe(2);
        fn(5, 2).ShouldBe(4);
        fn(6, 2).ShouldBe(4);
        fn(7, 2).ShouldBe(6);
        fn(8, 2).ShouldBe(6);
        fn(9, 2).ShouldBe(8);

        fn(0, 1).ShouldBe(0);
        fn(1, 1).ShouldBe(0);
        fn(2, 1).ShouldBe(1);
        fn(3, 1).ShouldBe(2);
        fn(4, 1).ShouldBe(3);
        fn(5, 1).ShouldBe(4);
        fn(6, 1).ShouldBe(5);
        fn(7, 1).ShouldBe(6);
        fn(8, 1).ShouldBe(7);
        fn(9, 1).ShouldBe(8);
    }
}
