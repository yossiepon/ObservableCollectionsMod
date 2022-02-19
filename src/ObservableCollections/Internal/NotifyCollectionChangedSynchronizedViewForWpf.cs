using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace ObservableCollections.Internal
{
    internal class NotifyCollectionChangedSynchronizedViewForWpf<T, TView> : INotifyCollectionChangedSynchronizedViewForWpf<T, TView>, IList
    {
        readonly ISynchronizedViewForWpf<T, TView> parent;
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new(nameof(Count));

        public NotifyCollectionChangedSynchronizedViewForWpf(ISynchronizedViewForWpf<T, TView> parent)
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

        bool IList.IsFixedSize => true;
        bool IList.IsReadOnly => true;
        bool ICollection.IsSynchronized => true;

        object? IList.this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return parent is IList list ? list[index] : throw new NotSupportedException();
                }
            }
            set => throw new NotSupportedException();
        }

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

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter) => parent.AttachFilter(filter);
        public void ResetFilter(Action<T, TView>? resetAction) => parent.ResetFilter(resetAction);
        public INotifyCollectionChangedSynchronizedViewForWpf<T, TView> WithINotifyCollectionChanged() => this;
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
                return parent is IList list ? list.Contains(value) : throw new NotSupportedException();
            }
        }

        int IList.IndexOf(object? value)
        {
            lock (SyncRoot)
            {
                return parent is IList list ? list.IndexOf(value) : throw new NotSupportedException();
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
                if (parent is IList list)
                {
                    list.CopyTo(array, index);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}