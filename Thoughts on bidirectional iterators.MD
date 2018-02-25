# Bi-directional (async) iterators in C#

JavaScript enables iterators (producers) to receive inputs from their consumers by passing an argument to the `next` call, which results in a bi-directional flow of data and control (e.g. triggering a `yield break` in C# lingo, or injecting a `throw`). These are also known as generators.

C# iterators are uni-directional for data from producers to consumers (`yield return`) and rigidly bi-directional for control flow. A producer can throw (or finish), thus injecting control flow at the consumer site. A consumer can influence control flow in the producer, obviously to produce the next element, but also to trigger a `yield break` from the producer's current location (e.g. by calling `Dispose`).

Could we enable bi-directional data flow and control flow in C# iterators?

## Interfaces

First, let's sketch what possible interfaces would look like. In all that follows, we'll assume that language syntax for consuming (`foreach`) and producing (iterator methods) is not tied to interface types, but rather to the members on types classified as iteratable-like or iterator-like, so any type of these "shapes" could be made to work with such language features.

### Synchronous

Interfaces for bi-directional synchronous iterators could look as follows:

```csharp
interface IBiterable<in TInput, out TOutput>
{
    IBiterator<TInput, TOutput> GetBiterator();
}

interface IBiterator<in TInput, out TOutput> : IDisposable
{
#if OPTION1
    bool MoveNext(TInput input);
    TOutput Current { get; }
#else
    TOutput TryGetNext(TInput input, out bool success);
#endif
}
```

Note that uni-directional iterators can be implemented using bi-directional iterators by using a `Boolean` input type that encodes a `shouldContinue` flag sent from the consumer to the producer. This allows the iterator to be cleaned up by injecting a `yield break` branch at the current position in the iterator, in a way similar to how `Dispose` works on iterators today. Note that passing a `Boolean` for this purpose is akin to passing a `CancellationToken`.

Besides passing data to the producer, it is trivially possible to influence control flow in the producer by using a suitable type for `TInput`. For example, one could use a `Func<T>` which can send either values to the producer, or cause the producer to throw an exception upon evaluating the function. Another option is to pass a `ValueOrError<T>` discriminated union type.

### Asynchronous

Interfaces for asynchronous bi-directional iterators could look as follows:

```csharp
interface IAsyncBiterable<in TInput, out TOutput>
{
    IAsyncBiterator<TInput, TOutput> GetAsyncBiterator();
}

interface IAsyncBiterator<in TInput, out TOutput> : IAsyncDisposable
{
#if OPTION1
    Task<bool> MoveNextAsync(TInput input);
    TOutput Current { get; }
#else
    Task<bool> WaitForNextAsync();
    TOutput TryGetNext(TInput input, out bool success);
#endif
}
```

Note that asynchronous iterators readily would get exception or cancellation injection by merely using `Task<T>` for the `TInput` type, and by having producers `await` the received input, which enables injecting an exception by passing a task in the `Faulted` state. Cancellation can be injected as well, by passing a task in the `Canceled` state. Or, if cancellation is the only irregular control flow to be injected in the producer, one could use `CancellationToken` for the the `TInput` parameter type.

## Consuming using `foreach`

How would one consume a bi-directional iterable or iterator type? Let's focus on iterables, which immediately shows how iterators can be consumed. Let's also assert that the expressive power of `OPTION1` suffices for the discussion; the way a consumer works is not fundamentally impacted if the alternative interface choice is made.

First, let's look at a uni-directional `foreach` loop, containing `break` and `continue` statements to illustrate all possible branches:

```csharp
foreach [await] (TOutput output in source)
{
    if (b)
        break;
    if (c)
        continue;

    Use(output);
}
```

This translates more or less to:

```csharp
using [await] (var e in source.Get[Async]Enumerator())
{
    while ([await] e.MoveNext[Async]())
    {
        if (b)
            break;
        if (c)
            continue;

        TOutput output = e.Current;

        Use(output);
    }
}
```

For enumeration over a bi-directional iterable, we require some way to send a value (assignable to `TInput`) to the producer. This is required for all `continue` statements, but also upon starting the loop, which has an implicit `continue` statement. This smells like definite assignment, because we need to have a `TInput` value available each time we wish to push the consumer forward. Given that the consumer receives values of type `TOutput`, it feels like there's an implicit `in` for values received, as if we can write:

```csharp
foreach (in TOutput output in source)
{
    ...
}
```

In fact, C# does not allow assignment to an iteration variable, which is treated as a readonly variable, which is what the new C# 7.2 feature of `in` represents as well.

For a bi-directional iterable, data is flowing `out` of the consumer into the producer, so it seems logical to use `out` for this:

```csharp
foreach ((in TOutput output, out TInput input) in source)
{
    ...
}
```

Now, we get definite assignment requirements at the consumer side, requiring `input` to be assigned before control flow leaves some scope. Regularly, that scope is the entire method body for a method with an `out` parameter. In this case, this scope is the body of the `foreach` loop. However, that still brings with it a further complication as to how to provide an initial value. On way to circumvent this would be to require the output parameter to be declared outside the loop:

```csharp
TInput input = default;

foreach ((in TOutput output, out input) in source)
{
    ...
}
```

That would work just fine and it would nicely work with parameters passed to the containing method, for example to flow a `CancellationToken` down into the iterator. We have to assign it before entering the loop, we could set it to something else while advancing through the loop, but the callee (the producing iterator) cannot assign to it, because the direction flips from their perspective (what's `out` for us, is `in` for them).

One thing is unsettling about this, namely that iteration variables cannot be declared outside the `foreach` loop, i.e. one can't write the following today:

```csharp
TOutput output;
foreach (TOutput output in source)
{
    ...
}
```

This leaves an asymmetry in `foreach` for its `in` and `out` parameters. One option would be to lift this restriction, but note that `output` will not be definitely assigned after exiting from the `foreach` loop, thus definite assignment may have to become flow-based, which is already the case for the analysis for C# 8.0 nullable reference types.

Alternatively, we can allow the declaration site of the `out` parameter to have an initializer, to restore the symmetry the other way. All variables have to be declared in the `foreach` loop, but assignments can be made:

```csharp
foreach ((in TOutput output, out TInput input = default) in source)
{
    ...
}
```

Note this would still allow to copy in some value from another value, such as the `CancellationToken` passed to the containing method. One merely ends up with another variable, which could even be desirable to contain the effects of assignment to this variable. If this route is viable, we can entertain the thought of having default values for `out` parameters, which could be handy for bail-out style code:

```csharp
bool TryGetValue(string s, out int x = 0)
{
    if (...)
    {
        x = 42;
        return true;
    }

    // don't bother me with definite assignment rules here
    return false;
}
```

Dreaming even further, the whole method could have a default return value, because the return type is akin to an `out` parameter anyway:

```csharp
bool TryGetValue(string s, out int x = 0) = false
{
    if (...)
    {
        x = 42;
        return true;
    }
}
```

If one squints, this is not too dissimilar from read-only property initializer syntax, but the default value for a return type could be constrained to the same restrictions imposed on default parameter values (i.e. constant).

With all of this, we get the following as a bi-directional loop construct, at least conceptually:

```csharp
foreach ((in TOutput output, out TInput input = default) in source)
{
    ...
}
```

The scope of `output` and `input` are both constrained to the loop. The use of a `continue` statement is obviously allowed and would not require re-assignment of the `input` variable. After all, when entering the loop, `input` will have been definitely assigned. However, the consumer could re-assign the value during the iteration, which seems totally fine as well, and enables bi-directional communication simply by assigning to the input, either before implicit iteration (falling from the bottom of the loop is an implicit `continue`) or at every `continue` site:

```csharp
foreach ((in StockTick tick, out Priority priority = Priority.Normal) in stockTicks)
{
    if (stock.Change < -0.05)
    {
        // The market is crashing, we dropped more than 5 percent. Act quickly.
        ...
        priority = Priority.High;
    }
    else if (stock.Change > 0.05)
    {
        // The market is booming, relax and cash in.
        ...
        priority = Priority.Low;
    }
    else
    {
        // Just another day on the market, stay alert.
        ...
        priority = Priority.Medium;
    }
}
```

The example shown here shows a use case that's not uncommon with cloud resources, where one wants to throttle request rates or adjust priorities (often correlated to the cloud consumption bill at the end of the month). For example, services like CosmosDB have request units (RUs), and for each continuation of a request to enumerate the results of a query, the amount of RUs spent is made available to the consumer. With a pattern like the one shown above, the consumer could not only receive these RUs but possibly influence the producer based on calculations over RUs.

This shows a dial which is different from a Boolean flag to keep going or to cancel, but one can see that just using a `shouldContinue` Boolean value would readily turn `break` into `continue` after setting `shouldContinue` to `false`:

```csharp
foreach ((in int x, out bool shouldContinue = true) in xs)
{
    // I could do this...
    if (x < 0)
    {
        break;
    }

    // ...or this...
    if (x < 0)
    {
        shouldContinue = false;
        continue;
    }

    // ...or even this...
    shouldContinue = x >= 0;

    ...
}
```

Note that the last form of breaking is almost akin to pushing a loop condition down to the producer.

One possible (syntactic) ambiguity here is with tuple syntax and loop variables allowing for decomposition syntax. A thought could be to detect `in` and `out` modifiers to disambiguate. Also, if the iterable produces tuples, decomposition would still work, but just require more parentheses:

```csharp
// Unidirectional
foreach ((int x, int y) in points)
{
    ...
}

// Bidirectional
foreach await ((in (int x, int y), out (int w, int h) resolution = (1024, 768)) in renderer)
{
    ...
}
```

The second example shows an image rendering iterable that allows for dynamic re-adjustment of the resolution being rendered, using a tuple that's passed in. If resolution changes during iteration, the producer can adjust its logic by returning more or less points than before. The consumer could decide to dial up a notch if the enumeration of pixels is going fast, effectively getting to progressive rendering. I've thrown in `await` in the example above, just to show the pattern should extend to asynchronous iterables as well, which makes sense for such an example where rendering a scene could be computationally intensive (or involve I/O to specialized hardware or a cloud service). I'll leave it to the reader to trivially extend the example to accept a cancellation token as well.

What could the lowered form of such a `foreach` loop look like?

```csharp
foreach [await] ((in TOutput x, out TInput y = z) in xs)
{
    if (b)
        break;
    if (c)
        continue;

    Use(x);
}
```

Can be lowered into the following form:

```csharp
{
    TInput y = z;

    using [await] (var e in xs.Get[Async]Biterator())
    {
        while ([await] e.MoveNext[Async](y))
        {
            TOutput x = e.Current;

            if (b)
                break;
            if (c)
                continue;

            Use(x);
        }
    }
}
```

Note that `out` parameters can't be captured in anonymous methods or lambda expressions, so the issue around the scope where the iteration variable is introduced (previuously requiring a breaking change to C#) does not apply here. The `out` parameter used by the iteration can be declared outside the loop.

In all of the above, an alternative to `out` could be `ref` but it feels strange the producer could then assign to the value as well, in addition to the already existing bi-directional flow enabled by the `in` and `out` pair.

## Producing using iterator methods (`yield`)

On the production side, iterator methods can be used, but these now require a means to accept input that's passed in from the consumer. This could be done by making `yield return` an expression rather than a statement.

```csharp
[async] I[Async]Biterable<TInput, TOutput> Iterator<TInput, TOutput>(...)
{
    ...
    TInput y = yield return x;
    ...
}
```

However, this poses the same challenge as for the consumer side with `foreach`. How does it get the value required for the initial iteration? In the sample above, the body of the iterator does not have any means to get a value for `y` without calling `yield return`. This follows the JavaScript design which seems unsettling.

Another approach could be to make `value` a contextual keyword (i.e. similar to setters for properties and indexers) and have the value come in that way, while retaining `yield return` as a statement. By reading `value` at the start and after every `yield return`, the producer can receive the values received from the consumer.

```csharp
[async] I[Async]Biterable<TInput, TOutput> Iterator<TInput, TOutput>(...)
{
    TInput y0 = value;
    ...
    yield return x;
    TInput y1 = value;
    ...
}
```

Another keyword could be used of course. A concrete example would be to flow cancellation into an async iterator by using a bi-directional asynchronous iterator:

```csharp
async IAsyncBiterable<CancellationToken, TOutput> Iterator<TOutput>(...)
{
    if (value.IsCancellationRequested)
    {
        yield break;
    }

    ...
    
    yield return x;
    value.ThrowIfCancellationRequested();
    
    ...
}
```

Obviously, the return type could have a generic arity of 1 and be a "cancellable async iterable" type, which would be a framework notion rather than a language construct, thus retaining the language's inawareness of cancellation constructs:

```csharp
interface IAsyncEnumerable[WithCancellation]<out T>
{
    IAsyncEnumerator<T> GetAsyncEnumerator();
}

interface IAsyncEnumerator[WithCancellation]<out T> : IAsyncDisposable
{
    Task<bool> MoveNextAsync([CancellationToken token = default]);
    T Current { get; }
}
```

Note that an optional cancellation token could be provided on a single such interface. When writing an iterator, that'd simply result in the producer observing a `default` value for a cancellation token through the `value` variable. On the consumer side, one could relate optional input parameters (on `MoveNext[Async]`) to an optional output parameter (the `out` parameter in the variable declaration list for `foreach [await]`), so the consumer doesn't have to write it either.

For example, let's assume an async enumerator with fine-grained cancellation, doing away with a specialized interface to support this:

```csharp
interface IAsyncEnumerator<out T> : IAsyncDisposable
{
    Task<bool> MoveNextAsync(CancellationToken token = default);
    T Current { get; }
}
```

An iterator could be written as follows:

```csharp
async IAsyncEnumerable<int> GetNumbersAsync()
{
    value.ThrowIfCancellationRequested();

    await Task.Delay(1000);
    yield return 42;
}
```

The type of `value` is derived from the single (optional) parameter type on `MoveNextAsync`. An iterator-like type would have at most one parameter for its `MoveNext[Async]` method. If it has no parameter, it's a one-directional iterator, and `value` has no special meaning. If it has a single parameter, `value` represents this parameter.

On the consuming side, one can write the following:

```csharp
foreach await ((in int x, out CancellationToken token = ...) in GetNumbersAsync())
{
    ...
}
```

but one can also omit the `out` parameter altogether, because the `MoveNextAsync` method has declared the parameter to be optional:

```csharp
foreach await (int x in GetNumbersAsync())
{
    ...
}
```

The iterator will always see `default(CancellationToken)` on its `value` variable.

All of this enables to dial in to the degree of cancellation involvement one likes. If you don't want to see any of it, and simply perform cancellation out of band (e.g. inside the loop body or by sending it to sources upon initiating the iteration), you can simply forget about it. But if one wants cancellation (or some other concern) to be deeply wired between consumer and producer, one can opt in to it by using an `out` parameter on the consumer side.

An alternative to using `value` (or some other keyword, contextual or otherwise) would be to introduce an `iterator` method that offers a declaration site for these input parameters, that may not be passed to the parent iterable (if any) because they are scoped to a single consumer:

```csharp
IAsyncEnumerable<int> GetNumbersAsync(int min, int max)
{
    if (min > max)
    {
        throw new ArgumentOutOfRangeException(nameof(min));
    }

    return Iterator();

    async iterator IAsyncEnumerable<int> Iterator(in CancellationToken token)
    {
        value.ThrowIfCancellationRequested();

        await Task.Delay(1000);
        yield return 42;
    }
}
```

Note that the use of local functions is already common for iterators, in order to perform eager argument validation rather than seeing these effects deferred until iteration time. With the code above, it feels very natural that the types that occur in the `foreach` variable declaration list are `CancellationToken` and `int`, but with the `in` and `out` positions flipped:

```csharp
var xs = GetNumbersAsync(0, 10);

foreach await ((in int x, out CancellationToken token = ...) in xs)
{
    ...
}
```

That is, `iterator` and `foreach` are each other's mirror images, and all typing is inferred from the `MoveNext[Async]` method and the `Current` property:

```csharp
class SomeIterator
{
    bool MoveNext(/* in */ Foo foo);
    /* out */ Bar Current { get; }
}
```

transposes to the corresponding consumer site declaration:

```csharp
foreach ((in Bar bar, out Foo foo = ...) in someIterator)
{
    ...
}
```

This would even be more direct when using the alternative `TryGetNext` method, where the order of output and input would be the same as on the consumer side. Allowing some relaxation of syntax to drive this point home:

```csharp
class                                       SomeIterator
{
         out Bar                                          TryGetNext(
                       in Foo foo                         , out bool success);
}

foreach ((in Bar bar, out Foo foo = ...) in someIterator) { ... }
```

As illustrated earlier, this bi-directional pattern enables all sorts of communication from the consumer to the producer. Examples are plenty. Let's just entertain the thought of using a `Task` as an input type to an asynchronous iterator. That alone would enable injection of cancellation but also injection of failure using an exception type.

```csharp
foreach ((in int x, out Task t = Task.CompletedTask) x in xs)
{
    if (b)
    {
        // Hey, stop (if you please)!
        t = Task.FromException(new Exception("Stop!"));
        continue;
    }

    if (c)
    {
        // Hey, cancel (I can't give you my token, but can wrap it)!
        t = Task.FromCanceled(token);
        continue;
    }

    if (d)
    {
        // Hey, take a break (for a while)!
        t = Task.FromDelay(1000);
        continue;
    }

    ...
}
```

The last example of injecting a delay seems like something the caller could do just as well. However, nobody is stating how such a received task is to be used. That's solely a contract between a producer and a consumer that happen to share the iterable and iterator pattern amongst them. For example, a bi-directional asynchronous iterator accepting tasks could use them to `WhenAny` some asynchronous activity with the task the consumer passed in. That would allow such a producer/consumer pair to have a contract of liveness where the consumer dictates how long it is willing to wait for the outcome of the next operation, while being able to dynamically change this over time.

Other real-world examples include passing compute credit from consumer to producer (e.g. for a cloud service being invoked), injection of timeouts, refreshing of tokens used in long-running computations, etc. With some imagination, one can see generalized co-routine style programming as well, and all sorts of channels.

## LINQ query operators

LINQ query operators for asynchronous iterable sequences (using "iterable" to refer to a bigger class of types that work with `foreach await` and iterators, including "enumerable" sequences) could naturally support deep cancellation this way, which would help with deep cancellation on a per-iteration basis, without requiring injection of cancellation at the source or at each callback (e.g. a predicate or selector function). Cancellation can be wired by query operators to their sources, but could also be observed in possible run-away computations that are eager (e.g. `OrderBy`) or bridge with non-cooperative sources (e.g. `SelectMany` with synchronous inner sequences).

An example is shown below, assuming the `value` contextual keyword providing access to the cancellation token, as shown earlier (and assuming that `MoveNextAsync` accepts such a parameter):

```csharp
public static IAsyncEnumerable<R> SelectMany<T, R>(this IAsyncEnumerable<T> source,
                                                   Func<T, IEnumerable<R>> selector)
{
    if (source == null)
        throw new ArgumentNullExcecption(nameof(source));
    if (selector == null)
        throw new ArgumentNullExcecption(nameof(selector));

    return Iterator();

    async IAsyncEnumerable<R> Iterator()
    {
        value.ThrowIfCancellationRequested();

        foreach await ((in T outer, out CancellationToken token = value) in source)
        {
            foreach (R inner in selector(outer))
            {
                value.ThrowIfCancellationRequested(); // or could use token

                yield return inner;
            }
        }
    }
}
```

Note we can pass the cancellation token down to the enumeration over the outer source, which may support cancellation based on inductive reasoning (the type says it supports it, and a concrete iterator might do it). However, the inner sequence does not support cancellation, so we can inject it ourselves here.

Eager query operators such as `OrderBy` (with potentially any number of `ThenBy` n-ary orderings applied on top) could benefit greatly from this because both the source enumeration and the sorting logic could be interrupted for cancellation.