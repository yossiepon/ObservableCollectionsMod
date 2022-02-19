using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public interface ISynchronizedSingleView<T, TView> : IReadOnlyCollection<TView>, IDisposable
    {
        object SyncRoot { get; }

        event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
        void ResetFilter(Action<T, TView>? resetAction);
        INotifyCollectionChangedSynchronizedSingleView<T, TView> WithINotifyCollectionChanged();
        ISynchronizedListView<T, TView> WithIListINotifyCollectionChanged();
    }

    public interface INotifyCollectionChangedSynchronizedSingleView<T, TView> : ISynchronizedSingleView<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }

    public interface ISynchronizedListView<T, TView> : ISynchronizedSingleView<T, TView>, IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }
}