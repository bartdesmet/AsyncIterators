//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System;
using System.Runtime.CompilerServices;

namespace AsyncIterators
{
    public abstract class ObservableAsyncIterator<T, TIterable> : AsyncIterableBuilder<T, TIterable>, IObservable<T>, IDisposable
        where TIterable : ObservableAsyncIterator<T, TIterable>
    {
        private int __initialThreadId;
        private IObserver<T> __observer;
        private int __state;
        private bool __dispose;

        protected ObservableAsyncIterator()
        {
            __initialThreadId = Environment.CurrentManagedThreadId;
        }

        public void Dispose() => __dispose = true;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (__observer == null && __initialThreadId == Environment.CurrentManagedThreadId)
            {
                return SubscribeCore(observer);
            }
            else
            {
                TIterable clone = Clone();
                return clone.SubscribeCore(observer);
            }
        }

        private IDisposable SubscribeCore(IObserver<T> observer)
        {
            __observer = observer;

            Run();

            return this;
        }

        private void Run()
        {
            while (true)
            {
                T result = TryMoveNext(__state, shouldBreak: __dispose, out var hasNext, out var nextState);

                if (hasNext == null)
                {
                    break;
                }
                else if (hasNext == true)
                {
                    __observer.OnNext(result);
                    __state = nextState;
                }
                else
                {
                    __observer.OnCompleted();
                    __state = nextState;
                }
            }
        }

        protected override void AwaitOnCompleted<TAwaiter>(int nextState, ref TAwaiter awaiter)
        {
            __state = nextState;
            awaiter.OnCompleted(Run);
        }

        protected override void AwaitUnsafeOnCompleted<TAwaiter>(int nextState, ref TAwaiter awaiter)
        {
            __state = nextState;
            awaiter.OnCompleted(Run);
        }

        protected override void OnDisposed() { }

        protected override void OnDisposing() { }

        protected override bool ShouldYieldReturn() => !__dispose;
    }
}
