//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

//#define SUPPORT_INTERFACES // REVIEW: Are the interfaces ever desirable (boxing caveat) or do we just bank on the "foreach await" pattern-based compilation?

using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    public struct ConfiguredAsyncEnumerable<T>
#if SUPPORT_INTERFACES
        : IAsyncEnumerable<T>
#endif
    {
        private readonly IAsyncEnumerable<T> _enumerable;
        private readonly bool _continueOnCapturedContext;

        internal ConfiguredAsyncEnumerable(IAsyncEnumerable<T> enumerable, bool continueOnCapturedContext)
        {
            _enumerable = enumerable;
            _continueOnCapturedContext = continueOnCapturedContext;
        }

        public Enumerator GetAsyncEnumerator() => new Enumerator(_enumerable.GetAsyncEnumerator(), _continueOnCapturedContext);

#if SUPPORT_INTERFACES
        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator() => GetAsyncEnumerator();
#endif

        public struct Enumerator
#if SUPPORT_INTERFACES
            : IAsyncEnumerator<T>
#endif
        {
            private readonly IAsyncEnumerator<T> _enumerator;
            private readonly bool _continueOnCapturedContext;

            internal Enumerator(IAsyncEnumerator<T> enumerator, bool continueOnCapturedContext)
            {
                _enumerator = enumerator;
                _continueOnCapturedContext = continueOnCapturedContext;
            }

            public ConfiguredTaskAwaitable<bool> WaitForNextAsync() => _enumerator.WaitForNextAsync().ConfigureAwait(_continueOnCapturedContext);

            public T TryGetNext(out bool success) => _enumerator.TryGetNext(out success);

            public ConfiguredTaskAwaitable DisposeAsync() => _enumerator.DisposeAsync().ConfigureAwait(_continueOnCapturedContext);

#if SUPPORT_INTERFACES
            async Task<bool> IAsyncEnumerator<T>.WaitForNextAsync() => await _enumerator.WaitForNextAsync().ConfigureAwait(_continueOnCapturedContext);

            Task IAsyncDisposable.DisposeAsync() => await _enumerator.DisposeAsync().ConfigureAwait(_continueOnCapturedContext);
#endif
        }
    }
}
