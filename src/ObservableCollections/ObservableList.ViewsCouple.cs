﻿using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;

namespace ObservableCollections
{
    public sealed partial class ObservableList<T> : IObservableCollectionToCoupleView<T>
    {
        public ISynchronizedCoupleView<T, TView> ToSynchronizedCoupleView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            return new SynchronizedCoupleView<TView>(this, transform, reverse);
        }

        sealed class SynchronizedCoupleView<TView> : ISynchronizedCoupleView<T, TView>
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            readonly bool reverse;
            readonly List<(T, TView)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public SynchronizedCoupleView(ObservableList<T> source, Func<T, TView> selector, bool reverse)
            {
                this.source = source;
                this.selector = selector;
                this.reverse = reverse;
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.list = source.list.Select(x => (x, selector(x))).ToList();
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
                                    var v = (e.NewItem, selector(e.NewItem));
                                    list.Add(v);
                                    filter.InvokeOnAdd(v, e);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Add(v, e.NewStartingIndex));
                                }
                                else
                                {
                                    // inefficient copy, need refactoring
                                    var newArray = new (T, TView)[e.NewItems.Length];
                                    var span = e.NewItems;
                                    for (int i = 0; i < span.Length; i++)
                                    {
                                        var v = (span[i], selector(span[i]));
                                        newArray[i] = v;
                                        filter.InvokeOnAdd(v, e);
                                    }
                                    list.AddRange(newArray);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Add(newArray, e.NewStartingIndex));
                                }
                            }
                            // Insert
                            else
                            {
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    list.Insert(e.NewStartingIndex, v);
                                    filter.InvokeOnAdd(v, e);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Add(v, e.NewStartingIndex));
                                }
                                else
                                {
                                    // inefficient copy, need refactoring
                                    var newArray = new (T, TView)[e.NewItems.Length];
                                    var span = e.NewItems;
                                    for (int i = 0; i < span.Length; i++)
                                    {
                                        var v = (span[i], selector(span[i]));
                                        newArray[i] = v;
                                        filter.InvokeOnAdd(v, e);
                                    }
                                    list.InsertRange(e.NewStartingIndex, newArray);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Add(newArray, e.NewStartingIndex));
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (e.IsSingleItem)
                            {
                                var v = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                filter.InvokeOnRemove(v, e);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Remove(v, e.OldStartingIndex));
                            }
                            else
                            {
                                var len = e.OldStartingIndex + e.OldItems.Length;
                                for (int i = e.OldStartingIndex; i < len; i++)
                                {
                                    var v = list[i];
                                    filter.InvokeOnRemove(v, e);
                                }

#if NET5_0_OR_GREATER
                                var range = CollectionsMarshal.AsSpan(list).Slice(e.OldStartingIndex, e.OldItems.Length);
#else
                                var range = list.GetRange(e.OldStartingIndex, e.OldItems.Length);
#endif
                                using (var xs = new CloneCollection<(T, TView)>(range))
                                {
                                    list.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Remove(xs.Span, e.OldStartingIndex));
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            // ObservableList does not support replace range
                            {
                                var v = (e.NewItem, selector(e.NewItem));

                                var oldItem = list[e.NewStartingIndex];
                                list[e.NewStartingIndex] = v;

                                filter.InvokeOnRemove(oldItem, e);
                                filter.InvokeOnAdd(v, e);

                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Replace(v, oldItem, e.NewStartingIndex));
                                break;
                            }
                        case NotifyCollectionChangedAction.Move:
                            {
                                var removeItem = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                list.Insert(e.NewStartingIndex, removeItem);

                                filter.InvokeOnMove(removeItem, e);

                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Move(removeItem, e.NewStartingIndex, e.OldStartingIndex));
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (!filter.IsNullFilter())
                            {
                                foreach (var item in list)
                                {
                                    filter.InvokeOnRemove(item, e);
                                }
                            }
                            list.Clear();
                            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<(T, TView)>.Reset());
                            break;
                        default:
                            break;
                    }

                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

        }
    }
}