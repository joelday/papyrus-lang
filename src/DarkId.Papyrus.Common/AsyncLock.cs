using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class AsyncLock
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task<IDisposable> WaitAsync()
        {
            await _lock.WaitAsync();
            return Disposable.Create(() => _lock.Release());
        }
    }
}
