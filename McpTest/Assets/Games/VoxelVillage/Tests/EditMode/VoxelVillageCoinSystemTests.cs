#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class VoxelVillageCoinSystemTests
    {
        [Test]
        public void FormatCoinCount_UsesXPrefixAndClampsNegativeValues()
        {
            var method = typeof(VoxelVillageGameController).GetMethod("FormatCoinCount", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Assert.AreEqual("x 0", method!.Invoke(null, new object[] { -3 }));
            Assert.AreEqual("x 7", method.Invoke(null, new object[] { 7 }));
        }

        [Test]
        public void CoinPrefab_LoadsFromResourcesWithPickupSetup()
        {
            var prefab = Resources.Load<GameObject>("VoxelVillage/Pickups/VV_Coin");
            Assert.That(prefab, Is.Not.Null);

            var pickup = prefab!.GetComponent<VoxelVillageCoinPickup>();
            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup!.PickupRadius, Is.GreaterThan(0.5f));

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(1));

            var collider = prefab.GetComponent<SphereCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider!.isTrigger, Is.True);
        }

        [Test]
        public void BuildWorld_SpawnsCoinPickupsAwayFromPlayerStart()
        {
            var controllerObject = new GameObject("VoxelVillageGameController_CoinTest");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<VoxelVillageGameController>();
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var buildWorld = typeof(VoxelVillageGameController).GetMethod("BuildWorld", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildWorld, Is.Not.Null);

                buildWorld!.Invoke(controller, null);

                var coins = controllerObject.GetComponentsInChildren<VoxelVillageCoinPickup>(true);
                Assert.That(coins.Length, Is.EqualTo(12));

                var player = controllerObject.transform.Find("VoxelVillageWorld/Player");
                Assert.That(player, Is.Not.Null);

                var closestCoinDistance = float.MaxValue;
                for (var index = 0; index < coins.Length; index++)
                {
                    closestCoinDistance = Mathf.Min(closestCoinDistance, Vector3.Distance(coins[index].transform.position, player!.position));
                }

                Assert.That(closestCoinDistance, Is.GreaterThan(2f));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
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
