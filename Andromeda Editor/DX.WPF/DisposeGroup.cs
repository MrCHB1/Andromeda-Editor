using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor
{
    public class DisposeGroup : IDisposable
    {
        List<IDisposable> list = new List<IDisposable>();

        public void Add(params IDisposable[] objects)
        {
            list.AddRange(from o in objects where o != null select o);
        }

        public T Add<T>(T ob)
            where T : IDisposable
        {
            if (ob != null)
                list.Add(ob);
            return ob;
        }

        public void Dispose()
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var d = list[i];
                list.RemoveAt(i);
                d.Dispose();
            }
        }
    }
}
