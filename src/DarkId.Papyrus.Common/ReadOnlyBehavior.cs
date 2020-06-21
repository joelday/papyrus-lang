using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text;

namespace DarkId.Papyrus.Common
{
    /// <summary>
    /// This doesn't exist in Rx standard libraries because it encourages a hybrid between Rx patterns and non-normal patterns:
    /// https://stackoverflow.com/questions/36895225/does-a-read-only-behaviorsubject-interface-exist-in-rx-and-if-not-is-it-a-bad-i
    ///
    /// This sort of concept does exist, though, in libraries like RxUI for GUIs.  However, in a non-GUI space, a custom wrapper is needed
    /// to act as the hybrid crossover between Rx-pure worlds and the typical usages where you want to get the current value at haphazard times.
    /// 
    /// If we ever import RxUI and want to utilize NotifyPropertyChanged systems to power this sort of logic, then this class can be removed.
    /// </summary>
    public class Behavior<T> : IReadOnlyBehavior<T>
    {
        private readonly BehaviorSubject<T> _behavior;

        public Behavior(T startingVal = default)
        {
            _behavior = new BehaviorSubject<T>(startingVal);
        }

        public T Value
        {
            get => _behavior.Value;
            set => _behavior.OnNext(value);
        }

        public IDisposable Subscribe(IObserver<T> observer) => _behavior.Subscribe(observer);
    }

    public interface IReadOnlyBehavior<T> : IObservable<T>
    {
        T Value { get; }
    }
}
