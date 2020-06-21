using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public static class IAsyncEnumerable
    {
        // https://stackoverflow.com/questions/56518305/how-to-use-c8-iasyncenumerablet-to-async-enumerate-tasks-run-in-parallel
        /// <summary>
        /// Awaits and resolves all tasks in parallel and returns them in the order they complete
        /// </summary>
        public static async IAsyncEnumerable<T> ParallelAnyAsync<T>(this IEnumerable<Task<T>> tasks)
        {
            var remaining = new List<Task<T>>(tasks);

            while (remaining.Count != 0)
            {
                var task = await Task.WhenAny(remaining);
                remaining.Remove(task);
                yield return await task;
            }
        }

        /// <summary>
        /// Awaits and resolves all tasks in parallel and returns them in order
        /// </summary>
        public static async IAsyncEnumerable<T> ParallelAllAsync<T>(this IEnumerable<Task<T>> tasks)
        {
            foreach (var t in await Task.WhenAll(tasks))
            {
                yield return t;
            }
        }
    }
}
