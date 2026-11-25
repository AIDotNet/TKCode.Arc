// Ported from vscode (src/vs/editor/test/common/viewLayout/lineHeights.test.ts, commit b57d5c9e), MIT license.
// The two suites are the two classes below; assert.strictEqual is StrictEqual.

using Shouldly;
using TKCode.Arc.Editor.ViewLayout;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Suite 'Editor ViewLayout - LineHeightsManager'.</summary>
public sealed class LineHeightsManagerTests
{
    [Fact]
    public void Default_Line_Height_Is_Used_When_No_Custom_Heights_Exist()
    {
        var manager = new LineHeightsManager(10, []);

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(5), 10);
        StrictEqual(manager.HeightForLineNumber(100), 10);

        // Check accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 50);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(10), 100);
    }

    [Fact]
    public void Can_Change_Default_Line_Height()
    {
        var manager = new LineHeightsManager(10, []);
        manager.DefaultLineHeight = 20;

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(1), 20);
        StrictEqual(manager.HeightForLineNumber(5), 20);

        // Check accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 20);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 100);
    }

    [Fact]
    public void Can_Add_Single_Custom_Line_Height()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(2), 10);
        StrictEqual(manager.HeightForLineNumber(3), 20);
        StrictEqual(manager.HeightForLineNumber(4), 10);

        // Check accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(2), 20);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 40);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 50);
    }

    [Fact]
    public void Can_Add_Multiple_Custom_Line_Heights()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 2, 2, 15);
        manager.InsertOrChangeCustomLineHeight("dec2", 4, 4, 25);

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(2), 15);
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(4), 25);
        StrictEqual(manager.HeightForLineNumber(5), 10);

        // Check accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(2), 25);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 35);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 60);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 70);
    }

    [Fact]
    public void Can_Add_Range_Of_Custom_Line_Heights()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 2, 4, 15);

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(2), 15);
        StrictEqual(manager.HeightForLineNumber(3), 15);
        StrictEqual(manager.HeightForLineNumber(4), 15);
        StrictEqual(manager.HeightForLineNumber(5), 10);

        // Check accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(2), 25);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 40);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 55);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 65);
    }

    [Fact]
    public void Can_Change_Existing_Custom_Line_Height()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        StrictEqual(manager.HeightForLineNumber(3), 20);

        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 30);
        StrictEqual(manager.HeightForLineNumber(3), 30);

        // Check accumulated heights after change
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 50);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 60);
    }

    [Fact]
    public void Can_Remove_Custom_Line_Height()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        StrictEqual(manager.HeightForLineNumber(3), 20);

        manager.RemoveCustomLineHeight("dec1");
        StrictEqual(manager.HeightForLineNumber(3), 10);

        // Check accumulated heights after removal
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 30);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 40);
    }

    [Fact]
    public void Handles_Overlapping_Custom_Line_Heights_Last_One_Wins()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 5, 20);
        manager.InsertOrChangeCustomLineHeight("dec2", 4, 6, 30);

        StrictEqual(manager.HeightForLineNumber(2), 10);
        StrictEqual(manager.HeightForLineNumber(3), 20);
        StrictEqual(manager.HeightForLineNumber(4), 30);
        StrictEqual(manager.HeightForLineNumber(5), 30);
        StrictEqual(manager.HeightForLineNumber(6), 30);
        StrictEqual(manager.HeightForLineNumber(7), 10);
    }

    [Fact]
    public void Handles_Deleting_Lines_Before_Custom_Line_Heights()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 10, 12, 20);

        manager.OnLinesDeleted(5, 7); // Delete lines 5-7

        StrictEqual(manager.HeightForLineNumber(7), 20); // Was line 10
        StrictEqual(manager.HeightForLineNumber(8), 20); // Was line 11
        StrictEqual(manager.HeightForLineNumber(9), 20); // Was line 12
        StrictEqual(manager.HeightForLineNumber(10), 10);
    }

    [Fact]
    public void Handles_Deleting_Lines_Overlapping_With_Custom_Line_Heights()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 10, 20);

        manager.OnLinesDeleted(7, 12); // Delete lines 7-12, including part of decoration

        StrictEqual(manager.HeightForLineNumber(5), 20);
        StrictEqual(manager.HeightForLineNumber(6), 20);
        StrictEqual(manager.HeightForLineNumber(7), 10);
    }

    [Fact]
    public void Handles_Deleting_Lines_Containing_Custom_Line_Heights_Completely()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 7, 20);

        manager.OnLinesDeleted(4, 8); // Delete lines 4-8, completely contains decoration

        // The decoration collapses to a single line which matches the behavior in the text buffer
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(4), 20);
        StrictEqual(manager.HeightForLineNumber(5), 10);
    }

    [Fact]
    public void Handles_Deleting_Lines_At_The_Very_Beginning()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("decA", 1, 1, 40);

        manager.OnLinesDeleted(2, 4); // Delete lines 2-4 after the variable line height

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(1), 40);
    }

    [Fact]
    public void Handles_Inserting_Lines_Before_Custom_Line_Heights()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 7, 20);

        manager.OnLinesInserted(3, 4); // Insert 2 lines at line 3

        StrictEqual(manager.HeightForLineNumber(5), 10);
        StrictEqual(manager.HeightForLineNumber(6), 10);
        StrictEqual(manager.HeightForLineNumber(7), 20); // Was line 5
        StrictEqual(manager.HeightForLineNumber(8), 20); // Was line 6
        StrictEqual(manager.HeightForLineNumber(9), 20); // Was line 7
    }

    [Fact]
    public void Handles_Inserting_Lines_Inside_Custom_Line_Heights_Range()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 7, 20);

        manager.OnLinesInserted(6, 7); // Insert 2 lines at line 6

        StrictEqual(manager.HeightForLineNumber(5), 20);
        StrictEqual(manager.HeightForLineNumber(6), 20);
        StrictEqual(manager.HeightForLineNumber(7), 20);
        StrictEqual(manager.HeightForLineNumber(8), 20);
        StrictEqual(manager.HeightForLineNumber(9), 20);
    }

    [Fact]
    public void Changing_Decoration_Id_Maintains_Custom_Line_Height()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 7, 20);

        manager.RemoveCustomLineHeight("dec1");
        manager.InsertOrChangeCustomLineHeight("dec2", 5, 7, 20);

        StrictEqual(manager.HeightForLineNumber(5), 20);
        StrictEqual(manager.HeightForLineNumber(6), 20);
        StrictEqual(manager.HeightForLineNumber(7), 20);
    }

    [Fact]
    public void Accumulates_Heights_Correctly_With_Complex_Setup()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 15);
        manager.InsertOrChangeCustomLineHeight("dec2", 5, 7, 20);
        manager.InsertOrChangeCustomLineHeight("dec3", 10, 10, 30);

        // Check accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(2), 20);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 35);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 45);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 65);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(7), 105);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(9), 125);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(10), 155);
    }

    [Fact]
    public void Partial_Deletion_With_Multiple_Lines_For_The_Same_Decoration_ID()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("decSame", 5, 5, 20);
        manager.InsertOrChangeCustomLineHeight("decSame", 6, 6, 25);

        // Delete one line that partially intersects the same decoration
        manager.OnLinesDeleted(6, 6);

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(5), 10);
        StrictEqual(manager.HeightForLineNumber(6), 25);
    }

    [Fact]
    public void Overlapping_Decorations_Use_Maximum_Line_Height()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("decA", 3, 5, 40);
        manager.InsertOrChangeCustomLineHeight("decB", 4, 6, 30);

        // Check individual line heights
        StrictEqual(manager.HeightForLineNumber(3), 40);
        StrictEqual(manager.HeightForLineNumber(4), 40);
        StrictEqual(manager.HeightForLineNumber(5), 40);
        StrictEqual(manager.HeightForLineNumber(6), 30);
    }

    [Fact]
    public void OnLinesInserted_With_Same_Decoration_ID_Extending_To_Inserted_Line()
    {
        var manager = new LineHeightsManager(10, []);
        // Set up a special line at line 1 with decoration "decA"
        manager.InsertOrChangeCustomLineHeight("decA", 1, 1, 30);

        StrictEqual(manager.HeightForLineNumber(1), 30);
        StrictEqual(manager.HeightForLineNumber(2), 10);

        // Insert line 2 to line 2, with the same decoration ID "decA" covering line 2
        manager.OnLinesInserted(2, 2);
        manager.InsertOrChangeCustomLineHeight("decA", 2, 2, 30);

        // After insertion, the decoration "decA" now covers line 2
        // Since insertOrChangeCustomLineHeight removes the old decoration first,
        // line 1 no longer has the custom height, and line 2 gets it
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(2), 30);
        StrictEqual(manager.HeightForLineNumber(3), 10);
    }


    private static void StrictEqual(double actual, double expected) => actual.ShouldBe(expected);
}

