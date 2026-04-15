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
