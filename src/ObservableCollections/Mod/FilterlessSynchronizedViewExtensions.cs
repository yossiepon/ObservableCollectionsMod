using System;

namespace ObservableCollections.Mod
{
    public static class FilterlessSynchronizedViewExtensions
    {
        public static IFilterlessSynchronizedView<T, TView> CreateFilterlessView<T, TView>(this ObservableList<T> source, Func<T, TView> transform, bool reverse = false)
        {
            return new ObservableListFilterlessSynchronizedView<T, TView>(source, transform, reverse);
        }
    }
}
