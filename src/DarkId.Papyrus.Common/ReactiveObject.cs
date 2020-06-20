using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace DarkId.Papyrus.Common
{
    /// <summary>
    /// A convenience class to wrap common disposable mechanics
    /// </summary>
    public class DisposableObject : IDisposable
    {
        private readonly CompositeDisposable _dispose = new CompositeDisposable();

        public virtual void Dispose()
        {
            _dispose.Dispose();
        }

        public void Add(IDisposable disposable)
        {
            _dispose.Add(disposable);
        }
    }
}
