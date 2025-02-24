﻿#if DEBUG
using NLog;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections.Internal
{
    internal partial class SynchronizedSingleView<T, TView> : ISynchronizedSingleView<T, TView>
    {
        public INotifyCollectionChangedListSynchronizedSingleView<T, TView> WithINotifyCollectionChangedList()
        {
            return new NCCListView(this);
        }

        class NCCListView : INotifyCollectionChangedListSynchronizedSingleView<T, TView>
        {
#if DEBUG
            private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
#endif

            readonly ISynchronizedSingleView<T, TView> parent;
            static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new(nameof(Count));

            public NCCListView(ISynchronizedSingleView<T, TView> parent)
            {
                this.parent = parent;
                this.parent.RoutingCollectionChanged += Parent_RoutingCollectionChanged;
            }

            private void Parent_RoutingCollectionChanged(in NotifyCollectionChangedEventArgs<TView> e)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.IsSingleItem)
                        {
                            CollectionChanged?.Invoke(this, e.ToStandardEventArgs());
                        }
                        else
                        {
                            var newItems = e.NewItems.ToArray();
                            for (int i = 0; i < newItems.Length; i++)
                            {
                                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(e.Action, newItems[i], e.NewStartingIndex + i));
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.IsSingleItem)
                        {
                            CollectionChanged?.Invoke(this, e.ToStandardEventArgs());
                        }
                        else
                        {
                            var oldItems = e.OldItems.ToArray();
                            for (int i = oldItems.Length - 1; i >= 0; i++)
                            {
                                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(e.Action, oldItems[i], e.OldStartingIndex + i));
                            }
                        }
                        break;
                    default:
                        CollectionChanged?.Invoke(this, e.ToStandardEventArgs());
                        break;
                }

                switch (e.Action)
                {
                    // add, remove, reset will change the count.
                    case NotifyCollectionChangedAction.Add:
                    case NotifyCollectionChangedAction.Remove:
                    case NotifyCollectionChangedAction.Reset:
                        PropertyChanged?.Invoke(this, CountPropertyChangedEventArgs);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Move:
                    default:
                        break;
                }
            }

            public object SyncRoot => parent.SyncRoot;

            public int Count => parent.Count;

            public event NotifyCollectionChangedEventHandler? CollectionChanged;
            public event PropertyChangedEventHandler? PropertyChanged;

            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged
            {
                add { parent.CollectionStateChanged += value; }
                remove { parent.CollectionStateChanged -= value; }
            }

            public event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged
            {
                add { parent.RoutingCollectionChanged += value; }
                remove { parent.RoutingCollectionChanged -= value; }
            }

            object? IList.this[int index]
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return parent is SynchronizedSingleView<T, TView> view ? view.list[index] : throw new NotSupportedException();
                    }
                }
                set => throw new NotSupportedException();
            }

            bool IList.IsFixedSize => true;
            bool IList.IsReadOnly => true;
            bool ICollection.IsSynchronized => true;

            public void AttachFilter(ISynchronizedViewFilter<T, TView> filter) => parent.AttachFilter(filter);
            public void ResetFilter(Action<T, TView>? resetAction) => parent.ResetFilter(resetAction);
            public INotifyCollectionChangedListSynchronizedSingleView<T, TView> WithINotifyCollectionChangedList() => this;
            public INotifyCollectionChangedSynchronizedSingleView<T, TView> WithINotifyCollectionChanged() => parent.WithINotifyCollectionChanged();

            public void Dispose()
            {
#if DEBUG
                logger.Trace("{0} disposing NCCListSingleView...", this.GetType().FullName);
#endif
                this.parent.RoutingCollectionChanged -= Parent_RoutingCollectionChanged;

#if DEBUG
                logger.Trace("{0} parent disposing...", this.GetType().FullName);
#endif

                parent.Dispose();

#if DEBUG
                logger.Trace("{0} parent and NCCListSingleView disposed.", this.GetType().FullName);
#endif
            }

            public IEnumerator<TView> GetEnumerator() => parent.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => parent.GetEnumerator();

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
                    return parent is SynchronizedSingleView<T, TView> view ? ((IList)view.list).Contains(value) : throw new NotSupportedException();
                }
            }

            int IList.IndexOf(object? value)
            {
                lock (SyncRoot)
                {
                    return parent is SynchronizedSingleView<T, TView> view ? ((IList)view.list).IndexOf(value) : throw new NotSupportedException();
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
                    if (parent is SynchronizedSingleView<T, TView> view)
                    {
                        ((ICollection)view.list).CopyTo(array, index);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }

        }
    }
}
