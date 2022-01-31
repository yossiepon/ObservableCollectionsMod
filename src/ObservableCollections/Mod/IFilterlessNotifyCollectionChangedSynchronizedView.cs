using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections.Mod
{
    public interface IFilterlessNotifyCollectionChangedSynchronizedView<T, TView> : IFilterlessSynchronizedView<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }

}
