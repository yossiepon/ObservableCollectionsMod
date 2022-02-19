using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public interface ISynchronizedViewForWpf<T, TView> : IReadOnlyCollection<TView>, IDisposable
    {
        object SyncRoot { get; }

        event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
        void ResetFilter(Action<T, TView>? resetAction);
        INotifyCollectionChangedSynchronizedViewForWpf<T, TView> WithINotifyCollectionChanged();
    }

    public interface INotifyCollectionChangedSynchronizedViewForWpf<T, TView> : ISynchronizedViewForWpf<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }
}