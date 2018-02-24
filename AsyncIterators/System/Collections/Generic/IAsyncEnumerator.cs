//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace System.Collections.Generic
{
    [AsyncIteratorBuilderType(typeof(AsyncIterator<>))]
    public interface IAsyncEnumerator<out T> : IAsyncDisposable
    {
        Task<bool> WaitForNextAsync();
        T TryGetNext(out bool success);
    }
}
