using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Empty<T>()
        {
#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                yield break;
            }
#else
            return new EmptyAsyncIterator<T>();
#endif
        }

        private sealed class EmptyAsyncIterator<T> : AsyncIterable<T, EmptyAsyncIterator<T>>
        {
            protected override EmptyAsyncIterator<T> Clone() => new EmptyAsyncIterator<T>();

            protected override T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
            {
                if (state == 0 || shouldBreak)
                {
                    nextState = ushort.MaxValue;
                    hasNext = false;
                    return default;
                }

                throw new NotImplementedException();
            }
        }
    }
}
