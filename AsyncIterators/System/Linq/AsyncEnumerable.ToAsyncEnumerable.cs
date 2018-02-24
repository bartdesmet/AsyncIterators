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
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] array)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                foreach (var item in array)
                {
                    yield return item;
                }
            }
#else
            return new ArrayToAsyncEnumerableAsyncIterator<T>(array);
#endif
        }

        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                foreach (var item in source)
                {
                    yield return item;
                }
            }
#else
            return new EnumerableToAsyncEnumerableAsyncIterator<T>(source);
#endif
        }

        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this Task<T> task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                yield return await task.ConfigureAwait(false);
            }
#else
            return new TaskToAsyncEnumerableAsyncIterator<T>(task);
#endif
        }

        private sealed class ArrayToAsyncEnumerableAsyncIterator<T> : AsyncIterable<T, ArrayToAsyncEnumerableAsyncIterator<T>>
        {
            private readonly T[] array;
            private int index;

            public ArrayToAsyncEnumerableAsyncIterator(T[] array)
            {
                this.array = array;
            }

            protected override ArrayToAsyncEnumerableAsyncIterator<T> Clone() => new ArrayToAsyncEnumerableAsyncIterator<T>(array);

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                if (shouldBreak)
                {
                    goto __YieldBreak;
                }

                switch (state)
                {
                    case 0:
                        nextState = 0;

                        if (index == array.Length)
                        {
                            goto __YieldBreak;
                        }

                        hasNext = true;
                        return array[index++];
                    default:
                        throw new NotImplementedException();
                }

                __YieldBreak:

                OnDisposing();

                try
                {
                    nextState = ushort.MaxValue;
                    hasNext = false;
                    return default;
                }
                finally
                {
                    OnDisposed();
                }
            }
        }

        private sealed class EnumerableToAsyncEnumerableAsyncIterator<T> : AsyncIterable<T, EnumerableToAsyncEnumerableAsyncIterator<T>>
        {
            private readonly IEnumerable<T> source;
            private IEnumerator<T> enumerator;

            public EnumerableToAsyncEnumerableAsyncIterator(IEnumerable<T> source)
            {
                this.source = source;
            }

            protected override EnumerableToAsyncEnumerableAsyncIterator<T> Clone() => new EnumerableToAsyncEnumerableAsyncIterator<T>(source);

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                Exception __ex = default;

                if (shouldBreak)
                {
                    goto __YieldBreak;
                }

                switch (state)
                {
                    case 0:
                        goto __State0;
                    case 1:
                        goto __State1;
                    default:
                        throw new NotImplementedException();
                }

                __State0:

                enumerator = source.GetEnumerator();

                __State1:

                try
                {
                    __BeginLoop1:

                    if (!enumerator.MoveNext())
                    {
                        goto __BreakLoop1;
                    }

                    var item = enumerator.Current;

                    if (ShouldYieldReturn())
                    {
                        nextState = 1;
                        hasNext = true;
                        return item;
                    }
                    else
                    {
                        goto __YieldBreak;
                    }

                    goto __BeginLoop1;

                    __BreakLoop1:;
                }
                catch (Exception __e)
                {
                    __ex = __e;
                }

                __YieldBreak:

                OnDisposing();

                try
                {
                    enumerator.Dispose();

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
                    OnDisposed();
                }
            }
        }

        private sealed class TaskToAsyncEnumerableAsyncIterator<T> : AsyncIterable<T, TaskToAsyncEnumerableAsyncIterator<T>>
        {
            private readonly Task<T> task;
            private ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter __awaiter;

            public TaskToAsyncEnumerableAsyncIterator(Task<T> task)
            {
                this.task = task;
            }

            protected override TaskToAsyncEnumerableAsyncIterator<T> Clone() => new TaskToAsyncEnumerableAsyncIterator<T>(task);

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter __awaiter;

                if (shouldBreak)
                {
                    goto __YieldBreak;
                }

                switch (state)
                {
                    case 0:
                        goto __State0;
                    case 1:
                        __awaiter = this.__awaiter;
                        goto __State1;
                    case 2:
                        goto __State2;
                    default:
                        throw new NotImplementedException();
                }

                __State0:

                __awaiter = task.ConfigureAwait(false).GetAwaiter();

                if (!__awaiter.IsCompleted)
                {
                    this.__awaiter = __awaiter;
                    nextState = 1;
                    AwaitOnCompleted(nextState, ref __awaiter);
                    hasNext = null;
                    return default;
                }

                __State1:

                T value = __awaiter.GetResult();

                if (ShouldYieldReturn())
                {
                    nextState = 2;
                    hasNext = true;
                    return value;
                }
                else
                {
                    goto __YieldBreak;
                }

                __State2:

                __YieldBreak:

                OnDisposing();

                try
                {
                    nextState = ushort.MaxValue;
                    hasNext = false;
                    return default;
                }
                finally
                {
                    OnDisposed();
                }
            }
        }
    }
}
