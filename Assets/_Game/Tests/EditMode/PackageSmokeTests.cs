using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace DanceWithTheDevil.Tests
{
    // Phase 0 smoke check: the math and native-container packages the voxel systems depend on resolve and run.
    public class PackageSmokeTests
    {
        [Test]
        public void Mathematics_Float3_Adds()
        {
            float3 sum = new float3(1f, 2f, 3f) + new float3(1f);
            Assert.AreEqual(new float3(2f, 3f, 4f), sum);
        }

        [Test]
        public void Collections_NativeArray_AllocatesAndDisposes()
        {
            var array = new NativeArray<int>(8, Allocator.Temp);
            array[7] = 42;
            Assert.AreEqual(42, array[7]);
            array.Dispose();
        }
    }
}
