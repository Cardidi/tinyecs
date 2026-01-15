using System.Collections.Concurrent;

namespace TinyECS.Utils
{
    public class Pool<T>
    {
        private readonly ConcurrentQueue<T> m_objects;
        private readonly Func<T> m_createFunc;
        private readonly Action<T> m_getAction;
        private readonly Action<T> m_returnAction;
        
        public Pool(Func<T> createFunc, Action<T> getAction = null, Action<T> returnAction = null, int initialCapacity = 0)
        {
            m_objects = new ConcurrentQueue<T>();
            m_createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            m_getAction = getAction;
            m_returnAction = returnAction;
            
            // 预创建指定数量的对象
            if (initialCapacity > 0)
            {
                for (int i = 0; i < initialCapacity; i++)
                {
                    T item = m_createFunc();
                    m_returnAction?.Invoke(item);
                    m_objects.Enqueue(item);
                }
            }
        }
        
        public T Get()
        {
            if (m_objects.TryDequeue(out T item))
            {
                m_getAction?.Invoke(item);
                return item;
            }
            
            item = m_createFunc();
            m_getAction?.Invoke(item);
            return item;
        }
        
        public void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            
            m_returnAction?.Invoke(item);
            m_objects.Enqueue(item);
        }
        
        public PooledItemDisposable Get(out T item)
        {
            item = Get();
            return new PooledItemDisposable(this, item);
        }
        
        public int Count => m_objects.Count;
        
        public void Clear()
        {
            m_objects.Clear();
        }
        
        public readonly struct PooledItemDisposable : IDisposable
        {
            private readonly Pool<T> m_pool;
            private readonly T m_item;
            
            public PooledItemDisposable(Pool<T> pool, T item)
            {
                m_pool = pool ?? throw new ArgumentNullException(nameof(pool));
                m_item = item;
            }
            
            public void Dispose()
            {
                m_pool.Release(m_item);
            }
        }
    }
}