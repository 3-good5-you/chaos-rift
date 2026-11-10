using System.Runtime.CompilerServices;

namespace PoC.Engine;

// Synchronous coroutine type for duel logic. `async Flow` methods run inline on the
// caller's thread; the only suspension point is a player prompt (Duel.Ask). Submit()
// stores the answer and resumes the stored continuation, so the whole duel is a
// deterministic function of (setup, commands). No threads, no SynchronizationContext.
[AsyncMethodBuilder(typeof(FlowBuilder))]
public class Flow
{
    internal Action? Continuation;
    internal Exception? Error;
    public bool IsCompleted { get; internal set; }

    public static Flow Done { get; } = new() { IsCompleted = true };

    internal void Complete(Exception? e = null)
    {
        Error = e;
        IsCompleted = true;
        var k = Continuation;
        Continuation = null;
        k?.Invoke();
    }

    public FlowAwaiter GetAwaiter() => new(this);
}

public readonly struct FlowAwaiter : ICriticalNotifyCompletion
{
    readonly Flow f;
    public FlowAwaiter(Flow f) => this.f = f;
    public bool IsCompleted => f.IsCompleted;
    public void GetResult()
    {
        if (f.Error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(f.Error);
    }
    public void OnCompleted(Action k) => f.Continuation = k;
    public void UnsafeOnCompleted(Action k) => f.Continuation = k;
}

public struct FlowBuilder
{
    Flow? task;
    public static FlowBuilder Create() => default;
    public Flow Task => task ??= new Flow();
    public void Start<TSM>(ref TSM sm) where TSM : IAsyncStateMachine => sm.MoveNext();
    public void SetStateMachine(IAsyncStateMachine sm) { }
    public void SetResult() => Task.Complete();
    public void SetException(Exception e) => Task.Complete(e);
    public void AwaitOnCompleted<TA, TSM>(ref TA a, ref TSM sm) where TA : INotifyCompletion where TSM : IAsyncStateMachine
    {
        _ = Task; // materialise before boxing so SetResult reaches the awaited instance
        a.OnCompleted(sm.MoveNext);
    }
    public void AwaitUnsafeOnCompleted<TA, TSM>(ref TA a, ref TSM sm) where TA : ICriticalNotifyCompletion where TSM : IAsyncStateMachine
    {
        _ = Task;
        a.UnsafeOnCompleted(sm.MoveNext);
    }
}

[AsyncMethodBuilder(typeof(FlowBuilder<>))]
public sealed class Flow<T> : Flow
{
    internal T Result = default!;
    public new FlowAwaiter<T> GetAwaiter() => new(this);
}

public readonly struct FlowAwaiter<T> : ICriticalNotifyCompletion
{
    readonly Flow<T> f;
    public FlowAwaiter(Flow<T> f) => this.f = f;
    public bool IsCompleted => f.IsCompleted;
    public T GetResult()
    {
        if (f.Error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(f.Error);
        return f.Result;
    }
    public void OnCompleted(Action k) => f.Continuation = k;
    public void UnsafeOnCompleted(Action k) => f.Continuation = k;
}

public struct FlowBuilder<T>
{
    Flow<T>? task;
    public static FlowBuilder<T> Create() => default;
    public Flow<T> Task => task ??= new Flow<T>();
    public void Start<TSM>(ref TSM sm) where TSM : IAsyncStateMachine => sm.MoveNext();
    public void SetStateMachine(IAsyncStateMachine sm) { }
    public void SetResult(T r) { Task.Result = r; Task.Complete(); }
    public void SetException(Exception e) => Task.Complete(e);
    public void AwaitOnCompleted<TA, TSM>(ref TA a, ref TSM sm) where TA : INotifyCompletion where TSM : IAsyncStateMachine
    {
        _ = Task;
        a.OnCompleted(sm.MoveNext);
    }
    public void AwaitUnsafeOnCompleted<TA, TSM>(ref TA a, ref TSM sm) where TA : ICriticalNotifyCompletion where TSM : IAsyncStateMachine
    {
        _ = Task;
        a.UnsafeOnCompleted(sm.MoveNext);
    }
}
