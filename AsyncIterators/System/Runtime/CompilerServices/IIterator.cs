//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Interface describing the shape of an iterator-like type. Such types are used by the <c>foreach</c>
    /// statement (in conjunction with the corresponding <see cref="IIterable{T}"/> type) and can be
    /// returned from a synchronous iterator method.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterator.</typeparam>
    public interface IIterator<out T>
    {
        /// <summary>
        /// Advances the iterator to the next element, if any.
        /// </summary>
        /// <returns><c>true</c> if an element is found; otherwise, <c>false</c>.</returns>
        bool MoveNext();

        /// <summary>
        /// Gets the current element.
        /// </summary>
        T Current { get; }

        /// <summary>
        /// Optional method to perform cleanup operations.
        /// </summary>
        void Dispose();
    }
}
