using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

#nullable enable

namespace ObservableCollections.Mod
{
    internal class ObservableListFilterlessNotifyCollectionChangedSynchronizedView<T, TView> : IFilterlessNotifyCollectionChangedSynchronizedView<T, TView>
    {
        readonly IFilterlessSynchronizedView<T, TView> parent;
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new PropertyChangedEventArgs("Count");

        public ObservableListFilterlessNotifyCollectionChangedSynchronizedView(IFilterlessSynchronizedView<T, TView> parent)
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

        public IFilterlessNotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged() => this;
        public void Dispose()
        {
            this.parent.RoutingCollectionChanged -= Parent_RoutingCollectionChanged;
            parent.Dispose();
        }

        public IEnumerator<TView> GetEnumerator() => parent.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => parent.GetEnumerator();
    }
}

#nullable restore
