#if DEBUG
using NLog;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace ObservableCollections.Internal
{
    internal class NotifyCollectionChangedSynchronizedCoupleView<T, TView> : INotifyCollectionChangedSynchronizedCoupleView<T, TView>
    {
#if DEBUG
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
#endif

        readonly ISynchronizedCoupleView<T, TView> parent;
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new(nameof(Count));

        public NotifyCollectionChangedSynchronizedCoupleView(ISynchronizedCoupleView<T, TView> parent)
        {
            this.parent = parent;
            this.parent.RoutingCollectionChanged += Parent_RoutingCollectionChanged;
        }
         
        private void Parent_RoutingCollectionChanged(in NotifyCollectionChangedEventArgs<(T, TView)> e)
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

        public event NotifyCollectionChangedEventHandler<(T, TView)>? RoutingCollectionChanged
        {
            add { parent.RoutingCollectionChanged += value; }
            remove { parent.RoutingCollectionChanged -= value; }
        }

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter) => parent.AttachFilter(filter);
        public void ResetFilter(Action<T, TView>? resetAction) => parent.ResetFilter(resetAction);
        public INotifyCollectionChangedSynchronizedCoupleView<T, TView> WithINotifyCollectionChanged() => this;
        public ISynchronizedSingleView<T, TView> ToSynchronizedSingleView(bool disposeParent, bool disposeElement) => parent.ToSynchronizedSingleView(disposeParent, disposeElement);
        public void Dispose()
        {
#if DEBUG
            logger.Trace("{0} disposing NCCCoupleView...", this.GetType().FullName);
#endif

            this.parent.RoutingCollectionChanged -= Parent_RoutingCollectionChanged;

#if DEBUG
            logger.Trace("{0} parent disposing...", this.GetType().FullName);
#endif

            parent.Dispose();

#if DEBUG
            logger.Trace("{0} parent and NCCCoupleView disposed.", this.GetType().FullName);
#endif
        }

        public IEnumerator<(T, TView)> GetEnumerator() => parent.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => parent.GetEnumerator();

    }
}