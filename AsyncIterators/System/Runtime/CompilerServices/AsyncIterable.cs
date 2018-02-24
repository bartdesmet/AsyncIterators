//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Base class for async iterators implementing <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements produced by the iterator.</typeparam>
    /// <remarks>
    /// This type is used by compilers to implement iterators.
    /// </remarks>
    public abstract class AsyncIterable<T, TIterable> : AsyncIterator<T>, IAsyncEnumerable<T>
        where TIterable : AsyncIterable<T, TIterable>
    {
        /// <summary>
        /// The identifier of the thread that created the first instance of the iterator, used to
        /// detect whether the instance can be used for enumeration, or whether a clone should be
        /// made due to concurrent enumeration initiation.
        /// </summary>
        /// <see cref="GetAsyncEnumerator"/>
        /// <see cref="Clone"/>
        private int __initialThreadId;

        /// <summary>
        /// Creates a new instance of the iterator.
        /// </summary>
        protected AsyncIterable()
        {
            __initialThreadId = Environment.CurrentManagedThreadId;
            __state = Created;
        }

        /// <summary>
        /// Gets an asynchronous enumerator to start enumeration over the sequence. Code in the iterator
        /// does not start running upon making this call, and is deferred until a subsequent call is made
        /// to either the <see cref="IAsyncEnumerator{T}.WaitForNextAsync"/> method (recommended) or the
        /// <see cref="IAsyncEnumerator{T}.TryGetNext"/> method.
        /// </summary>
        /// <returns>An asynchronous enumerator to enumerate over the sequence.</returns>
        /// <remarks>
        /// If the call to this method takes place on the original thread that created the iterator
        /// instance, and it has not  yet been used for an earlier enumeration, the current instance is
        /// returned. Otherwise, a clone of the object is made by calling <see cref="Clone"/>
        /// </remarks>
        public IAsyncEnumerator<T> GetAsyncEnumerator()
        {
            if (__state == Created && __initialThreadId == Environment.CurrentManagedThreadId)
            {
                __state = Running;
                return this;
            }
            else
            {
                TIterable clone = Clone();
                clone.__state = Running;
                return clone;
            }
        }

        /// <summary>
        /// Creates a fresh copy of the iterator. See remarks on <see cref="GetAsyncEnumerator"/>.
        /// </summary>
        /// <returns>A fresh copy of the iterator in the initial state.</returns>
        protected abstract TIterable Clone();
    }
}
