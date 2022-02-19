using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;

namespace ObservableCollections
{
    public sealed partial class ObservableList<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
    {
        public ISynchronizedViewForWpf<T, TView> CreateViewForWpf<TView>(Func<T, TView> transform, bool reverse = false)
        {
            return new ViewForWpf<TView>(this, transform, reverse);
        }

        sealed class ViewForWpf<TView> : ISynchronizedViewForWpf<T, TView>, IList
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            readonly bool reverse;
            readonly List<(T, TView)> list;
            readonly List<TView> viewList;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public ViewForWpf(ObservableList<T> source, Func<T, TView> selector, bool reverse)
            {
                this.source = source;
                this.selector = selector;
                this.reverse = reverse;
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.list = source.list.Select(x => (x, selector(x))).ToList();
                    this.viewList = this.list.Select(x => x.Item2).ToList();
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

            bool IList.IsFixedSize => true;
            bool IList.IsReadOnly => true;
            bool ICollection.IsSynchronized => true;

            object? IList.this[int index]
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return viewList[index];
                    }
                }
                set => throw new NotSupportedException();
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

            public INotifyCollectionChangedSynchronizedViewForWpf<T, TView> WithINotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedViewForWpf<T, TView>(this);
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
                            if (filter.IsMatch(item.Item1, item.Item2))
                            {
                                yield return item.Item2;
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in list.AsEnumerable().Reverse())
                        {
                            if (filter.IsMatch(item.Item1, item.Item2))
                            {
                                yield return item.Item2;
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
                                    viewList.Add(v.Item2);
                                    filter.InvokeOnAdd(v, e);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(v.Item2, e.NewStartingIndex));
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
                                    var newViewArray = newArray.Select(x => x.Item2).ToArray();
                                    viewList.AddRange(newViewArray);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(newViewArray, e.NewStartingIndex));
                                }
                            }
                            // Insert
                            else
                            {
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    list.Insert(e.NewStartingIndex, v);
                                    viewList.Insert(e.NewStartingIndex, v.Item2);
                                    filter.InvokeOnAdd(v, e);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(v.Item2, e.NewStartingIndex));
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
                                    var newViewArray = newArray.Select(x => x.Item2).ToArray();
                                    viewList.InsertRange(e.NewStartingIndex, newViewArray);
                                    RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Add(newViewArray, e.NewStartingIndex));
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (e.IsSingleItem)
                            {
                                var v = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                viewList.RemoveAt(e.OldStartingIndex);
                                filter.InvokeOnRemove(v, e);
                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Remove(v.Item2, e.OldStartingIndex));
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
                                    using (var xs2 = new CloneCollection<TView>(xs.AsEnumerable().Select(x => x.Item2)))
                                    {
                                        list.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                                        viewList.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                                        RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Remove(xs2.Span, e.OldStartingIndex));
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            // ObservableList does not support replace range
                            {
                                var v = (e.NewItem, selector(e.NewItem));

                                var oldItem = list[e.NewStartingIndex];
                                list[e.NewStartingIndex] = v;
                                viewList[e.NewStartingIndex] = v.Item2;

                                filter.InvokeOnRemove(oldItem, e);
                                filter.InvokeOnAdd(v, e);

                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Replace(v.Item2, oldItem.Item2, e.NewStartingIndex));
                                break;
                            }
                        case NotifyCollectionChangedAction.Move:
                            {
                                var removeItem = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                list.Insert(e.NewStartingIndex, removeItem);
                                viewList.RemoveAt(e.OldStartingIndex);
                                viewList.Insert(e.NewStartingIndex, removeItem.Item2);

                                filter.InvokeOnMove(removeItem, e);

                                RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Move(removeItem.Item2, e.NewStartingIndex, e.OldStartingIndex));
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
                            viewList.Clear();
                            RoutingCollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<TView>.Reset());
                            break;
                        default:
                            break;
                    }

                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

            int IList.Add(object? value)
            {
                throw new NotSupportedException();
            }

            void IList.Clear()
            {
                throw new NotSupportedException();
            }

            bool IList.Contains(object? value)
            {
                lock (SyncRoot)
                {
                    return ((IList)viewList).Contains(value);
                }
            }

            int IList.IndexOf(object? value)
            {
                lock (SyncRoot)
                {
                    return ((IList)viewList).IndexOf(value);
                }
            }

            void IList.Insert(int index, object? value)
            {
                throw new NotSupportedException();
            }

            void IList.Remove(object? value)
            {
                throw new NotSupportedException();
            }

            void IList.RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            void ICollection.CopyTo(Array array, int index)
            {
                lock (SyncRoot)
                {
                    ((IList)viewList).CopyTo(array, index);
                }
            }
        }
    }
}