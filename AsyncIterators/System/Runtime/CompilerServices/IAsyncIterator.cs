//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Interface describing the shape of an asynchronous iterator-like type. Such types are used by the
    /// <c>foreach await</c> statement (in conjunction with the corresponding <see cref="IAsyncIterable{T}"/>
    /// type) and can be returned from an asynchronous iterator method.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterator.</typeparam>
    public interface IAsyncIterator<out T>
    {
        /// <summary>
        /// See <see cref="IAsyncEnumerator{T}.TryGetNext"/> for more information.
        /// </summary>
        /// <param name="success">
        /// <c>true</c> if a value was returned synchronously; <c>false</c> if the consumer should call
        /// <see cref="WaitForNextAsync"/> to proceed.
        /// </param>
        /// <returns>
        /// An element returned by the iterator, if <paramref name="success"/> is <c>true</c>; otherwise,
        /// a default value.
        /// </returns>
        T TryGetNext(out bool success);

        /// <summary>
        /// See <see cref="IAsyncEnumerator{T}.WaitForNextAsync"/> for more information.
        /// </summary>
        /// <returns>
        /// A task representing the eventual outcome of advancing the iterator, or an exception. If the
        /// task returns <c>true</c>, the iterator has returned an element that can be retrieved by
        /// calling <see cref="TryGetNext"/>. If the task returns <c>false</c>, the iterator has reached
        /// the end, and cleanup operations have been run.
        /// </returns>
        Task<bool> WaitForNextAsync();

        /// <summary>
        /// Disposes the iterator and runs required cleanup logic.
        /// </summary>
        /// <returns>A task representing the eventual completion of cleanup operations.</returns>
        /// <remarks>
        /// Unlike the synchronous counterpart (<see cref="IIterator{T}"/>), this method is required. If
        /// <see cref="TryGetNext"/> starts asynchronous work, an asynchronous method is required to
        /// observe its eventual outcome. In case the consumer breaks from the loop, the only chance to
        /// obtain a handle to the asynchronous operation is a call to <see cref="DisposeAsync"/>.
        /// </remarks>
        Task DisposeAsync();
    }
}
