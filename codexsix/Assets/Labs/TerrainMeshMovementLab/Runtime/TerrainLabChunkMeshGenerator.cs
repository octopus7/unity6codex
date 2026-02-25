using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class TerrainLabChunkMeshGenerator : MonoBehaviour
    {
        [Header("Config")]
        public TerrainLabWorldConfig WorldConfig;

        [Header("Render")]
        public Color TerrainColor = new(0.36f, 0.7f, 0.38f, 1f);

        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _runtimeMaterial;

        private Vector2Int _currentChunkCoord;
        private int _currentSeed;
        private float[,] _interiorHeights;

        public Vector2Int CurrentChunkCoord => _currentChunkCoord;
        public int CurrentSeed => _currentSeed;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
            _meshRenderer = GetComponent<MeshRenderer>();
            EnsureMaterial();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    DestroyImmediate(_mesh);
                }
                else
                {
                    Destroy(_mesh);
                }
#else
                Destroy(_mesh);
#endif
                _mesh = null;
            }

            if (_runtimeMaterial != null)
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    DestroyImmediate(_runtimeMaterial);
                }
                else
                {
                    Destroy(_runtimeMaterial);
                }
#else
                Destroy(_runtimeMaterial);
#endif
                _runtimeMaterial = null;
            }
        }

        public bool BuildChunk(Vector2Int chunkCoord, int seed, bool forceRegenerate)
        {
            if (WorldConfig == null)
            {
                Debug.LogError("Terrain Lab: WorldConfig is missing on TerrainLabChunkMeshGenerator.");
                return false;
            }

            WorldConfig.ValidateInPlace();

            var data = TerrainLabHeightChunkStore.LoadOrCreateChunk(
                WorldConfig,
                seed,
                chunkCoord,
                forceRegenerate,
                autoGenerateMissing: true);

            if (data == null)
            {
                Debug.LogError($"Terrain Lab: Failed to load or create chunk height data {chunkCoord}.");
                return false;
            }

            BuildMeshFromHeightData(chunkCoord, seed, data);
            return true;
        }

        public bool TrySampleHeightAtWorldPosition(Vector3 worldPosition, out float height)
        {
            height = 0f;
            if (WorldConfig == null || _interiorHeights == null)
            {
                return false;
            }

            var local = transform.InverseTransformPoint(worldPosition);
            var gx = local.x / WorldConfig.CellSize;
            var gz = local.z / WorldConfig.CellSize;

            if (gx < 0f || gz < 0f || gx > WorldConfig.ChunkCells || gz > WorldConfig.ChunkCells)
            {
                return false;
            }

            var x0 = Mathf.FloorToInt(gx);
            var z0 = Mathf.FloorToInt(gz);
            var x1 = Mathf.Min(x0 + 1, WorldConfig.ChunkCells);
            var z1 = Mathf.Min(z0 + 1, WorldConfig.ChunkCells);

            var tx = gx - x0;
            var tz = gz - z0;

            var h00 = _interiorHeights[x0, z0];
            var h10 = _interiorHeights[x1, z0];
            var h01 = _interiorHeights[x0, z1];
            var h11 = _interiorHeights[x1, z1];

            var hx0 = Mathf.Lerp(h00, h10, tx);
            var hx1 = Mathf.Lerp(h01, h11, tx);
            height = Mathf.Lerp(hx0, hx1, tz);
            return true;
        }

        private void BuildMeshFromHeightData(Vector2Int chunkCoord, int seed, TerrainLabChunkHeightData data)
        {
            var chunkVertices = WorldConfig.ChunkVertices;
            var chunkCells = WorldConfig.ChunkCells;
            var cellSize = WorldConfig.CellSize;

            var vertexCount = chunkVertices * chunkVertices;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[chunkCells * chunkCells * 6];

            var heights = data.Heights;
            _interiorHeights = new float[chunkVertices, chunkVertices];

            var index = 0;
            for (var z = 0; z < chunkVertices; z++)
            {
                for (var x = 0; x < chunkVertices; x++)
                {
                    var h = heights[x + 1, z + 1];
                    _interiorHeights[x, z] = h;

                    vertices[index] = new Vector3(x * cellSize, h, z * cellSize);
                    normals[index] = CalculateNormalFromPadded(heights, x + 1, z + 1, cellSize);
                    uvs[index] = new Vector2(x / (float)chunkCells, z / (float)chunkCells);
                    index++;
                }
            }

            var triIndex = 0;
            for (var z = 0; z < chunkCells; z++)
            {
                for (var x = 0; x < chunkCells; x++)
                {
                    var v0 = (z * chunkVertices) + x;
                    var v1 = v0 + 1;
                    var v2 = v0 + chunkVertices;
                    var v3 = v2 + 1;

                    triangles[triIndex++] = v0;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v1;

                    triangles[triIndex++] = v1;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v3;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "TerrainLabChunkMesh"
                };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            else
            {
                _mesh.Clear();
            }

            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _mesh;

            transform.position = TerrainLabHeightSampler.GetChunkOriginWorld(WorldConfig, chunkCoord);

            _currentChunkCoord = chunkCoord;
            _currentSeed = seed;

            EnsureMaterial();
        }

        private static Vector3 CalculateNormalFromPadded(float[,] paddedHeights, int x, int z, float cellSize)
        {
            var left = paddedHeights[x - 1, z];
            var right = paddedHeights[x + 1, z];
            var down = paddedHeights[x, z - 1];
            var up = paddedHeights[x, z + 1];

            var dx = (right - left) / (2f * cellSize);
            var dz = (up - down) / (2f * cellSize);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        private void EnsureMaterial()
        {
            if (_meshRenderer == null)
            {
                return;
            }

            if (_meshRenderer.sharedMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return;
            }

            _runtimeMaterial = new Material(shader)
            {
                color = TerrainColor,
                name = "TerrainLabRuntimeMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };

            _meshRenderer.sharedMaterial = _runtimeMaterial;
        }
    }
}
