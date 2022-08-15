using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace ObservableCollections.Internal
{
    internal class NotifyCollectionChangedSynchronizedSingleView<T, TView> : INotifyCollectionChangedSynchronizedSingleView<T, TView>
    {
        readonly ISynchronizedSingleView<T, TView> parent;
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new(nameof(Count));

        public NotifyCollectionChangedSynchronizedSingleView(ISynchronizedSingleView<T, TView> parent)
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

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter) => parent.AttachFilter(filter);
        public void ResetFilter(Action<T, TView>? resetAction) => parent.ResetFilter(resetAction);
        public INotifyCollectionChangedSynchronizedSingleView<T, TView> WithINotifyCollectionChanged() => this;
        public INotifyCollectionChangedListSynchronizedSingleView<T, TView> WithINotifyCollectionChangedList() => parent.WithINotifyCollectionChangedList();

        public void Dispose()
        {
            this.parent.RoutingCollectionChanged -= Parent_RoutingCollectionChanged;
            parent.Dispose();
        }

        public IEnumerator<TView> GetEnumerator() => parent.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => parent.GetEnumerator();

    }
}