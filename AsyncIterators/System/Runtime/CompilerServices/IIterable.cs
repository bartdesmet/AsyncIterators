//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Interface describing the shape of an iterable-like type. Such types can be used for enumeration
    /// using the <c>foreach</c> statement and can be returned from a synchronous iterator method.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterable.</typeparam>
    public interface IIterable<out T>
    {
        /// <summary>
        /// Gets an enumerator instance used to enumerate over the iterable.
        /// </summary>
        /// <returns>An iterator used to enumerate over the iterable.</returns>
        IIterator<T> GetEnumerator();
    }
}
