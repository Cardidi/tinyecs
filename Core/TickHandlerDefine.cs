// ReSharper disable once CheckNamespace

namespace TinyECS.Core
{
    /*
     * # System boot and loop timetable
     *
     * We can divide those into 4 phase:
     *
     * 1. Basis Infrastructure Creation
     * 2. Initialization
     * 3. Tick
     * 4. Deinitialization
     * 
     * ## Basis Infrastructure Creation
     *
     * 1. World Creation (Override)
     *
     * ## Initialization
     * 
     * 1. Before World Initialization (Delegate)
     * 2. World Initialization (Delegate)
     * 3. After World Initialization (Delegate)
     *
     * ## Tick
     *
     *   1. Create Tick (Delegate)
     * | 2. Before Tick (Delegate)
     * | 3. Tick (Delegate)
     * | 4. After Tick (Delegate)
     *   5. Destroy Tick (Delegate)
     *
     * # Deinitialization
     *
     * 1. Before World Deinitialization (Delegate)
     * 2. World Deinitialization (Delegate)
     * 3. After World Deinitialization (Delegate)
     * 
     */
    
    public delegate void WorldInitHandler<in T>(T world)  where T : World<T>;
    
    public delegate void WorldTickHandler<in T>(T world, float dt)  where T : World<T>;
    
    public delegate void WorldDeinitHandler<in T>(T world)  where T : World<T>;

    public struct WorldEventInvoker<TWorld> where TWorld : World<TWorld>
    {

        public static Emitter<WorldInitHandler<TWorld>, WorldEventInvoker<TWorld>> 
            InitEmitter { get; } = (h, c) => h(c.World);
        
        public static Emitter<WorldTickHandler<TWorld>, WorldEventInvoker<TWorld>> 
            TickEmitter { get; } = (h, c) => h(c.World, c.DeltaTime);
        
        public static Emitter<WorldDeinitHandler<TWorld>, WorldEventInvoker<TWorld>> 
            DeinitEmitter { get; } = (h, c) => h(c.World);
        
        public WorldEventInvoker(TWorld world, float deltaTime = 0)
        {
            World = world;
            DeltaTime = deltaTime;
        }

        public readonly TWorld World;

        public readonly float DeltaTime;
    }
    
}