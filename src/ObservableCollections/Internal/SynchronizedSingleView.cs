#if DEBUG
using NLog;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;

namespace ObservableCollections.Internal
{
    internal partial class SynchronizedSingleView<T, TView> : ISynchronizedSingleView<T, TView>
    {
#if DEBUG
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
#endif

        readonly ISynchronizedCoupleView<T, TView> parent;
        readonly List<TView> list;

        private readonly bool disposeParent;
        private readonly bool disposeElement;

        public event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged;

        public object SyncRoot { get; }

        public SynchronizedSingleView(ISynchronizedCoupleView<T, TView> parent, bool disposeParent, bool disposeElement)
        {
            this.parent = parent;
            this.disposeParent = disposeParent;
            this.disposeElement = disposeElement;
            this.SyncRoot = new object();
            lock (parent.SyncRoot)
            {
                this.list = parent.Select(x => x.Item2).ToList();
                this.parent.RoutingCollectionChanged += Parent_RoutingCollectionChanged;
            }
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

        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged
        {
            add { parent.CollectionStateChanged += value; }
            remove { parent.CollectionStateChanged -= value; }
        }

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter) => parent.AttachFilter(filter);
        public void ResetFilter(Action<T, TView>? resetAction) => parent.ResetFilter(resetAction);

        public INotifyCollectionChangedSynchronizedSingleView<T, TView> WithINotifyCollectionChanged()
        {
            lock (SyncRoot)
            {
                return new NotifyCollectionChangedSynchronizedSingleView<T, TView>(this);
            }
        }

        public IEnumerator<TView> GetEnumerator()
        {
            lock (SyncRoot)
            {
                foreach (var item in list)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
#if DEBUG
            logger.Trace("{0} disposing SingleView...", this.GetType().FullName);
#endif

            this.parent.RoutingCollectionChanged -= Parent_RoutingCollectionChanged;

            if (this.disposeElement)
            {
#if DEBUG
                logger.Trace("{0} TView elements disposing...", this.GetType().FullName);
#endif

                foreach (var item in this.list)
                {
                    if (item is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

#if DEBUG
                logger.Trace("{0} TView elements disposed.", this.GetType().FullName);
#endif
            }

            if (this.disposeParent)
            {
#if DEBUG
                logger.Trace("{0} parent disposing...", this.GetType().FullName);
#endif

                this.parent.Dispose();

#if DEBUG
                logger.Trace("{0} parent disposed.", this.GetType().FullName);
#endif
            }

#if DEBUG
            logger.Trace("{0} SingleView disposed.", this.GetType().FullName);
#endif
        }

        private void Parent_RoutingCollectionChanged(in NotifyCollectionChangedEventArgs<(T, TView)> e)
        {
            lock (SyncRoot)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        // Add
                        if (e.NewStartingIndex == list.Count)
                        {
                            if (e.IsSingleItem)
                            {
                                var v = e.NewItem.Item2;
                                list.Add(v);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(v, e.NewStartingIndex));
                            }
                            else
                            {
                                // inefficient copy, need refactoring
                                var newArray = new TView[e.NewItems.Length];
                                var span = e.NewItems;
                                for (int i = 0; i < span.Length; i++)
                                {
                                    var v = span[i].Item2;
                                    newArray[i] = v;
                                }
                                list.AddRange(newArray);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(newArray, e.NewStartingIndex));
                            }
                        }
                        // Insert
                        else
                        {
                            if (e.IsSingleItem)
                            {
                                var v = e.NewItem.Item2;
                                list.Insert(e.NewStartingIndex, v);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(v, e.NewStartingIndex));
                            }
                            else
                            {
                                // inefficient copy, need refactoring
                                var newArray = new TView[e.NewItems.Length];
                                var span = e.NewItems;
                                for (int i = 0; i < span.Length; i++)
                                {
                                    var v = span[i].Item2;
                                    newArray[i] = v;
                                }
                                list.InsertRange(e.NewStartingIndex, newArray);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(newArray, e.NewStartingIndex));
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.IsSingleItem)
                        {
                            var v = list[e.OldStartingIndex];
                            list.RemoveAt(e.OldStartingIndex);
                            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Remove(v, e.OldStartingIndex));
                        }
                        else
                        {
#if NET5_0_OR_GREATER
                            var range = CollectionsMarshal.AsSpan(list).Slice(e.OldStartingIndex, e.OldItems.Length);
#else
                            var range = list.GetRange(e.OldStartingIndex, e.OldItems.Length);
#endif
                            using (var xs = new CloneCollection<TView>(range))
                            {
                                list.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Remove(xs.Span, e.OldStartingIndex));
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        // ObservableList does not support replace range
                        {
                            var v = e.NewItem.Item2;

                            var oldItem = list[e.NewStartingIndex];
                            list[e.NewStartingIndex] = v;

                            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Replace(v, oldItem, e.NewStartingIndex));
                            break;
                        }
                    case NotifyCollectionChangedAction.Move:
                        {
                            var removeItem = list[e.OldStartingIndex];
                            list.RemoveAt(e.OldStartingIndex);
                            list.Insert(e.NewStartingIndex, removeItem);

                            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Move(removeItem, e.NewStartingIndex, e.OldStartingIndex));
                        }
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        {
                            list.Clear();
                            list.AddRange(parent.Select(x => x.Item2));
                            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Reset());
                        }
                        break;
                    default:
                        break;
                }

            }
        }

    }
}