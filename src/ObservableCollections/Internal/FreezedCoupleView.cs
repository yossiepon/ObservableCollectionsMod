﻿#pragma warning disable CS0067

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal sealed class FreezedCoupleView<T, TView> : ISynchronizedCoupleView<T, TView>
    {
        readonly bool reverse;
        readonly List<(T, TView)> list;

        ISynchronizedViewFilter<T, TView> filter;

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public FreezedCoupleView(IEnumerable<T> source, Func<T, TView> selector, bool reverse)
        {
            this.reverse = reverse;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            this.list = source.Select(x => (x, selector(x))).ToList();
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

        }

        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView()
        {
            return new SynchronizedSingleView<T, TView>(this);
        }

        public INotifyCollectionChangedSynchronizedCoupleView<T, TView> WithINotifyCollectionChanged()
        {
            return new NotifyCollectionChangedSynchronizedCoupleView<T, TView>(this);
        }
    }

    internal sealed class FreezedSortableCoupleView<T, TView> : ISortableSynchronizedCoupleView<T, TView>
    {
        readonly (T, TView)[] array;

        ISynchronizedViewFilter<T, TView> filter;

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public FreezedSortableCoupleView(IEnumerable<T> source, Func<T, TView> selector)
        {
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            this.array = source.Select(x => (x, selector(x))).ToArray();
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

        }

        public void Sort(IComparer<T> valueComparer)
        {
            Array.Sort(array, new TComparer(valueComparer));
        }

        public void Sort(IComparer<TView> viewComparer)
        {
            Array.Sort(array, new TViewComparer(viewComparer));
        }

        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView()
        {
            return new SynchronizedSingleView<T, TView>(this);
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