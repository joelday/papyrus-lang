using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Common
{
    public static class TaskExtensions
    {
        public static T WaitForResult<T>(this Task<T> task)
        {
            task.Wait();

            if (!task.IsFaulted)
            {
                return task.Result;
            }

            return default(T);
        }
        
        public static void AndIgnore(this Task _)
        {
            // Noop.
        }
    }
}