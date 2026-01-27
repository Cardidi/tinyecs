# TinyECS - State First ECS Toolkit

TinyECS is a lightweight, easy-to-use Entity-Component-System (ECS) implementation designed for C#-based game applications. By combining ComponentStore and EntityGraph, it strikes an effective balance between flexibility and performance.

Unlike a full application framework, TinyECS is built for seamless integration. It can coexist alongside other ECS solutions, such as UnityECS, allowing you to incorporate it into existing projects with minimal friction.

The toolkit's design was refined through the successful development of a round-based game, making TinyECS inherently focused on efficient and robust state management for state-driven scenarios.

## Key Concepts

Those are really common concepts in ECS, and you can find them in most ECS implementations.

- **Entity**: A unique identifier that groups components together
- **Component**: A data structure that holds properties/data (no logic)
- **System**: Contains the logic that operates on entities with specific component combinations
- **World**: Container that manages entities, components, and systems
- **Matcher**: Defines criteria for selecting entities based on their components
- **Collector**: Tracks entities that match specific criteria and efficiently updates when entities change
- **Injector**: Resolves dependencies and injects them into systems, components, and collectors
- **Tick**: A single iteration of the ECS framework, where systems are processed in a defined order.
- **Mask**: A bitwise flag that is used to filter entities based on their component combinations.

## Quick Start Guide

### 1. Creating a World
A `World` represents the container for all entities, components, and systems. It manages the lifecycle of the ECS framework.

```csharp
using TinyECS;

var world = new World();
world.Startup(); // Initialize the world
```

Before call `Startup()`, you should NOT do any following operations:

- Create entities
- Add components to entities
- Register systems

But you can do those operations before World call `Startup()`:

- Configure Injector
- Inherit `World` class to add custom logic like additional ECS managers or do something while world is build, start, tick and shutdown.

Aware that the world is not thread-safe. You should only access the world from the main thread.

### 2. Defining Components
Components are simple data structures that implement the `IComponent<T>` interface. They hold data but don't contain logic.

```csharp
public struct PositionComponent : IComponent<PositionComponent>
{
    public float X;
    public float Y;
}

public struct VelocityComponent : IComponent<VelocityComponent>
{
    public float X;
    public float Y;
}

public struct HealthComponent : IComponent<HealthComponent>
{
    public float Value;
}
```

Components can optionally implement lifecycle methods:

```csharp
public struct LifecycleComponent : IComponent<LifecycleComponent>
{
    public bool OnCreateCalled;
    public bool OnDestroyCalled;
    
    public void OnCreate(ulong entityId)
    {
        OnCreateCalled = true;
    }
    
    public void OnDestroy(ulong entityId)
    {
        OnDestroyCalled = true;
    }
}
```

### 3. Creating Entities
Entities are unique identifiers that group components together. You can add a mask to an entity to filter which systems it should be processed by.

```csharp
// Create an entity
var entity = world.CreateEntity();
var entityWithMask = world.CreateEntity(1 << 1);
```

### 4. Adding Components to Entities
Components can be added to entities to give them properties and data.

```csharp
// Add components to an entity
var positionRef = entity.CreateComponent<PositionComponent>();
positionRef.RW = new PositionComponent { X = 10, Y = 20 };

var velocityRef = entity.CreateComponent<VelocityComponent>();
velocityRef.RW = new VelocityComponent { X = 1, Y = 1 };

// Alternative way to set component data
entity.CreateComponent<HealthComponent>().RW.Value = 100;
```

### 5. Accessing Components
Retrieve components from entities to read or modify their data. If you only need to read the component data, use `RO` property. If you need to modify the component data, use `RW` property.

```csharp
// Get a component from an entity
var positionRef = entity.GetComponent<PositionComponent>();
Console.WriteLine($"Position: ({positionRef.RO.X}, {positionRef.RO.Y})");

// Check if entity has a specific component
bool hasHealth = entity.HasComponent<HealthComponent>();
Console.WriteLine($"Has Health: {hasHealth}");

// Get all components of an entity
var allComponents = entity.GetComponents();
Console.WriteLine($"Total components: {allComponents.Length}");
```

### 6. Removing Components
Components can be removed from entities when no longer needed.

```csharp
// Remove a component by reference
entity.DestroyComponent(positionRef);

// Remove a component by type
entity.DestroyComponent<HealthComponent>();
```

