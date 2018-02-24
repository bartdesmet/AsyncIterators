//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncIterators
{
    class Program
    {
        static async Task Main(string[] args)
        {
            foreach (var (title, sequence) in new(string, IAsyncEnumerable<int>)[]
            {
#if !TEST
                ("Empty", AsyncEnumerable.Empty<int>()),

                ("Return", AsyncEnumerable.Return(42)),

                ("ToAsyncEnumerable(T[]) - Empty", AsyncEnumerable.ToAsyncEnumerable<int>()),
                ("ToAsyncEnumerable(T[]) - 1", AsyncEnumerable.ToAsyncEnumerable(1)),
                ("ToAsyncEnumerable(T[]) - 2", AsyncEnumerable.ToAsyncEnumerable(1, 2)),
                ("ToAsyncEnumerable(T[]) - 3", AsyncEnumerable.ToAsyncEnumerable(1, 2, 3)),
                ("ToAsyncEnumerable(IEnumerable<T>) - Enumerable", Enumerable.Range(0, 10).ToAsyncEnumerable()),
                ("ToAsyncEnumerable(IEnumerable<T>) - List", new List<int> { 2, 3, 5 }.ToAsyncEnumerable()),
                ("ToAsyncEnumerable(Task<T>) - FromResult", Task.FromResult(42).ToAsyncEnumerable()),
                ("ToAsyncEnumerable(Task<T>) - Yield", new Func<Task<int>>(async () => { await Task.Yield(); return 42; })().ToAsyncEnumerable()),      // NB: Need to build a Defer operator to make the tasks "cold".
                ("ToAsyncEnumerable(Task<T>) - Delay", new Func<Task<int>>(async () => { await Task.Delay(1000); return 42; })().ToAsyncEnumerable()),  // NB: Same as above.

                ("Where(Func<T, bool>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(x => x % 10 == 0)),
                ("Where(Func<T, Task<bool>>) - FromResult", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(x => Task.FromResult(x % 10 == 0))),
                ("Where(Func<T, Task<bool>>) - Delay", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(async x => { await Task.Delay(10); return x % 10 == 0; })),

                ("Select(Func<T, R>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(x => x + 1)),
                ("Select(Func<T, Task<R>>) - FromResult", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(x => Task.FromResult(x + 1))),
                ("Select(Func<T, Task<R>>) - Delay", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(async x => { await Task.Delay(10); return x + 1; })),

                ("Select(Func<T, R>).Where(Func<T, bool>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(x => x + 1).Where(x => x % 10 == 0)),
                ("Select(Func<T, R>).Where(Func<T, Task<bool>>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(x => x + 1).Where(async x => { await Task.Delay(10); return x % 10 == 0; })),
                ("Select(Func<T, Task<R>>).Where(Func<T, bool>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(async x => { await Task.Delay(10); return x + 1; }).Where(x => x % 10 == 0)),
                ("Select(Func<T, Task<R>>).Where(Func<T, Task<bool>>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Select(async x => { await Task.Delay(10); return x + 1; }).Where(async x => { await Task.Delay(10); return x % 10 == 0; })),

                ("Where(Func<T, bool>).Select(Func<T, R>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(x => x % 10 == 0).Select(x => x + 1)),
                ("Where(Func<T, bool>).Select(Func<T, Task<R>>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(x => x % 10 == 0).Select(async x => { await Task.Delay(10); return x + 1; })),
                ("Where(Func<T, Task<bool>>).Select(Func<T, R>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(async x => { await Task.Delay(10); return x % 10 == 0; }).Select(x => x + 1)),
                ("Where(Func<T, Task<bool>>).Select(Func<T, Task<R>>)", Enumerable.Range(0, 100).ToAsyncEnumerable().Where(async x => { await Task.Delay(10); return x % 10 == 0; }).Select(async x => { await Task.Delay(10); return x + 1; })),
                ("Where(Func<T, bool>).Select(Func<T, R>) - Trace", Enumerable.Range(0, 100).ToAsyncEnumerable().Debug("Range").Where(x => x % 10 == 0).Debug("Where").Select(x => x + 1).Debug("Select")),
                ("Where(Func<T, Task<bool>>).Select(Func<T, Task<R>>) - Trace", Enumerable.Range(0, 100).ToAsyncEnumerable().Debug("Range").Where(async x => { await Task.Delay(10); return x % 10 == 0; }).Debug("Where").Select(async x => { await Task.Delay(10); return x + 1; }).Debug("Select")),

                ("SelectMany(..., Func<T, C, R>)", Enumerable.Range(1, 5).ToAsyncEnumerable().SelectMany(i => Enumerable.Range(1, i).ToAsyncEnumerable(), (i, j) => i * 10 + j)),
                ("SelectMany(..., Func<T, C, Task<R>>) - FromResult", Enumerable.Range(1, 5).ToAsyncEnumerable().SelectMany(i => Enumerable.Range(1, i).ToAsyncEnumerable(), (i, j) => Task.FromResult(i * 10 + j))),
                ("SelectMany(..., Func<T, C, Task<R>>) - Delay", Enumerable.Range(1, 5).ToAsyncEnumerable().SelectMany(i => Enumerable.Range(1, i).ToAsyncEnumerable(), async (i, j) => { await Task.Delay(10); return i * 10 + j; })),

                ("Yield", Enumerable.Range(0, 10).ToAsyncEnumerable().Yield()),
                ("Yield - Trace", Enumerable.Range(0, 10).ToAsyncEnumerable().Debug("Range").Yield().Debug("Yield")),
#endif
            })
            {
                Console.WriteLine(title);

                await sequence
                    .ForEachAsync(x =>
                    {
                        Console.WriteLine(x);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);

                Console.WriteLine();
            }

            await CompareAsync(
                ("Sync", watcher => Enumerable.Range(0, 100).ToAsyncEnumerable().Watch(watcher).Where(x => x % 10 == 0).Watch(watcher).Select(x => x + 1).Watch(watcher)),
                ("Async", watcher => Enumerable.Range(0, 100).ToAsyncEnumerable().Yield().Watch(watcher).Where(async x => { await Task.Delay(10); return x % 10 == 0; }).Watch(watcher).Select(async x => { await Task.Delay(10); return x + 1; }).Watch(watcher))
            );

            await CompareAsync(
                ("Sync", watcher => Enumerable.Range(0, 10).ToAsyncEnumerable().Watch(watcher)),
                ("Yield", watcher => Enumerable.Range(0, 10).ToAsyncEnumerable().Yield().Watch(watcher))
            );
        }

        private static async Task CompareAsync<T>(params (string title, Func<IProgress<string>, IAsyncEnumerable<T>> create)[] tests)
        {
            foreach (var test in tests)
            {
                var count = new Dictionary<string, int>();

                var watch = new SyncProgress<string>(s =>
                {
                    lock (count)
                    {
                        if (!count.ContainsKey(s))
                        {
                            count[s] = 1;
                        }
                        else
                        {
                            count[s]++;
                        }
                    }
                });

                Console.WriteLine("Running " + test.title);

                await test.create(watch).ForEachAsync(x =>
                {
                    Console.WriteLine(x);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                Console.WriteLine();

                foreach (var entry in count)
                {
                    Console.WriteLine(entry);
                }

                Console.WriteLine();
                Console.WriteLine();
            }
        }

        private static (IProgress<string> watcher, Dictionary<string, int> count) CreateWatcher()
        {
            var count = new Dictionary<string, int>();

            var watcher = new Progress<string>(s =>
            {
                lock (count)
                {
                    if (!count.ContainsKey(s))
                    {
                        count[s] = 1;
                    }
                    else
                    {
                        count[s]++;
                    }
                }
            });

            return (watcher, count);
        }

        class SyncProgress<T> : IProgress<T>
        {
            private readonly Action<T> action;

            public SyncProgress(Action<T> action)
            {
                this.action = action;
            }

            public void Report(T value) => action(value);
        }
    }
}
