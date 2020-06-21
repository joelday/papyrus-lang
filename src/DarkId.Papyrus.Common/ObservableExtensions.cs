using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DarkId.Papyrus.Common
{
    public static class ObservableExtensions
    {
        public static IDisposable Subscribe<T>(this IObservable<T> source, Action action)
        {
            return source.Subscribe((i) => action());
        }

        public static IDisposable Subscribe<T>(this IObservable<T> source, Func<Task> action)
        {
            return source
                .SelectMany(async i =>
                {
                    await action().ConfigureAwait(false);
                    return System.Reactive.Unit.Default;
                })
                .Subscribe();
        }

        public static IDisposable Subscribe<T>(this IObservable<T> source, Func<T, Task> action)
        {
            return source
                .SelectMany(async i =>
                {
                    await action(i).ConfigureAwait(false);
                    return System.Reactive.Unit.Default;
                })
                .Subscribe();
        }

        public static IObservable<(T Previous, T Current)> WithPrevious<T>(this IObservable<T> source)
        {
            T prevStorage = default;
            return source.Select(i =>
            {
                var prev = prevStorage;
                prevStorage = i;
                return (prev, i);
            });
        }

        public static IObservable<R> Cast<T, R>(this IObservable<T> source)
            where T : R
        {
            return source.Select<T, R>(x => x);
        }

        public static IObservable<Unit> SelectTask<T>(this IObservable<T> source, Func<T, Task> task)
        {
            return source
                .SelectMany(async i =>
                {
                    await task(i).ConfigureAwait(false);
                    return System.Reactive.Unit.Default;
                });
        }

        public static IObservable<Unit> SelectTask<T>(this IObservable<T> source, Func<Task> task)
        {
            return source
                .SelectMany(async _ =>
                {
                    await task().ConfigureAwait(false);
                    return System.Reactive.Unit.Default;
                });
        }

        public static IObservable<R> SelectTask<T, R>(this IObservable<T> source, Func<Task<R>> task)
        {
            return source
                .SelectMany(_ => task());
        }

        public static IObservable<T> FilterSwitch<T>(this IObservable<T> source, IObservable<bool> filterSwitch)
        {
            return filterSwitch
                .Select(on =>
                {
                    if (on)
                    {
                        return source;
                    }
                    else
                    {
                        return Observable.Empty<T>();
                    }
                })
                .Switch();
        }

        public static IObservable<System.Reactive.Unit> Unit<T>(this IObservable<T> source)
        {
            return source.Select(u => System.Reactive.Unit.Default);
        }

        public static IObservable<T> NotNull<T>(this IObservable<T> source)
            where T : class
        {
            return source.Where(u => u != null);
        }

        public static IObservable<TSource> PublishRefCount<TSource>(this IObservable<TSource> source)
        {
            return source.Publish().RefCount();
        }

        public static IObservable<TSource> DisposeWith<TSource>(this IObservable<TSource> source, CompositeDisposable composite)
            where TSource : IDisposable
        {
            SerialDisposable serialDisposable = new SerialDisposable();
            composite.Add(serialDisposable);
            return source.Do(
                (item) =>
                {
                    serialDisposable.Disposable = item;
                });
        }

        public static IObservable<TRet> SelectLatestFrom<TSource, TRet>(this IObservable<TSource> source, IObservable<TRet> from)
        {
            return source.WithLatestFrom(
                from,
                resultSelector: (s, f) => f);
        }

        public static IObservable<TRet> SelectLatest<TSource, TRet>(this IObservable<TSource> source, IObservable<TRet> from)
        {
            return source.CombineLatest(
                from,
                resultSelector: (s, f) => f);
        }

        /// Inspiration:
        /// http://reactivex.io/documentation/operators/debounce.html
        /// https://stackoverflow.com/questions/20034476/how-can-i-use-reactive-extensions-to-throttle-events-using-a-max-window-size
        public static IObservable<T> Debounce<T>(this IObservable<T> source, TimeSpan interval, IScheduler scheduler)
        {
            return Observable.Create<T>(o =>
            {
                var hasValue = false;
                bool throttling = false;
                T value = default;

                var dueTimeDisposable = new SerialDisposable();

                void internalCallback()
                {
                    if (hasValue)
                    {
                        // We have another value that came in to fire.
                        // Reregister for callback
                        dueTimeDisposable.Disposable = scheduler.Schedule(interval, internalCallback);
                        o.OnNext(value);
                        value = default;
                        hasValue = false;
                    }
                    else
                    {
                        // Nothing to do, throttle is complete.
                        throttling = false;
                    }
                }

                return source.Subscribe(
                    onNext: (x) =>
                    {
                        if (!throttling)
                        {
                            // Fire initial value
                            o.OnNext(x);
                            // Mark that we're throttling
                            throttling = true;
                            // Register for callback when throttle is complete
                            dueTimeDisposable.Disposable = scheduler.Schedule(interval, internalCallback);
                        }
                        else
                        {
                            // In the middle of throttle
                            // Save value and return
                            hasValue = true;
                            value = x;
                        }
                    },
                    onError: o.OnError,
                    onCompleted: o.OnCompleted);
            });
        }

    }
}
