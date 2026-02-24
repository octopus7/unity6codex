using UnityEngine;
using UnityEngine.Rendering;

namespace CodexSix.TopdownShooter.Game
{
    public static class GemVisualFactory
    {
        public static Mesh CreateOctahedronMesh(float radius = 0.5f)
        {
            var clampedRadius = Mathf.Max(0.01f, radius);
            var mesh = new Mesh
            {
                name = "Runtime_GemOctahedron"
            };

            var vertices = new[]
            {
                new Vector3(0f, clampedRadius, 0f),
                new Vector3(clampedRadius, 0f, 0f),
                new Vector3(0f, 0f, clampedRadius),
                new Vector3(-clampedRadius, 0f, 0f),
                new Vector3(0f, 0f, -clampedRadius),
                new Vector3(0f, -clampedRadius, 0f)
            };

            var triangles = new[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                5, 2, 1,
                5, 3, 2,
                5, 4, 3,
                5, 1, 4
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material CreateGemMaterial()
        {
            var shader = Shader.Find("Standard")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                         ?? Shader.Find("Legacy Shaders/Diffuse");

            var material = new Material(shader)
            {
                name = "Runtime_GemMaterial"
            };

            ApplyGemMaterialProperties(material);
            return material;
        }

        public static void ApplyGemMaterialProperties(Material material)
        {
            if (material == null)
            {
                return;
            }

            var baseColor = new Color(0.62f, 0.94f, 1f, 0.42f);

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.95f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.94f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.94f);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.11f, 0.26f, 0.34f, 1f));
            }

            ConfigureTransparency(material);
        }

        private static void ConfigureTransparency(Material material)
        {
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
