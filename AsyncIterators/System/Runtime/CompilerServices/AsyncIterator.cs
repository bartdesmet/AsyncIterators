//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Base class for async iterators implementing <see cref="IAsyncEnumerator{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements produced by the iterator.</typeparam>
    /// <remarks>
    /// This type is used by compilers to implement iterators.
    /// </remarks>
    public abstract class AsyncIterator<T> : IAsyncEnumerator<T>, IAsyncStateMachine  // NB: Obeys the shape of AsyncIteratorBuilder<T>.
    {
        //
        // <WARNING>
        // When adding a flag here, keep the IteratorState enum in sync (see bottom of file).
        //

        private const int StateFlagMask = 0b________0111_1111_1111_1111__0000_0000_0000_0000;  // Bitmask for state flags maintained by the base class.
        private const int StartedFlag = 0b__________0100_0000_0000_0000__0000_0000_0000_0000;  // 0->1     Iterator has been started; i.e. a call to WaitForMoveNextAsync or TryGetNext was made.
        private const int IsBusyFlag = 0b___________0010_0000_0000_0000__0000_0000_0000_0000;  // 0->1->0  Iterator has been started, has not yet been disposed, and is not suspended after a yield statement.
        private const int HasResultFlag = 0b________0001_0000_0000_0000__0000_0000_0000_0000;  // 0->1->0  Iterator has yielded a result, stored in __current, and ready to be fetched by TryGetNext.
        private const int HasBuilderFlag = 0b_______0000_1000_0000_0000__0000_0000_0000_0000;  // 0->1     Iterator has an async method builder in __builder, used to report outcome of async operations.
        private const int AsyncPendingFlag = 0b_____0000_0100_0000_0000__0000_0000_0000_0000;  // 0->1->0  Iterator has triggered an async operation that is pending completion.
        private const int DisposeRequestedFlag = 0b_0000_0010_0000_0000__0000_0000_0000_0000;  // 0->1     Iterator has received a call to DisposeAsync.
        private const int DisposingFlag = 0b________0000_0001_0000_0000__0000_0000_0000_0000;  // 0->1     Iterator has initiated cleanup operations, either by a call to DisposeAsync, or by branching to yield break.
        private const int DisposedFlag = 0b_________0000_0000_1000_0000__0000_0000_0000_0000;  // 0->1     Iterator has completed cleanup operations.
        private const int DisposeReportedFlag = 0b__0000_0000_0100_0000__0000_0000_0000_0000;  // 0->1     Iterator has handed out the task used to report the outcome of disposal.
        private const int ReservedFlags = 0b________0000_0000_0011_1111__0000_0000_0000_0000;  // Bitmask for reserved state flags for future use.

        //
        // </WARNING>
        //

        /// <summary>
        /// Initial state for a fresh iterator instance, used by <see cref="AsyncIterable{T, TIterable}"/>
        /// to detect when to create a clone of the iterator.
        /// </summary>
        private protected const int Created = -1;

        /// <summary>
        /// State indicating that the iterator instance is running. This is the initial state for enumerator
        /// iterators, and the state used by <see cref="AsyncIterable{T, TIterable}"/> when kicking off the
        /// enumeration over the enumerable sequence.
        /// </summary>
        protected const int Running = 0;

        /// <summary>
        /// The maximum state value that can be used by the compiler-generated iterator code. See the state
        /// parameters on <see cref="TryMoveNext"/> and <see cref="AwaitOnCompletedCore"/>.
        /// </summary>
        protected const int MaxState = ushort.MaxValue; // NB: See StateFlagMask and AssertValidState.

        /// <summary>
        /// Lock object used to protect changes <see cref="__state"/>.
        /// </summary>
        /// <remarks>
        /// State transitions may be possible to implement using Interlocked operations alone. Right now,
        /// the lock protects a few other field assignments as well.
        /// </remarks>
        private readonly object __lock = new object();

        /// <summary>
        /// The current state of the iterator. The bits in <see cref="StateFlagMask"/> are managed by the
        /// base class; the lower 16 bits (see <see cref="MaxState"/>) represent the state provided by the
        /// iterator implementation in the derived class.
        /// </summary>
        private protected int __state = Running;

        /// <summary>
        /// The latest value yielded by the iterator but not yet been fetched by <see cref="TryGetNext"/>.
        /// </summary>
        /// <remarks>
        /// Only valid when the <see cref="HasResultFlag"/> flag is set.
        /// </remarks>
        private T __current;

        /// <summary>
        /// Async method builder for the ongoing async operation, if any. This builder's task is returned
        /// from async methods, including <see cref="DisposeAsync"/> (see state transitions involving
        /// <see cref="DisposeRequestedFlag"/> and <see cref="DisposeReportedFlag"/>).
        /// </summary>
        /// <remarks>
        /// Only valid when the <see cref="HasBuilderFlag"/> flag is set.
        /// </remarks>
        private AsyncTaskMethodBuilder<bool> __builder;

        /// <summary>
        /// Attempts to advance the iterator and to synchronously retrieve the next element. The operation
        /// reports its outcome through the <paramref name="success"/> output parameter, or by throwing an
        /// exception.
        /// <para>
        /// An element was returned, as indicated by <paramref name="success"/> returning <c>true</c>. The
        /// consumer can proceed to call <see cref="TryGetNext"/> to attempt to synchronously retrieve the
        /// next element. It is valid to proceed iteration by calling <see cref="WaitForNextAsync"/>, but
        /// synchronous consumption is recommended. It is valid to call <see cref="DisposeAsync"/> at this
        /// point to dispose the iterator.
        /// </para>
        /// <para>
        /// The end of the iterator was reached, and <paramref name="success"/> returned <c>false</c>. All
        /// cleanup operations will have been run. Subsequent calls to <see cref="WaitForNextAsync"/> will
        /// return <c>false</c>. Subsequent calls to <see cref="TryGetNext"/> will report <c>false</c>.
        /// Subsequent calls to <see cref="DisposeAsync"/> with have no side-effects and return success.
        /// </para>
        /// <para>
        /// Asynchronous work has been triggered, with an unknown outcome, and <paramref name="success"/>
        /// returned <c>false</c>. The consumer should proceed to call <see cref="WaitForNextAsync"/> and
        /// await the outcome of the asynchronous advancement of the iterator. Alternatively, the consumer
        /// can proceed to call <see cref="DisposeAsync"/>, which will trigger cleanup operations to run
        /// immediately after finishing the asynchronous advancement of the iterator. The outcome of the
        /// cleanup will be reported by the task returned from <see cref="DisposeAsync"/>. In this state,
        /// the result of calling <see cref="TryGetNext"/> is undefined; asynchronous methods should be
        /// used to await the outcome of the pending asynchronous operation.
        /// </para>
        /// <para>
        /// An exception occurred while executing synchronous iterator code. All cleanup operations will
        /// have been run, or a synchronous exception during cleanup has masked the original error and was
        /// propagated instead. Subsequent calls to <see cref="DisposeAsync"/> with have no side-effects
        /// and return success. If asynchronous work is triggered during cleanup, this method will return
        /// <c>false</c> on <paramref name="success"/>, as described in the previous case.
        /// </para>
        /// <para>
        /// A possible consumption sequence of an asynchronous iterator is shown below, including the use
        /// of a cancellation token to illustrate valid places to break from the enumeration and trigger
        /// cleanup operations:
        /// <code>
        /// async Task ConsumeAsync{T}(IAsyncEnumerator{T} source, Action{T} use, CancellationToken token)
        /// {
        ///     try
        ///     {
        ///         while (!token.IsCancellationRequested && await source.WaitNextAsync())
        ///         {
        ///             while (!token.IsCancellationRequested)
        ///             {
        ///                 T result = source.TryGetNext(out bool success);
        /// 
        ///                 if (!success)
        ///                 {
        ///                     break;
        ///                 }
        /// 
        ///                 use(result);
        ///             }
        ///         }
        ///     }
        ///     finally
        ///     {
        ///         await source.DisposeAsync();
        ///     }
        /// }
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="success"><c>true</c> if an element was returned; otherwise, <c>false</c>.</param>
        /// <returns>
        /// An element returned by the iterator, if <paramref name="success"/> is <c>true</c>; otherwise,
        /// a default value.
        /// </returns>
        public T TryGetNext(out bool success)
        {
            int state;

            lock (__lock)
            {
                GetFlagsAndState(out int flags, out state);

                //
                // Handle two dispose cases first.
                //
                // 1. If the iterator has been disposed, we keep returning false. Note this covers both the
                //    cases where the user initiated a DisposeAsync operation and where the iterator caused
                //    disposal itself (and finished the cleanup sequence).
                //
                // 2. If a call to DisposeAsync was ever made, the user was in control. After an explicit
                //    call to this method, we keep returning false. Note DisposeRequestedFlag stays set.
                //
                if ((flags & (DisposedFlag | DisposeRequestedFlag)) != 0)
                {
                    success = false;
                    return default;
                }

                //
                // If the iterator is disposing, the only remaining case is where the iterator triggered
                // the disposal itself. This is always the result of the user having advanced the iterator,
                // which may cause pending async work. Either way, the user is making concurrent calls to
                // TryGetNext or is not awaiting the outcome of async pending work through WaitForNextAsync
                // or by calling DisposeAsync. All these call sequences are invalid.
                //
                // Alternatively, we could return false here as well, and fold this case into the check
                // above. This would enable a consumer to violate the call sequence and call TryGetNext
                // even after a previous call to the method has returned false. By returning false, the
                // consumer is expected to rendez-vous on an async method to observe the eventual outcome.
                // Any exceptions due to the disposal that was triggered will come out there, and keeping
                // to call TryGetNext despite it having returned false will not make forward progress, so
                // we are quite strict about the consumption call sequence right now.
                //
                if ((flags & DisposingFlag) != 0)
                {
                    throw UnexpectedFlagIsSet(flags, DisposingFlag);
                }

                //
                // The iterator is busy and has not yet reached a "yield" statement suspension point. The
                // user is trying to make concurrent calls or is not properly consuming sequentially, so we
                // report an error.
                //
                if ((flags & IsBusyFlag) != 0)
                {
                    throw IteratorBusy();
                }

                //
                // The iterator reached a "yield return" statement and deposited a result that has to be
                // returned to the user. We return this result and turn off the flag, so a subsequent call
                // to this method can advance the iterator.
                //
                if ((flags & HasResultFlag) != 0)
                {
                    __state &= ~HasResultFlag;

                    T current = __current;
                    __current = default;
                    success = true;
                    return current;
                }

                //
                // If an async operation is found to be pending, the user is not properly consuming the
                // sequence, so we report an error.
                //
                if ((flags & AsyncPendingFlag) != 0)
                {
                    throw UnexpectedFlagIsSet(flags, AsyncPendingFlag);
                }

                //
                // This is the first time the iterator gets advanced. We assume it it valid to kick off the
                // iterator through TryGetNext, which may result in pending async work that will be awaited
                // by the consumer in the subsequent call to WaitForNextAsync (or DisposeAsync, also
                // triggering the cleanup sequence). Toggle the flag and continue.
                //
                if ((flags & StartedFlag) == 0)
                {
                    __state |= StartedFlag;
                }

                //
                // We are going to kick off the iterator, so we set the busy flag, which will be unset when
                // a "yield" statement is encountered.
                //
                __state |= IsBusyFlag;
            }

            T result;
            bool? hasNext = default;
            int nextState = -1;
            bool hasFaulted = true;

            //
            // Call the async iterator. We never ask it to break here; if a dispose operation was requested,
            // our checks higher up should have caught that. The following outcomes are possible:
            //
            // 1. An exception is thrown. The iterator was able to propagate an exception to the caller in
            //    a synchronous manner, which implies that a well-written iterator has also triggered all
            //    the cleanup logic needed, and then propagated the original exception or any exception
            //    that superseded it (e.g. thrown by a finally block). We will assert having crossed the
            //    disposing state, and ensure we end up in the disposed state.
            //
            // 2. A return value of true is reported on hasNext. We can "yield return" the value to our
            //    caller but need keep track of the successor state requested by the iterator in order to
            //    serve subsequent requests to advance the iterator.
            //
            // 3. A return value of false is reported on hasNext. No more values will ever be yielded
            //    because we have reached the end of the iterator. Disposal should have taken place.
            //
            // 4. A return value of null is reported on hasNext. The outcome of the TryGetNext call is
            //    inconclusive, and async work was triggered but is still pending. The iterator is still
            //    busy and its outcome can be awaited via the async method builder.
            //
            try
            {
                result = TryMoveNext(state, shouldBreak: false, out hasNext, out nextState);
                hasFaulted = false;
            }
            catch when (AssertDisposeTriggered()) { throw; /* NB: Only to satisfy definite assignment. */ }
            finally
            {
                lock (__lock)
                {
                    GetFlagsAndState(out int oldFlags, out var oldState);

                    int newFlags = oldFlags; // NB: Kept for asserts.

                    //
                    // Assert that we didn't advance the iterator somewhere else, too. That would be a
                    // broken invariant in our machinery. If async work was pending at the start of
                    // calling this method, we should have returned. If async work was left pending by
                    // the call to TryMoveNext, we should not check for this condition because such
                    // activity can change the iterator state at any time.
                    //
                    if (hasNext != null && oldState != state)
                    {
                        throw StateCorruptionDetected(oldState, state);
                    }

                    //
                    // If we faulted, a synchronous exception escaped, and we asserted that disposal was
                    // triggered correctly. However, cleanup logic itself may have thrown, failing to
                    // reach the call to OnDisposed. We don't want to cause any form of double dispose
                    // that is not idempotent, so we toggle the disposed flag here.
                    //
                    if (hasFaulted)
                    {
                        newFlags |= DisposedFlag;

                        //
                        // We can't just set nextState to state and assume it will be left unchanged,
                        // because it gets passed to an out parameter. So, we copy it here.
                        //
                        nextState = state;
                    }

                    //
                    // If we do not find pending async work, the outcome of the TryMoveNext call was
                    // conclusive (i.e. a yield return or a yield break) and the iterator is no longer
                    // busy. Unset the flag. Note this is okay for the faulted case as well.
                    //
                    if (hasNext != null)
                    {
                        newFlags &= ~IsBusyFlag;
                    }
                    else
                    {
                        //
                        // If async pending work was left behind by the iterator, it should have caused an
                        // async method builder to be created in Await[Unsafe]OnCompleted, in order for
                        // the subsequent call to WaitForNextAsync to pick up on the pending async work.
                        // Assert this invariant here. If we are throwing an exception, this invariant
                        // does not have to hold.
                        //
                        if (!hasFaulted && (oldFlags & HasBuilderFlag) == 0)
                        {
                            throw ExpectedFlagIsNotSet(oldFlags, HasBuilderFlag);
                        }
                    }

                    //
                    // If we detect that a dispose request was made through a call to DisposeAsync while
                    // we were executing the TryGetNext call, we raise an error because the consumer is
                    // violating the call sequence.
                    //
                    // NB: This behavior differs in the IAsyncStateMachine.MoveNext implementation, where
                    //     we may reenter to do completion of async pending work, and the consumer may
                    //     have called DisposeAsync after obtaining false from its call to TryGetValue.
                    //     In that code path, we grant the dispose request and resume the iterator with
                    //     the shouldBreak parameter set to true. If we deem a call to DisposeAsync valid
                    //     while a TryGetNext operation is in flight, we could grant the dispose request
                    //     here as well (as descrived above), which may cause pending async work.
                    //
                    // REVIEW: Can this case be left undefined instead?
                    //
                    if ((oldFlags & DisposeRequestedFlag) != 0)
                    {
                        throw UnexpectedFlagIsSet(oldFlags, DisposeRequestedFlag);
                    }

                    //
                    // If TryMoveNext signaled execution of asynchronous work, we should keep our hands
                    // off of the iterator state, because it may reenter and change the state in the call
                    // to IAsyncStateMachine.MoveNext. The only exception is the case where a fault
                    // occurred, and we don't expect any remaining activity in iterator code because a
                    // failure in Await[Unsafe]OnCompleted should prevent the continuation from getting
                    // scheduled. Well-written iterators should immediately return to the caller after
                    // scheduling such asynchronous work, so no exceptions are expected beyond this point.
                    //
                    if (hasNext != null || hasFaulted)
                    {
                        //
                        // Assert the state requested by the iterator is within bounds before committing it.
                        //
                        AssertValidState(nextState);

                        //
                        // Combine the next state and the flags, and set it.
                        //
                        __state = nextState | newFlags;
                    }
                    else
                    {
                        //
                        // If async pending work is happening (and we didn't presumably fail to kick it
                        // off), we don't expect any state changes here.
                        //
                        Debug.Assert(newFlags == oldFlags);
                    }
                }
            }

            //
            // Return whatever the iterator told us to return, if we even get here. If we didn't receive a
            // conclusive outcome from TryMoveNext, we report false to trigger a call to WaitForNextAsync
            // by the consumer (or a call to DisposeAsync if the consumer wants to bail out).
            //
            success = hasNext ?? false;
            return result;

            //
            // Helper method to assert proper disposal in case of an exception thrown by TryMoveNext.
            //
            bool AssertDisposeTriggered()
            {
                lock (__lock)
                {
                    GetFlagsAndState(out int flags, out _);

                    //
                    // If we don't find the disposing flag set (note it never gets unset), the iterator is
                    // not implemented correctly. An exception occurring in the iterator should trigger
                    // cleanup operations, and we should have been notified on OnDisposing.
                    //
                    // NB: It may be undesirable to require all iterators to emit a call to OnDisposing
                    //     even if they don't have any cleanup logic. We could remove this check.
                    //
                    if ((flags & DisposingFlag) == 0)
                    {
                        throw ExpectedFlagIsNotSet(flags, DisposingFlag);
                    }
                }

                //
                // Continue to propagate the exception.
                //
                return false;
            }
        }

        /// <summary>
        /// Advances the iterator to asynchronously retrieve the next element. For a detailed description
        /// of consuming an asynchronous iterator and valid call sequences, see <see cref="TryGetNext"/>.
        /// </summary>
        /// <returns>
        /// A task representing the eventual outcome of advancing the iterator, or an exception. If the
        /// task returns <c>true</c>, the iterator has returned an element that can be retrieved by
        /// calling <see cref="TryGetNext"/>. If the task returns <c>false</c>, the iterator has reached
        /// the end, and cleanup operations have been run.
        /// </returns>
        public Task<bool> WaitForNextAsync()
        {
            lock (__lock)
            {
                GetFlagsAndState(out int flags, out int state);

                //
                // See TryGetNext for this case.
                //
                if ((flags & (DisposedFlag | DisposeRequestedFlag)) != 0)
                {
                    return Task.FromResult(false);
                }

                //
                // If we find the iterator in the disposing state, without evidence of the user having
                // called DisposeAsync (which is handled by the case above), the iterator is self-
                // disposing because it encountered some error, but has not yet finished the cleanup.
                //
                // This can occur in the following cases:
                //
                // 1. A prior call to TryGetNext triggered pending async work, and the consumer is now
                //    awaiting the outcome. This is as if the async work was triggered by the current
                //    call advancing the iterator. Given the self-disposing nature of iterators, we are
                //    the conveyor of the bad news and the subsequent call to DisposeAsync will just
                //    return a completed task once we got beyond this point.
                //
                // 2. The consumer made a call to WaitForNextAsync but didn't await its outcome, and
                //    this prior invocation triggered cleanup operations. This can be deemed an invalid
                //    call sequence, so we are fine to pick some convenient behavior.
                //
                // In the first legitimate case, the eventual outcome of the cleanup triggered by the
                // pending async work will be reported on the async method builder's task, and we will
                // never swap out this builder for a new one anymore. In case of successful termination
                // of the iterator ("yield break"), the task will eventually have value false, which is
                // what we want here. In case of exceptional termination, the task will be faulted.
                //
                // After a TryGetNext call that leaves async work pending, either WaitForNextAsync or
                // DisposeAsync will observe the eventual outcome of such work. Only one of them should
                // throw an exception (if any), so we use an ownership flag. Note that this also happens
                // to produce acceptable behavior for the second case mentioned above.
                //
                if ((flags & DisposingFlag) != 0)
                {
                    if ((flags & DisposeReportedFlag) != 0)
                    {
                        __state |= DisposeReportedFlag;
                        return __builder.Task;
                    }
                    else
                    {
                        return Task.FromResult(false);
                    }
                }

                //
                // A previous result was deposited by a "yield return" statement but never was consumed
                // through a call to TryGetValue yet. We return true and force the consumer to pick up
                // the value by making a call to TryGetValue, which will reset this flag and allow to
                // advance the iterator after the value has been consumed. Note this situation can arise
                // when TryGetValue leaves async work pending, returns false to the caller, and by the
                // time the consumer calls WaitForNextAsync, the work has completed and the async pending
                // flag has been unset.
                //
                // REVIEW: Are there any cases remaining where the async pending flag gets unset and none
                //         of the checks here pass? If the iterator reached a yield break statement or
                //         failed, it should also set the dispose flags, so we pick up the result in the
                //         previous statement.
                //
                if ((flags & HasResultFlag) != 0)
                {
                    return Task.FromResult(true);
                }

                //
                // If an async pending operation is in progress, it will report the eventual outcome on
                // the async method builder task. Just return that task. Note this allows many calls to
                // WaitForNextAsync to each observe the task's outcome. If many outstanding calls to this
                // method are allowed, this behavior may be undesirable, because all of them can observe
                // a failure that triggered disposal; none of them can be the sole conveyor of such an
                // exception because the returned task instance is shared. Alternatively, we could reject
                // multiple calls to WaitForNextAsync by using an ownership flag for reporting of the
                // outcome of a pending async operation, and throw from calls that fail to set this flag
                // (for sure, we can't return Task.FromResult(false) or so).
                //
                if ((flags & AsyncPendingFlag) != 0)
                {
                    return __builder.Task;
                }

                //
                // We can only check for the busy flag after checking for pending asynchronous work.
                //
                if ((flags & IsBusyFlag) != 0)
                {
                    throw IteratorBusy();
                }

                //
                // This is the first time the iterator gets advanced. Toggle the flag and prepare an
                // async method builder that will be used to report the eventual outcome of the first run
                // through the iterator we're about to kick off.
                //
                if ((flags & StartedFlag) == 0)
                {
                    __state |= StartedFlag;

                    //
                    // Check for a broken invariant in our machinery. We should be the ones initializing
                    // a fresh builder instance.
                    //
                    if ((flags & HasBuilderFlag) != 0)
                    {
                        throw UnexpectedFlagIsSet(flags, HasBuilderFlag);
                    }

                    //
                    // Prepare a builder to report the outcome of the first "yield" statement to (or an
                    // exception that has triggered early cleanup).
                    //
                    __state |= HasBuilderFlag;
                    __builder = AsyncTaskMethodBuilder<bool>.Create();

                    //
                    // Mark the iterator as busy. This flag will be unset when the iterator yields.
                    //
                    __state |= IsBusyFlag;
                }
                else
                {
                    //
                    // The only case where we didn't return early to the caller is the case where we have
                    // to kick off the iterator for the first time. If we got here, an unexpected state
                    // was encountered, so we throw.
                    //
                    throw UnexpectedWaitForNextAsyncCall();
                }
            }

            //
            // Prepare the task on the builder, start the iterator, and return the task.
            //
            Task<bool> task = __builder.Task;

            var @this = this;
            __builder.Start(ref @this);

            return task;
        }

        /// <summary>
        /// Disposes the iterator and runs required cleanup logic.
        /// <para>
        /// If the iterator has successfully run to completion (<c>yield break</c>), calling this method
        /// has no effect and returns <see cref="Task.CompletedTask"/>.
        /// </para>
        /// <para>
        /// If the iterator has completed by throwing an exception, all cleanup logic has been run, and
        /// the exception has been reported by <see cref="WaitForNextAsync"/> or <see cref="TryGetNext"/>,
        /// calling this method has no effect and returns <see cref="Task.CompletedTask"/>. This includes
        /// the case where cleanup logic threw an exception that masked the original exception.
        /// </para>
        /// <para>
        /// If the iterator is running asynchronous code when <see cref="DisposeAsync"/> is called, the
        /// request to dispose the iterator is recorded. The iterator continues running until it either
        /// reaches a <c>yield break</c> statement, or reaches a <c>yield return</c> statement and
        /// immediately proceeds to <c>yield break</c>, or encounters an exception. After any of these
        /// states is reached, cleanup logic is executed, and the eventual outcome is reported by the task
        /// returned from this method. This case occurs when <see cref="TryGetNext"/> starts asynchronous
        /// work, returns <c>false</c>, and the consumer disposes the iterator.
        /// </para>
        /// <para>
        /// If the iterator is not running when <see cref="DisposeAsync"/> is called, the iterator gets
        /// resumed and is instructed to immediately proceed to <c>yield break</c> from its current state,
        /// causing cleanup operations to run. The task returned from this method represents the eventual
        /// completion of these cleanup operations. Any exception thrown during cleanup of the iterator
        /// will be thrown from this method.
        /// </para>
        /// <para>
        /// After the iterator has been disposed, subsequent calls to <see cref="DisposeAsync"/> will
        /// return <see cref="Task.CompletedTask"/>, and subsequent calls to <see cref="TryGetNext"/> or
        /// <see cref="WaitForNextAsync"/> will return <c>false</c>.
        /// </para>
        /// </summary>
        /// <returns>A task representing the eventual completion of cleanup operations.</returns>
        public Task DisposeAsync()
        {
            int state;

            lock (__lock)
            {
                GetFlagsAndState(out int flags, out state);

                //
                // See TryGetNext for this case; idempotent behavior of DisposeAsync.
                //
                if ((flags & (DisposedFlag | DisposeRequestedFlag)) != 0)
                {
                    return Task.CompletedTask;
                }

                //
                // See WaitForNextAsync for description of reporting ownership for the eventual outcome
                // of a pending async operation that has triggered cleanup.
                //
                if ((flags & DisposingFlag) != 0)
                {
                    if ((flags & DisposeReportedFlag) != 0)
                    {
                        __state |= DisposeReportedFlag;
                        return __builder.Task;
                    }
                    else
                    {
                        return Task.CompletedTask;
                    }
                }

                //
                // If the iterator was never started, immediately mark it as disposed and return. Note that
                // we set all the flags to indicate that disposal took place, which simplifies other checks.
                // All checks for disposal states happen before checks for the started flag, so we don't
                // need to toggle this flag, and can use it to detect that the iterator ever ran.
                //
                if ((flags & StartedFlag) == 0)
                {
                    __state |=
                        DisposeRequestedFlag |  // vene: the user asked for it,
                        DisposingFlag |         // vidi: we did the work (or pretend to),
                        DisposedFlag |          // vici: we succeeded, and
                        DisposeReportedFlag;    // dixi: we told the good news

                    return Task.CompletedTask;
                }

                //
                // The iterator may be busy in one legit case that does not violate the call sequence. This
                // occurs when TryGetNext goes down an async code path, yields execution by returning false,
                // and has left async pending work. For this case, we want to hijack the iterator upon its
                // transition out of the busy state (i.e. when encountering a "yield" statement), so it will
                // immediately proceed to "yield break" from its current position, triggering cleanup. The
                // async method builder's task is repurposed to report the outcome of disposal.
                //
                if ((flags & IsBusyFlag) != 0)
                {
                    //
                    // Set the request to perform disposal. If this flag was already set, we caught it higher
                    // up and returned success to retain idempotent behavior.
                    //
                    __state |= DisposeRequestedFlag;

                    //
                    // When the dispose request flag is observed by the running iterator, it promises not to
                    // report "yield return" success outcome on the task. Instead, it makes a commitment to
                    // use the task to report the outcome of the injected cleanup. Once dispose request flag
                    // is set, the builder can no longer be changed.
                    //
                    // NB: Right now, this flag only gets picked up by code in IAsyncStateMachine.MoveNext,
                    //     and not by code in TryGetNext that runs the iterator synchronously. See comments
                    //     in that method for the rationale of this being an invalid call sequence that may
                    //     be left as undefined behavior. With the current implementation, the task may never
                    //     get completed, causing DisposeAsync to "hang". We can make TryGetNext advance the
                    //     iterator when it unsets the busy flag. See remarks over there.
                    //
                    return __builder.Task;
                }

                //
                // Otherwise, we are in charge to trigger cleanup operations. Set the flag to indicate
                // disposal is initiated, and claim ourselves as the owner for reporting of the outcome.
                //
                __state |=
                    DisposingFlag |         // we'll do it,
                    IsBusyFlag |            // we'll have to start the iterator to do it,
                    DisposeRequestedFlag |  // we won't forget we're doing it, and
                    DisposeReportedFlag;    // we'll report about it

                //
                // The iterator was not busy, but it may have a builder from a previous execution. We need
                // a fresh builder we'll use to report the outcome of the cleanup operations.
                //
                __state |= HasBuilderFlag;
                __builder = AsyncTaskMethodBuilder<bool>.Create();
            }

            //
            // If we get here, we promised to do the work. Now go do it by resuming the iterator with the
            // shouldBreak flag set to true, and return the task.
            //
            // NB: The task we use is a Task<bool>. Reaching a "yield break" point without any exceptions
            //     will result in reporting false, which is as good as Task.CompletedTask. For the case of
            //     an exception being thrown during cleanup, the resulting task will become faulted. Either
            //     way, this task is valid to return from DisposeAsync because it derives from Task.
            //
            Task<bool> task = __builder.Task;

            bool? hasNext = default;

            try
            {
                _ = TryMoveNext(state, shouldBreak: true, out hasNext, nextState: out _);

                //
                // We expect hasNext to report null or false. The first case occurs when cleanup requires
                // running async code. The second case occurs when cleanup reaches the end of the iterator.
                // Check this condition. If it fails, we got a broken invariant in our machinery or the
                // iterator code is not behaving according to spec.
                //
                // NB: We could also require iterators to return a well-known nextState after rundown, so we
                //     can assert they reached the end of the cleanup sequence (if that's of use).
                //
                if (hasNext != null)
                {
                    if (hasNext.Value)
                    {
                        throw CleanupDidNotYieldBreak();
                    }
                    else
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            catch (Exception ex)
            {
                //
                // If the iterator throws an exception, it should have performed all of the cleanup logic
                // that is required, according to the spec for the behavior of iterators. If an exception
                // still makes it here, it should be due to an exception in the cleanup logic itself. We
                // report this exception here.
                //

                return Task.FromException<bool>(ex);
            }
            finally
            {
                //
                // If we succeeded to synchronously complete the dispose operation, we can toggle the
                // disposed flag and turn off the busy flag. Note that the dispose request flag will
                // ensure idempotent behavior (see higher up). In case the dispose operation is still
                // running, we will rely on the cleanup path toggling these flags eventually.
                //
                if (hasNext != null)
                {
                    lock (__lock)
                    {
                        __state |= DisposedFlag;
                        __state &= ~IsBusyFlag;
                    }
                }
            }

            return task;
        }

        /// <summary>
        /// Schedules the iterator to resume from the specified <paramref name="nextState"/> when the
        /// specified <paramref name="awaiter"/> completes.
        /// </summary>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <param name="nextState">
        /// The iterator state to resume from after completion of the pending asynchronous operation.
        /// </param>
        /// <param name="awaiter">
        /// The awaiter representing the pending asynchronous operation.
        /// </param>
        /// <remarks>
        /// After calling this method, the iterator should return from the <see cref="TryMoveNext"/>
        /// method immediately and prevent running any more work that has side-effects or that may
        /// throw an exception.
        /// <code>
        /// T TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
        /// {
        ///     ...
        ///     if (!awaiter.IsCompleted)
        ///     {
        ///         nextState = 17;
        ///         AwaitOnCompleted(17, ref awaiter);
        ///         hasNext = null;
        ///         return default;
        ///     }
        ///     ...
        /// }
        /// </code>
        /// </remarks>
        protected void AwaitOnCompleted<TAwaiter>(int nextState, ref TAwaiter awaiter)
            where TAwaiter : INotifyCompletion
        {
            AwaitOnCompletedCore(nextState);

            var @this = this;
            __builder.AwaitOnCompleted(ref awaiter, ref @this);
        }

        /// <summary>
        /// Schedules the iterator to resume from the specified <paramref name="nextState"/> when the
        /// specified <paramref name="awaiter"/> completes.
        /// </summary>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <param name="nextState">
        /// The iterator state to resume from after completion of the pending asynchronous operation.
        /// </param>
        /// <param name="awaiter">
        /// The awaiter representing the pending asynchronous operation.
        /// </param>
        /// <remarks>
        /// After calling this method, the iterator should return from the <see cref="TryMoveNext"/>
        /// method immediately. See code example on <see cref="AwaitOnCompleted"/>.
        /// </remarks>
        [SecuritySafeCritical]
        protected void AwaitUnsafeOnCompleted<TAwaiter>(int nextState, ref TAwaiter awaiter)
            where TAwaiter : ICriticalNotifyCompletion
        {
            AwaitOnCompletedCore(nextState);

            var @this = this;
            __builder.AwaitUnsafeOnCompleted(ref awaiter, ref @this);
        }

        /// <summary>
        /// Shared logic for <see cref="AwaitOnCompleted"/> and <see cref="AwaitUnsafeOnCompleted"/>.
        /// </summary>
        /// <param name="nextState">
        /// The iterator state to resume from after completion of the pending asynchronous operation.
        /// </param>
        private void AwaitOnCompletedCore(int nextState)
        {
            //
            // First check if the requested target state has a valid value.
            //
            AssertValidState(nextState);

            lock (__lock)
            {
                GetFlagsAndState(out int flags, out _);

                //
                // Assert we got here because the iterator is busy. If not, we have a broken invariant or
                // the iterator implementation is misbehaving. Note the iterator will be marked as busy
                // even by the DisposeAsync code path when triggering cleanup.
                //
                if ((flags & IsBusyFlag) == 0)
                {
                    throw ExpectedFlagIsNotSet(flags, IsBusyFlag);
                }

                //
                // Assert we have started at some point. If not, our machinery has a flaw and causes us to
                // end up here in a mysterious way.
                //
                if ((flags & StartedFlag) == 0)
                {
                    throw ExpectedFlagIsNotSet(flags, StartedFlag);
                }

                //
                // Assert we are not running beyond the disposed state. This would indicate we signaled
                // completion too early and had some runaway work left, or the iterator implementation
                // could have a flaw in it.
                //
                if ((flags & DisposedFlag) != 0)
                {
                    throw UnexpectedFlagIsSet(flags, DisposedFlag);
                }

                //
                // NB: Other flags do not have to be checked here, e.g. presence of a dispose request,
                //     an ongoing dispose operation (which may involve running async code), etc. is all
                //     fine and dandy.
                //

                //
                // Toggle the async pending flag. We don't have to check if the flag is already set; it's
                // valid for an iterator to encounter many await expressions while making progress to a
                // yield statement.
                //
                flags |= AsyncPendingFlag;

                //
                // We'll hand over the awaiter to the async method builder shortly and need to be prepared
                // for encountering a yield statement (or an exception) that needs to report status on the
                // async method builder. To do so, we need a builder. The postcondition of this block is
                // to have such a builder available. There are a few cases to consider:
                //
                // 1. We already have a builder, either because we're still making progress to some yield
                //    statement and have been here before, so the precondition is already met.
                //
                // 2. We already have a builder, because a caller allocated one. This happens when kicking
                //    off the iterator through WaitForNextAsync or from DisposeAsync.
                //
                // 3. We no longer have a builder because we encountered a yield statement that created a
                //    new builder and assigned it to the field.
                //
                // The last case if of interest. We get to this point without a builder in case TryGetNext
                // is running ahead and encounters async work. It will then return false to the caller and
                // assumes we will create a builder that WaitForNextAsync will be able to return, holding
                // the eventual outcome of the iterator resumption triggered by completion of the async
                // work. The only other way to get here is by repeated calls to the async state machine as
                // part of making progress to the next yield statement. In particular, WaitForNextAsync is
                // not a direct source of triggering our execution; it only does so the very first time.
                // Beyond that point, WaitForNextAsync is always on the "backfoot" and will find a builder
                // for the eventual outcome of the next pass through the iterator.
                //
                // NB: When we're going through async state machine continuations as part of cleanup work
                //     triggered by DisposeAsync, yield statements will never unset the builder flag, or
                //     cause new builders to be created, or signal a "yield return" result on the builder.
                //
                if ((flags & HasBuilderFlag) == 0)
                {
                    flags |= HasBuilderFlag;
                    __builder = AsyncTaskMethodBuilder<bool>.Create();
                }

                //
                // Finally, keep track of the requested next state. All of this has to happen before we
                // ask the builder to hook up the awaiter to the state machine, in the caller.
                //
                __state = flags | nextState;
            }
        }

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
            //
            // NB: We could consider relaxing the requirement on iterators having to call OnDisposing
            //     even if they don't require any cleanup. If we still want to assert some behavior in
            //     the base class, we could require the iterator to report MaxState upon finishing. Docs
            //     would be updated as follows:
            //
            //     <para>
            //     If the iterator does not require any cleanup code to run, calls to <see cref="OnDisposing"/>
            //     and <see cref="OnDisposed"/> can be omitted, but the iterator should return a next state
            //     value of <see cref="MaxState"/> from the <see cref="TryMoveNext"/> call and implement
            //     this state as a no-op operation.
            //     <code>
            //     T TryGetNext(int state, bool shouldBreak, bool? hasNext, out int nextState)
            //     {
            //         switch (state)
            //         {
            //             ...
            //             case MaxState:
            //                 goto __End;
            //         }
            //
            //         ...
            //
            //         __YieldBreak:
            //
            //         // OnDisposing();
            //         // OnDisposed();
            //
            //         __End:
            //
            //         hasNext = false;
            //         nextState = MaxState;
            //         return default;
            //     }
            //     </code>
            //     </para>
            //
            //     Note that this may still be too restrictive if we have too many assertions in the base
            //     class that check the MaxState value, because the iterator could throw an exception. We
            //     should revisit all such assertions and relax them as appropriate.
            //

            //
            // Called by the iterator body upon entering the dispose path, allowing use to observe this
            // condition when making decisions about disposal logic.
            //
            lock (__lock)
            {
                GetFlagsAndState(out int flags, out int state);

                //
                // We set the disposing flag upon going down the DisposeAsync path when we trigger the
                // iterator in order to perform cleanup, which can be detected using the dispose request
                // flag. If this flag is not set, we can assert the iterator only transitions to a self-
                // dispose state once in its lifetime.
                //
                if ((state & DisposeRequestedFlag) != 0)
                {
                    //
                    // NB: This makes some assumptions about the behavior of the code in the iterator
                    //     body, which may be too restrictive. We could do away with this assert.
                    //
                    if ((state & DisposingFlag) != 0)
                    {
                        throw UnexpectedFlagIsSet(flags, DisposeRequestedFlag | DisposingFlag);
                    }
                }

                flags |= DisposingFlag;

                __state = flags | state;
            }
        }

        /// <summary>
        /// Called by the iterator body upon completing cleanup operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// After calling this method, the iterator should have terminated, and no further code should
        /// be run. All asynchronous operations should have completed. Only a single call to this method
        /// is allowed to be made.
        /// </para>
        /// <para>
        /// While a call to this method is not required to guarantee idempotent behavior of dispose
        /// operations (see <see cref="OnDisposing"/>), it is recommended to construct iterator bodies in
        /// such a way that a call to this method is guaranteed to be made upon completion of the iterator.
        /// This forces an implementation pattern where iterator code in <c>finally</c> blocks is kept in
        /// <c>finally</c> blocks.
        /// <code>
        /// T TryGetNext(int state, bool shouldBreak, bool? hasNext, out int nextState)
        /// {
        ///     switch (state)
        ///     {
        ///         ...
        ///         case 17:  // Some async state during cleanup.
        ///             goto __EnterFinally1;
        ///         case MaxState:
        ///             goto __End;
        ///     }
        /// 
        ///     ...
        /// 
        ///     __YieldBreak:  // Target branch for "yield break" statements (only reached once).
        /// 
        ///     OnDisposing();
        /// 
        ///     __EnterFinally1:  // Label to re-enter the finally block when uysing async code.
        /// 
        ///     try { }
        ///     finally  // If original code was in finally block, keep it that way here.
        ///     {
        ///         bool __suppressFinally = false;
        ///         try
        ///         {
        ///             switch (state)
        ///             {
        ///                 ...
        ///                 case 17:
        ///                     goto __State17;
        ///                 ...
        ///             }
        /// 
        ///             ...
        /// 
        ///             if (...)  // Awaiter conditional exit pattern.
        ///             {
        ///                 ...
        ///                 nextState = 17;
        ///                 AwaitOnCompleted(nextState, ref ...);
        ///                 __suppressFinally = true;  // Prevent early declaration of dispose completion.
        ///                 hasNext = null;
        ///                 return default;
        ///             }
        ///             
        ///             __State17:
        ///             ...
        ///             
        ///             if (__ex != null)
        ///             {
        ///                 ExceptionDispatchInfo(__ex).Throw();  // Rethrow due to await in finally.
        ///             }
        ///         }
        ///         finally
        ///         {
        ///             if (!__suppressFinally)
        ///             {
        ///                 OnDisposed();
        ///             }
        ///         }
        ///     }
        /// 
        ///     __End:
        /// 
        ///     nextState = MaxState;
        ///     hasNext = false;
        ///     return default;
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        protected void OnDisposed()
        {
            //
            // Called by the iterator body upon reaching the final state of the iterator after having
            // self-disposed. This should be the final activity by the iterator body.
            //
            lock (__lock)
            {
                GetFlagsAndState(out int flags, out int state);

                //
                // Assert that we were notified earlier that a dispose was underway.
                //
                if ((state & DisposingFlag) != 0)
                {
                    throw UnexpectedFlagIsSet(flags, DisposingFlag);
                }

                //
                // Assert that we don't end up here more than once.
                //
                if ((state & DisposedFlag) != 0)
                {
                    throw UnexpectedFlagIsSet(flags, DisposedFlag);
                }

                flags |= DisposedFlag;
                flags &= ~IsBusyFlag;

                __state = flags | state;
            }
        }

        /// <summary>
        /// Called by the iterator body to check whether a <c>yield return</c> site should yield execution
        /// by returning from the <see cref="TryMoveNext"/> method, or should proceed to <c>yield break</c>
        /// from the current position. A branch to <c>yield break</c> gets injected when a dispose request
        /// was fielded by a call to <see cref="DisposeAsync"/> while an asynchronous operation was pending
        /// during the execution of the iterator.
        /// </summary>
        /// <returns>
        /// <c>true</c> to yield execution; <c>false</c> to proceed to <c>yield break</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is enables a performance optimization in generated iterator code, by avoiding
        /// having to return the caller at a <c>yield return</c> site when the iterator is supposed to
        /// branch to the <c>yield break</c> label, due to a pending dispose request.
        /// </para>
        /// <para>
        /// For example, the following code:
        /// <code>
        ///    async IAsyncEnumerable<T>()
        ///    {
        ///        ...
        ///        yield return F();
        ///        ...
        ///    }
        /// </code>
        /// can be compiled to:
        /// <code>
        ///    T TryMoveNext(int state, bool shouldBreak, out bool hasNext out int nextState)
        ///    {
        ///        switch (state)
        ///        {
        ///            ...
        ///            case 17:
        ///                if (shouldBreak)
        ///                    goto __State17_Break;
        ///                else
        ///                    goto __State17;
        ///            ...
        ///        }
        ///
        ///        ...
        ///
        ///        __nextState = 17;
        ///        __result = F();
        ///
        ///        if (ShouldYieldReturn())
        ///            goto __YieldReturn;
        ///
        ///        __State17_Break;
        ///        goto __YieldBreak;
        ///
        ///        __State17:
        ///
        ///        ...
        ///
        ///        __YieldReturn:
        ///        hasNext = true;
        ///        nextState = __nextState;
        ///        return __result;
        ///
        ///        __YieldBreak:
        ///        hasNext = false;
        ///        nextState = int.MaxValue;
        ///        return default;
        ///    }
        /// </code>
        /// </para>
        /// <para>
        /// Code is still correct without this optimization because the caller of <see cref="TryMoveNext"/>
        /// will suppress reporting values yielded from <see cref="TryMoveNext"/> when a dispose request is
        /// pending. Instead, it will immediately call <see cref="TryMoveNext"/> again, with the next state
        /// it just got back, but this time with the <c>shouldBreak</c> parameter set to <c>true</c>.
        /// </para>
        /// <para>
        /// This optimization avoids going through the iterator body again from the top through levels of
        /// branch tables which may involve restoring of locals from fields and whatnot. Instead, the
        /// generated code can emit a direct branch to a label it already has to support the regular
        /// dispose path which reenters the state machine at the latest <c>yield return</c> site and
        /// injects a branch to <c>yield break</c> from that position.
        /// </para>
        /// <para>
        /// Checks using <see cref="ShouldYieldReturn"/> can be omitted at <c>yield return</c> sites that
        /// are dominated (in CFG paralance) by synchronous-only code. That is, if such a site is solely
        /// reachable through synchronous-only code, no asynchronous work can exist that causes a call to
        /// <see cref="DisposeAsync"/> to record a disposal request that it cannot grant immediately. Note
        /// that this assumes a sequential consumption pattern of the iterator.
        /// </para>
        /// </remarks>
        protected bool ShouldYieldReturn()
        {
            lock (__lock)
            {
                return (__state & DisposeRequestedFlag) == 0;
            }
        }

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

        /// <summary>
        /// Advances the iterator to its next state upon completion of an asynchronous operation.
        /// </summary>
        void IAsyncStateMachine.MoveNext()
        {
            //
            // This method is called when an async operation caused the iterator to suspend, and its
            // completion caused a callback. We continue running until we encounter a yield statement,
            // which can be either of two cases:
            //
            // 1. A "yield return" statement will cause the __result value to be set and the task to be
            //    completed with a return value of true. We also set a has value flag for the call to
            //    to TryGetNext to pick up.
            //
            // 2. A "yield break" statement where we report false to the caller through the task in the
            //    builder. Reaching this state will have caused cleanup to run.
            //
            // There is more subtlety though. First, "yield return" statements may not be granted by this
            // code in case a request to dispose was detected. In that case, we reenter the iterator body
            // with the shouldBreak flag set to true. This explains the infinite loop structure below.
            // Second, errors thrown by the iterator body imply that cleanup took place but the cleanup
            // logic itself may have thrown an exception. This needs to be reported on the builder, too.
            //
            while (true)
            {
                int state;
                bool shouldBreak;

                lock (__lock)
                {
                    GetFlagsAndState(out int flags, out state);

                    //
                    // If we don't have an async method builder to report outcome to, the caller did not
                    // set us up for success. Assert this.
                    //
                    if ((flags & HasBuilderFlag) == 0)
                    {
                        throw ExpectedFlagIsNotSet(flags, HasBuilderFlag); // NB: If this is to remain a runtime check, where to throw?
                    }

                    try
                    {
                        //
                        // See remarks in AwaitOnCompletedCore.
                        //
                        if ((flags & IsBusyFlag) == 0)
                        {
                            throw ExpectedFlagIsNotSet(flags, IsBusyFlag);
                        }

                        //
                        // See remarks in AwaitOnCompletedCore.
                        //
                        if ((flags & StartedFlag) == 0)
                        {
                            throw ExpectedFlagIsNotSet(flags, StartedFlag);
                        }

                        //
                        // See remarks in AwaitOnCompletedCore.
                        //
                        if ((flags & DisposedFlag) != 0)
                        {
                            throw UnexpectedFlagIsSet(flags, DisposedFlag);
                        }

                        //
                        // Assert we knew about a pending asynchronous operation and expected to be called
                        // back to serve its completion. This check is not required when the iterator is
                        // still in its original state, where we call AsyncTaskMethodBuilder<bool>.Start
                        // to kick off the state machine for the first time.
                        //
                        if ((flags & AsyncPendingFlag) == 0 && state != Running)
                        {
                            throw UnexpectedAsyncContinuation();
                        }
                    }
                    catch (Exception assertEx)
                    {
                        __builder.SetException(assertEx);
                        return;
                    }

                    //
                    // We can't toggle AsyncPendingFlag here, because we haven't reported outcome on the
                    // builder yet, and we want WaitForNextAsync to be able to grab the task by checking
                    // this flag. Also, if we check this flag here, and proceed to call TryMoveNext, the
                    // flag may get set again, causing non-determinism when using it to make checks upon
                    // handling the outcome of the call to TryMoveNext (due to reentrancy).
                    //

                    //
                    // If a dispose request exists, we should ask the iterator body to proceed from the
                    // current state by performing a "yield break". Check this flag to determine this
                    // case.
                    //
                    // NB: We don't check the disposing flag and assume the iterator implementation has
                    //     sufficient state (either in fields or in the state value passed through us)
                    //     to determine it was already going down the path of cleanup. For example, all
                    //     states for await expressions in finally blocks have to be encoded in the state
                    //     already in order to be able to resume in the right place.
                    //
                    shouldBreak = (flags & DisposeRequestedFlag) != 0;

                    //
                    // Other flags don't have be manipulated here. In particular, we don't need to deal
                    // with the busy flag, which will remain set until we encounter a yield statement.
                    //
                }

                T result = default;
                bool? hasNext = default;
                int nextState = -1;
                Exception error = default;

                //
                // Call the iterator body. Unlikely TryGetNext, we can pass a non-trivial shouldBreak
                // value here. See remarks in TryGetNext.
                //
                try
                {
                    result = TryMoveNext(state, shouldBreak, out hasNext, out nextState);
                }
                catch (Exception ex)
                {
                    //
                    // We should report the exception on the builder, but this will run user code. We
                    // first want to edit our flags, so we remember the exception and call SetException
                    // later.
                    //
                    error = ex;
                }

                ResumeAction action = ResumeAction.None;
                AsyncTaskMethodBuilder<bool> builder = default;

                lock (__lock)
                {
                    GetFlagsAndState(out int flags, out var oldState);

                    try
                    {
                        //
                        // Assert the state requested by the iterator is within bounds.
                        //
                        AssertValidState(nextState);

                        //
                        // If we detect that a dispose request was made through a call to DisposeAsync
                        // while we were executing async work, we should grant the request here because
                        // that's our promise to DisposeAsync. See remarks over there for context. In
                        // this case, we'll go around our outer loop, which will detect this flag (note
                        // it can never be unset) and call TryMoveNext again with shouldBreak set to true.
                        //
                        // NB: The action may get overridden by logic below, e.g. an error or a conclusive
                        //     outcome from TryMoveNext indicating we terminated.
                        //
                        if ((flags & DisposeRequestedFlag) != 0)
                        {
                            action = ResumeAction.ContinueToYieldBreak;
                        }

                        //
                        // If TryMoveNext did throw an error, cleanup should have been initiated. Perform
                        // various checks, similar to the ones in TryGetNext.
                        //
                        if (error != null)
                        {
                            //
                            // If we faulted, a synchronous exception escaped and we expect the iterator
                            // to have initiated disposal itself. Assert this behavior.
                            //
                            if ((flags & DisposingFlag) == 0)
                            {
                                throw ExpectedFlagIsNotSet(flags, DisposingFlag);
                            }

                            //
                            // Cleanup logic itself may have thrown, failing to call OnDisposed. Toggle
                            // the dispose flag ourselves.
                            //
                            flags |= DisposedFlag;

                            //
                            // An exception leaves the nextState variable undefined, so restore original.
                            //
                            nextState = state;

                            //
                            // There may be an invariant assert error, so refrain from setting the action
                            // to ThrowError; we have to check later anyway.
                            //
                        }
                        else
                        {
                            //
                            // First check for hasNext reporting null, meaning that more async work was
                            // scheduled. In that case, we need to get out of here without touching any
                            // state. The next pass through this method will take care of things:
                            //
                            // - if null (again), it will be bailing out here again
                            // - if true, it will proceed to return true or to trigger "yield break"
                            // - if false, it will proceed to return false
                            // - if exception, it will get reported
                            //
                            if (hasNext == null)
                            {
                                return;
                            }

                            //
                            // In all other cases, we have a conclusive outcome from TryGetNext, which
                            // means we will report to the builder, or continue to grant a dispose request
                            // by performing a "yield break". Either way, there's no more async work
                            // pending, so we toggle off this flag.
                            //
                            flags &= ~AsyncPendingFlag;

                            //
                            // If we're at the end of the iterator, we know what to do already. There can
                            // be no further serving of dispose requests and whatnot.
                            //
                            if (!hasNext.Value)
                            {
                                action = ResumeAction.ReportYieldBreak;
                            }
                            else
                            {
                                //
                                // If we have received the result of a "yield return", we can suspend the
                                // iterator only if there's no pending dispose request. In that case, set
                                // the result and toggle the result flag.
                                //
                                if (action != ResumeAction.ContinueToYieldBreak)
                                {
                                    __current = result;
                                    flags |= HasResultFlag;

                                    action = ResumeAction.ReportYieldReturn;
                                }
                            }
                        }
                    }
                    catch (Exception assertEx)
                    {
                        error = assertEx;
                    }

                    //
                    // Any error wins over anything else, so check here and override the action.
                    //
                    if (error != null)
                    {
                        action = ResumeAction.ThrowError;
                    }

                    //
                    // If we're going to report a definitive outcome to the builder, we can toggle off the
                    // busy flag. The builder has served its purpose and we need subsequent iterations to
                    // use a fresh instance, so we'll refresh the builder here.
                    //
                    if (action != ResumeAction.ContinueToYieldBreak)
                    {
                        flags &= ~IsBusyFlag;

                        //
                        // Copy the current builder. It has a reference to a task in it that we handed out
                        // earlier when setting up the builder. We use the copy to signal completion.
                        //
                        builder = __builder;

                        //
                        // Create a new builder to be ready for the next iteration that requires async
                        // work. Note we already checked the builder flag upon entering this method.
                        //
                        __builder = AsyncTaskMethodBuilder<bool>.Create();
                    }

                    //
                    // Combine the next state and the flags, and set it.
                    //
                    __state = nextState | flags;
                }

                //
                // Report definitive outcomes to the original builder via the local copy.
                //
                switch (action)
                {
                    case ResumeAction.ThrowError:
                        builder.SetException(error);
                        return;
                    case ResumeAction.ReportYieldReturn:
                        builder.SetResult(true);
                        return;
                    case ResumeAction.ReportYieldBreak:
                        builder.SetResult(false);
                        return;
                    case ResumeAction.ContinueToYieldBreak:
                        break; // NB: Go around the loop.
                }
            }
        }

        /// <summary>
        /// Sets the heap-allocated async state machine.
        /// </summary>
        /// <param name="stateMachine">The heap-allocated async state machine.</param>
        /// <remarks>
        /// This method is implemented as a no-op because async iterators are implemented as classes.
        /// </remarks>
        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) { }

        /// <summary>
        /// Splits the <see cref="__state"/> value into <paramref name="flags"/> and the iterator
        /// implementation's <paramref name="state"/>.
        /// </summary>
        /// <param name="flags">The flags maintained by this class.</param>
        /// <param name="state">The state of the iterator implementation.</param>
        private void GetFlagsAndState(out int flags, out int state)
        {
            flags = __state & StateFlagMask;
            state = __state & ~StateFlagMask;
        }

        /// <summary>
        /// Asserts that the specified <paramref name="state"/> is a valid value that can be used by an
        /// iterator implementation. See <see cref="MaxState"/> and <see cref="StateFlagMask"/> for more
        /// information about our representation of iterator state.
        /// </summary>
        /// <param name="state">The state to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The state is invalid.</exception>
        private static void AssertValidState(int state)
        {
            if ((state & ~StateFlagMask) != state)
            {
                throw new ArgumentOutOfRangeException("The async iterator state is outside the bounds of supported state values.");
            }
        }

        /// <summary>
        /// Returns an exception object indicating that the iterator is busy when a call is made. This is
        /// used to prevent concurrent execution of the iterator which results in undefined behavior.
        /// </summary>
        /// <param name="caller">The name of the caller that attempted to use the iterator.</param>
        /// <returns>An exception describing the failure condition.</returns>
        /// <remarks>
        /// The use of the <see cref="IsBusyFlag"/> flag to check for this condition can be relaxed if we
        /// decide that concurrent calls on <see cref="IAsyncEnumerator{T}"/> methods result in undefined
        /// behavior for all implementations. This is already the case of synchronous iterators and no
        /// guards are put in place to prevent concurrent calls to its methods.
        /// </remarks>
        private static Exception IteratorBusy([CallerMemberName]string caller = null) => new InvalidOperationException(FormattableString.Invariant($"Calling {caller} is not supported while the iterator is running."));

        /// <summary>
        /// Returns an exception object indicating that the iterator state has change in an unexpected
        /// way. This may indicate a broken invariant or call sequence that has caused concurrent
        /// execution of the iterator, resulting in an ambiguous state machine state value.
        /// </summary>
        /// <param name="originalState">
        /// The original state of the iterator at the start of the operation. We don't expect this value
        /// to change while the iterator is running.
        /// </param>
        /// <param name="newState">
        /// The state of the iterator after finishing the call to <see cref="TryMoveNext"/>. We expect
        /// this value to match <paramref name="originalState"/>.
        /// </param>
        /// <returns>An exception describing the failure condition.</returns>
        private static Exception StateCorruptionDetected(int originalState, int newState) => new InvalidOperationException(FormattableString.Invariant($"The async iterator has transitioned to an unexpected state. The expected state is {originalState} but the state was observed to be {newState}."));

        /// <summary>
        /// Returns an exception object indicating that the iterator did not properly perform cleanup
        /// operations in response to a call to <see cref="DisposeAsync"/>. This may indicate that the
        /// iterator did not honor the <c>shouldBreak</c> parameter on <see cref="TryMoveNext"/>.
        /// </summary>
        /// <returns>An exception describing the failure condition.</returns>
        private static Exception CleanupDidNotYieldBreak() => new InvalidOperationException("The async iterator did not properly finish cleanup operations.");

        /// <summary>
        /// Returns an exception object indicating that the iterator has been called back on the
        /// <see cref="IAsyncStateMachine.MoveNext"/> method when no pending async work should be in
        /// progress. This may indicate that iterator code is not using <see cref="AwaitOnCompleted"/>
        /// or <see cref="AwaitUnsafeOnCompleted"/> properly to schedule an asynchronous continuation.
        /// </summary>
        /// <returns>An exception describing the failure condition.</returns>
        private static Exception UnexpectedAsyncContinuation() => new InvalidOperationException("The async iterator was resumed due to the completion of an untracked asynchronous operation.");

        /// <summary>
        /// Returns an exception object indicating that a call to <see cref="WaitForNextAsync"/> was
        /// made that was not expected at the current iterator state. This may indicate that the
        /// consumer is not violating the call sequence
        /// </summary>
        /// <returns>An exception describing the failure condition.</returns>
        private static Exception UnexpectedWaitForNextAsyncCall() => new InvalidOperationException("A call to WaitForNextAsync is unexpected at this time.");

        //
        // REMARKS: The methods below could be made into check-and-return methods that return null if the
        //          assertion passes, and an exception otherwise. This would work well with a `throw?`
        //          conditional throw statement, allowing callers to write:
        //
        //              throw? AssertFlagIsSet(flags, SomeFlag);
        //

        /// <summary>
        /// Returns an exception object indicating that the iterator has <paramref name="flag"/> set in
        /// the specified <paramref name="flags"/>, but this flag was not expected to be set.
        /// </summary>
        /// <param name="flags">The flags that were checked.</param>
        /// <param name="flag">The flag that was not expected to be set.</param>
        /// <returns>An exception describing the failure condition.</returns>
        private static Exception UnexpectedFlagIsSet(int flags, int flag) => new InvalidOperationException(FormattableString.Invariant($"The async iterator is in an unexpected state. Flag '{(IteratorFlags)flag}' should not be set, but it is. Current flags are '{(IteratorFlags)flags}'."));

        /// <summary>
        /// Returns an exception object indicating that the iterator does not have <paramref name="flag"/>
        /// set in the specified <paramref name="flags"/>, but this flag was expected to be set.
        /// </summary>
        /// <param name="flags">The flags that were checked.</param>
        /// <param name="flag">The flag that was not expected to be set.</param>
        /// <returns>An exception describing the failure condition.</returns>
        private static Exception ExpectedFlagIsNotSet(int flags, int flag) => new InvalidOperationException(FormattableString.Invariant($"The async iterator is in an unexpected state. Flag '{(IteratorFlags)flag}' should be set, but it is not. Current flags are '{(IteratorFlags)flags}'."));

        /// <summary>
        /// Enum representing the next iterator resume action to take after the iterator gets woken up due
        /// to an asynchronous operation (see <see cref="IAsyncStateMachine.MoveNext"/>).
        /// </summary>
        private enum ResumeAction
        {
            /// <summary>
            /// Default value.
            /// </summary>
            None,

            /// <summary>
            /// Report a <c>yield break</c> result to the consumer, by completing <see cref="__builder"/>
            /// with a <c>false</c> value.
            /// </summary>
            ReportYieldBreak,

            /// <summary>
            /// Report a <c>yield return</c> result to the consumer, by completing <see cref="__builder"/>
            /// with a <c>true</c> value.
            /// </summary>
            ReportYieldReturn,

            /// <summary>
            /// Report an exception result to the consumer, by completing <see cref="__builder"/> using
            /// a call to <see cref="AsyncTaskMethodBuilder.SetException"/>.
            /// </summary>
            ThrowError,

            /// <summary>
            /// Do not report a result to the consumer and proceed immediately to <c>yield break</c> out
            /// of the iterator, triggering cleanup. This is used to serve a <see cref="DisposeAsync"/>
            /// request that came in while an asynchronous operation was pending.
            /// </summary>
            ContinueToYieldBreak,
        }

        /// <summary>
        /// Enumeration corresponding to the flags. Used for error reporting using friendly names. Keep
        /// this in sync with the flags declared in this type.
        /// </summary>
        /// <remarks>
        /// Using raw constant values to work with flags in this class, rather than operating on enum
        /// values, in order to avoid spurious conversions required to apply some operators (such as ~)
        /// and to avoid common pitfalls with <see cref="Enum"/> methods.
        /// </remarks>
        [Flags]
        private enum IteratorFlags
        {
            StartedFlag = AsyncIterator<T>.StartedFlag,
            IsBusyFlag = AsyncIterator<T>.IsBusyFlag,
            HasResultFlag = AsyncIterator<T>.HasResultFlag,
            HasBuilderFlag = AsyncIterator<T>.HasBuilderFlag,
            AsyncPendingFlag = AsyncIterator<T>.AsyncPendingFlag,
            DisposeRequestedFlag = AsyncIterator<T>.DisposeRequestedFlag,
            DisposingFlag = AsyncIterator<T>.DisposingFlag,
            DisposedFlag = AsyncIterator<T>.DisposedFlag,
            DisposeReportedFlag = AsyncIterator<T>.DisposeReportedFlag,
        }
    }
}
