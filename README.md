# Async Iterators
This repository contains a prototype on asynchronous iterators which are proposed for C# 8.0 as part of the "async streams" feature. For more information, please refer to [
Async Streams](https://github.com/dotnet/csharplang/blob/master/proposals/async-streams.md).

## Interfaces
Two variants of the `IAsyncEnumerable<T>` and `IAsyncEnumerator<T>` interfaces are proposed, none of which support fine-grained cancellation using a `CancellationToken`, instead deferring the wiring of cancellation support to the consumer, with producers optionally supporting a means to provide cancellation (e.g. using a cancellation token passed upon creating an instance of the source).
```csharp
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator GetAsyncEnumerator();
    }
    
    public interface IAsyncEnumerator<out T> : IAsyncDisposable
    {
#if OPTION1
        Task<bool> MoveNextAsync();
        T Current { get; }
#else
        Task<bool> WaitForNextAsync();
        T TryGetNext(out bool success);
#endif
    }
}
```
The first option requires two method calls to retrieve an element, one of which returns a `Task<bool>` which is subject to at least three method calls to await the outcome (`GetAwaiter`, `IsCompleted`, `GetResult`, and possibly `OnCompleted`). The second option has the possibility to reduce retrieving an element to a single call to `TryGetNext`, allowing for a tight inner consumption loop in case elements are available in a synchronous manner.

## Consuming using `foreach await`
C# 8.0 is proposing language syntax to consume async streams using the `foreach await` statement construct which is pattern-based, akin to its synchronous `foreach` statement counterpart.
```csharp
async Task ConsumeAsync(IAsyncEnumerable<int> xs)
{
    foreach await (var x in xs)
    {
        Console.WriteLine(x);
    }
}
```
Depending on the choice of the interfaces (and thus the pattern the compiler will bind to), lowering of this language construct proceeds as follows:
```csharp
async Task ConsumeAsync(IAsyncEnumerable<int> xs)
{

    IAsyncEnumerator<int> e = xs.GetAsyncEnumerator();
    try
    {
#if OPTION1
        while (await e.MoveNextAsync())
        {
            var x = e.Current;
            Console.WriteLine(x);
        }
#else
        while (await e.WaitForNextAsync())
        {
            while (true)
            {
                var x = e.TryGetNext(out var success)
                
                if (!success)
                {
                    break;
                }
                
                var x = e.Current;
                Console.WriteLine(x);
            }
        }
#endif
    }
    finally
    {
        await e.DisposeAsync();
    }
}
```
The first option is straightforward. The second option requires an inner loop that enables fetching results in a synchronous manner when available. Note that an alternative option would be to change the outer loop from a `while` statement to a `do while` statement, if `TryGetNext` is defined to allow triggering an asynchronous operation, returning `false` in case the operation does not complete synchronously. This would enable a particular implementation strategy for async iterators.

## Asynchonous iterators
C# 8.0 is proposing support for asynchronous iterators, which combine `async` methods and iterators with `yield` statements. An example is shown below:
```csharp
public async IAsyncEnumerable<int> GetNumbersAsync()
{
    for (var x = 0; true; x++)
    {
        await Task.Delay(x);
        yield return x;
    }
}
```
This repository ponders the implementation of asynchronous iterators for various alternative interface choices for async streams.