### 7. Defining Systems
Systems contain the logic that operates on entities with specific component combinations.

If you want a system to access world, managers or anything can be get from DI container, just put them on constructor.

For reactive usecase, you can add a mask to a system to filter which systems it should process. In additional, create a collector to find relevant entities is also a good practice. For further details, please refer to EntityCollector.

As you can see, when we trying to iterating over entity via Collector, we should call `Change()` before iterating and do **NOT** use foreach loop.

```csharp
public class MovementSystem : ISystem
{
    private World m_world;
    private IEntityCollector m_movingEntities;
    
    public ulong TickGroup => 1 << 1;
    
    public MovementSystem(World world)
    {
        m_world = world;
    }
    
    public void OnCreate()
    {
        // Initialize system - create collectors to find relevant entities
        m_movingEntities = m_world.CreateCollector(
            EntityMatcher.With.OfAll<PositionComponent>().OfAll<VelocityComponent>()
        );
    }
    
    public void OnTick()
    {
        // Process all entities that match the collector's criteria
        m_movingEntities.Change();
        for (var i = 0; i < m_movingEntities.Collected.Count; i++)
        {
            var entity = new Entity(m_world, m_movingEntities.Collected[i]);
            
            var position = entity.GetComponent<PositionComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            
            // Update position based on velocity
            position.RW.X += velocity.RW.X;
            position.RW.Y += velocity.RW.Y;
        }
    }
    
    public void OnDestroy()
    {
        // Clean up resources
        m_movingEntities?.Dispose();
    }
}
```

### 8. Managing Systems
Register and manage systems within the world. You should better not register a system between `BeginTick` and `EndTick`. If you do that so, system will add properly, but modification will defer until next tick.

```csharp
// Create a world instance
var world = new World();
world.Startup();

// Register a system with the world
world.RegisterSystem<MovementSystem>();

// Find a system by type
var movementSystem = world.FindSystem<MovementSystem>();

// Tick the world in a loop
while (true) 
{
    world.BeginTick();
    world.Tick();
    world.EndTick();
}
```

### 9. Using Entity Matchers
Matchers allow you to filter entities based on their component composition.

```csharp
// Match entities that have ALL of the specified components
var positionOnlyMatcher = EntityMatcher.With.OfAll<PositionComponent>();

// Match entities that have AT LEAST ONE of the specified components
var positionOrVelocityMatcher = EntityMatcher.With.OfAny<PositionComponent>().OfAny<VelocityComponent>();

// Match entities that have specific components BUT NOT others
var positionWithoutHealthMatcher = EntityMatcher.With.OfAll<PositionComponent>().OfNone<HealthComponent>();

// Complex matcher - entities with Position AND Velocity but WITHOUT Health
var complexMatcher = EntityMatcher.With.OfAll<PositionComponent>().OfAll<VelocityComponent>().OfNone<HealthComponent>();

// Match entities with specific mask
var maskingMatcher = EntityMatcher.WithMask(1 << 1);
```

### 10. Entity Collector - Advanced Filtering and Change Tracking

EntityCollector is a powerful feature that tracks entities matching specific criteria and efficiently updates when entities change. It's essential for creating state-first game logic.

#### Basic Collector Usage

```csharp
// Create a collector that tracks all entities with PositionComponent
var positionCollector = world.CreateCollector(
    EntityMatcher.With.OfAll<PositionComponent>()
);

// Process collected entities
positionCollector.Change(); // Call Change() to apply any pending changes
for (int i = 0; i < positionCollector.Collected.Count; i++)
{
    var entity = new Entity(world, positionCollector.Collected[i]);
    // Process the entity
}
```

#### Collector Flags

Collectors support different behaviors through flags:

```csharp
// Normal behavior - entities are immediately added/removed from Collected
var normalCollector = world.CreateCollector(
    EntityMatcher.With.OfAll<PositionComponent>(),
    EntityCollectorFlag.None
);

// LazyAdd - newly matching entities won't appear in Collected until Change() is called
var lazyAddCollector = world.CreateCollector(
    EntityMatcher.With.OfAll<PositionComponent>(),
    EntityCollectorFlag.LazyAdd
);

// LazyRemove - entities won't be removed from Collected until Change() is called
var lazyRemoveCollector = world.CreateCollector(
    EntityMatcher.With.OfAll<PositionComponent>(),
    EntityCollectorFlag.LazyRemove
);

// Lazy - combines both LazyAdd and LazyRemove behaviors
var lazyCollector = world.CreateCollector(
    EntityMatcher.With.OfAll<PositionComponent>(),
    EntityCollectorFlag.Lazy
);
```

