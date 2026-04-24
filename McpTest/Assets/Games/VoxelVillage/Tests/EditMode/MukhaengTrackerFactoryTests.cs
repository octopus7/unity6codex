#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class MukhaengTrackerFactoryTests
    {
        [Test]
        public void CreateTracker_BuildsExpectedHierarchyAndController()
        {
            var result = MukhaengTrackerFactory.CreateTracker("TrackerTest", new Vector3(2f, 0f, -3f), 1.1f);

            try
            {
                Assert.That(result.Root, Is.Not.Null);
                Assert.That(result.Controller, Is.Not.Null);
                Assert.AreEqual(new Vector3(2f, 0f, -3f), result.Root.transform.position);
                Assert.AreEqual(Vector3.one * 1.1f, result.Root.transform.localScale);

                Assert.That(result.Root.transform.Find("LocomotionRoot/BodyPivot/MantleRoot/MantleCoreVisual"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("LocomotionRoot/BodyPivot/LegRing/Leg_FL/Leg_FL_Hip/Leg_FL_UpperVisual/Leg_FL_Knee/Leg_FL_LowerVisual/Leg_FL_Ankle/Leg_FL_TipVisual/Leg_FL_FootTarget"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("LocomotionRoot/BodyPivot/AttackTentacles/Tentacle_Attack_L/Tentacle_Attack_L_Base/Tentacle_Attack_L_Mid/Tentacle_Attack_L_Tip/Tentacle_Attack_L_HitOrigin"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("Sensors/ThreatCenter"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("Gameplay/BodyBlocker"), Is.Not.Null);
                Assert.That(result.Root.transform.Find("FX/EyeGlow_L"), Is.Not.Null);

                var renderers = result.Root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(12));

                var colliders = result.Root.GetComponentsInChildren<Collider>();
                Assert.That(colliders.Length, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }

        [Test]
        public void CreateTracker_InitializesControllerThreatCenter()
        {
            var result = MukhaengTrackerFactory.CreateTracker("TrackerTest", Vector3.zero);

            try
            {
                Assert.AreEqual(MukhaengTrackerPoseState.Search, result.Controller.PoseState);
                Assert.That(result.Controller.ThreatCenter, Is.Not.Null);
                Assert.AreEqual("ThreatCenter", result.Controller.ThreatCenter.name);

                var targetObject = new GameObject("Target");
                try
                {
                    result.Controller.SetTarget(targetObject.transform);
                    Assert.AreSame(targetObject.transform, result.Controller.Target);
                }
                finally
                {
                    Object.DestroyImmediate(targetObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }

        [Test]
        public void BuildWorld_SpawnsSingleMukhaengTrackerTargetingPlayer()
        {
            var controllerObject = new GameObject("VoxelVillageGameController_MukhaengTest");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<VoxelVillageGameController>();

            try
            {
                var buildWorld = typeof(VoxelVillageGameController).GetMethod("BuildWorld", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildWorld, Is.Not.Null);

                buildWorld!.Invoke(controller, null);

                var trackers = controllerObject.GetComponentsInChildren<MukhaengTrackerController>(true);
                Assert.That(trackers.Length, Is.EqualTo(1));

                var player = controllerObject.transform.Find("VoxelVillageWorld/Player");
                Assert.That(player, Is.Not.Null);
                Assert.AreSame(player, trackers[0].Target);
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                DestroyIfPresent("Main Camera");
                DestroyIfPresent("VoxelVillage Light");
                DestroyIfPresent("EventSystem");
                DestroyIfPresent("VoxelVillageHud");
                DestroyIfPresent("VoxelVillage Reflection Probe");
            }
        }

        static void DestroyIfPresent(string objectName)
        {
            var instance = GameObject.Find(objectName);
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
