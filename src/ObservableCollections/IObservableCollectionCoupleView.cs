using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public interface ISynchronizedCoupleView<T, TView> : IReadOnlyCollection<(T, TView)>, IDisposable
    {
        object SyncRoot { get; }

        event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
        void ResetFilter(Action<T, TView>? resetAction);
        INotifyCollectionChangedSynchronizedCoupleView<T, TView> WithINotifyCollectionChanged();
        ISynchronizedSingleView<T, TView> ToSynchronizedSingleView();
    }

    public interface INotifyCollectionChangedSynchronizedCoupleView<T, TView> : ISynchronizedCoupleView<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }
}