using System;
using NUnit.Framework;
using TinyECS;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS.Test
{
    [TestFixture]
    public class WorldTestUnit
    {
        [Test]
        public void World_CanBeCreatedAndStarted()
        {
            // Arrange & Act
            var world = new World();
            
            // Assert
            Assert.IsFalse(world.Ready);
            
            // Act
            world.Startup();
            
            // Assert
            Assert.IsTrue(world.Ready);
            Assert.AreEqual(0, world.TickCount);
            
            // Cleanup
            world.Shutdown();
            Assert.IsFalse(world.Ready);
        }
        
        [Test]
        public void World_CanTickMultipleTimes()
        {
            // Arrange
            var world = new World();
            world.Startup();
            world.RegisterSystem<TestSystem>();
            
            // Act
            for (int i = 0; i < 5; i++)
            {
                world.BeginTick();
                world.Tick();
                world.EndTick();
            }
            
            // Assert
            Assert.AreEqual(5, world.TickCount);
            var system = world.FindSystem<TestSystem>();
            Assert.AreEqual(5, system.TickCount);
            
            // Cleanup
            world.Shutdown();
        }
        
        [Test]
        public void World_CanCreateEntity()
        {
            // Arrange
            var world = new World();
            world.Startup();
            
            // Act
            var entity = world.CreateEntity();
            
            // Assert
            Assert.IsTrue(entity.IsValid);
            Assert.AreEqual(world, entity.World);
            Assert.IsTrue(entity.EntityId > 0);
            
            // Cleanup
            world.Shutdown();
        }
        
        [Test]
        public void World_CanDestroyEntity()
        {
            // Arrange
            var world = new World();
            world.Startup();
            var entity = world.CreateEntity();
            var entityId = entity.EntityId;
            
            // Act
            world.DestroyEntity(entity);
            
            // Assert
            Assert.IsFalse(entity.IsValid);
            Assert.IsNull(world.GetManager<EntityManager>().GetEntity(entityId));
            
            // Cleanup
            world.Shutdown();
        }
        
        [Test]
        public void World_CanRegisterAndExecuteSystem()
        {
            // Arrange
            var world = new World();
            world.Startup();
            world.RegisterSystem<TestSystem>();
            
            // Act
            world.BeginTick();
            world.Tick();
            world.EndTick();
            
            // Assert
            var system = world.FindSystem<TestSystem>();
            Assert.IsNotNull(system);
            Assert.IsTrue(system.OnCreateCalled);
            Assert.IsTrue(system.OnTickCalled);
            
            // Cleanup
            world.Shutdown();
        }
        
        [Test]
        public void World_MultipleWorldsIsolation()
        {
            // Arrange
            var world1 = new World();
            var world2 = new World();
            
            world1.Startup();
            world2.Startup();
            
            // Act
            var entity1 = world1.CreateEntity();
            var entity2 = world2.CreateEntity();
            
            // Assert
            Assert.AreEqual(entity1.EntityId, entity2.EntityId);
            Assert.AreNotEqual(entity1.World, entity2.World);
            
            // Cleanup
            world1.DestroyEntity(entity1);
            world2.DestroyEntity(entity2);
            world1.Shutdown();
            world2.Shutdown();
        }
        
        [Test]
        public void World_Injection_CanInjectDependencies()
        {
            // Arrange
            var world = new World();
            world.Startup();
            world.Injection.Register(new TestService());
            world.RegisterSystem<DependencyTestSystem>();
            
            // Act
            world.BeginTick();
            world.Tick();
            world.EndTick();
            
            // Assert
            var system = world.FindSystem<DependencyTestSystem>();
            Assert.IsNotNull(system);
            Assert.IsNotNull(system.TestService);
            Assert.IsTrue(system.TestServiceCalled);
            
            // Cleanup
            world.Shutdown();
        }
        
        // Test system
        private class TestSystem : ISystem
        {
            public bool OnCreateCalled { get; private set; }
            public bool OnTickCalled { get; private set; }
            public int TickCount { get; private set; }
            
            public void OnCreate()
            {
                OnCreateCalled = true;
            }
            
            public void OnTick()
            {
                OnTickCalled = true;
                TickCount++;
            }
            
            public void OnDestroy()
            {
            }
        }
        
        // Test system with dependency
        private class DependencyTestSystem : ISystem
        {
            public ITestService TestService { get; private set; }
            public bool TestServiceCalled { get; private set; }
            
            public void OnCreate()
            {
            }
            
            public void OnTick()
            {
                if (TestService != null)
                {
                    TestService.DoSomething();
                    TestServiceCalled = true;
                }
            }
            
            public void OnDestroy()
            {
            }

            public DependencyTestSystem(ITestService testService)
            {
                TestService = testService;
            }
        }
        
        // Test service interface and implementation
        private interface ITestService
        {
            void DoSomething();
        }
        
        private class TestService : ITestService
        {
            public void DoSomething()
            {
                // Do something
            }
        }
    }
}