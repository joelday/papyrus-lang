using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public static class TaskExtensions
    {
        public static T WaitForResult<T>(this Task<T> task)
        {
            task.Wait();

            return !task.IsFaulted ? task.Result : default;
        }
        
        public static void AndIgnore(this Task _)
        {
            // Noop.
        }
    }
}