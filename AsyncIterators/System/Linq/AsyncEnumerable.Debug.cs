using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Debug<T>(this IAsyncEnumerable<T> source, string label)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new DebugAsyncEnumerable<T>(source, label);
        }

        private sealed class DebugAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IAsyncEnumerable<T> source;
            private readonly string label;
            private int counter;

            public DebugAsyncEnumerable(IAsyncEnumerable<T> source, string label)
            {
                this.source = source;
                this.label = label;
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator()
            {
                var id = Interlocked.Increment(ref counter);

                Console.WriteLine($"[{label}] {id}> GetAsyncEnumerator");

                return new Enumerator(source.GetAsyncEnumerator(), label, id);
            }

            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private readonly IAsyncEnumerator<T> enumerator;
                private readonly string label;
                private readonly int id;
                private int counter;

                public Enumerator(IAsyncEnumerator<T> enumerator, string label, int id)
                {
                    this.enumerator = enumerator;
                    this.label = label;
                    this.id = id;
                }

                public async Task DisposeAsync()
                {
                    var id = Interlocked.Increment(ref counter);

                    Console.WriteLine($"[{label}] {this.id}:{id}> DisposeAsync - Start");

                    await enumerator.DisposeAsync().ConfigureAwait(false);

                    Console.WriteLine($"[{label}] {this.id}:{id}> DisposeAsync - Stop");
                }

                public T TryGetNext(out bool success)
                {
                    var id = Interlocked.Increment(ref counter);

                    Console.WriteLine($"[{label}] {this.id}:{id}> TryGetNext - Start");

                    var res = enumerator.TryGetNext(out success);

                    Console.WriteLine($"[{label}] {this.id}:{id}> TryGetNext - Stop({success}, {res})");

                    return res;
                }

                public async Task<bool> WaitForNextAsync()
                {
                    var id = Interlocked.Increment(ref counter);

                    Console.WriteLine($"[{label}] {this.id}:{id}> WaitForNextAsync - Start");

                    var res = await enumerator.WaitForNextAsync().ConfigureAwait(false);

                    Console.WriteLine($"[{label}] {this.id}:{id}> WaitForNextAsync - Stop({res})");

                    return res;
                }
            }
        }
    }
}
