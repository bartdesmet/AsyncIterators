//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Threading.Tasks;

namespace System
{
    public interface IAsyncDisposable
    {
        Task DisposeAsync();
    }
}
