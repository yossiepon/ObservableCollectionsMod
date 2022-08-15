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
            return new ListView(this);
        }

        class ListView : INotifyCollectionChangedListSynchronizedSingleView<T, TView>
        {
            readonly ISynchronizedSingleView<T, TView> parent;
            static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new(nameof(Count));

            public ListView(ISynchronizedSingleView<T, TView> parent)
            {
                this.parent = parent;
                this.parent.RoutingCollectionChanged += Parent_RoutingCollectionChanged;
            }

            private void Parent_RoutingCollectionChanged(in NotifyCollectionChangedEventArgs<TView> e)
            {
                CollectionChanged?.Invoke(this, e.ToStandardEventArgs());

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
                this.parent.RoutingCollectionChanged -= Parent_RoutingCollectionChanged;
                parent.Dispose();
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