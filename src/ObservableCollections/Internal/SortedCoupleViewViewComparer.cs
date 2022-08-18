using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal class SortedCoupleViewViewComparer<T, TKey, TView> : ISynchronizedCoupleView<T, TView>
            where TKey : notnull
    {
        readonly IObservableCollection<T> source;
        readonly Func<T, TView> transform;
        readonly Func<T, TKey> identitySelector;
        readonly Comparer comparer;
        readonly Dictionary<TKey, TView> viewMap; // view-map needs to use in remove.
        readonly List<(TView View, TKey Key)> keyList;
        readonly List<(T Value, TView View)> valueList;

        ISynchronizedViewFilter<T, TView> filter;

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public SortedCoupleViewViewComparer(IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> comparer)
        {
            this.source = source;
            this.identitySelector = identitySelector;
            this.transform = transform;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            lock (source.SyncRoot)
            {
                this.comparer = new Comparer(comparer);
                var dict = new SortedDictionary<(TView View, TKey Key), (T, TView)>(this.comparer);
                this.viewMap = new Dictionary<TKey, TView>();
                foreach (var value in source)
                {
                    var view = transform(value);
                    var id = identitySelector(value);
                    dict.Add((view, id), (value, view));
                    viewMap.Add(id, view);
                }

                this.keyList = dict.Keys.ToList();
                this.valueList = dict.Values.ToList();

                this.source.CollectionChanged += SourceCollectionChanged;
            }
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return keyList.Count;
                }
            }
        }

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
        {
            lock (SyncRoot)
            {
                this.filter = filter;
                foreach (var (value, view) in valueList)
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
                    foreach (var (value, view) in valueList)
                    {
                        resetAction(value, view);
                    }
                }
            }
        }

        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView()
        {
            lock (SyncRoot)
            {
                return new SynchronizedSingleView<T, TView>(this);
            }
        }

        public INotifyCollectionChangedSynchronizedCoupleView<T, TView> WithINotifyCollectionChanged()
        {
            lock (SyncRoot)
            {
                return new NotifyCollectionChangedSynchronizedCoupleView<T, TView>(this);
            }
        }

        public IEnumerator<(T, TView)> GetEnumerator()
        {

            lock (SyncRoot)
            {
                foreach (var item in valueList)
                {
                    if (filter.IsMatch(item.Value, item.View))
                    {
                        yield return item;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            this.source.CollectionChanged -= SourceCollectionChanged;
        }

        private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
        {
            lock (SyncRoot)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        {
                            // Add, Insert
                            if (e.IsSingleItem)
                            {
                                AddNewItem(e, e.NewItem);
                            }
                            else
                            {
                                foreach (var newItem in e.NewItems)
                                {
                                    AddNewItem(e, newItem);
                                }
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        {
                            if (e.IsSingleItem)
                            {
                                RemoveOldItem(e, e.OldItem);
                            }
                            else
                            {
                                foreach (var oldItem in e.OldItems)
                                {
                                    RemoveOldItem(e, oldItem);
                                }
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        // Replace is remove old item and insert new item.
                        {
                            RemoveOldItem(e, e.OldItem);
                            AddNewItem(e, e.NewItem);
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        // Move(index change) does not affect sorted list.
                        MoveItem(e, e.OldItem);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        if (!filter.IsNullFilter())
                        {
                            foreach (var value in valueList)
                            {
                                filter.InvokeOnRemove(value, e);
                            }
                        }
                        keyList.Clear();
                        valueList.Clear();
                        viewMap.Clear();
                        RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Reset());
                        break;
                    default:
                        break;
                }

                CollectionStateChanged?.Invoke(e.Action);
            }
        }

        private void AddNewItem(NotifyCollectionChangedEventArgs<T> e, T? newItem)
        {
            if (newItem == null)
            {
                throw new ArgumentNullException(nameof(newItem));
            }

            var id = identitySelector(newItem);
            var view = transform(newItem);

            var key = (view, id);
            int newStartingIndex = keyList.BinarySearch(key, this.comparer);

            newStartingIndex = newStartingIndex < 0 ? ~newStartingIndex : newStartingIndex + 1;

            var value = (newItem, view);

            keyList.Insert(newStartingIndex, key);
            valueList.Insert(newStartingIndex, value);

            viewMap.Add(id, view);

            filter.InvokeOnAdd(value, e);
            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Add(value, newStartingIndex));
        }

        private void RemoveOldItem(NotifyCollectionChangedEventArgs<T> e, T? oldItem)
        {
            if (oldItem == null)
            {
                throw new ArgumentNullException(nameof(oldItem));
            }

            var id = identitySelector(oldItem);

            if (!viewMap.Remove(id, out var view))
            {
                throw new ArgumentException($"old item id [{id}] not found.");
            }

            var key = (view, id);

            int oldStartingIndex = keyList.BinarySearch(key);

            if (oldStartingIndex < 0)
            {
                throw new ArgumentException($"old item [{oldItem}] not found.");
            }

            var value = valueList[oldStartingIndex];

            keyList.RemoveAt(oldStartingIndex);
            valueList.RemoveAt(oldStartingIndex);

            filter.InvokeOnRemove(value, e);
            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Remove(value, oldStartingIndex));
        }

        private void MoveItem(NotifyCollectionChangedEventArgs<T> e, T? oldItem)
        {
            if (oldItem == null)
            {
                throw new ArgumentNullException(nameof(oldItem));
            }

            var id = identitySelector(oldItem);

            if (!viewMap.TryGetValue(id, out var view))
            {
                throw new ArgumentException($"old item id [{id}] not found.");
            }

            var key = (view, id);

            int oldStartingIndex = keyList.BinarySearch(key);

            if (oldStartingIndex < 0)
            {
                throw new ArgumentException($"old item [{oldItem}] not found.");
            }

            var value = valueList[oldStartingIndex];

            // Move(index change) does not affect sorted list.

            filter.InvokeOnMove(value, e);
            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Move(value, oldStartingIndex, oldStartingIndex));
        }

        sealed class Comparer : IComparer<(TView view, TKey id)>
        {
            readonly IComparer<TView> comparer;

            public Comparer(IComparer<TView> comparer)
            {
                this.comparer = comparer;
            }

            public int Compare((TView view, TKey id) x, (TView view, TKey id) y)
            {
                var compare = comparer.Compare(x.view, y.view);
                if (compare == 0)
                {
                    compare = Comparer<TKey>.Default.Compare(x.id, y.id);
                }

                return compare;
            }
        }
    }
}