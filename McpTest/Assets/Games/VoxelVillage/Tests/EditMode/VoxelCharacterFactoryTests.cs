#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class VoxelCharacterFactoryTests
    {
        [Test]
        public void CreateCharacter_BuildsMeshBackedCharacter()
        {
            var result = VoxelCharacterFactory.CreateCharacter(
                "FactoryTest",
                new Vector3(1f, 2f, 3f),
                new Color(0.2f, 0.4f, 0.8f),
                VoxelCharacterAccessoryType.CourierPack,
                false,
                1.05f);

            try
            {
                Assert.That(result.Root, Is.Not.Null);
                Assert.That(result.HeadOffset, Is.GreaterThan(0.5f));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), result.Root.transform.position);

                var meshFilter = result.Root.GetComponentInChildren<MeshFilter>();
                var meshRenderer = result.Root.GetComponentInChildren<MeshRenderer>();

                Assert.That(meshFilter, Is.Not.Null);
                Assert.That(meshRenderer, Is.Not.Null);
                Assert.That(meshFilter!.sharedMesh, Is.Not.Null);
                Assert.That(meshFilter.sharedMesh.vertexCount, Is.GreaterThan(0));
                Assert.That(meshRenderer!.sharedMaterials.Length, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }
    }
}
