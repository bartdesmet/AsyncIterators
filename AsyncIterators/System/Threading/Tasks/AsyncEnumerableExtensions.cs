//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks
{
    public static partial class AsyncEnumerableExtensions
    {
        /// <summary>
        /// Configures all awaiters used when enumerating over the specified <paramref name="source"/> sequence.
        /// </summary>
        /// <typeparam name="T">The type of the elements produced by the sequence.</typeparam>
        /// <param name="source">The source sequence for which to configure all awaiters.</param>
        /// <param name="continueOnCapturedContext"><c>true</c> to attempt to marshal continuations back to the original context captured; otherwise, <c>false</c>.</param>
        /// <returns>An enumerable sequence used to enumerate over the <paramref name="source"/> sequence with the configured await behavior.</returns>
        public static ConfiguredAsyncEnumerable<T> ConfigureAwait<T>(this IAsyncEnumerable<T> source, bool continueOnCapturedContext)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new ConfiguredAsyncEnumerable<T>(source, continueOnCapturedContext);
        }

        /// <summary>
        /// Creates an enumerable sequence that asynchronously yields back to the current context when awaiting each element.
        /// </summary>
        /// <typeparam name="T">The type of the elements produced by the sequence.</typeparam>
        /// <param name="source">The source sequence to enumerate over with asynchronous yielding.</param>
        /// <returns>An enumerable sequence used to enumerate over the <paramref name="source"/> sequence but yielding asynchronously for each element.</returns>
        public static IAsyncEnumerable<T> Yield<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

#if CSHARP8
            return Iterator();

            async IAsyncEnumerable<T> Iterator()
            {
                foreach await (var item in source)
                {
                    await Task.Yield();
                    yield return item;
                }
            }
#else
            return source.Select(async x => { await Task.Yield(); return x; });
#endif
        }
    }
}
