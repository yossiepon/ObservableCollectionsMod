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
    internal class SortedCoupleViewValueComparer<T, TKey, TView> : ISynchronizedCoupleView<T, TView>
        where TKey : notnull
    {
#if DEBUG
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
#endif

        readonly IObservableCollection<T> source;
        readonly Func<T, TView> transform;
        readonly Func<T, TKey> identitySelector;
        readonly List<(T Value, TKey Key)> keyList;
        readonly List<(T Value, TView View)> valueList;

        ISynchronizedViewFilter<T, TView> filter;

        private bool disposeElement;

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public SortedCoupleViewValueComparer(IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer, bool disposeElement)
        {
            this.source = source;
            this.identitySelector = identitySelector;
            this.transform = transform;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            this.disposeElement = disposeElement;
            lock (source.SyncRoot)
            {
                var dict = new SortedDictionary<(T, TKey), (T, TView)>(new Comparer(comparer));
                foreach (var v in source)
                {
                    dict.Add((v, identitySelector(v)), (v, transform(v)));
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

        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView(bool disposeParent, bool disposeElement)
        {
            lock (SyncRoot)
            {
                return new SynchronizedSingleView<T, TView>(this, disposeParent, disposeElement);
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
#if DEBUG
            logger.Trace("{0} disposing SortedCoupleView (ValueComparer)...", this.GetType().FullName);
#endif

            this.source.CollectionChanged -= SourceCollectionChanged;

            if (this.disposeElement)
            {
#if DEBUG
                logger.Trace("{0} (T, TView) elements disposing...", this.GetType().FullName);
#endif

                foreach (var item in this.valueList)
                {
                    if (item.View is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

#if DEBUG
                logger.Trace("{0} (T, TView) elements disposed.", this.GetType().FullName);
#endif
            }

#if DEBUG
            logger.Trace("{0} SortedCoupleView (ValueComparer) disposed.", this.GetType().FullName);
#endif
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
                        // ReplaceRange is not supported in all ObservableCollections collections
                        // Replace is remove old item and insert new item.
                        {
                            RemoveOldItem(e, e.OldItem);
                            AddNewItem(e, e.NewItem);
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        {
                            // Move(index change) does not affect sorted list.
                            MoveItem(e, e.OldItem);
                        }
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
            var key = (newItem, id);

            int newStartingIndex = keyList.BinarySearch(key);

            newStartingIndex = newStartingIndex < 0 ? ~newStartingIndex : newStartingIndex + 1;

            var view = transform(newItem);
            var value = (newItem, view);

            keyList.Insert(newStartingIndex, key);
            valueList.Insert(newStartingIndex, value);

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
            var key = (oldItem, id);

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
            var key = (oldItem, id);

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

        sealed class Comparer : IComparer<(T value, TKey id)>
        {
            readonly IComparer<T> comparer;

            public Comparer(IComparer<T> comparer)
            {
                this.comparer = comparer;
            }

            public int Compare((T value, TKey id) x, (T value, TKey id) y)
            {
                var compare = comparer.Compare(x.value, y.value);
                if (compare == 0)
                {
                    compare = Comparer<TKey>.Default.Compare(x.id, y.id);
                }

                return compare;
            }
        }
    }
}
