using ObservableCollections.Internal;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public interface IObservableCollectionToCoupleView<T>
    {
        ISynchronizedCoupleView<T, TView> ToSynchronizedCoupleView<TView>(Func<T, TView> transform, bool reverse = false);
    }

    public interface IFreezedCollectionToCoupleView<T> : IEnumerable<T>
    {
        ISynchronizedCoupleView<T, TView> ToSynchronizedCoupleView<TView>(Func<T, TView> transform, bool reverse = false);

        ISortableSynchronizedCoupleView<T, TView> ToSynchronizedSortableCoupleView<TView>(Func<T, TView> transform);
    }

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

    public interface ISortableSynchronizedCoupleView<T, TView> : ISynchronizedCoupleView<T, TView>
    {
        void Sort(IComparer<T> valueComparer);
        void Sort(IComparer<TView> viewComparer);
    }

    public interface INotifyCollectionChangedSynchronizedCoupleView<T, TView> : ISynchronizedCoupleView<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }

    public static class SynchronizedCoupleViewExtensions
    {
        public static ISynchronizedCoupleView<T, TView> ToSynchronizedSortedCoupleView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
            where TKey : notnull
        {
            return new SortedCoupleViewValueComparer<T, TKey, TView>(source, identitySelector, transform, comparer);
        }

        public static ISynchronizedCoupleView<T, TView> ToSynchronizedSortedCoupleView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer)
            where TKey : notnull
        {
            return new SortedCoupleViewViewComparer<T, TKey, TView>(source, identitySelector, transform, viewComparer);
        }

        public static ISynchronizedCoupleView<T, TView> ToSynchronizedSortedCoupleViewValueComparer<T, TKey, TView, TCompare>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, Func<T, TCompare> compareSelector, bool ascending = true)
            where TKey : notnull
        {
            return source.ToSynchronizedSortedCoupleView(identitySelector, transform, new AnonymousComparer<T, TCompare>(compareSelector, ascending));
        }

        public static ISynchronizedCoupleView<T, TView> ToSynchronizedSortedCoupleViewViewComparer<T, TKey, TView, TCompare>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, Func<TView, TCompare> compareSelector, bool ascending = true)
            where TKey : notnull
        {
            return source.ToSynchronizedSortedCoupleView(identitySelector, transform, new AnonymousComparer<TView, TCompare>(compareSelector, ascending));
        }

        public static ISynchronizedCoupleView<T, TView> ToSynchronizedCoupleView<T, TView>(this IFreezedCollectionToCoupleView<T> source, Func<T, TView> transform, bool reverse = false)
        {
            return new FreezedCoupleView<T, TView>(source, transform, reverse);
        }

        public static ISortableSynchronizedCoupleView<T, TView> ToSynchronizedSortableCoupleView<T, TView>(this IFreezedCollectionToCoupleView<T> source, Func<T, TView> transform)
        {
            return new FreezedSortableCoupleView<T, TView>(source, transform);
        }

        public static ISortableSynchronizedCoupleView<T, TView> ToSynchronizedSortableCoupleView<T, TView>(this IFreezedCollectionToCoupleView<T> source, Func<T, TView> transform, IComparer<T> initialSort)
        {
            var view = source.ToSynchronizedSortableCoupleView(transform);
            view.Sort(initialSort);
            return view;
        }

        public static ISortableSynchronizedCoupleView<T, TView> ToSynchronizedSortableCoupleView<T, TView>(this IFreezedCollectionToCoupleView<T> source, Func<T, TView> transform, IComparer<TView> initialViewSort)
        {
            var view = source.ToSynchronizedSortableCoupleView(transform);
            view.Sort(initialViewSort);
            return view;
        }

        public static ISortableSynchronizedCoupleView<T, TView> ToSynchronizedSortableCoupleView<T, TView, TCompare>(this IFreezedCollectionToCoupleView<T> source, Func<T, TView> transform, Func<T, TCompare> initialCompareSelector, bool ascending = true)
        {
            var view = source.ToSynchronizedSortableCoupleView(transform);
            view.Sort(initialCompareSelector, ascending);
            return view;
        }

        public static void Sort<T, TView, TCompare>(this ISortableSynchronizedCoupleView<T, TView> source, Func<T, TCompare> compareSelector, bool ascending = true)
        {
            source.Sort(new AnonymousComparer<T, TCompare>(compareSelector, ascending));
        }

        class AnonymousComparer<T, TCompare> : IComparer<T>
        {
            readonly Func<T, TCompare> selector;
            readonly int f;

            public AnonymousComparer(Func<T, TCompare> selector, bool ascending)
            {
                this.selector = selector;
                this.f = ascending ? 1 : -1;
            }

            public int Compare(T? x, T? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return 1 * f;
                if (y == null) return -1 * f;

                return Comparer<TCompare>.Default.Compare(selector(x), selector(y)) * f;
            }
        }
    }
}