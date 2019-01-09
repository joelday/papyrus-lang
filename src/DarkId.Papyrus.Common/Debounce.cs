using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class Debounce : IDisposable
    {
        private readonly object _lock = new object();

        private readonly Func<Task> _asyncFunc;
        private readonly TimeSpan _throttleDuration;

        private volatile Task _currentTask;
        private volatile bool _hasTrailing;
        private volatile bool _isDisposed;

        public Debounce(Func<Task> asyncFunc, TimeSpan throttleDuration)
        {
            _asyncFunc = asyncFunc;
            _throttleDuration = throttleDuration;
        }

        public void Trigger()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_lock)
            {
                if (_currentTask != null)
                {
                    _hasTrailing = true;
                    return;
                }

                _hasTrailing = false;

                _currentTask = Task.Run(async () =>
                {
                    try
                    {
                        await _asyncFunc();
                    }
                    finally
                    {
                        await Task.Delay(_throttleDuration);

                        _currentTask = null;
                        if (_hasTrailing)
                        {
                            Trigger();
                        }
                    }
                });
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
