// Ported from vscode (src/vs/platform/undoRedo/test/common/undoRedoService.test.ts, commit b57d5c9e), MIT license.
// The first four tests are the upstream tests; the rest cover snapshot/restore, SetElementsValidFlag, source-based
// undo, the error path, the synchronous-completion guarantee and the two workspace prompt choices.

using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class UndoRedoServiceTests
{
    private static Uri File(string name) => new("file:///" + name);

    private static UndoRedoService CreateUndoRedoService(
        TestDialogService? dialogService = null,
        TestNotificationService? notificationService = null)
    {
        return new UndoRedoService(
            dialogService ?? new TestDialogService(),
            notificationService ?? new TestNotificationService());
    }

    [Fact]
    public async Task SimpleSingleResourceElements()
    {
        var resource = File("test.txt");
        var service = CreateUndoRedoService();

        service.CanUndo(resource).ShouldBeFalse();
        service.CanRedo(resource).ShouldBeFalse();
        service.HasElements(resource).ShouldBeFalse();
        service.GetLastElement(resource).ShouldBeNull();

        var undoCall1 = 0;
        var redoCall1 = 0;
        var element1 = new TestResourceElement
        {
            Resource = resource,
            Label = "typing 1",
            UndoAction = () => { undoCall1++; },
            RedoAction = () => { redoCall1++; },
        };
        service.PushElement(element1);

        undoCall1.ShouldBe(0);
        redoCall1.ShouldBe(0);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeFalse();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeSameAs(element1);

        await service.Undo(resource);
        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(0);
        service.CanUndo(resource).ShouldBeFalse();
        service.CanRedo(resource).ShouldBeTrue();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeNull();

        await service.Redo(resource);
        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(1);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeFalse();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeSameAs(element1);

        var undoCall2 = 0;
        var redoCall2 = 0;
        var element2 = new TestResourceElement
        {
            Resource = resource,
            Label = "typing 2",
            UndoAction = () => { undoCall2++; },
            RedoAction = () => { redoCall2++; },
        };
        service.PushElement(element2);

        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(1);
        undoCall2.ShouldBe(0);
        redoCall2.ShouldBe(0);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeFalse();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeSameAs(element2);

        await service.Undo(resource);

        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(1);
        undoCall2.ShouldBe(1);
        redoCall2.ShouldBe(0);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeTrue();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeNull();

        var undoCall3 = 0;
        var redoCall3 = 0;
        var element3 = new TestResourceElement
        {
            Resource = resource,
            Label = "typing 2",
            UndoAction = () => { undoCall3++; },
            RedoAction = () => { redoCall3++; },
        };
        service.PushElement(element3);

        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(1);
        undoCall2.ShouldBe(1);
        redoCall2.ShouldBe(0);
        undoCall3.ShouldBe(0);
        redoCall3.ShouldBe(0);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeFalse();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeSameAs(element3);

        await service.Undo(resource);

        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(1);
        undoCall2.ShouldBe(1);
        redoCall2.ShouldBe(0);
        undoCall3.ShouldBe(1);
        redoCall3.ShouldBe(0);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeTrue();
        service.HasElements(resource).ShouldBeTrue();
        service.GetLastElement(resource).ShouldBeNull();
    }

    [Fact]
    public async Task MultiResourceElements()
    {
        var resource1 = File("test1.txt");
        var resource2 = File("test2.txt");
        var dialogService = new TestDialogService { PromptResult = UndoChoice.All };
        var service = CreateUndoRedoService(dialogService);

        var undoCall1 = 0;
        var undoCall11 = 0;
        var undoCall12 = 0;
        var redoCall1 = 0;
        var redoCall11 = 0;
        var redoCall12 = 0;
        var element1 = new TestWorkspaceElement
        {
            Resources = new[] { resource1, resource2 },
            Label = "typing 1",
            UndoAction = () => { undoCall1++; },
            RedoAction = () => { redoCall1++; },
            SplitAction = () => new IResourceUndoRedoElement[]
            {
                new TestResourceElement
                {
                    Resource = resource1,
                    Label = "typing 1.1",
                    UndoAction = () => { undoCall11++; },
                    RedoAction = () => { redoCall11++; },
                },
                new TestResourceElement
                {
                    Resource = resource2,
                    Label = "typing 1.2",
                    UndoAction = () => { undoCall12++; },
                    RedoAction = () => { redoCall12++; },
                },
            },
        };
        service.PushElement(element1);

        service.CanUndo(resource1).ShouldBeTrue();
        service.CanRedo(resource1).ShouldBeFalse();
        service.HasElements(resource1).ShouldBeTrue();
        service.GetLastElement(resource1).ShouldBeSameAs(element1);
        service.CanUndo(resource2).ShouldBeTrue();
        service.CanRedo(resource2).ShouldBeFalse();
        service.HasElements(resource2).ShouldBeTrue();
        service.GetLastElement(resource2).ShouldBeSameAs(element1);

        await service.Undo(resource1);

        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(0);
        service.CanUndo(resource1).ShouldBeFalse();
        service.CanRedo(resource1).ShouldBeTrue();
        service.HasElements(resource1).ShouldBeTrue();
        service.GetLastElement(resource1).ShouldBeNull();
        service.CanUndo(resource2).ShouldBeFalse();
        service.CanRedo(resource2).ShouldBeTrue();
        service.HasElements(resource2).ShouldBeTrue();
        service.GetLastElement(resource2).ShouldBeNull();

        await service.Redo(resource2);
        undoCall1.ShouldBe(1);
        redoCall1.ShouldBe(1);
        undoCall11.ShouldBe(0);
        redoCall11.ShouldBe(0);
        undoCall12.ShouldBe(0);
        redoCall12.ShouldBe(0);
        service.CanUndo(resource1).ShouldBeTrue();
        service.CanRedo(resource1).ShouldBeFalse();
        service.HasElements(resource1).ShouldBeTrue();
        service.GetLastElement(resource1).ShouldBeSameAs(element1);
        service.CanUndo(resource2).ShouldBeTrue();
        service.CanRedo(resource2).ShouldBeFalse();
        service.HasElements(resource2).ShouldBeTrue();
        service.GetLastElement(resource2).ShouldBeSameAs(element1);
    }

    [Fact]
    public void UndoRedoGroupNoneUsesId0()
    {
        UndoRedoGroup.None.Id.ShouldBe(0);
        UndoRedoGroup.None.NextOrder().ShouldBe(0);
        UndoRedoGroup.None.NextOrder().ShouldBe(0);
    }

    [Fact]
    public void RestoreSnapshotPreservesElementsThatMatchTheSnapshot()
    {
        var resource = File("test.txt");
        var service = CreateUndoRedoService();

        // Push three elements
        var element1 = new TestResourceElement { Resource = resource, Label = "typing 1" };
        var element2 = new TestResourceElement { Resource = resource, Label = "typing 2" };
        var element3 = new TestResourceElement { Resource = resource, Label = "typing 3" };
        service.PushElement(element1);
        service.PushElement(element2);
        service.PushElement(element3);

        // Create snapshot after 3 elements: [element1, element2, element3]
        var snapshot = service.CreateSnapshot(resource);

        // Push more elements after the snapshot
        var element4 = new TestResourceElement { Resource = resource, Label = "typing 4" };
        var element5 = new TestResourceElement { Resource = resource, Label = "typing 5" };
        service.PushElement(element4);
        service.PushElement(element5);

        // Verify we have 5 elements now
        var elements = service.GetElements(resource);
        elements.Past.Count.ShouldBe(5);
        elements.Future.Count.ShouldBe(0);

        // Restore snapshot - should remove element4 and element5, but keep element1, element2, element3
        service.RestoreSnapshot(snapshot);

        // Verify that elements matching the snapshot are preserved
        elements = service.GetElements(resource);
        elements.Past.Count.ShouldBe(3, "Should have 3 past elements after restore");
        elements.Future.Count.ShouldBe(0, "Should have 0 future elements after restore");
        elements.Past[0].ShouldBeSameAs(element1, "First element should be element1");
        elements.Past[1].ShouldBeSameAs(element2, "Second element should be element2");
        elements.Past[2].ShouldBeSameAs(element3, "Third element should be element3");
    }

    [Fact]
    public void SynchronousElementsCompleteSynchronouslyAndReleaseLocks()
    {
        var resource = File("test.txt");
        var service = CreateUndoRedoService();

        var undoCall = 0;
        var redoCall = 0;
        service.PushElement(new TestResourceElement
        {
            Resource = resource,
            UndoAction = () => { undoCall++; },
            RedoAction = () => { redoCall++; },
        });

        // The element completes synchronously, so the returned task is already completed and the stacks are already
        // moved: no continuation was posted.
        var undoTask = service.Undo(resource);
        undoTask.IsCompletedSuccessfully.ShouldBeTrue();
        undoCall.ShouldBe(1);
        service.CanUndo(resource).ShouldBeFalse();
        service.CanRedo(resource).ShouldBeTrue();

        // The lock was released before Undo returned: a second operation runs instead of being refused.
        var redoTask = service.Redo(resource);
        redoTask.IsCompletedSuccessfully.ShouldBeTrue();
        redoCall.ShouldBe(1);
        service.CanUndo(resource).ShouldBeTrue();
        service.CanRedo(resource).ShouldBeFalse();
    }

    [Fact]
    public void SetElementsValidFlagInvalidatesElements()
    {
        var resource = File("test.txt");
        var service = CreateUndoRedoService();

        var undoCall = 0;
        service.PushElement(new TestResourceElement
        {
            Resource = resource,
            Label = "typing 1",
            UndoAction = () => { undoCall++; },
        });
        service.PushElement(new TestResourceElement
        {
            Resource = resource,
            Label = "typing 2",
            UndoAction = () => { undoCall++; },
        });

        service.SetElementsValidFlag(resource, false, _ => true);

        // An invalid element flushes the whole stack instead of being undone.
        var undoTask = service.Undo(resource);
        undoTask.IsCompletedSuccessfully.ShouldBeTrue();
        undoCall.ShouldBe(0);
        service.HasElements(resource).ShouldBeFalse();

        // Re-validating has no effect on a stack that was flushed.
        service.SetElementsValidFlag(resource, true, _ => true);
        service.HasElements(resource).ShouldBeFalse();
    }

    [Fact]
    public void SourceBasedUndoFindsTheElementPushedWithThatSource()
    {
        var resource1 = File("test1.txt");
        var resource2 = File("test2.txt");
        var dialogService = new TestDialogService();
        var service = CreateUndoRedoService(dialogService);
        var source = new UndoRedoSource();

        var undoCall1 = 0;
        var undoCall2 = 0;
        service.PushElement(
            new TestResourceElement
            {
                Resource = resource1,
                UndoAction = () => { undoCall1++; },
            },
            group: null,
            source: source);
        service.PushElement(new TestResourceElement
        {
            Resource = resource2,
            UndoAction = () => { undoCall2++; },
        });

        service.CanUndo(source).ShouldBeTrue();
        service.CanRedo(source).ShouldBeFalse();

        var undoTask = service.Undo(source);
        undoTask.IsCompletedSuccessfully.ShouldBeTrue();
        undoCall1.ShouldBe(1);
        undoCall2.ShouldBe(0);

        // Undoing through the element's own source does not ask for confirmation.
        dialogService.ConfirmCount.ShouldBe(0);

        service.CanRedo(source).ShouldBeTrue();
        var redoTask = service.Redo(source);
        redoTask.IsCompletedSuccessfully.ShouldBeTrue();
        service.CanUndo(source).ShouldBeTrue();
    }

    [Fact]
    public void FailingElementGoesThroughTheErrorPath()
    {
        var resource = File("test.txt");
        var notificationService = new TestNotificationService();
        var service = CreateUndoRedoService(notificationService: notificationService);

        var error = new InvalidOperationException("boom");
        service.PushElement(new TestResourceElement
        {
            Resource = resource,
            UndoAction = () => throw error,
        });

        var undoTask = service.Undo(resource);

        // The element throws synchronously, so the error is handled synchronously too.
        undoTask.IsCompletedSuccessfully.ShouldBeTrue();
        notificationService.Errors.Count.ShouldBe(1);
        notificationService.Errors[0].ShouldBeSameAs(error);
        service.HasElements(resource).ShouldBeFalse();
    }

    [Fact]
    public void CancelAtTheWorkspacePromptKeepsTheElements()
    {
        var resource1 = File("test1.txt");
        var resource2 = File("test2.txt");
        var dialogService = new TestDialogService { PromptResult = UndoChoice.Cancel };
        var service = CreateUndoRedoService(dialogService);

        var undoCall = 0;
        service.PushElement(new TestWorkspaceElement
        {
            Resources = new[] { resource1, resource2 },
            Label = "typing 1",
            UndoAction = () => { undoCall++; },
            SplitAction = () => Array.Empty<IResourceUndoRedoElement>(),
        });

        var undoTask = service.Undo(resource1);

        undoTask.IsCompletedSuccessfully.ShouldBeTrue();
        dialogService.PromptCount.ShouldBe(1);
        undoCall.ShouldBe(0);
        service.CanUndo(resource1).ShouldBeTrue();
        service.CanUndo(resource2).ShouldBeTrue();
    }

    [Fact]
    public void UndoThisFileSplitsTheWorkspaceElement()
    {
        var resource1 = File("test1.txt");
        var resource2 = File("test2.txt");
        var dialogService = new TestDialogService { PromptResult = UndoChoice.This };
        var service = CreateUndoRedoService(dialogService);

        var undoCall = 0;
        var undoCall1 = 0;
        var undoCall2 = 0;
        var individual1 = new TestResourceElement
        {
            Resource = resource1,
            Label = "typing 1.1",
            UndoAction = () => { undoCall1++; },
        };
        var individual2 = new TestResourceElement
        {
            Resource = resource2,
            Label = "typing 1.2",
            UndoAction = () => { undoCall2++; },
        };
        service.PushElement(new TestWorkspaceElement
        {
            Resources = new[] { resource1, resource2 },
            Label = "typing 1",
            UndoAction = () => { undoCall++; },
            SplitAction = () => new IResourceUndoRedoElement[] { individual1, individual2 },
        });

        var undoTask = service.Undo(resource1);

        undoTask.IsCompletedSuccessfully.ShouldBeTrue();
        dialogService.PromptCount.ShouldBe(1);
        undoCall.ShouldBe(0);
        undoCall1.ShouldBe(1);
        undoCall2.ShouldBe(0);

        // Only the chosen resource was undone; the other one now holds its individual element.
        service.CanUndo(resource1).ShouldBeFalse();
        service.CanRedo(resource1).ShouldBeTrue();
        service.CanUndo(resource2).ShouldBeTrue();
        service.GetLastElement(resource2).ShouldBeSameAs(individual2);
    }

    private sealed class TestResourceElement : IResourceUndoRedoElement
    {
        public Uri Resource { get; init; } = new("file:///test.txt");

        public string Label { get; init; } = "typing";

        public string Code { get; init; } = "typing";

        public bool ConfirmBeforeUndo { get; init; }

        public Action? UndoAction { get; init; }

        public Action? RedoAction { get; init; }

        public UndoRedoElementType Type => UndoRedoElementType.Resource;

        public ValueTask Undo()
        {
            UndoAction?.Invoke();
            return ValueTask.CompletedTask;
        }

        public ValueTask Redo()
        {
            RedoAction?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestWorkspaceElement : ISplittableWorkspaceUndoRedoElement
    {
        public IReadOnlyList<Uri> Resources { get; init; } = Array.Empty<Uri>();

        public string Label { get; init; } = "typing";

        public string Code { get; init; } = "typing";

        public bool ConfirmBeforeUndo { get; init; }

        public Action? UndoAction { get; init; }

        public Action? RedoAction { get; init; }

        public Func<IReadOnlyList<IResourceUndoRedoElement>> SplitAction { get; init; } = () => Array.Empty<IResourceUndoRedoElement>();

        public UndoRedoElementType Type => UndoRedoElementType.Workspace;

        public ValueTask Undo()
        {
            UndoAction?.Invoke();
            return ValueTask.CompletedTask;
        }

        public ValueTask Redo()
        {
            RedoAction?.Invoke();
            return ValueTask.CompletedTask;
        }

        public IReadOnlyList<IResourceUndoRedoElement> Split() => SplitAction();
    }

    private sealed class TestDialogService : IUndoRedoDialogService
    {
        public UndoChoice PromptResult { get; set; } = UndoChoice.All;

        public bool ConfirmResult { get; set; } = true;

        public int PromptCount { get; private set; }

        public int ConfirmCount { get; private set; }

        public ValueTask<UndoChoice> PromptAsync(UndoRedoPromptOptions options)
        {
            PromptCount++;
            return ValueTask.FromResult(PromptResult);
        }

        public ValueTask<bool> ConfirmAsync(UndoRedoConfirmOptions options)
        {
            ConfirmCount++;
            return ValueTask.FromResult(ConfirmResult);
        }
    }

    private sealed class TestNotificationService : IUndoRedoNotificationService
    {
        public List<string> Warnings { get; } = new();

        public List<Exception> Errors { get; } = new();

        public void Warn(string message) => Warnings.Add(message);

        public void Error(Exception error) => Errors.Add(error);
    }
}
