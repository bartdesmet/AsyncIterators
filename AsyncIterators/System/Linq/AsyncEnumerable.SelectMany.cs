//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, IAsyncEnumerable<TResult>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<TResult> Iterator()
            {
                foreach await (var outer in source.ConfigureAwait(false))
                {
                    foreach await (var inner in selector(outer).ConfigureAwait(false))
                    {
                        yield return inner;
                    }
                }
            }
#else
            return SelectMany(source, selector, (_, item) => item);
#endif
        }

        public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (collectionSelector == null)
                throw new ArgumentNullException(nameof(collectionSelector));
            if (resultSelector == null)
                throw new ArgumentNullException(nameof(resultSelector));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<TResult> Iterator()
            {
                foreach await (var outer in source.ConfigureAwait(false))
                {
                    foreach await (var inner in selector(outer).ConfigureAwait(false))
                    {
                        yield return resultSelector(outer, inner);
                    }
                }
            }
#else
            return new SelectManyAsyncIteratorWithSyncSelector<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);
#endif
        }

        public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, Task<TResult>> resultSelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (collectionSelector == null)
                throw new ArgumentNullException(nameof(collectionSelector));
            if (resultSelector == null)
                throw new ArgumentNullException(nameof(resultSelector));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<TResult> Iterator()
            {
                foreach await (var outer in source.ConfigureAwait(false))
                {
                    foreach await (var inner in selector(outer).ConfigureAwait(false))
                    {
                        yield return await resultSelector(outer, inner).ConfigureAwait(false);
                    }
                }
            }
#else
            return new SelectManyAsyncIteratorWithAsyncSelector<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);
