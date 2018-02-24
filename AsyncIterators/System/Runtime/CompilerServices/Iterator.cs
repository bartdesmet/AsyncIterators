//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Base class for iterators implementing <see cref="IEnumerator{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements produced by the iterator.</typeparam>
    /// <remarks>
    /// This type is used by compilers to implement iterators.
    /// </remarks>
    public abstract class Iterator<T> : IEnumerator<T>  // NB: Obeys the shape of IteratorBuilder<T>.
    {
        /// <summary>
        /// Initial state for a fresh iterator instance, used by <see cref="Iterable{T, TIterable}"/>
        /// to detect when to create a clone of the iterator.
        /// </summary>
        private protected const int Created = -1;

        /// <summary>
        /// State indicating that the iterator instance is running. This is the initial state for enumerator
        /// iterators, and the state used by <see cref="Iterable{T, TIterable}"/> when kicking off the
        /// enumeration over the enumerable sequence.
        /// </summary>
        protected const int Running = 0;

        /// <summary>
        /// State indicating whether the iterator has been disposed, set by <see cref="OnDisposing"/> when
        /// the iterator implementation indicates that cleanup has been initiated, in order to ensure
        /// idempotent behavior. The <see cref="Dispose"/> method sets and checks this flag, too.
        /// </summary>
        /// <remarks>
        /// The value of <see cref="int.MinValue"/> is used to to avoid conflicting with states generated
        /// by the C# compiler when emitting states for synchronous iterators, which use consecutive
        /// integer positive and negative values starting from <c>0</c>.
        /// </remarks>
        protected const int Disposed = int.MinValue;

        /// <summary>
        /// The current state of the iterator.
        /// </summary>
        private protected int __state = Running;

        /// <summary>
        /// Gets the latest element produced by the iterator.
        /// </summary>
        public T Current { get; private set; }

        /// <summary>
        /// See <see cref="Current"/>.
        /// </summary>
        object IEnumerator.Current => Current;

        /// <summary>
        /// Disposes the iterator and triggers cleanup operations to run if the iterator has been started
        /// but has not yet run cleanup operations through a <c>yield break</c> statement.
        /// </summary>
        public void Dispose()
        {
            if (__state == Disposed)
            {
                return;
            }

            if (__state == Running)
            {
                __state = Disposed;
                return;
            }

            int state = __state;
            __state = Disposed;

            _ = TryMoveNext(state, shouldBreak: true, out _, out _);
        }

        /// <summary>
        /// Advances the iterator to the next element, if any.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the iterator was successfully advanced to the next element; <c>false</c> if
        /// the iterator has terminated and no more elements will be returned.
        /// </returns>
        public bool MoveNext()
        {
            if (__state == Disposed)
            {
                return false;
            }

            T result = TryMoveNext(__state, shouldBreak: false, out bool hasNext, out __state);

            if (hasNext)
            {
                Current = result;
            }
            else
            {
                __state = Disposed;
            }

            return hasNext;
        }

        //
        // NB: Synchronous iterator implementations for IEnumerable<T> and IEnumerator<T> do not require
        //     the OnDisposed and ShouldYieldReturn methods.
        //

        /// <summary>
        /// Called by the iterator body when starting to execute the cleanup logic associated with
        /// termination of the iterator (<c>yield break</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method should be called prior to starting any cleanup logic and is used by the base class
        /// to ensure that such cleanup logic can only be run once. This method should be called exactly
        /// once during the lifetime of a (finite) iterator. If the cleanup logic involves asynchronous
        /// operations, any branches used to resume execution after completion of such an operation should
        /// skip over calls to this method.
        /// </para>
        /// </remarks>
        protected void OnDisposing()
        {
            __state = Disposed;
        }

        /// <summary>
        /// Overridden by derived classes to provide the implementation of the iterator body. This method
        /// gets called for each advancement of the iterator, due to calls to the <see cref="IEnumerator{T}"/>
        /// methods.
        /// </summary>
        /// <param name="state">
        /// The state to resume the iterator from. For the initial call to the iterator, this value is set
        /// to <see cref="Running"/>, or <c>0</c> if no such value is defined.
        /// </param>
        /// <param name="shouldBreak">
        /// If <c>true</c>, the iterator should perform a <c>yield break</c> immediately.
        /// </param>
        /// <param name="hasNext">
        /// Value denoting the result of resuming the iterator. If <c>true</c>, a value was produced by a
        /// <c>yield return</c> statement. A subsequent call to <see cref="IEnumerator.MoveNext"/> should
        /// return <c>true</c> and the value should be available via <see cref="IEnumerator{T}.Current"/>.
        /// If <c>false</c>, the iterator has run to the end (<c>yield break</c>) and a subsequent call to
        /// <see cref="IEnumerator.MoveNext"/> should return <c>false</c>.
        /// </param>
        /// <param name="nextState">
        /// The state to pass to the <see cref="TryMoveNext"/> when resuming the iterator the next time.
        /// </param>
        /// <returns>
        /// If <paramref name="hasNext"/> is set to <c>true</c>, the value that was produced by the
        /// iterator due to the evaluation of a <c>yield return</c> statement. Otherwise, a default value.
        /// </returns>
        protected abstract T TryMoveNext(int state, bool shouldBreak, out bool hasNext, out int nextState);

        /// <summary>
        /// Resets the iterator.
        /// </summary>
        void IEnumerator.Reset() => throw new NotSupportedException();
    }
}
