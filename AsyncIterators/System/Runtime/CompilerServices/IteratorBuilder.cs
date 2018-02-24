//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Type describing the shape of a base class that can be used for compilers to emit synchronous
    /// iterators for iterator-like types. See <see cref="AsyncIteratorBuilder{T}"/> for more
    /// information.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterator.</typeparam>
    public abstract class IteratorBuilder<T>
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
        /// Optional method on an iterator builder type. If present, the emitted code for the iterator
        /// should call this method for every <c>yield break</c> branch to indicate the start of cleanup
        /// operations.
        /// </summary>
        protected virtual void OnDisposing() => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// Optional method on an iterator builder type. If present, the emitted code for the iterator
        /// should call this method upon having finished cleanup operations, regardless of whether an
        /// exception has occurred. After this method is called, the iterator will never be invoked again.
        /// </summary>
        protected virtual void OnDisposed() => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// Optional method on an iterator builder type. If present, the emitted code for the iterator
        /// should call this method for every <c>yield return</c> site to check whether to return the
        /// element to the caller, or to proceed to perform a <c>yield break</c>.
        /// </summary>
        /// <returns>
        /// <c>true</c> to proceed to perform the <c>yield return</c>; <c>false</c> to proceed to perform
        /// a <c>yield break</c> from the current iterator state.
        /// </returns>
        /// <remarks>
        /// If this method is present, the builder wants to be able to break from an iterator early,
        /// for example to support an iterator type that can observe cancellation or disposal. By
        /// using this method, an extra roundtrip through <see cref="TryMoveNext"/> can be avoided.
        /// </remarks>
        protected virtual bool ShouldYieldReturn() => throw new NotImplementedException("Implemented by platform.");

        /// <summary>
        /// Generated code for the iterator should override this method to implement the iterator logic
        /// using a state machine. This method gets called for each advancement of the iterator, due to
        /// calls to the <see cref="IIterator{T}"/> methods.
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
        /// <c>yield return</c> statement. A subsequent call to <see cref="IIterator{T}.MoveNext"/> should
        /// return <c>true</c> and the value should be available via <see cref="IIterator{T}.Current"/>.
        /// If <c>false</c>, the iterator has run to the end (<c>yield break</c>) and a subsequent call to
        /// <see cref="IIterator{T}.MoveNext"/> should return <c>false</c>.
        /// </param>
        /// <param name="nextState">
        /// The state to pass to the <see cref="TryMoveNext"/> when resuming the iterator the next time.
        /// </param>
        /// <returns>
        /// If <paramref name="hasNext"/> is set to <c>true</c>, the value that was produced by the
        /// iterator due to the evaluation of a <c>yield return</c> statement. Otherwise, a default value.
        /// </returns>
        protected abstract T TryMoveNext(int state, bool shouldBreak, out bool hasNext, out int nextState);
    }
}
