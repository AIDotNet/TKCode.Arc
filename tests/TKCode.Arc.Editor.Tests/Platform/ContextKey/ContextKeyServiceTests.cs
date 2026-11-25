// Ported from vscode (src/vs/platform/contextkey/test/browser/contextkey.test.ts, commit b57d5c9e), MIT license.
// 16 of the 17 upstream tests. Differences: scoped services are created without a DOM element; 'updateParent reports
// exactly the keys whose value changed' reads `config.editor.fontSize` from a root without a configuration context (the
// key is expected to be unaffected either way); 'does not collect the values of the ancestors that the old and new
// parent share' spies with a Context subclass instead of sinon, at the Context level the service delegates to.
// Left out: 'issue #147732: URIs as context values' needs the `_setContext` command and URI marshalling, which are not
// part of this port. The tests after the upstream ones cover overlays, disposal and RawContextKey binding.

using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class ContextKeyServiceTests
{
    [Fact]
    public void UpdateParent()
    {
        using var root = new ContextKeyService();
        using var parent1 = root.CreateScoped();
        using var parent2 = root.CreateScoped();

        using var child = parent1.CreateScoped();
        parent1.CreateKey("testA", 1);
        parent1.CreateKey("testB", 2);
        parent1.CreateKey("testD", 0);

        parent2.CreateKey("testA", 3);
        parent2.CreateKey("testC", 4);
        parent2.CreateKey("testD", 0);

        var fired = 0;
        child.DidChangeContext += e =>
        {
            fired++;
            e.AffectsSome(ReadableSet.Of("testA")).ShouldBeTrue("testA changed");
            e.AffectsSome(ReadableSet.Of("testB")).ShouldBeTrue("testB changed");
            e.AffectsSome(ReadableSet.Of("testC")).ShouldBeTrue("testC changed");
            e.AffectsSome(ReadableSet.Of("testD")).ShouldBeFalse("testD did not change");

            child.GetContextKeyValue("testA").ShouldBe(3);
            child.GetContextKeyValue("testB").ShouldBeNull();
            child.GetContextKeyValue("testC").ShouldBe(4);
            child.GetContextKeyValue("testD").ShouldBe(0);
        };

        var errors = CaptureErrors(() => child.UpdateParent(parent2));
        errors.ShouldBeEmpty();
        fired.ShouldBe(1);
    }

    [Fact]
    public void UpdateParent_to_same_service()
    {
        using var root = new ContextKeyService();
        using var parent1 = root.CreateScoped();

        using var child = parent1.CreateScoped();
        parent1.CreateKey("testA", 1);
        parent1.CreateKey("testB", 2);
        parent1.CreateKey("testD", 0);

        var eventFired = false;
        child.DidChangeContext += _ => eventFired = true;

        child.UpdateParent(parent1);

        eventFired.ShouldBeFalse();
    }

    [Fact]
    public void UpdateParent_reports_exactly_the_keys_whose_value_changed()
    {
        using var root = new ContextKeyService();
        root.GetContextKeyValue("config.editor.fontSize");
        root.CreateKey("rootOnly", 1);
        root.CreateKey("shadowed", "root");

        using var a = root.CreateScoped();
        a.CreateKey("shared", "a");
        a.CreateKey("shadowed", "a");
        a.CreateKey("onlyOld", 1);
        a.CreateKey("same", new Dictionary<string, object?> { ["value"] = 5 });

        using var b = a.CreateScoped();
        b.CreateKey("inB", true);
        b.CreateKey("ownedByChild", "b");

        using var c = root.CreateScoped();
        c.CreateKey("shared", "c");
        c.CreateKey("same", new Dictionary<string, object?> { ["value"] = 5 });
        c.CreateKey("onlyNew", "c");

        using var child = b.CreateScoped();
        child.CreateKey("ownedByChild", "child");

        var allKeys = new[] { "config.editor.fontSize", "rootOnly", "shadowed", "shared", "onlyOld", "same", "inB", "ownedByChild", "onlyNew" };
        var affectedKeys = new List<string[]>();
        child.DidChangeContext += e => affectedKeys.Add(allKeys.Where(key => e.AffectsSome(ReadableSet.Of(key))).ToArray());

        child.UpdateParent(c);
        child.UpdateParent(b);

        affectedKeys.ShouldBe(new[]
        {
            new[] { "shadowed", "shared", "onlyOld", "inB", "onlyNew" },
            new[] { "shadowed", "shared", "onlyOld", "inB", "onlyNew" },
        });
    }

    private sealed class SpyContext(int id, Context? parent) : Context(id, parent)
    {
        public int CollectAllValuesCallCount { get; private set; }

        public override Dictionary<string, object?> CollectAllValues()
        {
            CollectAllValuesCallCount++;
            return base.CollectAllValues();
        }
    }

    [Fact]
    public void UpdateParent_does_not_collect_the_values_of_the_ancestors_that_the_old_and_new_parent_share()
    {
        // The service delegates to Context.UpdateParent with the same contexts: root <- parent1/parent2 <- child.
        var root = new SpyContext(0, null);
        var parent1 = new Context(1, root);
        var parent2 = new Context(2, root);
        var child = new Context(3, parent1);
        parent1.SetValue("testA", 1);
        parent2.SetValue("testA", 2);

        var changed = child.UpdateParent(parent2);

        root.CollectAllValuesCallCount.ShouldBe(0);
        changed.ShouldBe(new[] { "testA" });
        child.GetValue("testA").ShouldBe(2);
    }

    [Fact]
    public void Suppress_update_event_from_parent_when_one_key_is_overridden_by_child()
    {
        using var root = new ContextKeyService();
        using var child = root.CreateScoped();

        root.CreateKey("testA", 1);
        child.CreateKey("testA", 4);

        var fired = false;
        Action<IContextKeyChangeEvent> listener = _ => fired = true;
        child.DidChangeContext += listener;
        root.SetContext("testA", 10);
        fired.ShouldBeFalse("Should not fire event when overridden key is updated in parent");
        child.DidChangeContext -= listener;
    }

    [Fact]
    public void Suppress_update_event_from_parent_when_all_keys_are_overridden_by_child()
    {
        using var root = new ContextKeyService();
        using var child = root.CreateScoped();

        root.CreateKey("testA", 1);
        root.CreateKey("testB", 2);
        root.CreateKey("testC", 3);

        child.CreateKey("testA", 4);
        child.CreateKey("testB", 5);
        child.CreateKey("testD", 6);

        var fired = false;
        Action<IContextKeyChangeEvent> listener = _ => fired = true;
        child.DidChangeContext += listener;
        root.BufferChangeEvents(() =>
        {
            root.SetContext("testA", 10);
            root.SetContext("testB", 20);
            root.SetContext("testD", 30);
        });

        fired.ShouldBeFalse("Should not fire event when overridden key is updated in parent");
        child.DidChangeContext -= listener;
    }

    [Fact]
    public void Pass_through_update_event_from_parent_when_one_key_is_not_overridden_by_child()
    {
        using var root = new ContextKeyService();
        using var child = root.CreateScoped();

        root.CreateKey("testA", 1);
        root.CreateKey("testB", 2);
        root.CreateKey("testC", 3);

        child.CreateKey("testA", 4);
        child.CreateKey("testB", 5);
        child.CreateKey("testD", 6);

        var fired = 0;
        child.DidChangeContext += e =>
        {
            fired++;
            e.AffectsSome(ReadableSet.Of("testA")).ShouldBeTrue("testA changed");
            e.AffectsSome(ReadableSet.Of("testB")).ShouldBeTrue("testB changed");
            e.AffectsSome(ReadableSet.Of("testC")).ShouldBeTrue("testC changed");
        };

        var errors = CaptureErrors(() => root.BufferChangeEvents(() =>
        {
            root.SetContext("testA", 10);
            root.SetContext("testB", 20);
            root.SetContext("testC", 30);
        }));

        errors.ShouldBeEmpty();
        fired.ShouldBe(1);
    }

    private static bool FiresOnSet<T>(T initial, T next)
    {
        using var root = new ContextKeyService();
        var key = root.CreateKey("test", initial);

        var eventFired = false;
        root.DidChangeContext += _ => eventFired = true;

        key.Set(next);
        return eventFired;
    }

    [Fact]
    public void Setting_identical_array_values_should_not_fire_change_event()
    {
        // Set the same array content (different reference)
        FiresOnSet(new[] { "a", "b", "c" }, new[] { "a", "b", "c" }).ShouldBeFalse("Should not fire event when setting identical array");
    }

    [Fact]
    public void Setting_different_array_values_should_fire_change_event()
    {
        FiresOnSet(new[] { "a", "b", "c" }, new[] { "a", "b", "d" }).ShouldBeTrue("Should fire event when setting different array");
    }

    [Fact]
    public void Setting_identical_complex_object_should_not_fire_change_event()
    {
        FiresOnSet(
            new Dictionary<string, object?> { ["foo"] = "bar", ["count"] = 42 },
            new Dictionary<string, object?> { ["foo"] = "bar", ["count"] = 42 }).ShouldBeFalse("Should not fire event when setting identical object");
    }

    [Fact]
    public void Setting_different_complex_object_should_fire_change_event()
    {
        FiresOnSet(
            new Dictionary<string, object?> { ["foo"] = "bar", ["count"] = 42 },
            new Dictionary<string, object?> { ["foo"] = "bar", ["count"] = 43 }).ShouldBeTrue("Should fire event when setting different object");
    }

    [Fact]
    public void Setting_empty_arrays_should_not_fire_change_event_when_identical()
    {
        FiresOnSet(Array.Empty<string>(), Array.Empty<string>()).ShouldBeFalse("Should not fire event when setting identical empty array");
    }

    [Fact]
    public void Setting_nested_arrays_should_handle_deep_equality()
    {
        FiresOnSet(new[] { "a:b", "c:d" }, new[] { "a:b", "c:d" }).ShouldBeFalse("Should not fire event when setting identical array with complex values");
    }

    [Fact]
    public void Setting_same_primitive_values_should_not_fire_change_event()
    {
        FiresOnSet("hello", "hello").ShouldBeFalse("Should not fire event when setting identical string");
    }

    [Fact]
    public void Setting_different_primitive_values_should_fire_change_event()
    {
        FiresOnSet(42, 43).ShouldBeTrue("Should fire event when setting different number");
    }

    [Fact]
    public void DisposeContext_forwards_through_disposed_scoped_service()
    {
        using var root = new ContextKeyService();
        var scoped = root.CreateScoped();
        var child = scoped.CreateScoped();
        var childContextId = ((AbstractContextKeyService)child).ContextId;

        // Set a value on the child so we can observe it
        child.CreateKey("testKey", "value");
        child.GetContextKeyValue("testKey").ShouldBe("value");

        // Dispose the intermediate scoped service first
        scoped.Dispose();

        // Now dispose the child — this should still forward to root
        // and clean up the child's context entry.
        child.Dispose();

        // The child's context should no longer be accessible through root
        root.GetContextKeyValue("testKey").ShouldBeNull("Child context should be cleaned up even when parent scoped service was disposed first");
        root.GetContextValuesContainer(childContextId).ShouldBeSameAs(NullContext.INSTANCE);
    }

    // ---- not upstream ----

    [Fact]
    public void Overlay_overrides_keys_and_evaluates_rules()
    {
        using var root = new ContextKeyService();
        root.CreateKey("a", 1);
        root.CreateKey("b", "x");

        var overlay = root.CreateOverlay(new[] { new KeyValuePair<string, object?>("b", "y") });
        overlay.GetContextKeyValue("a").ShouldBe(1);
        overlay.GetContextKeyValue("b").ShouldBe("y");
        overlay.ContextMatchesRules(ContextKeyExpr.Deserialize("a == 1 && b == y")).ShouldBeTrue();
        overlay.GetContext().GetValue("b").ShouldBe("y");
        root.ContextMatchesRules(ContextKeyExpr.Deserialize("b == y")).ShouldBeFalse();

        var nested = overlay.CreateOverlay(new[] { new KeyValuePair<string, object?>("a", 2) });
        nested.ContextMatchesRules(ContextKeyExpr.Deserialize("a == 2 && b == y")).ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => overlay.CreateKey("c", 1));
        Should.Throw<NotSupportedException>(() => overlay.CreateScoped());
    }

    [Fact]
    public void Scoped_service_inherits_and_shadows_parent_values()
    {
        using var root = new ContextKeyService();
        using var scoped = root.CreateScoped();
        var rootKey = EditorContextKeys.ReadOnly.BindTo(root);
        var scopedKey = EditorContextKeys.HasMultipleSelections.BindTo(scoped);

        scoped.ContextMatchesRules(EditorContextKeys.Writable).ShouldBeTrue();
        rootKey.Set(true);
        scoped.ContextMatchesRules(EditorContextKeys.Writable).ShouldBeFalse();
        scopedKey.Set(true);
        scoped.ContextMatchesRules(EditorContextKeys.HasMultipleSelections).ShouldBeTrue();
        root.ContextMatchesRules(EditorContextKeys.HasMultipleSelections).ShouldBeFalse();
        scoped.GetContext().GetValue("editorReadonly").ShouldBe(true);

        // a key whose default is undefined is not set until it is given a value
        var composite = EditorContextKeys.InCompositeEditor.BindTo(scoped);
        composite.Get().ShouldBeNull();
        scoped.GetContextKeyValue("inCompositeEditor").ShouldBeNull();
        composite.Set(false);
        composite.Get().ShouldBe(false);
    }

    [Fact]
    public void Disposed_service_rejects_new_keys_and_reads_nothing()
    {
        var root = new ContextKeyService();
        root.CreateKey("a", 1);
        root.Dispose();

        root.GetContextKeyValue("a").ShouldBeNull();
        root.GetContext().ShouldBeSameAs(NullContext.INSTANCE);
        Should.Throw<ObjectDisposedException>(() => root.CreateKey("b", 1));
        Should.Throw<ObjectDisposedException>(() => root.CreateScoped());
        Should.Throw<InvalidOperationException>(() => root.UpdateParent(root));
    }

    private static List<Exception> CaptureErrors(Action action)
    {
        var errors = new List<Exception>();
        Action<Exception> handler = errors.Add;
        UnexpectedErrors.Occurred += handler;
        try
        {
            action();
        }
        finally
        {
            UnexpectedErrors.Occurred -= handler;
        }
        return errors;
    }
}
