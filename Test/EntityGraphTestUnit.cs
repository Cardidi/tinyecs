namespace CoreECS.Test
{
    [TestFixture]
    public class EntityGraphTestUnit
    {
        [Test]
        public void EntityGraph_Pool_AllocatesAndReleasesCorrectly()
        {
            // Act
            var entityGraph = EntityGraph.Pool.Get();
            
            // Assert
            Assert.AreEqual(0, entityGraph.EntityId);
            Assert.AreEqual(0, entityGraph.Mask);
            Assert.IsFalse(entityGraph.WishDestroy);
            Assert.AreEqual(0, entityGraph.ArchetypeId);
            Assert.AreEqual(-1, entityGraph.Row);
            
            // Act
            entityGraph.EntityId = 100;
            entityGraph.Mask = 0b1010;
            entityGraph.WishDestroy = true;
            entityGraph.ArchetypeId = 3;
            entityGraph.Row = 7;
            
            // Assert
            Assert.AreEqual(100, entityGraph.EntityId);
            Assert.AreEqual(0b1010, entityGraph.Mask);
            Assert.IsTrue(entityGraph.WishDestroy);
            Assert.AreEqual(3, entityGraph.ArchetypeId);
            Assert.AreEqual(7, entityGraph.Row);
            
            // Act
            EntityGraph.Pool.Release(entityGraph);
            
            // Assert
            Assert.AreEqual(0, entityGraph.EntityId);
            Assert.AreEqual(0, entityGraph.Mask);
            Assert.IsFalse(entityGraph.WishDestroy);
            Assert.AreEqual(0, entityGraph.ArchetypeId);
            Assert.AreEqual(-1, entityGraph.Row);
        }
        
        [Test]
        public void EntityGraph_Pool_ResetClearsLocation()
        {
            // Arrange
            var graph = EntityGraph.Pool.Get();
            graph.ArchetypeId = 3;
            graph.Row = 7;
            
            // Act
            EntityGraph.Pool.Release(graph);
            var again = EntityGraph.Pool.Get();
            
            // Assert
            Assert.AreEqual(0, again.ArchetypeId);
            Assert.AreEqual(-1, again.Row);
            
            // Cleanup
            EntityGraph.Pool.Release(again);
        }
        
        [Test]
        public void EntityGraph_EntityIdProperty_SetAndGetWorks()
        {
            // Arrange
            var entityGraph = EntityGraph.Pool.Get();
            
            // Act & Assert
            entityGraph.EntityId = 12345;
            Assert.AreEqual(12345, entityGraph.EntityId);
            
            entityGraph.EntityId = ulong.MaxValue;
            Assert.AreEqual(ulong.MaxValue, entityGraph.EntityId);
            
            entityGraph.EntityId = 0;
            Assert.AreEqual(0, entityGraph.EntityId);
            
            // Cleanup
            EntityGraph.Pool.Release(entityGraph);
        }
        
        [Test]
        public void EntityGraph_MaskProperty_SetAndGetWorks()
        {
            // Arrange
            var entityGraph = EntityGraph.Pool.Get();
            
            // Act & Assert
            entityGraph.Mask = 0b1010;
            Assert.AreEqual(0b1010, entityGraph.Mask);
            
            entityGraph.Mask = 0b11110000;
            Assert.AreEqual(0b11110000, entityGraph.Mask);
            
            entityGraph.Mask = 0;
            Assert.AreEqual(0, entityGraph.Mask);
            
            // Cleanup
            EntityGraph.Pool.Release(entityGraph);
        }
        
        [Test]
        public void EntityGraph_WishDestroyProperty_SetAndGetWorks()
        {
            // Arrange
            var entityGraph = EntityGraph.Pool.Get();
            
            // Act & Assert
            entityGraph.WishDestroy = true;
            Assert.IsTrue(entityGraph.WishDestroy);
            
            entityGraph.WishDestroy = false;
            Assert.IsFalse(entityGraph.WishDestroy);
            
            // Cleanup
            EntityGraph.Pool.Release(entityGraph);
        }

        [Test]
        public void EntityGraph_LocationProperties_SetAndGetWork()
        {
            // Arrange
            var entityGraph = EntityGraph.Pool.Get();
            
            // Act & Assert
            entityGraph.ArchetypeId = 11;
            entityGraph.Row = 42;
            Assert.AreEqual(11, entityGraph.ArchetypeId);
            Assert.AreEqual(42, entityGraph.Row);
            
            entityGraph.ArchetypeId = 0;
            entityGraph.Row = -1;
            Assert.AreEqual(0, entityGraph.ArchetypeId);
            Assert.AreEqual(-1, entityGraph.Row);
            
            // Cleanup
            EntityGraph.Pool.Release(entityGraph);
        }
        
        [Test]
        public void EntityGraph_Reset_ClearsAllProperties()
        {
            // Arrange
            var entityGraph = EntityGraph.Pool.Get();
            entityGraph.EntityId = 12345;
            entityGraph.Mask = 0b1010;
            entityGraph.WishDestroy = true;
            entityGraph.ArchetypeId = 5;
            entityGraph.Row = 9;
            
            // Act
            EntityGraph.Pool.Release(entityGraph);
            
            // Assert
            Assert.AreEqual(0, entityGraph.EntityId);
            Assert.AreEqual(0, entityGraph.Mask);
            Assert.IsFalse(entityGraph.WishDestroy);
            Assert.AreEqual(0, entityGraph.ArchetypeId);
            Assert.AreEqual(-1, entityGraph.Row);
        }
    }
}
