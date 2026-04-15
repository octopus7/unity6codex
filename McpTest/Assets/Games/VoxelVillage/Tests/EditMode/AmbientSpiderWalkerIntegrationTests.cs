#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class AmbientSpiderWalkerIntegrationTests
    {
        [Test]
        public void BuildWorld_SpawnsAmbientSpiderWalkerAwayFromPlayerStart()
        {
            var controllerObject = new GameObject("VoxelVillageGameController_Test");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<VoxelVillageGameController>();

            try
            {
                var buildWorld = typeof(VoxelVillageGameController).GetMethod("BuildWorld", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildWorld, Is.Not.Null);

                buildWorld!.Invoke(controller, null);

                var spiders = controllerObject.GetComponentsInChildren<AmbientSpiderWalkerController>(true);
                Assert.That(spiders.Length, Is.EqualTo(1));

                var player = controllerObject.transform.Find("VoxelVillageWorld/Player");
                Assert.That(player, Is.Not.Null);
                Assert.That(Vector3.Distance(spiders[0].transform.position, player!.position), Is.GreaterThan(6f));
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

        [Test]
        public void ForwardStrideBias_LeadsLeadGroupAndTrailsSupportingGroup()
        {
            var controllerObject = new GameObject("AmbientSpiderWalkerController_Test");
            var locomotionRoot = new GameObject("LocomotionRoot").transform;
            locomotionRoot.SetParent(controllerObject.transform, false);
            var controller = controllerObject.AddComponent<AmbientSpiderWalkerController>();

            try
            {
                var activeLeg = CreateLegState(locomotionRoot, "ActiveLeg", 0);
                var supportLeg = CreateLegState(locomotionRoot, "SupportLeg", 1);

                var leadGroupField = typeof(AmbientSpiderWalkerController).GetField("_leadGaitGroup", BindingFlags.Instance | BindingFlags.NonPublic);
                var strideBiasMethod = typeof(AmbientSpiderWalkerController).GetMethod("GetForwardStrideBias", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(leadGroupField, Is.Not.Null);
                Assert.That(strideBiasMethod, Is.Not.Null);

                leadGroupField!.SetValue(controller, 0);

                var activeBias = (float)strideBiasMethod!.Invoke(controller, new object[] { activeLeg })!;
                var supportBias = (float)strideBiasMethod.Invoke(controller, new object[] { supportLeg })!;

                Assert.That(activeBias, Is.GreaterThan(0f));
                Assert.That(supportBias, Is.LessThan(0f));
                Assert.That(Mathf.Abs(activeBias), Is.EqualTo(Mathf.Abs(supportBias)).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void StartGroupStep_MakesSteppingGroupTheNewLeadGroupAndSchedulesOppositeGroupNext()
        {
            var controllerObject = new GameObject("AmbientSpiderWalkerController_Test");
            var locomotionRoot = new GameObject("LocomotionRoot").transform;
            locomotionRoot.SetParent(controllerObject.transform, false);
            var controller = controllerObject.AddComponent<AmbientSpiderWalkerController>();

            try
            {
                var leadLeg = CreateLegState(locomotionRoot, "LeadLeg", 0);
                var trailingLeg = CreateLegState(locomotionRoot, "TrailingLeg", 1);

                var leadGroupField = typeof(AmbientSpiderWalkerController).GetField("_leadGaitGroup", BindingFlags.Instance | BindingFlags.NonPublic);
                var nextStepGroupField = typeof(AmbientSpiderWalkerController).GetField("_nextStepGroup", BindingFlags.Instance | BindingFlags.NonPublic);
                var legsField = typeof(AmbientSpiderWalkerController).GetField("_legs", BindingFlags.Instance | BindingFlags.NonPublic);
                var locomotionRootField = typeof(AmbientSpiderWalkerController).GetField("_locomotionRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                var startGroupStepMethod = typeof(AmbientSpiderWalkerController).GetMethod("StartGroupStep", BindingFlags.Instance | BindingFlags.NonPublic);
                var strideBiasMethod = typeof(AmbientSpiderWalkerController).GetMethod("GetForwardStrideBias", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(leadGroupField, Is.Not.Null);
                Assert.That(nextStepGroupField, Is.Not.Null);
                Assert.That(legsField, Is.Not.Null);
                Assert.That(locomotionRootField, Is.Not.Null);
                Assert.That(startGroupStepMethod, Is.Not.Null);
                Assert.That(strideBiasMethod, Is.Not.Null);

                leadGroupField!.SetValue(controller, 0);
                nextStepGroupField!.SetValue(controller, 1);
                legsField!.SetValue(controller, new[] { leadLeg, trailingLeg });
                locomotionRootField!.SetValue(controller, locomotionRoot);

                startGroupStepMethod!.Invoke(controller, new object[] { 1 });

                var scheduledNextGroup = (int)nextStepGroupField.GetValue(controller)!;
                var formerLeadBias = (float)strideBiasMethod!.Invoke(controller, new object[] { leadLeg })!;
                var newLeadBias = (float)strideBiasMethod.Invoke(controller, new object[] { trailingLeg })!;

                Assert.That(scheduledNextGroup, Is.EqualTo(0));
                Assert.That(formerLeadBias, Is.LessThan(0f));
                Assert.That(newLeadBias, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void ComputeDesiredFootPosition_UsesBodyForwardAxisForStride()
        {
            var controllerObject = new GameObject("AmbientSpiderWalkerController_Test");
            var locomotionRoot = new GameObject("LocomotionRoot").transform;
            locomotionRoot.SetParent(controllerObject.transform, false);
            var controller = controllerObject.AddComponent<AmbientSpiderWalkerController>();

            try
            {
                var leg = CreateLegState(locomotionRoot, "StrideLeg", 0);

                var locomotionRootField = typeof(AmbientSpiderWalkerController).GetField("_locomotionRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                var currentVelocityField = typeof(AmbientSpiderWalkerController).GetField("_currentVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
                var currentWidthScaleField = typeof(AmbientSpiderWalkerController).GetField("_currentWidthScale", BindingFlags.Instance | BindingFlags.NonPublic);
                var leadGroupField = typeof(AmbientSpiderWalkerController).GetField("_leadGaitGroup", BindingFlags.Instance | BindingFlags.NonPublic);
                var desiredFootMethod = typeof(AmbientSpiderWalkerController).GetMethod("ComputeDesiredFootPosition", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(locomotionRootField, Is.Not.Null);
                Assert.That(currentVelocityField, Is.Not.Null);
                Assert.That(currentWidthScaleField, Is.Not.Null);
                Assert.That(leadGroupField, Is.Not.Null);
                Assert.That(desiredFootMethod, Is.Not.Null);

                locomotionRootField!.SetValue(controller, locomotionRoot);
                currentVelocityField!.SetValue(controller, Vector3.right * 2f);
                currentWidthScaleField!.SetValue(controller, 1f);
                leadGroupField!.SetValue(controller, 0);

                var desired = (Vector3)desiredFootMethod!.Invoke(controller, new object[] { leg })!;

                Assert.That(desired.x, Is.EqualTo(leg.RestLocalTarget.x).Within(0.0001f));
                Assert.That(desired.z, Is.EqualTo(leg.RestLocalTarget.z + 1.44f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void KneeHints_PointOutwardAndUpFromEachHip()
        {
            var build = VoxelSpiderWalkerFactory.CreateInstance("SpiderWalker_TestRig", Vector3.zero);
            var controller = build.Controller;

            try
            {
                var legsField = typeof(AmbientSpiderWalkerController).GetField("_legs", BindingFlags.Instance | BindingFlags.NonPublic);
                var locomotionRootField = typeof(AmbientSpiderWalkerController).GetField("_locomotionRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                var hintMethod = typeof(AmbientSpiderWalkerController).GetMethod("ComputeKneeHintWorldPosition", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(legsField, Is.Not.Null);
                Assert.That(locomotionRootField, Is.Not.Null);
                Assert.That(hintMethod, Is.Not.Null);

                var legs = (SpiderLegState[])legsField!.GetValue(controller)!;
                var locomotionRoot = (Transform)locomotionRootField!.GetValue(controller)!;

                foreach (var leg in legs)
                {
                    var hint = (Vector3)hintMethod!.Invoke(controller, new object[] { leg })!;
                    var hintOffset = hint - leg.Hip.position;
                    var outward = Vector3.Dot(hintOffset, locomotionRoot.right * leg.SideSign);
                    var upward = Vector3.Dot(hintOffset, locomotionRoot.up);

                    Assert.That(outward, Is.GreaterThan(0.05f), leg.Id + " hint should stay outside the body.");
                    Assert.That(upward, Is.GreaterThan(0.05f), leg.Id + " hint should stay above the hip.");
                }
            }
            finally
            {
                Object.DestroyImmediate(build.Root);
            }
        }

        [Test]
        public void RestTargets_StayOutsideFrontBackHipRows()
        {
            var build = VoxelSpiderWalkerFactory.CreateInstance("SpiderWalker_TestRig", Vector3.zero);
            var controller = build.Controller;

            try
            {
                var legsField = typeof(AmbientSpiderWalkerController).GetField("_legs", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(legsField, Is.Not.Null);

                var legs = (SpiderLegState[])legsField!.GetValue(controller)!;
                foreach (var leg in legs)
                {
                    var signedFootOffset = (leg.RestLocalTarget.z - leg.LegRootBaseLocalPosition.z) * leg.ForeSign;
                    var signedHintOffset = (leg.KneeHintLocal.z - leg.LegRootBaseLocalPosition.z) * leg.ForeSign;

                    Assert.That(signedFootOffset, Is.GreaterThan(0.3399f), leg.Id + " foot target should stay outside its hip row.");
                    Assert.That(signedHintOffset, Is.GreaterThan(0.2399f), leg.Id + " knee hint should stay outside its hip row.");
                }
            }
            finally
            {
                Object.DestroyImmediate(build.Root);
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

        static SpiderLegState CreateLegState(Transform parent, string name, int gaitGroup)
        {
            var legRoot = new GameObject(name).transform;
            legRoot.SetParent(parent, false);

            var hip = new GameObject("Hip").transform;
            hip.SetParent(legRoot, false);

            var knee = new GameObject("Knee").transform;
            knee.SetParent(hip, false);

            var footTarget = new GameObject("FootTarget").transform;
            footTarget.SetParent(legRoot, false);

            return new SpiderLegState(
                name,
                legRoot,
                hip,
                knee,
                footTarget,
                new Vector3(1f, -1f, 0f),
                new Vector3(0.6f, 1.2f, 0f),
                1f,
                1f,
                gaitGroup,
                1f,
                1f);
        }
    }
}
