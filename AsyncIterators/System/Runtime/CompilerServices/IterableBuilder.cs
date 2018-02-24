//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Type describing the shape of a base class that can be used for compilers to emit synchronous
    /// iterators for iterable-like types. See <see cref="AsyncIterableBuilder{T, TIterator}"/> for
    /// more information.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterator.</typeparam>
    /// <typeparam name="TIterator">The type of the derived iterator type.</typeparam>
    public abstract class IterableBuilder<T, TIterator> : IteratorBuilder<T>
    {
        /// <summary>
        /// Generated code for the iterator should override this method to create a clone of the
        /// iterator instance.
        /// </summary>
        /// <returns>A clone of the iterator in an initialized but non-started state.</returns>
        protected abstract TIterator Clone();
    }
}
