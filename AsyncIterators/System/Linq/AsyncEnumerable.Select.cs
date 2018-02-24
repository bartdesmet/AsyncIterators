using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<R> Select<T, R>(this IAsyncEnumerable<T> source, Func<T, R> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<R> Iterator()
            {
                foreach await (var item in source.ConfigureAwait(false))
                {
                    yield return selector(item);
                }
            }
#else
            return new SelectAsyncIteratorWithSyncSelector<T, R>(source, selector);
#endif
        }

        public static IAsyncEnumerable<R> Select<T, R>(this IAsyncEnumerable<T> source, Func<T, Task<R>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<R> Iterator()
            {
                foreach await (var item in source.ConfigureAwait(false))
                {
                    yield return selector(item).ConfigureAwait(false);
                }
            }
#else
            return new SelectAsyncIteratorWithAsyncSelector<T, R>(source, selector);
#endif
        }

        private sealed class SelectAsyncIteratorWithSyncSelector<T, R> : AsyncIterable<R, SelectAsyncIteratorWithSyncSelector<T, R>>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly Func<T, R> selector;
            private ConfiguredAsyncEnumerable<T>.Enumerator __enumerator;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1;
            private Exception __ex;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter2;

            public SelectAsyncIteratorWithSyncSelector(IAsyncEnumerable<T> source, Func<T, R> selector)
            {
                this.source = source;
                this.selector = selector;
            }

            protected override SelectAsyncIteratorWithSyncSelector<T, R> Clone() => new SelectAsyncIteratorWithSyncSelector<T, R>(source, selector);

            protected override R TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                ConfiguredAsyncEnumerable<T>.Enumerator __enumerator = default;
                ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1 = default;
                Exception __ex = default;

                switch (state)
                {
                    case 0:
                        goto __State0;
                    case 1:
                    case 2:
                        goto __EnterTry1;
                    case 3:
                        __ex = this.__ex;
                        goto __EnterTry2;
                    default:
                        throw new NotImplementedException();
                }

                __State0:

                __enumerator = source.ConfigureAwait(false).GetAsyncEnumerator();

                __EnterTry1:

                try
                {
                    switch (state)
                    {
                        case 0:
                            break;
                        case 1:
                            __enumerator = this.__enumerator;
                            __awaiter1 = this.__awaiter1;
                            goto __State1;
                        case 2:
                            __enumerator = this.__enumerator;
                            if (shouldBreak)
                                goto __State2_Break;
                            else
                                goto __State2;
                        default:
                            throw new NotImplementedException();
                    }

                    __BeginLoop1:

                    __awaiter1 = __enumerator.WaitForNextAsync().GetAwaiter();

                    if (!__awaiter1.IsCompleted)
                    {
                        this.__enumerator = __enumerator;
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

                    T item = __enumerator.TryGetNext(out var __success);

                    if (!__success)
                    {
                        goto __BreakLoop2;
                    }

                    R result = selector(item);

                    if (!ShouldYieldReturn())
                    {
                        goto __State2_Break;
                    }

                    this.__enumerator = __enumerator;
                    nextState = 2;
                    hasNext = true;
                    return result;

                    __State2_Break:

                    goto __YieldBreak;

                    __State2:

                    goto __BeginLoop2;

                    __BreakLoop2:

                    goto __BeginLoop1;

                    __BreakLoop1:

                    goto __YieldBreak;
                }
                catch (Exception __e)
                {
                    __ex = __e;
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
                            break;
                        case 3:
                            __awaiter2 = this.__awaiter2;
                            goto __State3;
                        default:
                            throw new NotImplementedException();
                    }

                    __awaiter2 = __enumerator.DisposeAsync().GetAwaiter();

                    if (!__awaiter2.IsCompleted)
                    {
                        this.__ex = __ex;
                        this.__awaiter2 = __awaiter2;
                        nextState = 3;
                        AwaitOnCompleted(nextState, ref __awaiter2);
                        __suppressFinally = true;
                        hasNext = null;
                        return default;
                    }

                    __State3:

                    __awaiter2.GetResult();

                    if (__ex != null)
                    {
                        ExceptionDispatchInfo.Capture(__ex).Throw();
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

        private sealed class SelectAsyncIteratorWithAsyncSelector<T, R> : AsyncIterable<R, SelectAsyncIteratorWithAsyncSelector<T, R>>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly Func<T, Task<R>> selector;
            private ConfiguredAsyncEnumerable<T>.Enumerator __enumerator;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1;
            private ConfiguredTaskAwaitable<R>.ConfiguredTaskAwaiter __awaiter2;
            private Exception __ex;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter3;

            public SelectAsyncIteratorWithAsyncSelector(IAsyncEnumerable<T> source, Func<T, Task<R>> selector)
            {
                this.source = source;
                this.selector = selector;
            }

            protected override SelectAsyncIteratorWithAsyncSelector<T, R> Clone() => new SelectAsyncIteratorWithAsyncSelector<T, R>(source, selector);

            protected override R TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                ConfiguredAsyncEnumerable<T>.Enumerator __enumerator = default;
                ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1 = default;
                ConfiguredTaskAwaitable<R>.ConfiguredTaskAwaiter __awaiter2 = default;
                Exception __ex = default;

                switch (state)
                {
                    case 0:
                        goto __State0;
                    case 1:
                    case 2:
                    case 3:
                        goto __EnterTry1;
                    case 4:
                        __ex = this.__ex;
                        goto __EnterTry2;
                    default:
                        throw new NotImplementedException();
                }

                __State0:

                __enumerator = source.ConfigureAwait(false).GetAsyncEnumerator();

                __EnterTry1:

                try
                {
                    switch (state)
                    {
                        case 0:
                            break;
                        case 1:
                            __enumerator = this.__enumerator;
                            __awaiter1 = this.__awaiter1;
                            goto __State1;
                        case 2:
                            __enumerator = this.__enumerator;
                            __awaiter2 = this.__awaiter2;
                            goto __State2;
                        case 3:
                            __enumerator = this.__enumerator;
                            if (shouldBreak)
                                goto __State3_Break;
                            else
                                goto __State3;
                        default:
                            throw new NotImplementedException();
                    }

                    __BeginLoop1:

                    __awaiter1 = __enumerator.WaitForNextAsync().GetAwaiter();

                    if (!__awaiter1.IsCompleted)
                    {
                        this.__enumerator = __enumerator;
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

                    T item = __enumerator.TryGetNext(out var __success);

                    if (!__success)
                    {
                        goto __BreakLoop2;
                    }

                    __awaiter2 = selector(item).ConfigureAwait(false).GetAwaiter();

                    if (!__awaiter2.IsCompleted)
                    {
                        this.__enumerator = __enumerator;
                        this.__awaiter2 = __awaiter2;
                        nextState = 2;
                        AwaitOnCompleted(nextState, ref __awaiter2);
                        hasNext = null;
                        return default;
                    }

                    __State2:

                    R result = __awaiter2.GetResult();

                    if (!ShouldYieldReturn())
                    {
                        goto __State3_Break;
                    }

                    this.__enumerator = __enumerator;
                    nextState = 3;
                    hasNext = true;
                    return result;

                    __State3_Break:

                    goto __YieldBreak;

                    __State3:

                    goto __BeginLoop2;

                    __BreakLoop2:

                    goto __BeginLoop1;

                    __BreakLoop1:

                    goto __YieldBreak;
                }
                catch (Exception __e)
                {
                    __ex = __e;
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
                            break;
                        case 4:
                            __awaiter3 = this.__awaiter3;
                            goto __State4;
                        default:
                            throw new NotImplementedException();
                    }

                    __awaiter3 = __enumerator.DisposeAsync().GetAwaiter();

                    if (!__awaiter3.IsCompleted)
                    {
                        this.__ex = __ex;
                        this.__awaiter3 = __awaiter3;
                        nextState = 4;
                        AwaitOnCompleted(nextState, ref __awaiter3);
                        __suppressFinally = true;
                        hasNext = null;
                        return default;
                    }

                    __State4:

                    __awaiter3.GetResult();

                    if (__ex != null)
                    {
                        ExceptionDispatchInfo.Capture(__ex).Throw();
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
