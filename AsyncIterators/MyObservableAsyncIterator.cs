//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System;

namespace AsyncIterators
{
    public class MyObservableAsyncIterator : ObservableAsyncIterator<int, MyObservableAsyncIterator>
    {
        protected override MyObservableAsyncIterator Clone() => new MyObservableAsyncIterator();

        protected override int TryMoveNext(int state, bool shouldBreak, out bool? hasNext, out int nextState)
        {
            throw new NotImplementedException();
        }
    }
}