#endif
        }

        private sealed class SelectManyAsyncIteratorWithSyncSelector<T, C, R> : AsyncIterable<R, SelectManyAsyncIteratorWithSyncSelector<T, C, R>>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly Func<T, IAsyncEnumerable<C>> collectionSelector;
            private readonly Func<T, C, R> resultSelector;
            private ConfiguredAsyncEnumerable<T>.Enumerator __enumerator0;
            private T outer;
            private ConfiguredAsyncEnumerable<C>.Enumerator __enumerator1;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1;
            private Exception __ex0;
            private Exception __ex1;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter2;

            public SelectManyAsyncIteratorWithSyncSelector(IAsyncEnumerable<T> source, Func<T, IAsyncEnumerable<C>> collectionSelector, Func<T, C, R> resultSelector)
            {
                this.source = source;
                this.collectionSelector = collectionSelector;
                this.resultSelector = resultSelector;
            }

            protected override SelectManyAsyncIteratorWithSyncSelector<T, C, R> Clone() => new SelectManyAsyncIteratorWithSyncSelector<T, C, R>(source, collectionSelector, resultSelector);

            protected override R TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                ConfiguredAsyncEnumerable<T>.Enumerator __enumerator0 = default;
                ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1 = default;
                Exception __ex0 = default;

                switch (state)
                {
                    case 0:
                        goto __State0;
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        goto __EnterTry1;
                    case 5:
                        __ex0 = this.__ex0;
                        goto __EnterTry2;
                    default:
                        throw new NotImplementedException();
                }

                __State0:

                __enumerator0 = source.ConfigureAwait(false).GetAsyncEnumerator();

                __EnterTry1:

                try
                {
                    ConfiguredAsyncEnumerable<C>.Enumerator __enumerator1 = default;
                    T outer = default;
                    Exception __ex1 = default;
                    ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter2 = default;

                    switch (state)
                    {
                        case 0:
                            break;
                        case 1:
                            __enumerator0 = this.__enumerator0;
                            __awaiter1 = this.__awaiter1;
                            goto __State1;
                        case 2:
                        case 3:
                            __enumerator0 = this.__enumerator0;
                            goto __EnterTry11;
                        case 4:
                            __enumerator0 = this.__enumerator0;
                            outer = this.outer;
                            __ex1 = this.__ex1;
                            __awaiter2 = this.__awaiter2;
                            goto __State4;
                        default:
                            throw new NotImplementedException();
                    }

                    __BeginLoop1:

                    __awaiter1 = __enumerator0.WaitForNextAsync().GetAwaiter();

                    if (!__awaiter1.IsCompleted)
                    {
                        this.__enumerator0 = __enumerator0;
                        this.__awaiter1 = __awaiter1;
                        nextState = 1;
                        AwaitOnCompleted(nextState, ref __awaiter1);
                        hasNext = null;
                        return default;
                    }

                    __State1:

                    if (!__awaiter1.GetResult())
                    {
                        goto __BreakLoop1;
                    }

                    __BeginLoop2:

                    state = 1; // NB: We may jump back here

                    outer = __enumerator0.TryGetNext(out var __success);

                    if (!__success)
                    {
                        goto __BreakLoop2;
                    }

                    __enumerator1 = collectionSelector(outer).ConfigureAwait(false).GetAsyncEnumerator();

                    __EnterTry11:

                    try
                    {
                        switch (state)
                        {
                            case 0:
                            case 1:
                                break;
                            case 2:
                                outer = this.outer;
                                __awaiter1 = this.__awaiter1;
                                goto __State2;
                            case 3:
                                outer = this.outer;
                                __enumerator1 = this.__enumerator1;
                                if (shouldBreak)
                                    goto __State3_Break;
                                else
                                    goto __State3;
                            default:
                                throw new NotImplementedException();
                        }

                        __BeginLoop11:

                        __awaiter1 = __enumerator1.WaitForNextAsync().GetAwaiter();

                        if (!__awaiter1.IsCompleted)
                        {
                            this.__enumerator0 = __enumerator0;
                            this.outer = outer;
                            this.__enumerator1 = __enumerator1;
                            this.__awaiter1 = __awaiter1;
                            nextState = 2;
                            AwaitOnCompleted(nextState, ref __awaiter1);
                            hasNext = null;
                            return default;
                        }

                        __State2:

                        if (!__awaiter1.GetResult())
                        {
                            goto __BreakLoop11;
                        }

                        __BeginLoop12:

                        C inner = __enumerator1.TryGetNext(out var __success2);

                        if (!__success2)
                        {
                            goto __BreakLoop12;
                        }

                        R result = resultSelector(outer, inner);

                        if (!ShouldYieldReturn())
                        {
                            goto __State3_Break;
                        }

                        this.__enumerator0 = __enumerator0;
                        this.__enumerator1 = __enumerator1;
                        this.outer = outer;
                        nextState = 3;
                        hasNext = true;
                        return result;

                        __State3_Break:

                        goto __YieldBreak;

                        __State3:

                        goto __BeginLoop12;

                        __BreakLoop12:

                        goto __BeginLoop11;

                        __BreakLoop11:
                        ;
                    }
                    catch (Exception __e)
                    {
                        __ex1 = __e;
                    }

                    __awaiter2 = __enumerator1.DisposeAsync().GetAwaiter();

                    if (!__awaiter2.IsCompleted)
                    {
                        this.__enumerator0 = __enumerator0;
                        this.outer = outer;
                        this.__ex1 = __ex1;
                        this.__awaiter2 = __awaiter2;
                        nextState = 4;
                        AwaitOnCompleted(nextState, ref __awaiter2);
                        hasNext = null;
                        return default;
                    }

                    __State4:

                    __awaiter2.GetResult();

                    if (__ex1 != null)
                    {
                        ExceptionDispatchInfo.Capture(__ex1).Throw();
                    }

                    goto __BeginLoop2;

                    __BreakLoop2:

                    goto __BeginLoop1;

                    __BreakLoop1:

                    goto __YieldBreak;
                }
                catch (Exception __e)
                {
                    __ex0 = __e;
                }

                __YieldBreak:

                OnDisposing();

                __EnterTry2:

                bool __suppressFinally = false;
                try
                {
                    ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter2 = default;

                    switch (state)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                            break;
                        case 5:
                            __awaiter2 = this.__awaiter2;
                            goto __State5;
                        default:
                            throw new NotImplementedException();
                    }

                    __awaiter2 = __enumerator0.DisposeAsync().GetAwaiter();

                    if (!__awaiter2.IsCompleted)
                    {
                        this.__ex0 = __ex0;
                        this.__awaiter2 = __awaiter2;
                        nextState = 5;
                        AwaitOnCompleted(nextState, ref __awaiter2);
                        __suppressFinally = true;
                        hasNext = null;
                        return default;
                    }

                    __State5:

                    __awaiter2.GetResult();

                    if (__ex0 != null)
                    {
                        ExceptionDispatchInfo.Capture(__ex0).Throw();
                    }

                    nextState = ushort.MaxValue;
                    hasNext = false;
                    return default;
                }
                finally
                {
                    if (!__suppressFinally)
                    {
                        OnDisposed();
                    }
                }
            }
        }

        private sealed class SelectManyAsyncIteratorWithAsyncSelector<T, C, R> : AsyncIterable<R, SelectManyAsyncIteratorWithAsyncSelector<T, C, R>>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly Func<T, IAsyncEnumerable<C>> collectionSelector;
            private readonly Func<T, C, Task<R>> resultSelector;
            private ConfiguredAsyncEnumerable<T>.Enumerator __enumerator0;
            private T outer;
            private ConfiguredAsyncEnumerable<C>.Enumerator __enumerator1;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1;
            private ConfiguredTaskAwaitable<R>.ConfiguredTaskAwaiter __awaiter2;
            private Exception __ex0;
            private Exception __ex1;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter3;

            public SelectManyAsyncIteratorWithAsyncSelector(IAsyncEnumerable<T> source, Func<T, IAsyncEnumerable<C>> collectionSelector, Func<T, C, Task<R>> resultSelector)
            {
                this.source = source;
                this.collectionSelector = collectionSelector;
                this.resultSelector = resultSelector;
            }

            protected override SelectManyAsyncIteratorWithAsyncSelector<T, C, R> Clone() => new SelectManyAsyncIteratorWithAsyncSelector<T, C, R>(source, collectionSelector, resultSelector);

            protected override R TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                ConfiguredAsyncEnumerable<T>.Enumerator __enumerator0 = default;
                ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1 = default;
                Exception __ex0 = default;

                switch (state)
                {
                    case 0:
                        goto __State0;
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                        goto __EnterTry1;
                    case 6:
                        __ex0 = this.__ex0;
                        goto __EnterTry2;
                    default:
                        throw new NotImplementedException();
                }

                __State0:

                __enumerator0 = source.ConfigureAwait(false).GetAsyncEnumerator();

                __EnterTry1:

                try
                {
                    ConfiguredAsyncEnumerable<C>.Enumerator __enumerator1 = default;
                    T outer = default;
                    Exception __ex1 = default;
                    ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter3 = default;

                    switch (state)
                    {
                        case 0:
                            break;
                        case 1:
                            __enumerator0 = this.__enumerator0;
                            __awaiter1 = this.__awaiter1;
                            goto __State1;
                        case 2:
                        case 3:
                            __enumerator0 = this.__enumerator0;
                            outer = this.outer;
                            goto __EnterTry11;
                        case 4:
                            __enumerator0 = this.__enumerator0;
                            outer = this.outer;
                            goto __EnterTry11;
                        case 5:
                            __enumerator0 = this.__enumerator0;
                            outer = this.outer;
                            __ex1 = this.__ex1;
                            __awaiter3 = this.__awaiter3;
                            goto __State5;
                        default:
                            throw new NotImplementedException();
                    }

                    __BeginLoop1:

                    __awaiter1 = __enumerator0.WaitForNextAsync().GetAwaiter();

                    if (!__awaiter1.IsCompleted)
                    {
                        this.__enumerator0 = __enumerator0;
                        this.__awaiter1 = __awaiter1;
                        nextState = 1;
                        AwaitOnCompleted(nextState, ref __awaiter1);
                        hasNext = null;
                        return default;
                    }

                    __State1:

                    if (!__awaiter1.GetResult())
                    {
                        goto __BreakLoop1;
                    }

                    __BeginLoop2:

                    state = 1; // NB: We may jump back here

                    outer = __enumerator0.TryGetNext(out var __success);

                    if (!__success)
                    {
                        goto __BreakLoop2;
                    }

                    __enumerator1 = collectionSelector(outer).ConfigureAwait(false).GetAsyncEnumerator();

                    __EnterTry11:

                    try
                    {
                        ConfiguredTaskAwaitable<R>.ConfiguredTaskAwaiter __awaiter2 = default;

                        switch (state)
                        {
                            case 0:
                            case 1:
                                break;
                            case 2:
                                outer = this.outer;
                                __awaiter1 = this.__awaiter1;
                                goto __State2;
                            case 3:
                                outer = this.outer;
                                __enumerator1 = this.__enumerator1;
                                __awaiter2 = this.__awaiter2;
                                goto __State3;
                            case 4:
                                outer = this.outer;
                                __enumerator1 = this.__enumerator1;
                                if (shouldBreak)
                                    goto __State4_Break;
                                else
                                    goto __State4;
                            default:
                                throw new NotImplementedException();
                        }

                        __BeginLoop11:

                        __awaiter1 = __enumerator1.WaitForNextAsync().GetAwaiter();

                        if (!__awaiter1.IsCompleted)
                        {
                            this.__enumerator0 = __enumerator0;
                            this.outer = outer;
                            this.__enumerator1 = __enumerator1;
                            this.__awaiter1 = __awaiter1;
                            nextState = 2;
                            AwaitOnCompleted(nextState, ref __awaiter1);
                            hasNext = null;
                            return default;
                        }

                        __State2:

                        if (!__awaiter1.GetResult())
                        {
                            goto __BreakLoop11;
                        }

                        __BeginLoop12:

                        C inner = __enumerator1.TryGetNext(out var __success2);

                        if (!__success2)
                        {
                            goto __BreakLoop12;
                        }

                        __awaiter2 = resultSelector(outer, inner).ConfigureAwait(false).GetAwaiter();

                        if (!__awaiter2.IsCompleted)
                        {
                            this.__enumerator0 = __enumerator0;
                            this.outer = outer;
                            this.__enumerator1 = __enumerator1;
                            this.__awaiter2 = __awaiter2;
                            nextState = 3;
                            AwaitOnCompleted(nextState, ref __awaiter2);
                            hasNext = null;
                            return default;
                        }

                        __State3:

                        var result = __awaiter2.GetResult();

                        if (!ShouldYieldReturn())
                        {
                            goto __State4_Break;
                        }

                        this.__enumerator0 = __enumerator0;
                        this.__enumerator1 = __enumerator1;
                        this.outer = outer;
                        nextState = 4;
                        hasNext = true;
                        return result;

                        __State4_Break:

                        goto __YieldBreak;

                        __State4:

                        goto __BeginLoop12;

                        __BreakLoop12:

                        goto __BeginLoop11;

                        __BreakLoop11:
                        ;
                    }
                    catch (Exception __e)
                    {
                        __ex1 = __e;
                    }

                    __awaiter3 = __enumerator1.DisposeAsync().GetAwaiter();

                    if (!__awaiter3.IsCompleted)
                    {
                        this.__enumerator0 = __enumerator0;
                        this.outer = outer;
                        this.__ex1 = __ex1;
                        this.__awaiter3 = __awaiter3;
                        nextState = 5;
                        AwaitOnCompleted(nextState, ref __awaiter3);
                        hasNext = null;
                        return default;
                    }

                    __State5:

                    __awaiter3.GetResult();

                    if (__ex1 != null)
                    {
                        ExceptionDispatchInfo.Capture(__ex1).Throw();
                    }

                    goto __BeginLoop2;

                    __BreakLoop2:

                    goto __BeginLoop1;

                    __BreakLoop1:

                    goto __YieldBreak;
                }
                catch (Exception __e)
                {
                    __ex0 = __e;
                }

                __YieldBreak:

                OnDisposing();

                __EnterTry2:

                bool __suppressFinally = false;
                try
                {
                    ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter3 = default;

                    switch (state)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                            break;
                        case 6:
                            __awaiter3 = this.__awaiter3;
                            goto __State6;
                        default:
                            throw new NotImplementedException();
                    }

                    __awaiter3 = __enumerator0.DisposeAsync().GetAwaiter();

                    if (!__awaiter3.IsCompleted)
                    {
                        this.__ex0 = __ex0;
                        this.__awaiter3 = __awaiter3;
                        nextState = 6;
                        AwaitOnCompleted(nextState, ref __awaiter3);
                        __suppressFinally = true;
                        hasNext = null;
                        return default;
                    }

                    __State6:

                    __awaiter3.GetResult();

                    if (__ex0 != null)
                    {
                        ExceptionDispatchInfo.Capture(__ex0).Throw();
                    }

                    nextState = ushort.MaxValue;
                    hasNext = false;
                    return default;
                }
                finally
                {
                    if (!__suppressFinally)
                    {
                        OnDisposed();
                    }
                }
            }
        }
    }
}
