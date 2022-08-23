using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ObservableCollections
{
    public sealed partial class FreezedList<T> : IFreezedCollectionToCoupleView<T>
    {
        public ISynchronizedCoupleView<T, TView> ToSynchronizedCoupleView<TView>(Func<T, TView> transform, bool reverse = false, bool disposeElement = true)
        {
            return new FreezedCoupleView<T, TView>(list, transform, reverse, disposeElement);
        }

        public ISortableSynchronizedCoupleView<T, TView> ToSynchronizedSortableCoupleView<TView>(Func<T, TView> transform, bool disposeElement = true)
        {
            return new FreezedSortableCoupleView<T, TView>(list, transform, disposeElement);
        }
    }
}