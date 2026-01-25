using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinyECS;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS.Test
{
    [TestFixture]
    public class EntityGraphTestUnit
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
        public void EntityGraph_GetComponentCount_ReturnsCorrectCount()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            entity.CreateComponent<PositionComponent>(); // Adding another PositionComponent to test counting
            
            var entityGraph = GetEntityGraph(entity.EntityId);
            
            // Act
            var positionCount = entityGraph.GetComponentCount<PositionComponent>();
            var velocityCount = entityGraph.GetComponentCount<VelocityComponent>();
            var healthCount = entityGraph.GetComponentCount<HealthComponent>();
            
            // Assert
            Assert.AreEqual(2, positionCount);
            Assert.AreEqual(1, velocityCount);
            Assert.AreEqual(0, healthCount);
        }
        
        [Test]
        public void EntityGraph_GetComponents_ICollectionRefillCorrectly()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            entity.CreateComponent<HealthComponent>();
            
            var entityGraph = GetEntityGraph(entity.EntityId);
            var results = new List<ComponentRef>();
            
            // Act
            var count = entityGraph.GetComponents(results);
            
            // Assert
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, results.Count);
            
            var componentTypes = new HashSet<Type>();
            foreach (var comp in results)
            {
                componentTypes.Add(comp.RuntimeType);
            }
            
            Assert.IsTrue(componentTypes.Contains(typeof(PositionComponent)));
            Assert.IsTrue(componentTypes.Contains(typeof(VelocityComponent)));
            Assert.IsTrue(componentTypes.Contains(typeof(HealthComponent)));
        }
        
        [Test]
        public void EntityGraph_GetComponentsT_ArrayReturnsCorrectly()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            entity.CreateComponent<PositionComponent>(); // Add another of the same type
            
            var entityGraph = GetEntityGraph(entity.EntityId);
            
            // Act
            var positionComponents = entityGraph.GetComponents<PositionComponent>();
            var velocityComponents = entityGraph.GetComponents<VelocityComponent>();
            var healthComponents = entityGraph.GetComponents<HealthComponent>();
            
            // Assert
            Assert.AreEqual(2, positionComponents.Length);
            Assert.AreEqual(1, velocityComponents.Length);
            Assert.AreEqual(0, healthComponents.Length);
        }
        
        [Test]
        public void EntityGraph_GetComponentsT_ICollectionRefillCorrectly()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            entity.CreateComponent<PositionComponent>(); // Add another of the same type
            
            var entityGraph = GetEntityGraph(entity.EntityId);
            var positionResults = new List<ComponentRef<PositionComponent>>();
            var velocityResults = new List<ComponentRef<VelocityComponent>>();
            var healthResults = new List<ComponentRef<HealthComponent>>();
            
            // Act
            var positionCount = entityGraph.GetComponents(positionResults);
            var velocityCount = entityGraph.GetComponents(velocityResults);
            var healthCount = entityGraph.GetComponents(healthResults);
            
            // Assert
            Assert.AreEqual(2, positionCount);
            Assert.AreEqual(1, velocityCount);
            Assert.AreEqual(0, healthCount);
            
            Assert.AreEqual(2, positionResults.Count);
            Assert.AreEqual(1, velocityResults.Count);
            Assert.AreEqual(0, healthResults.Count);
        }
        
        [Test]
        public void EntityGraph_GetComponents_MethodsConsistent()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            entity.CreateComponent<HealthComponent>();
            
            var entityGraph = GetEntityGraph(entity.EntityId);
            
            // Act - Test both methods for getting all components
            var allComponentsArray = entityGraph.GetComponents();
            var allComponentsList = new List<ComponentRef>();
            var allComponentsCount = entityGraph.GetComponents(allComponentsList);
            
            // Assert
            Assert.AreEqual(3, allComponentsArray.Length);
            Assert.AreEqual(3, allComponentsCount);
            Assert.AreEqual(3, allComponentsList.Count);
            
            // Verify both methods return the same components
            var arrayTypes = new HashSet<Type>();
            foreach (var comp in allComponentsArray)
            {
                arrayTypes.Add(comp.RuntimeType);
            }
            
            var listTypes = new HashSet<Type>();
            foreach (var comp in allComponentsList)
            {
                listTypes.Add(comp.RuntimeType);
            }
            
            CollectionAssert.AreEquivalent(arrayTypes, listTypes);
        }
        
        // Helper method to access EntityGraph from entity ID
        private EntityGraph GetEntityGraph(ulong entityId)
        {
            var entityManager = _world.GetManager<EntityManager>();
            return entityManager.GetEntity(entityId);
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
