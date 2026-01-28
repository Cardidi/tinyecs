using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using TinyECS;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS.Test
{
    [TestFixture]
    public class StressTestUnit
    {
        [Test]
        public void StressTest_LargeNumberOfEntities()
        {
            // Arrange
            var world = new World();
            world.Startup();
            
            const int entityCount = 10000; // Increased for stress test
            var entities = new List<Entity>();
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < entityCount; i++)
            {
                var entity = world.CreateEntity();
                entity.CreateComponent<PositionComponent>();
                entity.CreateComponent<VelocityComponent>();
                entities.Add(entity);
            }
            
            stopwatch.Stop();
            
            // Assert
            Assert.AreEqual(entityCount, entities.Count);
            Console.WriteLine($"Created {entityCount} entities in {stopwatch.ElapsedMilliseconds} ms");
            Assert.Less(stopwatch.ElapsedMilliseconds, 5000); // Should create 10k entities in less than 5 seconds
            
            // Cleanup
            stopwatch.Restart();
            foreach (var entity in entities)
            {
                world.DestroyEntity(entity);
            }
            stopwatch.Stop();
            Console.WriteLine($"Destroyed {entityCount} entities in {stopwatch.ElapsedMilliseconds} ms");
            
            world.Shutdown();
        }
        
        [Test]
        public void StressTest_HighFrequencyTicks()
        {
            // Arrange
            var world = new World();
            world.Startup();
            
            const int tickCount = 1000; // More ticks for stress test
            const int entitiesPerTick = 50;
            
            world.RegisterSystem<MovementSystem>();
            var entities = new List<Entity>();
            
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < tickCount; i++)
            {
                // Create entities periodically
                if (i % 100 == 0)
                {
                    for (int j = 0; j < entitiesPerTick; j++)
                    {
                        var entity = world.CreateEntity();
                        entity.CreateComponent<PositionComponent>();
                        entity.CreateComponent<VelocityComponent>();
                        entities.Add(entity);
                    }
                }
                
                // Tick the world
                world.BeginTick();
                world.Tick();
                world.EndTick();
            }
            
            stopwatch.Stop();
            
            // Assert
            Assert.AreEqual(tickCount, world.TickCount);
            Assert.IsTrue(world.FindSystem<MovementSystem>().ProcessedEntities);
            Console.WriteLine($"Completed {tickCount} ticks with {entities.Count} total entities in {stopwatch.ElapsedMilliseconds} ms");
            
            // Cleanup
            foreach (var entity in entities)
            {
                world.DestroyEntity(entity);
            }
            world.Shutdown();
        }
        
        [Test]
        public void StressTest_ComponentOperationsUnderLoad()
        {
            // Arrange
            var world = new World();
            world.Startup();
            
            const int entityCount = 5000;
            var entities = new List<Entity>();
            
            // Create entities with components
            for (int i = 0; i < entityCount; i++)
            {
                var entity = world.CreateEntity();
                entity.CreateComponent<PositionComponent>();
                entities.Add(entity);
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            // Act - Perform many component operations
            for (int i = 0; i < 1000; i++)
            {
                var randomEntity = entities[i % entities.Count];
                
                // Add and remove components
                if (randomEntity.HasComponent<VelocityComponent>())
                {
                    var velRef = randomEntity.GetComponent<VelocityComponent>();
                    randomEntity.DestroyComponent(velRef);
                }
                else
                {
                    randomEntity.CreateComponent<VelocityComponent>();
                }
                
                // Update component data
                var posRef = randomEntity.GetComponent<PositionComponent>();
                posRef.RW.X = i % 100;
                posRef.RW.Y = i % 100;
            }
            
            stopwatch.Stop();
            
            // Assert
            Console.WriteLine($"Performed 1000 component operations in {stopwatch.ElapsedMilliseconds} ms");
            Assert.Less(stopwatch.ElapsedMilliseconds, 2000); // Should complete in under 2 seconds
            
            // Cleanup
            foreach (var entity in entities)
            {
                world.DestroyEntity(entity);
            }
            world.Shutdown();
        }
        
        [Test]
        public void StressTest_ManyCollectors()
        {
            // Arrange
            var world = new World();
            world.Startup();
            
            // Create many different collectors with various matchers
            var collectors = new List<IEntityCollector>();
            
            var matcher1 = EntityMatcher.With.OfAll<PositionComponent>();
            var matcher2 = EntityMatcher.With.OfAll<VelocityComponent>();
            var matcher3 = EntityMatcher.With.OfAll<PositionComponent>().OfAll<VelocityComponent>();
            var matcher4 = EntityMatcher.With.OfAll<HealthComponent>();
            var matcher5 = EntityMatcher.With.OfAll<PositionComponent>().OfAny<VelocityComponent>().OfAny<HealthComponent>();
            
            collectors.Add(world.CreateCollector(matcher1));
            collectors.Add(world.CreateCollector(matcher2));
            collectors.Add(world.CreateCollector(matcher3));
            collectors.Add(world.CreateCollector(matcher4));
            collectors.Add(world.CreateCollector(matcher5));
            
            // Create entities
            const int entityCount = 1000;
            var entities = new List<Entity>();
            
            for (int i = 0; i < entityCount; i++)
            {
                var entity = world.CreateEntity();
                
                // Randomly assign components to create varied matching scenarios
                entity.CreateComponent<PositionComponent>();
                
                if (i % 2 == 0)
                    entity.CreateComponent<VelocityComponent>();
                
                if (i % 3 == 0)
                    entity.CreateComponent<HealthComponent>();
                
                entities.Add(entity);
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            // Run multiple ticks while using collectors
            for (int i = 0; i < 100; i++)
            {
                world.BeginTick();
                world.Tick();
                
                // Update all collectors
                foreach (var collector in collectors)
                {
                    collector.Change();
                }
                
                world.EndTick();
            }
            
            stopwatch.Stop();
            
            // Assert
            Assert.AreEqual(100, world.TickCount);
            Console.WriteLine($"Ran {entityCount} entities with 5 collectors through 100 ticks in {stopwatch.ElapsedMilliseconds} ms");
            
            // Cleanup
            foreach (var collector in collectors)
            {
                collector.Dispose();
            }
            
            foreach (var entity in entities)
            {
                world.DestroyEntity(entity);
            }
            
            world.Shutdown();
        }
        
        [Test]
        public void StressTest_EntityCreationAndDestruction()
        {
            // Arrange
            var world = new World();
            world.Startup();
            
            const int iterations = 1000;
            const int batchSize = 50;
            
            var stopwatch = Stopwatch.StartNew();
            
            // Act - Repeatedly create and destroy batches of entities
            for (int i = 0; i < iterations; i++)
            {
                var batch = new List<Entity>();
                
                // Create a batch of entities
                for (int j = 0; j < batchSize; j++)
                {
                    var entity = world.CreateEntity();
                    entity.CreateComponent<PositionComponent>();
                    entity.CreateComponent<VelocityComponent>();
                    batch.Add(entity);
                }
                
                // Tick to process the new entities
                world.BeginTick();
                world.Tick();
                world.EndTick();
                
                // Destroy the batch
                foreach (var entity in batch)
                {
                    world.DestroyEntity(entity);
                }
            }
            
            stopwatch.Stop();
            
            // Assert
            Console.WriteLine($"Created and destroyed {iterations * batchSize} entities in {stopwatch.ElapsedMilliseconds} ms");
            Assert.Less(stopwatch.ElapsedMilliseconds, 10000); // Should complete in under 10 seconds
            
            world.Shutdown();
        }
        
        // Test components
        private struct PositionComponent : IComponent<PositionComponent>
        {
            public float X;
            public float Y;
        }
        
        private struct VelocityComponent : IComponent<VelocityComponent>
        {
            public float X;
            public float Y;
        }
        
        private struct HealthComponent : IComponent<HealthComponent>
        {
            public float Value;
        }
        
        // Test systems
        private class MovementSystem : ISystem
        {
            public bool ProcessedEntities { get; private set; }
         
            private World m_world;
            
            private IEntityCollector m_movementCollector;
            
            public void OnCreate()
            {
                m_movementCollector = m_world?.CreateCollector(
                    EntityMatcher.With.OfAll<PositionComponent>().OfAll<VelocityComponent>()
                );
            }
            
            public void OnTick()
            {
                if (m_movementCollector != null)
                {
                    m_movementCollector.Change();
                    foreach (var entityId in m_movementCollector.Collected)
                    {
                        ProcessedEntities = true;
                        
                        var entity = m_world.GetEntity(entityId);
                        var positionComp = entity.GetComponent<PositionComponent>();
                        var velocityComp = entity.GetComponent<VelocityComponent>();
                        
                        positionComp.RW.X += velocityComp.RO.X;
                        positionComp.RW.Y += velocityComp.RO.Y;
                    }
                }
            }
            
            public void OnDestroy()
            {
                m_movementCollector?.Dispose();
            }

            public MovementSystem(World world)
            {
                m_world = world;
            }
        }
    }
}