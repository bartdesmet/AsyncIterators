using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Return<T>(T value)
        {
#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                yield return value;
            }
#else
            return new ReturnAsyncIterator<T>(value);
#endif
        }

        private sealed class ReturnAsyncIterator<T> : AsyncIterable<T, ReturnAsyncIterator<T>>
        {
            private readonly T value;

            public ReturnAsyncIterator(T value)
            {
                this.value = value;
            }

            protected override ReturnAsyncIterator<T> Clone() => new ReturnAsyncIterator<T>(value);

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                if (shouldBreak)
                {
                    goto __YieldBreak;
                }

                switch (state)
                {
                    case 0:
                        nextState = 1;
                        hasNext = true;
                        return value;
                    case 1:
                        goto __YieldBreak;
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
    }
}
