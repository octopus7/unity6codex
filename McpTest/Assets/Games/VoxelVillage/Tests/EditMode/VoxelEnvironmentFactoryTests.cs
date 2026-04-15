#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class VoxelEnvironmentFactoryTests
    {
        [Test]
        public void CreateHouse_BuildsOpaqueAndTransparentRevealMaterials()
        {
            var result = VoxelEnvironmentFactory.CreateHouse(
                "HouseFactoryTest",
                new Vector3(2f, 2.4f, -1f),
                new Vector3(5f, 4.8f, 5f),
                0f,
                new Color(0.82f, 0.74f, 0.61f),
                new Color(0.48f, 0.27f, 0.24f),
                new Color(0.35f, 0.28f, 0.21f));

            try
            {
                var meshRenderers = result.Root.GetComponentsInChildren<MeshRenderer>();

                Assert.That(meshRenderers.Length, Is.EqualTo(2));

                var opaqueShader = Shader.Find(VoxelEnvironmentFactory.HouseRevealShaderName);
                var transparentShader = Shader.Find(VoxelEnvironmentFactory.HouseRevealTransparentShaderName);
                var foundOpaqueRenderer = false;
                var foundTransparentRenderer = false;

                for (var rendererIndex = 0; rendererIndex < meshRenderers.Length; rendererIndex++)
                {
                    var renderer = meshRenderers[rendererIndex];
                    Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(6));

                    if (opaqueShader != null)
                    {
                        var matchesOpaqueShader = true;
                        for (var index = 0; index < renderer.sharedMaterials.Length; index++)
                        {
                            if (renderer.sharedMaterials[index].shader != opaqueShader)
                            {
                                matchesOpaqueShader = false;
                                break;
                            }
                        }

                        foundOpaqueRenderer |= matchesOpaqueShader;
                    }

                    if (transparentShader != null)
                    {
                        var matchesTransparentShader = true;
                        for (var index = 0; index < renderer.sharedMaterials.Length; index++)
                        {
                            if (renderer.sharedMaterials[index].shader != transparentShader)
                            {
                                matchesTransparentShader = false;
                                break;
                            }
                        }

                        foundTransparentRenderer |= matchesTransparentShader;
                    }
                }

                if (opaqueShader != null)
                {
                    Assert.That(foundOpaqueRenderer, Is.True);
                }

                if (transparentShader != null)
                {
                    Assert.That(foundTransparentRenderer, Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(result.Root);
            }
        }

        [Test]
        public void CreateFence_BuildsMeshBackedFenceCell()
        {
            var result = VoxelEnvironmentFactory.CreateFence(
                "FenceFactoryTest",
                new Vector3(1f, 0.85f, 3f),
                new Vector3(2f, 1.7f, 2f),
                true,
                true,
                false,
                false,
                new Color(0.58f, 0.37f, 0.19f));

            try
            {
                Assert.That(result.Root, Is.Not.Null);
                Assert.AreEqual(new Vector3(1f, 0.85f, 3f), result.Root.transform.position);

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
