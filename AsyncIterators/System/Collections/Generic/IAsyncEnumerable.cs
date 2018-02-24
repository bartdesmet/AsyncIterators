//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    [AsyncIterableBuilderType(typeof(AsyncIterable<,>))]
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }
}