#### Change Tracking

Collectors track which entities have changed since the last `Change()` call:

```csharp
var collector = world.CreateCollector(
    EntityMatcher.With.OfAll<PositionComponent>()
);

// Add some entities with components
var entity1 = world.CreateEntity();
entity1.CreateComponent<PositionComponent>();

// Call Change() to process pending changes
collector.Change();

// Access change tracking lists
var matchingEntities = collector.Matching;  // New entities that started matching
var clashingEntities = collector.Clashing;  // Entities that stopped matching

foreach (var entityId in matchingEntities)
{
    Console.WriteLine($"New matching entity: {entityId}");
}

foreach (var entityId in clashingEntities)
{
    Console.WriteLine($"Stopped matching entity: {entityId}");
}
```

#### Best Practices for Using Collectors

1. **Always call `Change()` before processing collected entities** to ensure the collection is up-to-date with recent changes.

2. **Do NOT use foreach loops** when iterating over `Collected` - use indexed for loops instead to prevent issues with potential collection modifications during iteration:

```csharp
// CORRECT - Use indexed for loop
for (int i = 0; i < collector.Collected.Count; i++)
{
    var entity = new Entity(world, collector.Collected[i]);
    // Process entity
}

// INCORRECT - Avoid foreach loops
// foreach (var entityId in collector.Collected) // This can cause issues
// {
//     var entity = new Entity(world, entityId);
//     // Process entity
// }
```

3. **Proper cleanup** - Always dispose collectors when no longer needed:

```csharp
// In system's OnDestroy method
public void OnDestroy()
{
    m_collector?.Dispose();
}
```

4. **Choose appropriate flags** based on your use case:
   - Use `Lazy` flag when you want to process all changes at once in a predictable manner
   - Use `None` flag when you need immediate updates to the collection


### 11. Complete Example
Here's a complete example demonstrating the basic usage:

```csharp
using System;
using TinyECS;
using TinyECS.Defines;

// Define components
public struct PositionComponent : IComponent<PositionComponent>
{
    public float X;
    public float Y;
}

public struct VelocityComponent : IComponent<VelocityComponent>
{
    public float X;
    public float Y;
}

// Define system
public class MovementSystem : ISystem
{
    private World m_world;
    private IEntityCollector m_movingEntities;
    
    public MovementSystem(World world)
    {
        m_world = world;
    }
    
    public void OnCreate()
    {
        m_movingEntities = m_world.CreateCollector(
            EntityMatcher.With.OfAll<PositionComponent>().OfAll<VelocityComponent>()
        );
    }
    
    public void OnTick()
    {
        m_movingEntities.Change(); // Apply pending changes
        for (var i = 0; i < m_movingEntities.Collected.Count; i++)
        {
            var entity = new Entity(m_world, m_movingEntities.Collected[i]);
            
            var position = entity.GetComponent<PositionComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            
            position.RW.X += velocity.RW.X * 0.016f; // Assuming ~60 FPS
            position.RW.Y += velocity.RW.Y * 0.016f;
        }
    }
    
    public void OnDestroy()
    {
        m_movingEntities?.Dispose();
    }
}

// Usage
class Program
{
    static void Main(string[] args)
    {
        // Create and start world
        var world = new World();
        world.Startup();
        
        // Register system
        world.RegisterSystem<MovementSystem>();
        
        // Create an entity with position and velocity
        var entity = world.CreateEntity();
        entity.CreateComponent<PositionComponent>().RW = new PositionComponent { X = 0, Y = 0 };
        entity.CreateComponent<VelocityComponent>().RW = new VelocityComponent { X = 10, Y = 5 };
        
        // Run simulation loop
        for (int i = 0; i < 100; i++)
        {
            world.BeginTick();
            world.Tick();
            world.EndTick();
            
            var position = entity.GetComponent<PositionComponent>();
            Console.WriteLine($"Frame {i}: Position = ({position.RW.X:F2}, {position.RW.Y:F2})");
        }
        
        // Cleanup
        world.Shutdown();
    }
}
```