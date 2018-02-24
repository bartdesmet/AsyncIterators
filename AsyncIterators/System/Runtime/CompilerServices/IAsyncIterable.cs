//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Interface describing the shape of an asynchronous iterable-like type. Such types can be used for
    /// enumeration using the <c>foreach await</c> statement and can be returned from an asynchronous
    /// iterator method.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterable.</typeparam>
    public interface IAsyncIterable<out T>
    {
        /// <summary>
        /// Gets an enumerator instance used to enumerate over the iterable.
        /// </summary>
        /// <returns>An iterator used to enumerate over the iterable.</returns>
        IAsyncIterator<T> GetAsyncEnumerator();
    }
}
