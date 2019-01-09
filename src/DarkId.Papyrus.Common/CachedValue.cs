using System;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class CachedValue<T> where T : class
    {
        private readonly object _lock = new object();
        private readonly Func<T> _valueFunc;
        private readonly Func<T, bool> _isInvalidatedFunc;
        private Thread _currentUpdateThread;

        private T _value;

        public CachedValue(Func<T> valueFunc, Func<T, bool> isInvalidatedFunc = null)
        {
            _valueFunc = valueFunc;
            _isInvalidatedFunc = isInvalidatedFunc;
        }

        public T Value
        {
            get
            {
                lock (_lock)
                {
                    RefreshIfInvalidated();
                    return _value;
                }
            }
        }

        public T CurrentValue => _value;

        public void RefreshIfInvalidated()
        {
            lock (_lock)
            {
                if (_currentUpdateThread == Thread.CurrentThread)
                {
                    throw new InvalidOperationException("Attempted to read value from the current updating thread.");
                }

                try
                {
                    if (_value == null || (_isInvalidatedFunc != null && _isInvalidatedFunc(_value)))
                    {
                        _currentUpdateThread = Thread.CurrentThread;

                        var newValue = _valueFunc();
                        if (_value != newValue)
                        {
                            (_value as IDisposable)?.Dispose();
                            _value = newValue;
                        }
                    }
                }
                finally
                {
                    _currentUpdateThread = null;
                }
            }
        }

        public static implicit operator T(CachedValue<T> value)
        {
            return value.Value;
        }
    }
}