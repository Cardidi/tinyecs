namespace TinyECS.Core
{
    public class TimePlugin<TWorld> : IPlugin<TWorld> where TWorld :World<TWorld>
    {
        public int TickCount { get; private set; }
        
        public float DeltaTime { get; private set; }
        
        public float UnscaledDeltaTime { get; private set; }
        
        public float TotalTime { get; private set; }
        
        public float UnscaledTotalTime { get; private set; }

        public float TimeScale { get; set; }
        
        public void CreateTick(TWorld world, float tick)
        {
            TickCount += 1;
            
            DeltaTime = tick * TimeScale;
            TotalTime += DeltaTime;

            UnscaledDeltaTime = tick;
            UnscaledTotalTime += UnscaledDeltaTime;
        }

        public void OnBuilt(
            TWorld world,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyDictionary<object, object> envData)
        {
            world.OnCreateTick.Add(CreateTick);
        }
    }
}