using System.Collections.ObjectModel;
using TinyECS.Vendor;

namespace TinyECS
{
    /// <summary>
    /// Plugin on manage system schedule and tick.
    /// </summary>
    public sealed class SystemPlugin<TWorld> : IPlugin<TWorld> where TWorld : World<TWorld>
    {
        public Queue<ISystem<TWorld>> SystemTickSchedule { get; } = new();
        
        public IReadOnlyList<ISystem<TWorld>> Systems { get; private set; }
        
        public IReadOnlyDictionary<Type, int> SystemTransformer { get; private set; }

        public bool IsTickFinalized => SystemTickSchedule.Count == 0;
        
        public void OnConstruct(TWorld world, 
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems, 
            IReadOnlyDictionary<object, object> envData)
        {
            CacheSystems(systems);
            world.OnInit.Add(Init);
            world.OnCreateTick.Add(CreateTick, -100);
            world.OnTick.Add(Tick);
            world.OnDeinit.Add(Deinit);
        }

        public void CacheSystems(IReadOnlyCollection<ISystem<TWorld>> systemTypes)
        {
            Assertion.IsNotNull(systemTypes);

            var sysList = new List<ISystem<TWorld>>();
            var sysTransformer = new Dictionary<Type, int>();
            
            foreach (var t in systemTypes)
            {
                sysTransformer.Add(t.GetType(), sysList.Count);
                sysList.Add(t);
            }

            SystemTickSchedule.Clear();
            SystemTransformer = new ReadOnlyDictionary<Type, int>(sysTransformer);
            Systems = sysList;
        }

        private void Init(TWorld world)
        {
            for (var i = 0; i < Systems.Count; i++)
            {
                var sys = Systems[i];
                try
                {
                    sys.OnCreate(world);
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
            }
        }
        
        private void CreateTick(TWorld w, float dt)
        {
            foreach (var s in Systems)
            {
                SystemTickSchedule.Enqueue(s);
            }
        }

        private void Tick(TWorld world, float dt)
        {
            try
            {
                SystemTickSchedule.Dequeue().OnTick(world, dt);
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
        }
        
        private void Deinit(TWorld world)
        {
            for (var i = 0; i < Systems.Count; i++)
            {
                var sys = Systems[i];
                try
                {
                    sys.OnCreate(world);
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
            }
        }
    }
}