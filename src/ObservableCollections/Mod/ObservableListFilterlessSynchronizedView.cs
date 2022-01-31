using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

#nullable enable

namespace ObservableCollections.Mod
{
    internal class ObservableListFilterlessSynchronizedView<T, TView> : IFilterlessSynchronizedView<T, TView>
    {
        readonly ObservableList<T> source;
        readonly Func<T, TView> selector;
        readonly bool reverse;
        readonly List<TView> list;

        public event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; }

        public ObservableListFilterlessSynchronizedView(ObservableList<T> source, Func<T, TView> selector, bool reverse)
        {
            this.source = source;
            this.selector = selector;
            this.reverse = reverse;
            this.SyncRoot = new object();
            lock (source.SyncRoot)
            {
                FieldInfo? info = source.GetType().GetField("list", BindingFlags.NonPublic | BindingFlags.Instance);
                List<T>? list = (List<T>?)info?.GetValue(source);
                this.list = list != null ? list.Select(x => selector(x)).ToList() : new();
                this.source.CollectionChanged += SourceCollectionChanged;
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

        public IFilterlessNotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
        {
            lock (SyncRoot)
            {
                return new ObservableListFilterlessNotifyCollectionChangedSynchronizedView<T, TView>(this);
            }
        }

        public IEnumerator<TView> GetEnumerator()
        {
            lock (SyncRoot)
            {
                if (!reverse)
                {
                    foreach (var item in list)
                    {
                        yield return item;
                    }
                }
                else
                {
                    foreach (var item in list.AsEnumerable().Reverse())
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
                        // Add
                        if (e.NewStartingIndex == list.Count)
                        {
                            if (e.IsSingleItem)
                            {
                                var v = selector(e.NewItem);
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
                                    var v = selector(span[i]);
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
                                var v = selector(e.NewItem);
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
                                    var v = selector(span[i]);
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
                            var v = selector(e.NewItem);

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
                        list.Clear();
                        RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Reset());
                        break;
                    default:
                        break;
                }

                CollectionStateChanged?.Invoke(e.Action);
            }
        }
    }
}

#nullable restore