/// <summary>Suite 'Editor ViewLayout - LineHeightsManager (auto-commit on read)'.</summary>
public sealed class LineHeightsManagerAutoCommitTests
{
    // --- Auto-commit on read: reads without explicit commit() ---

    [Fact]
    public void Read_After_Single_Insert_Without_Commit()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // No commit() call — read should still work
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(3), 20);
        StrictEqual(manager.HeightForLineNumber(4), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 40);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 50);
    }

    [Fact]
    public void Read_After_Multiple_Inserts_Without_Commit()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 2, 2, 15);
        manager.InsertOrChangeCustomLineHeight("dec2", 4, 4, 25);
        // No commit() call
        StrictEqual(manager.HeightForLineNumber(2), 15);
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(4), 25);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 60);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 70);
    }

    [Fact]
    public void Read_After_Remove_Without_Commit()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        StrictEqual(manager.HeightForLineNumber(3), 20);

        manager.RemoveCustomLineHeight("dec1");
        // No commit() call
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 30);
    }

    [Fact]
    public void Insert_Then_Remove_Same_Decoration_Without_Commit()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        manager.RemoveCustomLineHeight("dec1");
        // No commit() call — should see default height
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 30);
    }

    [Fact]
    public void Insert_Same_Decoration_ID_Twice_Without_Commit_Replaces_First()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 5, 30);
        // No commit() — second call should replace first
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(5), 30);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 70);
    }

    [Fact]
    public void Interleaved_Callers_Remove_Must_Cancel_Queued_Inserts_Before_First_Flush()
    {
        var manager = new LineHeightsManager(10, []);

        // Caller A queues decoration insert.
        manager.InsertOrChangeCustomLineHeight("decA", 3, 3, 20);
        // Caller B queues independent insert.
        manager.InsertOrChangeCustomLineHeight("decB", 4, 4, 30);
        // Caller A removes its decoration before any flush occurs.
        manager.RemoveCustomLineHeight("decA");
        // Caller B triggers a structural change that causes queue flush in the middle of commit.
        manager.OnLinesInserted(1, 1);

        // decA must stay removed. If queued inserts are not canceled on remove, decA incorrectly survives.
        StrictEqual(manager.HeightForLineNumber(4), 10);
        StrictEqual(manager.HeightForLineNumber(5), 30);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 70);
    }

    [Fact]
    public void Interleaved_Callers_Remove_Must_Cancel_Queued_Inserts_Before_Delete_Flush()
    {
        var manager = new LineHeightsManager(10, []);

        manager.InsertOrChangeCustomLineHeight("decA", 3, 3, 20);
        manager.InsertOrChangeCustomLineHeight("decB", 5, 5, 30);
        manager.RemoveCustomLineHeight("decA");
        manager.OnLinesDeleted(1, 1);

        // After deleting line 1, decB shifts from line 5 to line 4.
        // decA must remain removed even though its insert was queued before the remove.
        StrictEqual(manager.HeightForLineNumber(2), 10);
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(4), 30);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 60);
    }

    // --- Interleaved operations ---

    [Fact]
    public void Interleaved_Insert_Insert_OnLinesInserted_OnLinesDeleted_Remove_Remove_Insert_Insert_Read()
    {
        var manager = new LineHeightsManager(10, []);
        // Step 1-2: two inserts
        manager.InsertOrChangeCustomLineHeight("dec1", 2, 2, 20);
        manager.InsertOrChangeCustomLineHeight("dec2", 5, 5, 30);
        // Step 3: insert 2 lines at line 3 (shifts dec2 from line 5 → 7)
        manager.OnLinesInserted(3, 4);
        // Step 4: delete line 1 (shifts dec1 from line 2 → 1, dec2 from line 7 → 6)
        manager.OnLinesDeleted(1, 1);
        // Step 5-6: remove the two decorations
        manager.RemoveCustomLineHeight("dec1");
        manager.RemoveCustomLineHeight("dec2");
        // Step 7-8: two new inserts
        manager.InsertOrChangeCustomLineHeight("dec3", 3, 3, 40);
        manager.InsertOrChangeCustomLineHeight("dec4", 5, 5, 50);
        // Read — no explicit commit
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(3), 40);
        StrictEqual(manager.HeightForLineNumber(4), 10);
        StrictEqual(manager.HeightForLineNumber(5), 50);
        StrictEqual(manager.HeightForLineNumber(6), 10);
    }

    [Fact]
    public void Interleaved_Insert_OnLinesInserted_Remove_Read()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // Insert 1 line at line 1 → dec1 shifts from 3 → 4
        manager.OnLinesInserted(1, 1);
        manager.RemoveCustomLineHeight("dec1");
        // Read — no explicit commit
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(4), 10);
    }

    [Fact]
    public void Interleaved_OnLinesDeleted_Insert_Read()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 5, 5, 20);
        // Delete lines 1-2 → dec1 shifts from 5 → 3
        manager.OnLinesDeleted(1, 2);
        // Insert a new decoration
        manager.InsertOrChangeCustomLineHeight("dec2", 1, 1, 30);
        // Read — no explicit commit
        StrictEqual(manager.HeightForLineNumber(1), 30);
        StrictEqual(manager.HeightForLineNumber(2), 10);
        StrictEqual(manager.HeightForLineNumber(3), 20);
    }

    [Fact]
    public void Interleaved_Insert_OnLinesDeleted_Insert_Read()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // Delete line 1 → dec1 should shift from 3 → 2
        manager.OnLinesDeleted(1, 1);
        // Add another decoration
        manager.InsertOrChangeCustomLineHeight("dec2", 5, 5, 30);
        // Read — no explicit commit
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(2), 20);
        StrictEqual(manager.HeightForLineNumber(5), 30);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 80);
    }

    // --- Edge cases ---

    [Fact]
    public void OnLinesInserted_Then_OnLinesDeleted_Without_Reads_Between()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // Insert 2 lines at line 1 → dec1 moves from 3 → 5
        manager.OnLinesInserted(1, 2);
        // Delete line 1 → dec1 moves from 5 → 4
        manager.OnLinesDeleted(1, 1);
        // Read
        StrictEqual(manager.HeightForLineNumber(4), 20);
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 50);
    }

    [Fact]
    public void Multiple_OnLinesInserted_Without_Reads_Between()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // Insert 1 line at line 1 → dec1 at 3 → 4
        manager.OnLinesInserted(1, 1);
        // Insert 1 line at line 1 → dec1 at 4 → 5
        manager.OnLinesInserted(1, 1);
        // Read
        StrictEqual(manager.HeightForLineNumber(5), 20);
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(4), 10);
    }

    [Fact]
    public void Multiple_OnLinesDeleted_Without_Reads_Between()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 10, 10, 20);
        // Delete lines 1-2 → dec1 at 10 → 8
        manager.OnLinesDeleted(1, 2);
        // Delete lines 1-2 → dec1 at 8 → 6
        manager.OnLinesDeleted(1, 2);
        // Read
        StrictEqual(manager.HeightForLineNumber(6), 20);
        StrictEqual(manager.HeightForLineNumber(7), 10);
    }

    [Fact]
    public void Pending_Insert_Then_OnLinesDeleted_Affecting_That_Line()
    {
        var manager = new LineHeightsManager(10, []);
        // Insert a decoration at line 3 (pending, not committed)
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // Delete line 3 — should remove/collapse the pending decoration
        manager.OnLinesDeleted(3, 3);
        // Read — the decoration was on the deleted line
        // The decoration collapses to line 3 (fromLineNumber) per onLinesDeleted behavior
        StrictEqual(manager.HeightForLineNumber(3), 20);
    }

    [Fact]
    public void Pending_Insert_Then_OnLinesInserted_Shifting_That_Line()
    {
        var manager = new LineHeightsManager(10, []);
        // Insert a decoration at line 3 (pending, not committed)
        manager.InsertOrChangeCustomLineHeight("dec1", 3, 3, 20);
        // Insert 2 lines before it at line 1 → should shift dec1 from 3 → 5
        manager.OnLinesInserted(1, 2);
        // Read
        StrictEqual(manager.HeightForLineNumber(3), 10);
        StrictEqual(manager.HeightForLineNumber(5), 20);
    }

    [Fact]
    public void Accumulated_Heights_Correct_After_Interleaved_Ops_Without_Commit()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 2, 2, 15);
        manager.InsertOrChangeCustomLineHeight("dec2", 4, 4, 25);
        // No commit — verify accumulated heights
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(1), 10);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(2), 25);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(3), 35);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(4), 60);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(5), 70);
    }

    [Fact]
    public void Constructor_With_Initial_Data_Works_Without_Explicit_Commit()
    {
        CustomLineHeightData[] data = [
            new CustomLineHeightData("dec1", 2, 4, 20),
            new CustomLineHeightData("dec2", 6, 6, 30),
        ];
        var manager = new LineHeightsManager(10, data);
        StrictEqual(manager.HeightForLineNumber(1), 10);
        StrictEqual(manager.HeightForLineNumber(2), 20);
        StrictEqual(manager.HeightForLineNumber(3), 20);
        StrictEqual(manager.HeightForLineNumber(4), 20);
        StrictEqual(manager.HeightForLineNumber(5), 10);
        StrictEqual(manager.HeightForLineNumber(6), 30);
        StrictEqual(manager.GetAccumulatedLineHeightsIncludingLineNumber(6), 110);
    }

    [Fact]
    public void Deleting_Line_2_With_LineHeightsRemoved_Re_Adding_At_Line_1_Moves_Special_Line_To_Line_1()
    {
        var manager = new LineHeightsManager(10, []);
        manager.InsertOrChangeCustomLineHeight("dec1", 2, 2, 20);
        StrictEqual(manager.HeightForLineNumber(2), 20);
        manager.OnLinesDeleted(2, 2);
        manager.InsertOrChangeCustomLineHeight("dec1", 1, 1, 20);
        StrictEqual(manager.HeightForLineNumber(1), 20);
    }

    private static void StrictEqual(double actual, double expected) => actual.ShouldBe(expected);
}
