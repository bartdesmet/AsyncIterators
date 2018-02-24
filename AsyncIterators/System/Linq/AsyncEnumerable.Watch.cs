//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections.Generic;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Watch<T>(this IAsyncEnumerable<T> source, IProgress<string> watch)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (watch == null)
                throw new ArgumentNullException(nameof(watch));

            return new WatchAsyncEnumerable<T>(source, watch);
        }

        private sealed class WatchAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly IProgress<string> watch;

            public WatchAsyncEnumerable(IAsyncEnumerable<T> source, IProgress<string> watch)
            {
                this.source = source;
                this.watch = watch;
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator()
            {
                watch.Report("GetAsyncEnumerator");

                return new Enumerator(source.GetAsyncEnumerator(), watch);
            }

            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private readonly IAsyncEnumerator<T> enumerator;
                private readonly IProgress<string> watch;

                public Enumerator(IAsyncEnumerator<T> enumerator, IProgress<string> watch)
                {
                    this.enumerator = enumerator;
                    this.watch = watch;
                }

                public Task DisposeAsync()
                {
                    watch.Report("DisposeAsync");

                    return enumerator.DisposeAsync();
                }

                public T TryGetNext(out bool success)
                {
                    watch.Report("TryGetNext");

                    return enumerator.TryGetNext(out success);
                }

                public Task<bool> WaitForNextAsync()
                {
                    watch.Report("WaitForNextAsync");

                    return enumerator.WaitForNextAsync();
                }
            }
        }
    }
}
