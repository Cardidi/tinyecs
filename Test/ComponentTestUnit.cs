using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinyECS;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS.Test
{
    [TestFixture]
    public class ComponentTestUnit
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
        public void ComponentRef_CanAccessComponentData()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            
            // Act
            positionRef.RW = new PositionComponent { X = 15, Y = 25 };
            
            // Assert
            Assert.AreEqual(15, positionRef.RW.X);
            Assert.AreEqual(25, positionRef.RW.Y);
            
            // Modify through reference
            positionRef.RW.X = 30;
            Assert.AreEqual(30, entity.GetComponent<PositionComponent>().RW.X);
        }
        
        [Test]
        public void ComponentManager_CanHandleMultipleComponentTypes()
        {
            // Arrange
            var entity1 = _world.CreateEntity();
            var entity2 = _world.CreateEntity();
            
            // Act
            entity1.CreateComponent<PositionComponent>();
            entity1.CreateComponent<VelocityComponent>();
            entity2.CreateComponent<PositionComponent>();
            entity2.CreateComponent<HealthComponent>();
            
            // Assert
            var componentManager = _world.GetManager<ComponentManager>();
            
            Assert.IsTrue(entity1.HasComponent<PositionComponent>());
            Assert.IsTrue(entity1.HasComponent<VelocityComponent>());
            Assert.IsFalse(entity1.HasComponent<HealthComponent>());
            
            Assert.IsTrue(entity2.HasComponent<PositionComponent>());
            Assert.IsFalse(entity2.HasComponent<VelocityComponent>());
            Assert.IsTrue(entity2.HasComponent<HealthComponent>());
        }
        
        [Test]
        public void Component_OnCreateAndDestroy_CalledCorrectly()
        {
            // Arrange
            var entity = _world.CreateEntity();
            
            // Act
            var componentRef = entity.CreateComponent<LifecycleComponent>();
            
            // Assert
            Assert.IsTrue(componentRef.RW.OnCreateCalled);
            Assert.IsFalse(componentRef.RW.OnDestroyCalled);
            
            // Act
            entity.DestroyComponent(componentRef);
            
            // Assert
            Assert.Throws<NullReferenceException>(() => { _ = componentRef.RW.OnDestroyCalled; });
        }
        
        [Test]
        public void ComponentManager_ComponentPoolExpansion()
        {
            // Arrange
            const int entityCount = 100;
            var entities = new List<Entity>();
            
            // Act
            for (int i = 0; i < entityCount; i++)
            {
                var entity = _world.CreateEntity();
                entity.CreateComponent<LargeComponent>();
                entities.Add(entity);
            }
            
            // Assert
            foreach (var entity in entities)
            {
                Assert.IsTrue(entity.HasComponent<LargeComponent>());
                var component = entity.GetComponent<LargeComponent>();
                Assert.AreEqual(100, component.RW.Value.Data.Length);
            }
            
            // Cleanup
            foreach (var entity in entities)
            {
                _world.DestroyEntity(entity);
            }
        }
        
        [Test]
        public void ComponentRef_CanCheckType()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            
            // Act & Assert
            Assert.IsTrue(positionRef.Core.RefLocator.IsT(typeof(PositionComponent)));
            Assert.IsFalse(positionRef.Core.RefLocator.IsT(typeof(VelocityComponent)));
        }
        
        [Test]
        public void ComponentRef_CanGetEntityType()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            
            // Act
            var entityType = positionRef.Core.RefLocator.GetT();
            
            // Assert
            Assert.AreEqual(typeof(PositionComponent), entityType);
        }
        
        [Test]
        public void ComponentRef_CanGetEntityId()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            
            // Act
            var entityId = positionRef.Core.RefLocator.GetEntityId(positionRef.Core.Offset);
            
            // Assert
            Assert.AreEqual(entity.EntityId, entityId);
        }
        
        [Test]
        public void ComponentRef_CanGetRefCore()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            
            // Act
            var refCore = positionRef.Core;
            
            // Assert
            Assert.IsNotNull(refCore);
            Assert.AreEqual(positionRef.Core.Offset, refCore.Offset);
        }
        
        [Test]
        public void ComponentRef_CanRelocate()
        {
            // Arrange
            var entity = _world.CreateEntity();
            var positionRef = entity.CreateComponent<PositionComponent>();
            var originalOffset = positionRef.Core.Offset;
            var originalVersion = positionRef.Core.Version;
            
            // Act
            (positionRef.Core as ComponentRefCore).Relocate(positionRef.Core.RefLocator, originalOffset + 1, positionRef.Core.Version + 1);
            
            // Assert
            Assert.AreEqual(originalOffset + 1, positionRef.Core.Offset);
            Assert.AreEqual(originalVersion + 1, positionRef.Core.Version);
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
        
        private struct LargeComponent : IComponent<LargeComponent>
        {
            public LargeData Value;
            
            public struct LargeData
            {
                public int[] Data;
                
                public LargeData(int size)
                {
                    Data = new int[size];
                }
            }

            public void OnCreate(ulong entityId)
            {
                Value = new LargeData(100);
            }
        }
        
        private struct LifecycleComponent : IComponent<LifecycleComponent>
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
    }
}