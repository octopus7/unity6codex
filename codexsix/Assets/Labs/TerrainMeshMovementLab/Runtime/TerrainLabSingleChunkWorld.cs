using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    public sealed class TerrainLabSingleChunkWorld : MonoBehaviour
    {
        [Header("World")]
        public TerrainLabWorldConfig WorldConfig;
        public bool RandomizeSeedOnRegenerate;
        public int FixedSeed = 12345;

        [Header("Chunk Generation")]
        [Min(3)] public int GeneratedGridSize = 3;
        public bool FollowPlayerChunk = true;
        [Min(0.05f)] public float ChunkCenterCheckIntervalSeconds = 0.2f;

        [Header("References")]
        public TerrainLabChunkMeshGenerator ChunkMeshGenerator;
        public Transform PlayerRoot;
        public CharacterController PlayerCharacterController;
        public TerrainLabMinimapController MinimapController;
        public TerrainLabPerformanceGraph PerformanceGraph;

        [Header("Player Spawn")]
        public float PlayerHeightOffset = 0.25f;

        [Header("Runtime")]
        [SerializeField] private int _currentSeed = 12345;

        private readonly List<TerrainLabChunkMeshGenerator> _spawnedChunkClones = new();
        private Vector2Int _activeCenterChunkCoord = new(int.MinValue, int.MinValue);
        private float _nextCenterChunkCheckAt;

        public int CurrentSeed => _currentSeed;

        private void Awake()
        {
            if (WorldConfig != null)
            {
                WorldConfig.ValidateInPlace();
            }
            NormalizeGeneratedGridSize();

            if (ChunkMeshGenerator == null)
            {
                ChunkMeshGenerator = FindFirstObjectByType<TerrainLabChunkMeshGenerator>();
            }

            if (MinimapController == null)
            {
                MinimapController = FindFirstObjectByType<TerrainLabMinimapController>();
            }

            if (PerformanceGraph == null)
            {
                PerformanceGraph = GetComponent<TerrainLabPerformanceGraph>();
                if (PerformanceGraph == null)
                {
                    PerformanceGraph = FindFirstObjectByType<TerrainLabPerformanceGraph>();
                }
            }

            if (PerformanceGraph == null)
            {
                PerformanceGraph = gameObject.AddComponent<TerrainLabPerformanceGraph>();
                PerformanceGraph.ScreenRect = new Rect(16f, 270f, 320f, 130f);
            }

            if (PlayerRoot == null)
            {
                var playerController = FindFirstObjectByType<TerrainLabCharacterController>();
                if (playerController != null)
                {
                    PlayerRoot = playerController.transform;
                    if (PlayerCharacterController == null)
                    {
                        PlayerCharacterController = playerController.GetComponent<CharacterController>();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DestroySpawnedChunkClones();
        }

        private void Start()
        {
            RegenerateWorld(forceRegenerate: false, preserveSeed: true);
        }

        private void Update()
        {
            TickPlayerCenteredChunkGrid();
        }

        public void RegenerateWorld(bool forceRegenerate, bool preserveSeed = false)
        {
            if (WorldConfig == null || ChunkMeshGenerator == null)
            {
                Debug.LogError("Terrain Lab: Missing WorldConfig or ChunkMeshGenerator.");
                return;
            }

            WorldConfig.ValidateInPlace();

            if (!preserveSeed)
            {
                if (RandomizeSeedOnRegenerate)
                {
                    _currentSeed = Random.Range(0, int.MaxValue);
                }
                else
                {
                    _currentSeed = FixedSeed;
                }
            }
            else
            {
                _currentSeed = FixedSeed;
            }

            NormalizeGeneratedGridSize();
            _activeCenterChunkCoord = ResolveCenterChunkCoord();
            BuildChunkGrid(forceRegenerate, _activeCenterChunkCoord);
            SnapPlayerToGround();

            if (MinimapController != null)
            {
                MinimapController.BindWorld(this);
                MinimapController.RequestImmediateRefresh();
            }

            _nextCenterChunkCheckAt = Time.time + Mathf.Max(0.05f, ChunkCenterCheckIntervalSeconds);
        }

        public void ForceRegenerateFromInput()
        {
            RegenerateWorld(forceRegenerate: true, preserveSeed: false);
        }

        public bool TrySampleHeightAtWorld(Vector3 worldPosition, out float height)
        {
            if (WorldConfig == null)
            {
                height = 0f;
                return false;
            }

            return TerrainLabHeightSampler.TrySampleHeight(
                WorldConfig,
                _currentSeed,
                new Vector2(worldPosition.x, worldPosition.z),
                autoGenerateMissing: true,
                out height);
        }

        public TerrainLabChunkHeightData GetOrCreateChunkData(Vector2Int chunkCoord)
        {
            if (WorldConfig == null)
            {
                return null;
            }

            return TerrainLabHeightChunkStore.LoadOrCreateChunk(
                WorldConfig,
                _currentSeed,
                chunkCoord,
                forceRegenerate: false,
                autoGenerateMissing: true);
        }

        private void BuildChunkGrid(bool forceRegenerate, Vector2Int centerChunkCoord)
        {
            ChunkMeshGenerator.WorldConfig = WorldConfig;

            DestroySpawnedChunkClones();

            var targetCoords = BuildGridCoordsAroundCenter(centerChunkCoord, GeneratedGridSize);
            for (var index = 0; index < targetCoords.Count; index++)
            {
                var coord = targetCoords[index];
                var generator = index == 0 ? ChunkMeshGenerator : CreateChunkClone(index);
                generator.WorldConfig = WorldConfig;

                if (!generator.BuildChunk(coord, _currentSeed, forceRegenerate))
                {
                    Debug.LogError($"Terrain Lab: failed to build chunk {coord}.");
                }
            }
        }

        private void TickPlayerCenteredChunkGrid()
        {
            if (!FollowPlayerChunk || PlayerRoot == null || WorldConfig == null || ChunkMeshGenerator == null)
            {
                return;
            }

            if (Time.time < _nextCenterChunkCheckAt)
            {
                return;
            }

            _nextCenterChunkCheckAt = Time.time + Mathf.Max(0.05f, ChunkCenterCheckIntervalSeconds);
            var playerCenterChunk = ResolveCenterChunkCoord();
            if (playerCenterChunk == _activeCenterChunkCoord)
            {
                return;
            }

            _activeCenterChunkCoord = playerCenterChunk;
            BuildChunkGrid(forceRegenerate: false, _activeCenterChunkCoord);
            if (MinimapController != null)
            {
                MinimapController.RequestImmediateRefresh();
            }
        }

        private Vector2Int ResolveCenterChunkCoord()
        {
            if (!FollowPlayerChunk || PlayerRoot == null || WorldConfig == null)
            {
                return Vector2Int.zero;
            }

            return TerrainLabHeightSampler.GetChunkCoordFromWorld(
                WorldConfig,
                new Vector2(PlayerRoot.position.x, PlayerRoot.position.z));
        }

        private void NormalizeGeneratedGridSize()
        {
            GeneratedGridSize = Mathf.Max(3, GeneratedGridSize);
            if ((GeneratedGridSize & 1) == 0)
            {
                GeneratedGridSize += 1;
            }
        }

        private List<Vector2Int> BuildGridCoordsAroundCenter(Vector2Int center, int gridSize)
        {
            var result = new List<Vector2Int>();

            var clamped = Mathf.Max(3, gridSize);
            if ((clamped & 1) == 0)
            {
                clamped += 1;
            }

            var radius = clamped / 2;
            result.Add(center);

            for (var z = -radius; z <= radius; z++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x == 0 && z == 0)
                    {
                        continue;
                    }

                    result.Add(new Vector2Int(center.x + x, center.y + z));
                }
            }

            return result;
        }

        private TerrainLabChunkMeshGenerator CreateChunkClone(int index)
        {
            var parent = ChunkMeshGenerator.transform.parent;
            var clone = Instantiate(ChunkMeshGenerator, parent);
            clone.name = $"Terrain_Chunk_{index:00}";
            _spawnedChunkClones.Add(clone);
            return clone;
        }

        private void DestroySpawnedChunkClones()
        {
            if (_spawnedChunkClones.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _spawnedChunkClones.Count; i++)
            {
                var clone = _spawnedChunkClones[i];
                if (clone == null)
                {
                    continue;
                }

#if UNITY_EDITOR
                if (Application.isEditor && !Application.isPlaying)
                {
                    DestroyImmediate(clone.gameObject);
                }
                else
                {
                    Destroy(clone.gameObject);
                }
#else
                Destroy(clone.gameObject);
#endif
            }

            _spawnedChunkClones.Clear();
        }

        private void SnapPlayerToGround()
        {
            if (PlayerRoot == null)
            {
                return;
            }

            if (!TrySampleHeightAtWorld(PlayerRoot.position, out var sampledHeight))
            {
                return;
            }

            var finalHeight = sampledHeight + PlayerHeightOffset;
            if (PlayerCharacterController != null)
            {
                finalHeight += PlayerCharacterController.height * 0.5f;
                PlayerCharacterController.enabled = false;
                var position = PlayerRoot.position;
                position.y = finalHeight;
                PlayerRoot.position = position;
                PlayerCharacterController.enabled = true;
                return;
            }

            var fallback = PlayerRoot.position;
            fallback.y = finalHeight;
            PlayerRoot.position = fallback;
        }
    }
}
