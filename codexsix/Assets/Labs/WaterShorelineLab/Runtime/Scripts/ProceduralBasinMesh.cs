using UnityEngine;

namespace Labs.WaterShorelineLab
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ProceduralBasinMesh : MonoBehaviour
    {
        private const string GeneratedMeshName = "WaterShorelineLab_BasinMesh";

        [SerializeField, Range(16, 240)] private int resolution = 80;
        [SerializeField, Min(1f)] private float width = 24f;
        [SerializeField, Min(1f)] private float length = 24f;
        [SerializeField, Min(0.05f)] private float maxDepth = 1.1f;
        [SerializeField, Range(0f, 0.9f)] private float flatRadius = 0.22f;
        [SerializeField, Min(0.1f)] private float slopePower = 1.7f;

        private Mesh generatedMesh;

        private void OnEnable()
        {
            RebuildMesh();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildMesh();
        }
#endif

        private void OnDestroy()
        {
            if (generatedMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }
        }

        private void RebuildMesh()
        {
            resolution = Mathf.Clamp(resolution, 16, 240);
            width = Mathf.Max(width, 1f);
            length = Mathf.Max(length, 1f);
            maxDepth = Mathf.Max(maxDepth, 0.05f);
            flatRadius = Mathf.Clamp01(flatRadius);
            slopePower = Mathf.Max(slopePower, 0.1f);

            var meshFilter = GetComponent<MeshFilter>();
            EnsureMesh(meshFilter);

            int vertsPerAxis = resolution + 1;
            var vertices = new Vector3[vertsPerAxis * vertsPerAxis];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[resolution * resolution * 6];

            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;
            int vertexIndex = 0;

            for (int z = 0; z <= resolution; z++)
            {
                float v = z / (float)resolution;
                float pz = (v - 0.5f) * length;

                for (int x = 0; x <= resolution; x++)
                {
                    float u = x / (float)resolution;
                    float px = (u - 0.5f) * width;

                    float nx = px / halfWidth;
                    float nz = pz / halfLength;
                    float radial = Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz));
                    float normalized = Mathf.InverseLerp(flatRadius, 1f, radial);
                    float slopeT = Mathf.Pow(normalized, slopePower);
                    float smoothSlope = slopeT * slopeT * (3f - 2f * slopeT);
                    float basinDepth = 1f - smoothSlope;
                    float py = -maxDepth * basinDepth;

                    vertices[vertexIndex] = new Vector3(px, py, pz);
                    uvs[vertexIndex] = new Vector2(u, v);
                    vertexIndex++;
                }
            }

            int triIndex = 0;
            for (int z = 0; z < resolution; z++)
            {
                int rowStart = z * vertsPerAxis;
                int nextRowStart = (z + 1) * vertsPerAxis;

                for (int x = 0; x < resolution; x++)
                {
                    int a = rowStart + x;
                    int b = a + 1;
                    int c = nextRowStart + x;
                    int d = c + 1;

                    triangles[triIndex++] = a;
                    triangles[triIndex++] = c;
                    triangles[triIndex++] = b;
                    triangles[triIndex++] = b;
                    triangles[triIndex++] = c;
                    triangles[triIndex++] = d;
                }
            }

            generatedMesh.Clear();
            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();

            meshFilter.sharedMesh = generatedMesh;
        }

        private void EnsureMesh(MeshFilter meshFilter)
        {
            if (generatedMesh == null)
            {
                generatedMesh = meshFilter.sharedMesh;
            }

            if (generatedMesh == null || generatedMesh.name != GeneratedMeshName)
            {
                generatedMesh = new Mesh
                {
                    name = GeneratedMeshName
                };
            }
        }
    }
}
