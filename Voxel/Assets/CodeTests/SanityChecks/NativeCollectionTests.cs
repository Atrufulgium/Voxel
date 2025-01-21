using Atrufulgium.Voxel.Base;
using NUnit.Framework;
using Unity.Collections;

namespace Atrufulgium.Voxel.SanityCheckTests {
    public class NativeCollectionTests {

        /// <summary>
        /// This test is just to verify that you can pass NativeList instances
        /// around as references, as list resizes won't mess with the pointer.
        /// </summary>
        [Test]
        public unsafe void TestListRepointer() {
            using NativeList<int> listA = new(1, Allocator.Persistent);
            var listB = listA;
            int initialCapacity = listA.Capacity;
            // If you peek at the UnsafeList<> code, you'll see capacity isn't
            // _just_ the number you pass, so just absolutely hammer the list
            // to make sure it resizes after our initial "1" capacity.
            for (int i = 0; i < 10000; i++) {
                listA.Add(i);
            }
            Assert.AreNotEqual(initialCapacity, listA.Capacity);
            Assert.AreEqual((long)listA.GetUnsafeTypedPtr(), (long)listB.GetUnsafeTypedPtr());
        }
    }
}
