using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinyECS;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS.Test
{
    [TestFixture]
    public class EntityMatcherTestUnit
    {
        private World _world;
        
        [SetUp]
        public void Setup()
        {
            _world = new World();
            _world.Startup();
        }
        
        [TearDown]
        public void TearDown()
        {
            _world?.Shutdown();
        }
        
        [Test]
        public void EntityMatcher_OfAll_CanMatchEntitiesWithAllComponents()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            var entity3 = _world.CreateEntity();
            
            entity1.CreateComponent<PositionComponent>();
            entity2.CreateComponent<PositionComponent>();
            entity2.CreateComponent<VelocityComponent>();
            entity3.CreateComponent<VelocityComponent>();
            
            var matcher = EntityMatcher.With.OfAll<PositionComponent>();
            
            // Act
            var entityManager = _world.GetManager<EntityManager>();
            var matchedEntities = new List<Entity>();
            
            foreach (var kvp in entityManager.EntityCaches)
            {
                var entity = new Entity(_world, kvp.Key);
                if (matcher.ComponentFilter(kvp.Value.GetComponents().Select(x => x.Core).ToArray()))
                {
                    matchedEntities.Add(entity);
                }
            }
            
            // Assert
            Assert.AreEqual(2, matchedEntities.Count);
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity1.EntityId));
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity2.EntityId));
            Assert.IsFalse(matchedEntities.Exists(e => e.EntityId == entity3.EntityId));
        }
        
        [Test]
        public void EntityMatcher_OfAny_CanMatchEntitiesWithAnyComponent()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            var entity3 = _world.CreateEntity();
            
            entity1.CreateComponent<PositionComponent>();
            entity2.CreateComponent<VelocityComponent>();
            entity3.CreateComponent<HealthComponent>();
            
            var matcher = EntityMatcher.With.OfAny<PositionComponent>().OfAny<VelocityComponent>();
            
            // Act
            var entityManager = _world.GetManager<EntityManager>();
            var matchedEntities = new List<Entity>();
            
            foreach (var kvp in entityManager.EntityCaches)
            {
                var entity = new Entity(_world, kvp.Key);
                if (matcher.ComponentFilter(kvp.Value.GetComponents().Select(x => x.Core).ToArray()))
                {
                    matchedEntities.Add(entity);
                }
            }
            
            // Assert
            Assert.AreEqual(2, matchedEntities.Count);
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity1.EntityId));
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity2.EntityId));
            Assert.IsFalse(matchedEntities.Exists(e => e.EntityId == entity3.EntityId));
        }
        
        [Test]
        public void EntityMatcher_OfNone_CanExcludeEntitiesWithComponent()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            var entity3 = _world.CreateEntity();
            
            entity1.CreateComponent<PositionComponent>();
            entity2.CreateComponent<PositionComponent>();
            entity2.CreateComponent<VelocityComponent>();
            entity3.CreateComponent<VelocityComponent>();
            
            var matcher = EntityMatcher.With.OfAll<PositionComponent>().OfNone<VelocityComponent>();
            
            // Act
            var entityManager = _world.GetManager<EntityManager>();
            var matchedEntities = new List<Entity>();
            
            foreach (var kvp in entityManager.EntityCaches)
            {
                var entity = new Entity(_world, kvp.Key);
                if (matcher.ComponentFilter(kvp.Value.GetComponents().Select(x => x.Core).ToArray()))
                {
                    matchedEntities.Add(entity);
                }
            }
            
            // Assert
            Assert.AreEqual(1, matchedEntities.Count);
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity1.EntityId));
            Assert.IsFalse(matchedEntities.Exists(e => e.EntityId == entity2.EntityId));
            Assert.IsFalse(matchedEntities.Exists(e => e.EntityId == entity3.EntityId));
        }
        
        [Test]
        public void EntityMask_CanFilterEntitiesByMask()
        {
            // Arrange
            var entity1 = _world.CreateEntity(0b0001); // Mask with bit 0 set
            var entity2 = _world.CreateEntity(0b0010); // Mask with bit 1 set
            var entity3 = _world.CreateEntity(0b0011); // Mask with bits 0 and 1 set
            
            var matcher = EntityMatcher.WithMask(0b0001); // Match entities with bit 0 set
            
            // Act
            var entityManager = _world.GetManager<EntityManager>();
            var matchedEntities = new List<Entity>();
            
            foreach (var kvp in entityManager.EntityCaches)
            {
                var entity = new Entity(_world, kvp.Key);
                if ((entity.Mask & matcher.EntityMask) == matcher.EntityMask)
                {
                    matchedEntities.Add(entity);
                }
            }
            
            // Assert
            Assert.AreEqual(2, matchedEntities.Count);
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity1.EntityId));
            Assert.IsFalse(matchedEntities.Exists(e => e.EntityId == entity2.EntityId));
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity3.EntityId));
        }
        
        [Test]
        public void EntityMatcher_ComplexFiltering()
        {
            // Arrange
            var entities = new List<Entity>();
            
            // Create entities with different component combinations
            for (int i = 0; i < 20; i++)
            {
                var entity = _world.CreateEntity();
                
                if (i % 2 == 0)
                    entity.CreateComponent<PositionComponent>();
                
                if (i % 3 == 0)
                    entity.CreateComponent<VelocityComponent>();
                
                if (i % 5 == 0)
                    entity.CreateComponent<HealthComponent>();
                
                entities.Add(entity);
            }
            
            // Create matchers for different queries
            var positionMatcher = EntityMatcher.With.OfAll<PositionComponent>();
            var positionOrVelocityMatcher = EntityMatcher.With.OfAny<PositionComponent>().OfAny<VelocityComponent>();
            var positionWithoutHealthMatcher = EntityMatcher.With.OfAll<PositionComponent>().OfNone<HealthComponent>();
            
            // Act
            var positionEntities = new List<Entity>();
            var positionOrVelocityEntities = new List<Entity>();
            var positionWithoutHealthEntities = new List<Entity>();
            
            foreach (var entity in entities)
            {
                if (positionMatcher.ComponentFilter(entity.GetComponents().Select(x => x.Core).ToArray()))
                    positionEntities.Add(entity);
                
                if (positionOrVelocityMatcher.ComponentFilter(entity.GetComponents().Select(x => x.Core).ToArray()))
                    positionOrVelocityEntities.Add(entity);
                
                if (positionWithoutHealthMatcher.ComponentFilter(entity.GetComponents().Select(x => x.Core).ToArray()))
                    positionWithoutHealthEntities.Add(entity);
            }
            
            // Assert
            Assert.AreEqual(10, positionEntities.Count); // Every second entity (0, 2, 4, ...)
            Assert.AreEqual(13, positionOrVelocityEntities.Count); // Entities with Position or Velocity
            Assert.AreEqual(8, positionWithoutHealthEntities.Count); // Position entities without Health
            
            // Cleanup
            foreach (var entity in entities)
            {
                _world.DestroyEntity(entity);
            }
        }
        
        [Test]
        public void EntityMatcher_CanHandleEmptyComponentList()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var matcher = EntityMatcher.With.OfAll<PositionComponent>();
            
            // Act
            var result = matcher.ComponentFilter(entity.GetComponents().Select(x => x.Core).ToArray());
            
            // Assert
            Assert.IsFalse(result);
        }
        
        [Test]
        public void EntityMatcher_CanHandleMultipleOfAny()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            var entity3 = _world.CreateEntity();
            var entity4 = _world.CreateEntity();
            
            entity1.CreateComponent<PositionComponent>();
            entity2.CreateComponent<VelocityComponent>();
            entity3.CreateComponent<HealthComponent>();
            entity4.CreateComponent<PositionComponent>();
            entity4.CreateComponent<VelocityComponent>();
            
            var matcher = EntityMatcher.With.OfAny<PositionComponent>().OfAny<VelocityComponent>().OfAny<HealthComponent>();
            
            // Act
            var entityManager = _world.GetManager<EntityManager>();
            var matchedEntities = new List<Entity>();
            
            foreach (var kvp in entityManager.EntityCaches)
            {
                var entity = new Entity(_world, kvp.Key);
                if (matcher.ComponentFilter(kvp.Value.GetComponents().Select(x => x.Core).ToArray()))
                {
                    matchedEntities.Add(entity);
                }
            }
            
            // Assert
            Assert.AreEqual(4, matchedEntities.Count); // All entities should match
        }
        
        [Test]
        public void EntityMatcher_CanHandleMultipleOfNone()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            var entity3 = _world.CreateEntity();
            var entity4 = _world.CreateEntity();
            
            entity1.CreateComponent<PositionComponent>();
            entity2.CreateComponent<VelocityComponent>();
            entity3.CreateComponent<HealthComponent>();
            entity4.CreateComponent<PositionComponent>();
            entity4.CreateComponent<VelocityComponent>();
            
            var matcher = EntityMatcher.With.OfNone<PositionComponent>().OfNone<VelocityComponent>();
            
            // Act
            var entityManager = _world.GetManager<EntityManager>();
            var matchedEntities = new List<Entity>();
            
            foreach (var kvp in entityManager.EntityCaches)
            {
                var entity = new Entity(_world, kvp.Key);
                if (matcher.ComponentFilter(kvp.Value.GetComponents().Select(x => x.Core).ToArray()))
                {
                    matchedEntities.Add(entity);
                }
            }
            
            // Assert
            Assert.AreEqual(1, matchedEntities.Count); // Only entity3 should match
            Assert.IsTrue(matchedEntities.Exists(e => e.EntityId == entity3.EntityId));
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
    }
}