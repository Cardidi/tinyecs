using System;
using System.Collections.Generic;
using CoreECS.Defines;

namespace CoreECS.Managers
{
    public sealed class SparseSetProxy
    {
        public List<IComponentRefCore> Handles { get; } = new List<IComponentRefCore>();

        public void Add(IComponentRefCore core)
        {
            Handles.Add(core);
        }

        public bool Remove(IComponentRefCore core)
        {
            return Handles.Remove(core);
        }

        public bool Has(Type t)
        {
            for (var i = 0; i < Handles.Count; i++)
            {
                var core = Handles[i];
                if (core?.RefLocator != null && core.RefLocator.IsT(t))
                    return true;
            }

            return false;
        }

        public void Clear()
        {
            Handles.Clear();
        }
    }
}
