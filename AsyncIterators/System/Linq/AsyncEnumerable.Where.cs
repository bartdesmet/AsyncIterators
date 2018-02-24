using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Where<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                foreach await (var item in source.ConfigureAwait(false))
                {
                    if (predicate(item))
                    {
                        yield return item;
                    }
                }
            }
#else
            return new WhereAsyncIteratorWithSyncPredicate<T>(source, predicate);
#endif
        }

        public static IAsyncEnumerable<T> Where<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                foreach await (var item in source.ConfigureAwait(false))
                {
                    if (await predicate(item).ConfigureAwait(false))
                    {
                        yield return item;
                    }
                }
            }
#else
            return new WhereAsyncIteratorWithAsyncPredicate<T>(source, predicate);
#endif
        }

        private sealed class WhereAsyncIteratorWithSyncPredicate<T> : AsyncIterable<T, WhereAsyncIteratorWithSyncPredicate<T>>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly Func<T, bool> predicate;
            private ConfiguredAsyncEnumerable<T>.Enumerator __enumerator;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1;
            private Exception __ex;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter2;

            public WhereAsyncIteratorWithSyncPredicate(IAsyncEnumerable<T> source, Func<T, bool> predicate)
            {
                this.source = source;
                this.predicate = predicate;
            }

            protected override WhereAsyncIteratorWithSyncPredicate<T> Clone() => new WhereAsyncIteratorWithSyncPredicate<T>(source, predicate);

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
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

                    if (!predicate(item))
                    {
                        goto __EndIf1;
                    }

                    if (!ShouldYieldReturn())
                    {
                        goto __State2_Break;
                    }

                    this.__enumerator = __enumerator;
                    nextState = 2;
                    hasNext = true;
                    return item;

                    __State2_Break:

                    goto __YieldBreak;

                    __State2:

                    __EndIf1:

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

                    if (!__awaiter1.IsCompleted)
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

        private sealed class WhereAsyncIteratorWithAsyncPredicate<T> : AsyncIterable<T, WhereAsyncIteratorWithAsyncPredicate<T>>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly Func<T, Task<bool>> predicate;
            private ConfiguredAsyncEnumerable<T>.Enumerator __enumerator;
            private ConfiguredTaskAwaitable<bool>.ConfiguredTaskAwaiter __awaiter1;
            private T item;
            private Exception __ex;
            private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter2;

            public WhereAsyncIteratorWithAsyncPredicate(IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
            {
                this.source = source;
                this.predicate = predicate;
            }

            protected override WhereAsyncIteratorWithAsyncPredicate<T> Clone() => new WhereAsyncIteratorWithAsyncPredicate<T>(source, predicate);

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
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
                    T item;

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
                            __awaiter1 = this.__awaiter1;
                            item = this.item;
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

                    item = __enumerator.TryGetNext(out var __success);

                    if (!__success)
                    {
                        goto __BreakLoop2;
                    }

                    __awaiter1 = predicate(item).ConfigureAwait(false).GetAwaiter();

                    if (!__awaiter1.IsCompleted)
                    {
                        this.__enumerator = __enumerator;
                        this.item = item;
                        this.__awaiter1 = __awaiter1;
                        nextState = 2;
                        AwaitOnCompleted(nextState, ref __awaiter1);
                        hasNext = null;
                        return default;
                    }

                    __State2:

                    if (!__awaiter1.GetResult())
                    {
                        goto __EndIf1;
                    }

                    if (!ShouldYieldReturn())
                    {
                        goto __State3_Break;
                    }

                    this.__enumerator = __enumerator;
                    nextState = 3;
                    hasNext = true;
                    return item;

                    __State3_Break:

                    goto __YieldBreak;

                    __State3:

                    __EndIf1:

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
                        case 3:
                            break;
                        case 4:
                            __awaiter2 = this.__awaiter2;
                            goto __State4;
                        default:
                            throw new NotImplementedException();
                    }

                    __awaiter2 = __enumerator.DisposeAsync().GetAwaiter();

                    if (!__awaiter1.IsCompleted)
                    {
                        this.__ex = __ex;
                        this.__awaiter2 = __awaiter2;
                        nextState = 3;
                        AwaitOnCompleted(nextState, ref __awaiter2);
                        __suppressFinally = true;
                        hasNext = null;
                        return default;
                    }

                    __State4:

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
    }
}
