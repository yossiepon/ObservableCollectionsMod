using ObservableCollections.Mod;
using System.Collections.ObjectModel;
using System.Linq;

namespace ObservableCollections.Tests
{
    public class ObservableListViewModTest
    {
        [Fact]
        public void FilterlessView()
        {
            var reference = new ObservableCollection<int>();
            var list = new ObservableList<int>();
            var view = list.CreateFilterlessView(x => new ViewContainer<int>(x));

            list.Add(10); reference.Add(10); // 0
            list.Add(50); reference.Add(50); // 1
            list.Add(30); reference.Add(30); // 2
            list.Add(20); reference.Add(20); // 3
            list.Add(40); reference.Add(40); // 4

            void Equal(params int[] expected)
            {
                reference.Should().Equal(expected);
                list.Should().Equal(expected);
                view.Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            void Equal2(params int[] expected)
            {
                list.Should().Equal(expected);
                view.Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            Equal(10, 50, 30, 20, 40);

            reference.Move(3, 1);
            list.Move(3, 1);
            Equal(10, 20, 50, 30, 40);

            reference.Insert(2, 99);
            list.Insert(2, 99);
            Equal(10, 20, 99, 50, 30, 40);

            reference.RemoveAt(2);
            list.RemoveAt(2);
            Equal(10, 20, 50, 30, 40);

            reference[3] = 88;
            list[3] = 88;
            Equal(10, 20, 50, 88, 40);

            reference.Clear();
            list.Clear();
            Equal(new int[0]);

            list.AddRange(new[] { 100, 200, 300 });
            Equal2(100, 200, 300);

            list.InsertRange(1, new[] { 400, 500, 600 });
            Equal2(100, 400, 500, 600, 200, 300);

            list.RemoveRange(2, 2);
            Equal2(100, 400, 200, 300);
        }

        [Fact]
        public void FilterlessNotifyChangedCollectionView()
        {
            var reference = new ObservableCollection<int>();
            var list = new ObservableList<int>();
            var view = list.CreateFilterlessView(x => new ViewContainer<int>(x)).WithINotifyCollectionChanged();

            list.Add(10); reference.Add(10); // 0
            list.Add(50); reference.Add(50); // 1
            list.Add(30); reference.Add(30); // 2
            list.Add(20); reference.Add(20); // 3
            list.Add(40); reference.Add(40); // 4

            void Equal(params int[] expected)
            {
                reference.Should().Equal(expected);
                list.Should().Equal(expected);
                view.Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            void Equal2(params int[] expected)
            {
                list.Should().Equal(expected);
                view.Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            Equal(10, 50, 30, 20, 40);

            reference.Move(3, 1);
            list.Move(3, 1);
            Equal(10, 20, 50, 30, 40);

            reference.Insert(2, 99);
            list.Insert(2, 99);
            Equal(10, 20, 99, 50, 30, 40);

            reference.RemoveAt(2);
            list.RemoveAt(2);
            Equal(10, 20, 50, 30, 40);

            reference[3] = 88;
            list[3] = 88;
            Equal(10, 20, 50, 88, 40);

            reference.Clear();
            list.Clear();
            Equal(new int[0]);

            list.AddRange(new[] { 100, 200, 300 });
            Equal2(100, 200, 300);

            list.InsertRange(1, new[] { 400, 500, 600 });
            Equal2(100, 400, 500, 600, 200, 300);

            list.RemoveRange(2, 2);
            Equal2(100, 400, 200, 300);
        }

    }
}
