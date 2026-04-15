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

        [Test]
        public void EnsureInstance_AlignsLegVisualTipsWithJointsAndFootTargets()
        {
            var result = VoxelSpiderWalkerFactory.CreateInstance("FactorySpider", Vector3.zero);

            try
            {
                AssertLegAlignment(result.Root.transform, "Leg_FL");
                AssertLegAlignment(result.Root.transform, "Leg_FR");
                AssertLegAlignment(result.Root.transform, "Leg_BL");
                AssertLegAlignment(result.Root.transform, "Leg_BR");
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }

        static void AssertLegAlignment(Transform root, string legName)
        {
            var legRoot = root.Find("LocomotionRoot/" + legName);
            Assert.That(legRoot, Is.Not.Null);

            var upperVisual = legRoot!.Find("Hip/UpperVisual");
            var knee = upperVisual!.Find("Knee");
            var lowerVisual = knee!.Find("LowerVisual");
            var footTarget = legRoot.Find("FootTarget");
            var upperMesh = upperVisual.Find("UpperVisualMesh");
            var lowerMesh = lowerVisual!.Find("LowerVisualMesh");

            Assert.That(upperVisual, Is.Not.Null);
            Assert.That(knee, Is.Not.Null);
            Assert.That(lowerVisual, Is.Not.Null);
            Assert.That(footTarget, Is.Not.Null);
            Assert.That(upperMesh, Is.Not.Null);
            Assert.That(lowerMesh, Is.Not.Null);

            Assert.That(upperVisual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(lowerVisual.localPosition, Is.EqualTo(Vector3.zero));

            var upperMeshAsset = upperMesh!.GetComponent<MeshFilter>()!.sharedMesh!;
            var lowerMeshAsset = lowerMesh!.GetComponent<MeshFilter>()!.sharedMesh!;

            var upperTipWorld = upperVisual.TransformPoint(new Vector3(0f, upperMeshAsset.bounds.size.y * upperMesh.localScale.y, 0f));
            var lowerTipWorld = lowerVisual.TransformPoint(new Vector3(0f, lowerMeshAsset.bounds.size.y * lowerMesh.localScale.y, 0f));

            Assert.That(Vector3.Distance(upperTipWorld, knee.position), Is.LessThan(0.001f), legName + " knee pivot should sit on upper segment tip.");
            Assert.That(Vector3.Distance(lowerTipWorld, footTarget!.position), Is.LessThan(0.025f), legName + " lower segment tip should match planted foot target.");
        }
    }
}
