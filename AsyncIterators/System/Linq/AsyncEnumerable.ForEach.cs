using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static async Task ForEachAsync<T>(this IAsyncEnumerable<T> source, Func<T, Task> body)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            IAsyncEnumerator<T> e = source.GetAsyncEnumerator();

            try
            {
                while (await e.WaitForNextAsync().ConfigureAwait(false))
                {
                    while (true)
                    {
                        T item = e.TryGetNext(out bool success);

                        if (!success)
                        {
                            break;
                        }

                        await body(item).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await e.DisposeAsync().ConfigureAwait(false);
            }
        }

        public static async Task ForEachAsync<T>(this IAsyncEnumerable<T> source, Func<T, CancellationToken, Task> body, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            cancellationToken.ThrowIfCancellationRequested();

            IAsyncEnumerator<T> e = source.GetAsyncEnumerator();

            try
            {
                while (await e.WaitForNextAsync().ConfigureAwait(false))
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        T item = e.TryGetNext(out bool success);

                        if (!success)
                        {
                            break;
                        }

                        await body(item, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await e.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
