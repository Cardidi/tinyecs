using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinyECS;
using TinyECS.Defines;

namespace TinyECS.Test
{
    [TestFixture]
    public class EntityTestUnit
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
        public void Entity_IsValidAfterCreation()
        {
            // Act
            var entity = _world.CreateEntity();
            
            // Assert
            Assert.IsTrue(entity.IsValid);
            Assert.AreEqual(_world, entity.World);
            Assert.IsTrue(entity.EntityId > 0);
        }
        
        [Test]
        public void Entity_IsInvalidAfterDestruction()
        {
            // Arrange
            var entity = _world.CreateEntity();
            
            // Act
            _world.DestroyEntity(entity);
            
            // Assert
            Assert.IsFalse(entity.IsValid);
        }
        
        [Test]
        public void Entity_CanAccessMask()
        {
            // Arrange
            var entity = _world.CreateEntity(0b1010);
            
            // Act
            var mask = entity.Mask;
            
            // Assert
            Assert.AreEqual(0b1010, mask);
        }
        
        [Test]
        public void Entity_InvalidAccessThrowsException()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var entityId = entity.EntityId;
            
            // Destroy the entity
            _world.DestroyEntity(entity);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => entity.CreateComponent<PositionComponent>());
            Assert.Throws<InvalidOperationException>(() => entity.GetComponent<PositionComponent>());
            Assert.Throws<InvalidOperationException>(() => entity.HasComponent<PositionComponent>());
            Assert.Throws<InvalidOperationException>(() => entity.GetComponents());
            Assert.Throws<InvalidOperationException>(() => { _ = entity.Mask; });
        }
        
        [Test]
        public void Entity_CanCreateComponent()
        {
            // Arrange
            var entity = _world.CreateEntity();
            
            // Act
            var positionRef = entity.CreateComponent<PositionComponent>();
            positionRef.RW = new PositionComponent { X = 10, Y = 20 };
            
            // Assert
            Assert.IsNotNull(positionRef);
            Assert.AreEqual(10, positionRef.RW.X);
            Assert.AreEqual(20, positionRef.RW.Y);
        }
        
        [Test]
        public void Entity_CanGetComponent()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            positionRef.RW = new PositionComponent { X = 10, Y = 20 };
            
            // Act
            var retrievedRef = entity.GetComponent<PositionComponent>();
            
            // Assert
            Assert.IsNotNull(retrievedRef);
            Assert.AreEqual(10, retrievedRef.RW.X);
            Assert.AreEqual(20, retrievedRef.RW.Y);
        }
        
        [Test]
        public void Entity_CanDestroyComponent()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            
            // Act
            entity.DestroyComponent(positionRef);
            
            // Assert
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
        }
        
        [Test]
        public void Entity_CanGetAllComponents()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            entity.CreateComponent<HealthComponent>();
            
            // Act
            var allComponents = entity.GetComponents();
            
            // Assert
            Assert.AreEqual(3, allComponents.Length);
            
            var componentTypes = new List<Type>();
            foreach (var comp in allComponents)
            {
                componentTypes.Add(comp.RuntimeType);
            }
            
            Assert.Contains(typeof(PositionComponent), componentTypes);
            Assert.Contains(typeof(VelocityComponent), componentTypes);
            Assert.Contains(typeof(HealthComponent), componentTypes);
        }
        
        [Test]
        public void Entity_CanHaveMultipleComponents()
        {
            // Arrange
            var entity = _world.CreateEntity();
            
            // Act
            var positionRef = entity.CreateComponent<PositionComponent>();
            var velocityRef = entity.CreateComponent<VelocityComponent>();
            
            positionRef.RW = new PositionComponent { X = 10, Y = 20 };
            velocityRef.RW = new VelocityComponent { X = 1, Y = 2 };
            
            // Assert
            Assert.IsTrue(entity.HasComponent<PositionComponent>());
            Assert.IsTrue(entity.HasComponent<VelocityComponent>());
            
            var pos = entity.GetComponent<PositionComponent>();
            var vel = entity.GetComponent<VelocityComponent>();
            
            Assert.AreEqual(10, pos.RW.X);
            Assert.AreEqual(20, pos.RW.Y);
            Assert.AreEqual(1, vel.RW.X);
            Assert.AreEqual(2, vel.RW.Y);
        }
        
        [Test]
        public void Entity_CanCreateComponentByType()
        {
            // Arrange
            var entity = _world.CreateEntity();
            
            // Act
            var componentRef = entity.CreateComponent<PositionComponent>();
            
            // Assert
            Assert.IsNotNull(componentRef);
            Assert.AreEqual(typeof(PositionComponent), componentRef.RW.GetType());
        }
        
        [Test]
        public void Entity_CanDestroyComponentByRef()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var componentRef = entity.CreateComponent<PositionComponent>();
            
            // Act
            entity.DestroyComponent(componentRef);
            
            // Assert
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
        }
        
        [Test]
        public void Entity_DestroyComponentByType()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();

            // Assert that component exists
            Assert.IsTrue(entity.HasComponent<PositionComponent>());

            // Act
            entity.DestroyComponent<PositionComponent>();

            // Assert that component no longer exists
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
        }
        
        [Test]
        public void Entity_DestroyMultipleComponentsOfType()
        {
            // Arrange
            var entity = _world.CreateEntity();
            entity.CreateComponent<PositionComponent>();
            entity.CreateComponent<VelocityComponent>();
            
            // Assert components exist
            Assert.IsTrue(entity.HasComponent<PositionComponent>());
            Assert.IsTrue(entity.HasComponent<VelocityComponent>());
            
            // Act
            entity.DestroyComponent<PositionComponent>();
            
            // Assert only PositionComponent was removed
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
            Assert.IsTrue(entity.HasComponent<VelocityComponent>());
        }
        
        [Test]
        public void Entity_DestroyComponentByUntypedRef()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var typedRef = entity.CreateComponent<PositionComponent>();
            var untypedRef = typedRef.Expand(); // Convert to untyped reference
            
            // Assert component exists
            Assert.IsTrue(entity.HasComponent<PositionComponent>());
            
            // Act
            entity.DestroyComponent(untypedRef);
            
            // Assert component no longer exists
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
        }
        
        [Test]
        public void Entity_DestroyForeignComponent_ThrowsException()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            var componentRef = entity1.CreateComponent<PositionComponent>();
            
            // Act & Assert - Should throw exception because component belongs to different entity
            Assert.Throws<InvalidOperationException>(() => entity2.DestroyComponent(componentRef));
        }
        
        [Test]
        public void Entity_GetComponentAfterDestruction_ThrowsException()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            positionRef.RW = new PositionComponent { X = 10, Y = 20 };
            
            // Verify component exists and is accessible
            var retrievedRef = entity.GetComponent<PositionComponent>();
            Assert.AreEqual(10, retrievedRef.RW.X);
            
            // Act - Destroy the component
            entity.DestroyComponent(positionRef);
            
            // Assert - Getting the component should now fail
            Assert.IsFalse(entity.GetComponent<PositionComponent>().NotNull);
        }
        
        [Test]
        public void Entity_CreateAndDestroySameComponentMultipleTimes()
        {
            // Arrange
            var entity = _world.CreateEntity();
            
            // Act & Assert - First cycle
            var comp1 = entity.CreateComponent<PositionComponent>();
            comp1.RW = new PositionComponent { X = 10, Y = 20 };
            Assert.IsTrue(entity.HasComponent<PositionComponent>());
            Assert.AreEqual(10, entity.GetComponent<PositionComponent>().RW.X);
            
            entity.DestroyComponent(comp1);
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
            
            // Second cycle
            var comp2 = entity.CreateComponent<PositionComponent>();
            comp2.RW = new PositionComponent { X = 30, Y = 40 };
            Assert.IsTrue(entity.HasComponent<PositionComponent>());
            Assert.AreEqual(30, entity.GetComponent<PositionComponent>().RW.X);
            
            entity.DestroyComponent(comp2);
            Assert.IsFalse(entity.HasComponent<PositionComponent>());
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