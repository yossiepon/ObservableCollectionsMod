using ObservableCollections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

#nullable enable

namespace ObservableCollections.Mod
{
    public interface IFilterlessSynchronizedView<T, TView> : IReadOnlyCollection<TView>, IDisposable
    {
        object SyncRoot { get; }

        event NotifyCollectionChangedEventHandler<TView>? RoutingCollectionChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        IFilterlessNotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged();
    }
}

#nullable restore