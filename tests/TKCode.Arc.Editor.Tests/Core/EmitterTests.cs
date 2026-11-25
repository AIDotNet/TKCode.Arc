// Tests for the delivery order of Emitter, which ports vscode's src/vs/base/common/event.ts (Emitter.fire,
// commit b57d5c9e, MIT license); the cases follow upstream's event.test.ts 'reusing event function and context',
// 'Emitter - In Order Delivery' and 'onListenerError'.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class EmitterTests
{
    [Fact]
    public void In_Order_Delivery_When_A_Listener_Fires_Again()
    {
        var emitter = new Emitter<string>();
        var listener2Events = new List<string>();
        emitter.Add(e =>
        {
            if (e == "e1")
            {
                emitter.Fire("e2");
                listener2Events.ShouldBe(["e1", "e2"]);
            }
        });
        emitter.Add(listener2Events.Add);

        emitter.Fire("e1");

        // a .NET event would have given ["e2", "e1"]
        listener2Events.ShouldBe(["e1", "e2"]);
    }

    [Fact]
    public void A_Throwing_Listener_Is_Reported_And_The_Others_Still_Run()
    {
        var emitter = new Emitter<int>();
        var reported = new List<Exception>();
        Action<Exception> report = reported.Add;
        UnexpectedErrors.Occurred += report;
        try
        {
            var calls = 0;
            emitter.Add(_ => throw new InvalidOperationException("boom"));
            emitter.Add(_ => calls++);

            emitter.Fire(1);

            calls.ShouldBe(1);
            reported.ShouldContain(e => e.Message == "boom");
        }
        finally
        {
            UnexpectedErrors.Occurred -= report;
        }
    }

    [Fact]
    public void A_Listener_Removed_During_Delivery_Does_Not_Get_The_Event_And_An_Added_One_Waits()
    {
        var emitter = new Emitter<int>();
        var calls = new List<string>();
        Action<int> second = _ => calls.Add("second");
        Action<int> added = _ => calls.Add("added");
        emitter.Add(_ =>
        {
            calls.Add("first");
            emitter.Remove(second);
            emitter.Add(added);
        });
        emitter.Add(second);

        emitter.Fire(1);
        calls.ShouldBe(["first"]);

        calls.Clear();
        emitter.Fire(2);
        calls.ShouldBe(["first", "added"]);
    }

    [Fact]
    public void Remove_Takes_The_Last_Listener_Added_With_The_Key()
    {
        var emitter = new Emitter<int>();
        var count = 0;
        Action<int> listener = _ => count++;
        emitter.Add(listener);
        emitter.Add(listener);

        emitter.Remove(listener);
        emitter.Fire(1);
        count.ShouldBe(1);

        emitter.Remove(listener);
        emitter.HasListeners.ShouldBeFalse();
        emitter.Fire(2);
        count.ShouldBe(1);
    }
}
