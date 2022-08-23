#pragma warning disable CS0067

#if DEBUG
using NLog;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal sealed class FreezedCoupleView<T, TView> : ISynchronizedCoupleView<T, TView>
    {
#if DEBUG
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
#endif

        readonly bool reverse;
        readonly List<(T, TView)> list;

        ISynchronizedViewFilter<T, TView> filter;

        private readonly bool disposeElement;

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public FreezedCoupleView(IEnumerable<T> source, Func<T, TView> transform, bool reverse, bool disposeElement)
        {
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            this.list = source.Select(x => (x, transform(x))).ToList();

            this.reverse = reverse;
            this.disposeElement = disposeElement;
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return list.Count;
                }
            }
        }

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
        {
            lock (SyncRoot)
            {
                this.filter = filter;
                foreach (var (value, view) in list)
                {
                    filter.InvokeOnAttach(value, view);
                }
            }
        }

        public void ResetFilter(Action<T, TView>? resetAction)
        {
            lock (SyncRoot)
            {
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                if (resetAction != null)
                {
                    foreach (var (item, view) in list)
                    {
                        resetAction(item, view);
                    }
                }
            }
        }

        public IEnumerator<(T, TView)> GetEnumerator()
        {
            lock (SyncRoot)
            {
                if (!reverse)
                {
                    foreach (var item in list)
                    {
                        if (filter.IsMatch(item.Item1, item.Item2))
                        {
                            yield return item;
                        }
                    }
                }
                else
                {
                    foreach (var item in list.AsEnumerable().Reverse())
                    {
                        if (filter.IsMatch(item.Item1, item.Item2))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
#if DEBUG
            logger.Trace("{0} disposing FreezedCoupleView...", this.GetType().FullName);
#endif

            if (this.disposeElement)
            {
#if DEBUG
                logger.Trace("{0} (T, TView) elements disposing...", this.GetType().FullName);
#endif

                foreach (var item in this.list)
                {
                    if (item.Item2 is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
#if DEBUG
                logger.Trace("{0} (T, TView) elements disposed.", this.GetType().FullName);
#endif
            }

#if DEBUG
            logger.Trace("{0} FreezedCoupleView disposed.", this.GetType().FullName);
#endif
        }

        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView(bool disposeParent, bool disposeElement)
        {
            return new SynchronizedSingleView<T, TView>(this, disposeParent, disposeElement);
        }

        public INotifyCollectionChangedSynchronizedCoupleView<T, TView> WithINotifyCollectionChanged()
        {
            return new NotifyCollectionChangedSynchronizedCoupleView<T, TView>(this);
        }
    }

    internal sealed class FreezedSortableCoupleView<T, TView> : ISortableSynchronizedCoupleView<T, TView>
    {
#if DEBUG
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
#endif

        readonly (T, TView)[] array;
        ISynchronizedViewFilter<T, TView> filter;

        private readonly bool disposeElement;

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public FreezedSortableCoupleView(IEnumerable<T> source, Func<T, TView> transform, bool disposeElement)
        {
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            this.array = source.Select(x => (x, transform(x))).ToArray();
            this.disposeElement = disposeElement;
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return array.Length;
                }
            }
        }

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
        {
            lock (SyncRoot)
            {
                this.filter = filter;
                foreach (var (value, view) in array)
                {
                    filter.InvokeOnAttach(value, view);
                }
            }
        }

        public void ResetFilter(Action<T, TView>? resetAction)
        {
            lock (SyncRoot)
            {
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                if (resetAction != null)
                {
                    foreach (var (item, view) in array)
                    {
                        resetAction(item, view);
                    }
                }
            }
        }

        public IEnumerator<(T, TView)> GetEnumerator()
        {
            lock (SyncRoot)
            {
                foreach (var item in array)
                {
                    if (filter.IsMatch(item.Item1, item.Item2))
                    {
                        yield return item;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
#if DEBUG
            logger.Trace("{0} disposing FreezedSortableCoupleView...", this.GetType().FullName);
#endif

            if (this.disposeElement)
            {
#if DEBUG
                logger.Trace("{0} (T, TView) elements disposing...", this.GetType().FullName);
#endif

                foreach (var item in this.array)
                {
                    if (item.Item2 is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
#if DEBUG
                logger.Trace("{0} (T, TView) elements disposed.", this.GetType().FullName);
#endif
            }

#if DEBUG
            logger.Trace("{0} FreezedSortableCoupleView disposed.", this.GetType().FullName);
#endif
        }

            public void Sort(IComparer<T> valueComparer)
        {
            Array.Sort(array, new TComparer(valueComparer));

            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Reset());
        }

        public void Sort(IComparer<TView> viewComparer)
        {
            Array.Sort(array, new TViewComparer(viewComparer));

            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Reset());
        }

        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView(bool disposeParent, bool disposeElement)
        {
            return new SynchronizedSingleView<T, TView>(this, disposeParent, disposeElement);
        }

        public INotifyCollectionChangedSynchronizedCoupleView<T, TView> WithINotifyCollectionChanged()
        {
            return new NotifyCollectionChangedSynchronizedCoupleView<T, TView>(this);
        }

        class TComparer : IComparer<(T, TView)>
        {
            readonly IComparer<T> comparer;

            public TComparer(IComparer<T> comparer)
            {
                this.comparer = comparer;
            }

            public int Compare((T, TView) x, (T, TView) y)
            {
                return comparer.Compare(x.Item1, y.Item1);
            }
        }

        class TViewComparer : IComparer<(T, TView)>
        {
            readonly IComparer<TView> comparer;

            public TViewComparer(IComparer<TView> comparer)
            {
                this.comparer = comparer;
            }

            public int Compare((T, TView) x, (T, TView) y)
            {
                return comparer.Compare(x.Item2, y.Item2);
            }
        }
    }
}