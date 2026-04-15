#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class VoxelSpiderWalkerFactoryTests
    {
        [Test]
        public void EnsureInstance_PopulatesLoadedPrefabWithExpectedHierarchy()
        {
            var prefab = Resources.Load<GameObject>("VoxelVillage/Threats/VV_Ambient_SpiderWalker");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);

            try
            {
                var result = VoxelSpiderWalkerFactory.EnsureInstance(instance);

                Assert.That(result.Controller, Is.Not.Null);
                Assert.That(result.Controller.IsRigBound, Is.True);
                Assert.That(result.Root.transform.Find("LocomotionRoot/BodyPivot/BodyShell"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("LocomotionRoot/BodyPivot/EyeCluster"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("LocomotionRoot/Leg_FL/Hip/UpperVisual/Knee/LowerVisual"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("LocomotionRoot/Leg_FL/FootTarget"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("LocomotionRoot/Leg_BR/Hip/UpperVisual/Knee/LowerVisual"), Is.Not.Null);

                var eyeCluster = result.Root.transform.Find("LocomotionRoot/BodyPivot/EyeCluster");
                Assert.That(eyeCluster, Is.Not.Null);
                Assert.That(eyeCluster!.childCount, Is.EqualTo(8));
                Assert.That(eyeCluster.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(8));
                Assert.That(result.Root.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CreateInstance_BuildsFallbackSpiderFromScratch()
        {
            var result = VoxelSpiderWalkerFactory.CreateInstance("FactorySpider", new Vector3(2f, 0f, -1f), 1.1f);

            try
            {
                Assert.AreEqual("VV_Ambient_SpiderWalker", result.Root.name);
                Assert.AreEqual(new Vector3(2f, 0f, -1f), result.Root.transform.position);
                Assert.AreEqual(Vector3.one * 1.1f, result.Root.transform.localScale);
                Assert.That(result.Root.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThanOrEqualTo(13));
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }
    }
}
