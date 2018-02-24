//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections.Generic; // NB: Used in XML doc comments.
using System.Security;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Type describing the shape of a base class that can be used for compilers to emit asynchronous
    /// iterators for iterator-like types. See <see cref="AsyncIterableBuilder{T, TIterator}"/> for more
    /// information.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterator.</typeparam>
    public abstract class AsyncIteratorBuilder<T>
    {
        /// <summary>
        /// Optional constant on an iterator builder type. If present, the emitted code for the iterator
        /// can make the assumption that the first call to <see cref="TryMoveNext"/> will have this state
        /// value passed, and the iterator can use an increasing sequence of values up to and including
        /// <see cref="MaxState"/>. If not present, the iterator has no restrictions on state values.
        /// </summary>
        protected const int Running = 0; // NB: example value

        /// <summary>
        /// Optional constant on an iterator builder type. If present, the iterator should use state
        /// values in the range [<see cref="Running"/>, <see cref="MaxState"/>] and return state values
        /// in this range from <see cref="TryMoveNext"/>.
        /// </summary>
        protected const int MaxState = int.MaxValue; // NB: example value

        /// <summary>
        /// See <see cref="AsyncIterator{T}.AwaitOnCompleted"/> for more information.
        /// </summary>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <param name="nextState">
        /// The iterator state to resume from after completion of the pending asynchronous operation.
        /// </param>
        /// <param name="awaiter">
        /// The awaiter representing the pending asynchronous operation.
        /// </param>
        protected virtual void AwaitOnCompleted<TAwaiter>(int nextState, ref TAwaiter awaiter) where TAwaiter : INotifyCompletion => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// See <see cref="AsyncIterator{T}.AwaitUnsafeOnCompleted"/> for more information.
        /// </summary>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <param name="nextState">
        /// The iterator state to resume from after completion of the pending asynchronous operation.
        /// </param>
        /// <param name="awaiter">
        /// The awaiter representing the pending asynchronous operation.
        /// </param>
        [SecuritySafeCritical]
        protected virtual void AwaitUnsafeOnCompleted<TAwaiter>(int nextState, ref TAwaiter awaiter) where TAwaiter : ICriticalNotifyCompletion => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// See <see cref="AsyncIterator{T}.OnDisposing"/> for more information.
        /// </summary>
        protected virtual void OnDisposing() => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// See <see cref="AsyncIterator{T}.OnDisposed"/> for more information.
        /// </summary>
        protected virtual void OnDisposed() => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// See <see cref="AsyncIterator{T}.ShouldYieldReturn"/> for more information.
        /// </summary>
        protected virtual bool ShouldYieldReturn() => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// Overridden by derived classes to provide the implementation of the iterator body. This method
        /// gets called for each advancement of the iterator, either due to completion of asynchronous
        /// operations (<c>await</c>) or due to calls to the <see cref="IAsyncEnumerator{T}"/> methods.
        /// </summary>
        /// <param name="state">
        /// The state to resume the iterator from. For the initial call to the iterator, this value is set
        /// to <see cref="Running"/>.
        /// </param>
        /// <param name="shouldBreak">
        /// If <c>true</c>, the iterator should perform a <c>yield break</c> immediately, if the current
        /// <paramref name="state"/> represents a resumption point of a <c>yield return</c> site, or after
        /// having evaluated the operand to the subsequent <c>yield return</c> statement otherwise. This
        /// case occurs when the iterator is resumed due to the completion of asynchronous work.
        /// </param>
        /// <param name="hasNext">
        /// Value denoting the result of resuming the iterator. If <c>true</c>, a value was produced by a
        /// <c>yield return</c> statement and should be made available to the consumer. If <c>false</c>,
        /// the iterator has run to the end (<c>yield break</c>). If <c>null</c>, the iterator is yielding
        /// execution after having initiated an asynchronous operation that has not yet been completed.
        /// </param>
        /// <param name="nextState">
        /// The state to pass to the <see cref="TryMoveNext"/> when resuming the iterator the next time.
        /// This value is ignored when <paramref name="hasNext"/> returns <c>null</c>; in this case, the
        /// next state passed to <see cref="AwaitOnCompleted"/> or <see cref="AwaitUnsafeOnCompleted"/> is
        /// used instead, because the iterator can already have resumed on a different thread.
        /// </param>
        /// <returns>
        /// If <paramref name="hasNext"/> is set to <c>true</c>, the value that was produced by the
        /// iterator due to the evaluation of a <c>yield return</c> statement. Otherwise, a default value.
        /// </returns>
        protected abstract T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState);
    }
}
