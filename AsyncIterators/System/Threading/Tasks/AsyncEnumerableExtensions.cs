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
        public static ConfiguredAsyncEnumerable<T> ConfigureAwait<T>(this IAsyncEnumerable<T> source, bool continueOnCapturedContext)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new ConfiguredAsyncEnumerable<T>(source, continueOnCapturedContext);
        }

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
