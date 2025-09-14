namespace TinyECS.CoreExtension
{
    public static class ModifyCommandPool
    {
        private static readonly Dictionary<Type, object> TypedPool = new();
        
        private static ObjectPool<T> GetPool<T>() where T : class, IModifyCommand, new ()
        {
            var tt = typeof(T);
            if (!TypedPool.TryGetValue(tt, out var pool))
            {
                pool = new ObjectPool<T>(
                    createFunc: () => new T(),
                    actionOnRelease: x => x.OnReset());

                TypedPool.Add(tt, pool);
            }

            return (ObjectPool<T>) pool;
        }

        public static T Get<T>() where T : class, IModifyCommand, new()
            => GetPool<T>().Get();

        public static PooledObject<T> Get<T>(out T obj) where T : class, IModifyCommand, new() 
            => GetPool<T>().Get(out obj);

        public static void Release<T>(T obj)  where T : class, IModifyCommand, new() 
            => GetPool<T>().Release(obj);
        
    }
    
    /// <summary>
    /// Minimal definition of a modification in battle world
    /// </summary>
    public interface IModifyCommand
    {
        /// <summary>
        /// Actual content on modify
        /// </summary>
        public void OnModify(ModifyCommandQueue queue);

        /// <summary>
        /// Pooling resetting method.
        /// </summary>
        public void OnReset();
    }
    
    /// <summary>
    /// Queuing and executing utility for IWorldModify to manage and executing modifications in battle world.
    /// </summary>
    public class ModifyCommandQueue
    { 
        
        private readonly struct WorldModifySchedule : IComparable<WorldModifySchedule>
        {
            public int Timing { get; }
            
            public IModifyCommand Command { get; }
            
            public IDisposable Disp { get; }

            public WorldModifySchedule(int timing, IModifyCommand command, IDisposable disp)
            {
                Timing = timing;
                Command = command;
                Disp = disp;
            }

            public int CompareTo(WorldModifySchedule other)
            {
                return Timing.CompareTo(other.Timing);
            }
        }

        private readonly Dictionary<object, List<WorldModifySchedule>> m_modifyQueue = new();
        
        private readonly Dictionary<object, List<WorldModifySchedule>> m_immediateModifyQueue = new();

        private readonly Queue<WorldModifySchedule> m_execQueue = new();

        private readonly Stack<object> m_safeCheckStack = new();

        private bool m_executing = false;

        private List<WorldModifySchedule> GetQueueInternal(object type, bool isImmediate, bool create)
        {
            var src = isImmediate ? m_immediateModifyQueue : m_modifyQueue;
            if (!src.TryGetValue(type, out var qType))
            {
                if (!create) return null;
                qType = new List<WorldModifySchedule>();
                src.Add(type, qType);
            }

            return qType;
        }

        private void ExecuteCommand(WorldModifySchedule schedule)
        {
            m_executing = true;
            
            try
            {
                schedule.Command.OnModify(this);
                schedule.Disp.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            m_executing = false;
        }

        /// <summary>
        /// Is this queue has something running on.
        /// </summary>
        public bool Raising => m_safeCheckStack.Count > 0;

        /// <summary>
        /// Is this queue has any IWorldModify is executing right now.
        /// </summary>
        public bool ModifyExecuting => m_executing;

        /// <summary>
        /// Check in type stack if this type is raising now.
        /// </summary>
        public bool IsTypeRaising(object type) => m_safeCheckStack.Contains(type);
        
        /// <summary>
        /// Check if possible to add an immediate modification at this time.
        /// </summary>
        /// <param name="type">Modification Type</param>
        /// <param name="ignoreImmediatelyAccessCheck">If true, this will not check this modification has any kind of
        /// situation that intention being misled...</param>
        /// <param name="ignoreLoopDefense">If true, this will not check will this modification cause infinity loop
        /// issue.</param>
        public bool CanAddImmediatelyModify(object type,
            bool ignoreImmediatelyAccessCheck = false, bool ignoreLoopDefense = false)
        {
            var r = true;
            r &= ignoreImmediatelyAccessCheck || m_safeCheckStack.Contains(type);
            r &= ignoreLoopDefense || !m_executing;
            
            return r;
        }

        /// <summary>
        /// Add a modification onto queue and await to be schedule.
        /// </summary>
        /// <param name="type">Modification Type</param>
        /// <param name="timing">Insert this modification at where of queue.</param>
        /// <param name="immediately">Will insert this modification at front of queue.</param>
        /// <param name="ignoreImmediatelyAccessCheck">If true, this will not check this modification has any kind of
        /// situation that intention being misled...</param>
        /// <param name="ignoreLoopDefense">If true, this will not check will this modification cause infinity loop
        /// issue.</param>
        public T AddModify<T>(object type, int timing = 0, bool immediately = false,
            bool ignoreImmediatelyAccessCheck = false, bool ignoreLoopDefense = false)
            where T : class, IModifyCommand, new()
        {
            Assert.IsNotNull(type, "Type must be a valid target.");
            Assert.IsFalse(m_executing && !ignoreLoopDefense,
                "Not allow to add immediately modify because you are trying to add a modify when another " +
                "modify is on air which may cause infinity loop.");
            Assert.IsFalse(
                immediately && !ignoreImmediatelyAccessCheck && !m_safeCheckStack.Contains(type), 
                "Not allow to add immediately modify because this modification may never being called in " +
                "this tick.");
            
            var h = ModifyCommandPool.Get<T>(out var m);
            var target = GetQueueInternal(type, immediately, true);
            target.Add(new(timing, m, h));
            
            return m;
        }

        /// <summary>
        /// Add a modification onto queue and await to be schedule.
        /// </summary>
        /// <param name="type">Modification Type</param>
        /// <param name="worldModify">If wanted to manually manage IWorldModify, pass in IWorldModify which managed
        /// by caller.</param>
        /// <param name="timing">Insert this modification at where of queue.</param>
        /// <param name="immediately">Will insert this modification at front of queue.</param>
        /// <param name="ignoreImmediatelyAccessCheck">If true, this will not check this modification has any kind of
        /// situation that intention being misled...</param>
        /// <param name="ignoreLoopDefense">If true, this will not check will this modification cause infinity loop
        /// issue.</param>
        public T AddModify<T>(T worldModify, object type, int timing = 0, bool immediately = false,
            bool ignoreImmediatelyAccessCheck = false, bool ignoreLoopDefense = false)
            where T : class, IModifyCommand, new()
        {
            Assert.IsNotNull(type, "Type must be a valid target.");
            Assert.IsNotNull(worldModify);
            Assert.IsFalse(m_executing && !ignoreLoopDefense,
                "Not allow to add immediately modify because you are trying to add a modify when another " +
                "modify is on air which may cause infinity loop.");
            Assert.IsFalse(
                immediately && !ignoreImmediatelyAccessCheck && !m_safeCheckStack.Contains(type), 
                "Not allow to add immediately modify because this modification may never being called in " +
                "this tick.");
            
            var target = GetQueueInternal(type, immediately, true);
            target.Add(new(timing, worldModify, Disposable.Empty));
            
            return worldModify;
        }
        
        /// <summary>
        /// Trying to add a modification onto queue and await to be schedule.
        /// </summary>
        /// <param name="type">Modification Type</param>
        /// <param name="timing">Insert this modification at where of queue.</param>
        /// <param name="immediately">Will insert this modification at front of queue.</param>
        /// <param name="ignoreImmediatelyAccessCheck">If true, this will not check this modification has any kind of
        /// situation that intention being misled...</param>
        /// <param name="ignoreLoopDefense">If true, this will not check will this modification cause infinity loop
        /// issue.</param>
        public bool TryAddModify<T>(out T worldModify, object type, int timing = 0, bool immediately = false,
            bool ignoreImmediatelyAccessCheck = false, bool ignoreLoopDefense = false)
            where T : class, IModifyCommand, new()
        {
            worldModify = null;
            if (type == null) return false;
            if (m_executing && !ignoreLoopDefense) return false;
            if (immediately && !ignoreImmediatelyAccessCheck && !m_safeCheckStack.Contains(type)) return false;
            
            var h = ModifyCommandPool.Get<T>(out worldModify);
            var target = GetQueueInternal(type, immediately, true);
            target.Add(new(timing, worldModify, h));

            return true;
        }

        /// <summary>
        /// Trying to add a modification onto queue and await to be schedule.
        /// </summary>
        /// <param name="type">Modification Type</param>
        /// <param name="worldModify">If wanted to manually manage IWorldModify, pass in IWorldModify which managed
        /// by caller.</param>
        /// <param name="timing">Insert this modification at where of queue.</param>
        /// <param name="immediately">Will insert this modification at front of queue.</param>
        /// <param name="ignoreImmediatelyAccessCheck">If true, this will not check this modification has any kind of
        /// situation that intention being misled...</param>
        /// <param name="ignoreLoopDefense">If true, this will not check will this modification cause infinity loop
        /// issue.</param>
        public bool TryAddModify<T>(T worldModify, object type, int timing = 0, bool immediately = false,
            bool ignoreImmediatelyAccessCheck = false, bool ignoreLoopDefense = false)
            where T : class, IModifyCommand, new()
        {
            if (type == null) return false;
            if (worldModify == null) return false;
            if (m_executing && !ignoreLoopDefense) return false;
            if (immediately && !ignoreImmediatelyAccessCheck && !m_safeCheckStack.Contains(type)) return false;
            
            var target = GetQueueInternal(type, immediately, true);
            target.Add(new(timing, worldModify, Disposable.Empty));
            
            return true;
        }

        /// <summary>
        /// Raise those pending modifications.
        /// </summary>
        /// <param name="type">Modification Type</param>
        /// <param name="ignoreLoopCheck">If true, this will not check will this modification cause infinity loop
        /// issue.</param>
        /// <param name="noImmediately">If true, this call will never handle with immediately modification.</param>
        /// <exception cref="AggregateException">If any kind of error occurs in modification, this will being
        /// thrown.</exception>
        public void Raise(object type, bool ignoreLoopCheck = false, bool noImmediately = false)
        {
            Assert.IsNotNull(type, "Type must be a valid target.");
            Assert.IsFalse(
                !ignoreLoopCheck && m_safeCheckStack.Contains(type),
                "Not allowed to raise queue due to another raising is on air.");

            var basisQueue = GetQueueInternal(type, false, false);
            if (basisQueue == null) return;
            

            // Collect queue targets
            m_execQueue.Clear();
            basisQueue.Sort();
            
            for (var i = 0; i < basisQueue.Count; i++) m_execQueue.Enqueue(basisQueue[i]);
            basisQueue.Clear();
            
            // Execute modification
            m_safeCheckStack.Push(type);
            
            try
            {
                var immediatelyQueue = GetQueueInternal(type, true, true);
                while (m_execQueue.Count > 0 || (!noImmediately && immediatelyQueue.Count > 0))
                {
                    // If has immediately command, execute it first.
                    while (!noImmediately && immediatelyQueue.Count > 0)
                    {
                        var bidx = 0;
                        var minTime = immediatelyQueue[0].Timing;
                        for (var i = 1; i < immediatelyQueue.Count; i++)
                        {
                            var c = immediatelyQueue[i];
                            if (c.Timing >= minTime) continue;
                            
                            bidx = i;
                            minTime = c.Timing;
                        }

                        var itr = immediatelyQueue[bidx];
                        immediatelyQueue.RemoveAt(bidx);
                        ExecuteCommand(itr);
                    }
                    
                    // Process first element dequeue from queue.
                    var tr = m_execQueue.Dequeue();
                    ExecuteCommand(tr);
                }
                
            }
            finally
            {
                m_safeCheckStack.Pop();
                m_execQueue.Clear();
            }
        }
    }
}