using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor
{
    public static class Utils
    {
        public static int BinarySearch<T>(List<T> iterable, int value, Func<int,int> filter)
        {
            int high = iterable.Count() - 1;
            int low = 0;

            if (filter(0) >= value) return 0;
            if (filter(high) <= value) return high;

            int mid;
            while (low <= high)
            {
                mid = (high + low) / 2;
                if (filter(mid) == value) return mid;
                else if (filter(mid) > value) high = mid - 1;
                else low = mid + 1;
            }

            return -1;
        }
    }

    public class FastList<T> : IEnumerable<T>
    {
        private class ListItem
        {
            public ListItem Next;
            public T item;
        }

        private ListItem root = new ListItem();
        private ListItem last = null;

        public T First
        {
            get
            {
                if (root.Next != null) return root.Next.item;
                else return default(T);
            }
        }

        public class Iterator
        {
            FastList<T> _iList;

            private ListItem prev;
            private ListItem curr;

            internal Iterator(FastList<T> ll)
            {
                _iList = ll;
                Reset();
            }

            public bool MoveNext(out T v)
            {
                ListItem ll = curr.Next;

                if (ll == null)
                {
                    v = default(T);
                    _iList.last = curr;
                    return false;
                }

                v = ll.item;

                prev = curr;
                curr = ll;

                return true;
            }

            public void Remove()
            {
                if (_iList.last.Equals(curr)) _iList.last = prev;
                prev.Next = curr.Next;
            }

            public void Insert(T item)
            {
                ListItem i = new ListItem()
                {
                    item = item,
                    Next = curr
                };
                if (prev == null) _iList.root.Next = i;
                else prev.Next = i;
            }

            public void Reset()
            {
                this.prev = null;
                this.curr = _iList.root;
            }
        }

        public class FastIterator : IEnumerator<T>
        {
            FastList<T> _ilist;

            private ListItem curr;

            internal FastIterator(FastList<T> ll)
            {
                _ilist = ll;
                Reset();
            }

            public object Current => curr.item;

            T IEnumerator<T>.Current => curr.item;

            public void Dispose()
            {

            }

            public bool MoveNext()
            {
                try
                {
                    curr = curr.Next;

                    return curr != null;
                }
                catch { return false; }
            }

            public void Reset()
            {
                this.curr = _ilist.root;
            }
        }

        public void Add(T item)
        {
            ListItem li = new ListItem();
            li.item = item;

            if (root.Next != null && last != null)
            {
                while (last.Next != null) last = last.Next;
                last.Next = li;
            }
            else
                root.Next = li;

            last = li;
        }

        public T Pop()
        {
            ListItem el = root.Next;
            root.Next = el.Next;
            return el.item;
        }

        public Iterator Iterate()
        {
            return new Iterator(this);
        }

        public bool ZeroLen => root.Next == null;

        public IEnumerator<T> FastIterate()
        {
            return new FastIterator(this);
        }

        public void Unlink()
        {
            root.Next = null;
            last = null;
        }

        public int Count()
        {
            int n = 0;

            ListItem li = root.Next;
            while (li != null)
            {
                n++;
                li = li.Next;
            }

            return n;
        }

        public bool Any()
        {
            return root.Next != null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return FastIterate();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return FastIterate();
        }
    }
}