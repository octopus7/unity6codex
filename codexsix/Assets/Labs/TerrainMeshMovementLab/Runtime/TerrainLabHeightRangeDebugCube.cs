using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainLabHeightRangeDebugCube : MonoBehaviour
    {
        private const string DebugShaderName = "TerrainLab/DebugVertexColorUnlit";
        private const float StaticAnchorX = 5f;
        private const float StaticAnchorZ = 0f;
        private static Mesh _cachedBuiltInCubeMesh;

        [Header("Bindings")]
        public TerrainLabWorldConfig WorldConfig;
        public Transform PlayerTarget;

        [Header("Placement")]
        public bool FollowPlayer = false;
        public Vector3 PlayerOffset = new(5f, 0f, 0f);
        [Min(0.1f)] public float CubeSizeMeters = 1f;

        [Header("Runtime")]
        [SerializeField] private Color _minHeightColor = Color.black;
        [SerializeField] private Color _maxHeightColor = Color.white;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _runtimeMesh;
        private Material _runtimeMaterial;
        private float _lastBottomSampleHeight = float.NaN;
        private float _lastTopSampleHeight = float.NaN;
        private float _lastCubeSize = float.NaN;
        private float _lastHeightSpan = float.NaN;

        public void BindWorldConfig(TerrainLabWorldConfig config)
        {
            WorldConfig = config;
        }

        public void BindPlayer(Transform playerTarget)
        {
            PlayerTarget = playerTarget;
        }

        public void RequestImmediateRefresh()
        {
            RefreshVisuals(force: true);
        }

        private void Awake()
        {
            CacheComponents();
            RefreshVisuals(force: true);
        }

        private void OnEnable()
        {
            CacheComponents();
            RefreshVisuals(force: true);
        }

        private void OnValidate()
        {
            CacheComponents();
            RefreshVisuals(force: true);
        }

        private void LateUpdate()
        {
            RefreshVisuals(force: false);
        }

        private void OnDestroy()
        {
            ReleaseRuntimeMaterial();
            ReleaseRuntimeMesh();
        }

        private void CacheComponents()
        {
            _meshFilter ??= GetComponent<MeshFilter>();
            _meshRenderer ??= GetComponent<MeshRenderer>();
        }

        private void RefreshVisuals(bool force)
        {
            if (_meshFilter == null || _meshRenderer == null)
            {
                return;
            }

            EnsureRuntimeMesh();
            EnsureRuntimeMaterial();

            float bottomSampleHeight = float.NaN;
            float topSampleHeight = float.NaN;
            var hasHeightRange = false;
            if (WorldConfig != null)
            {
                WorldConfig.ValidateInPlace();
                bottomSampleHeight = WorldConfig.HeightMin;
                topSampleHeight = TerrainLabTerrainMeshColorRamp.EvaluateHighGreenPeakHeight(WorldConfig);
                if (topSampleHeight < bottomSampleHeight)
                {
                    topSampleHeight = bottomSampleHeight;
                }

                hasHeightRange = true;
            }

            var targetSize = Mathf.Max(0.1f, CubeSizeMeters);
            var targetHeightSpan = hasHeightRange
                ? Mathf.Max(0.01f, topSampleHeight - bottomSampleHeight)
                : targetSize;
            if (force
                || !Mathf.Approximately(_lastCubeSize, targetSize)
                || !Mathf.Approximately(_lastHeightSpan, targetHeightSpan))
            {
                transform.localScale = new Vector3(targetSize, targetHeightSpan, targetSize);
                _lastCubeSize = targetSize;
                _lastHeightSpan = targetHeightSpan;
            }

            var targetPosition = transform.position;
            if (FollowPlayer && PlayerTarget != null)
            {
                targetPosition = PlayerTarget.position + PlayerOffset;
            }
            else
            {
                targetPosition.x = StaticAnchorX;
                targetPosition.z = StaticAnchorZ;
            }

            if (hasHeightRange)
            {
                // Keep X/Z anchor, but pin Y so bottom/top of cube match selected world height range.
                targetPosition.y = bottomSampleHeight + (targetHeightSpan * 0.5f);
            }

            if (force || (transform.position - targetPosition).sqrMagnitude > 0.000001f)
            {
                transform.position = targetPosition;
            }

            if (WorldConfig == null || _runtimeMesh == null)
            {
                return;
            }
            if (!force
                && Mathf.Approximately(_lastBottomSampleHeight, bottomSampleHeight)
                && Mathf.Approximately(_lastTopSampleHeight, topSampleHeight))
            {
                return;
            }

            var minColor = TerrainLabTerrainMeshColorRamp.Evaluate(WorldConfig, bottomSampleHeight);
            var maxColor = TerrainLabTerrainMeshColorRamp.Evaluate(WorldConfig, topSampleHeight);

            _minHeightColor = minColor;
            _maxHeightColor = maxColor;

            ApplyGradientToMesh(_runtimeMesh, minColor, maxColor);

            _lastBottomSampleHeight = bottomSampleHeight;
            _lastTopSampleHeight = topSampleHeight;
        }

        private void EnsureRuntimeMesh()
        {
            if (_runtimeMesh != null)
            {
                if (_meshFilter.sharedMesh != _runtimeMesh)
                {
                    _meshFilter.sharedMesh = _runtimeMesh;
                }

                return;
            }

            var sourceMesh = _meshFilter.sharedMesh;
            if (sourceMesh == null)
            {
                sourceMesh = GetBuiltInCubeMesh();
            }

            if (sourceMesh == null)
            {
                return;
            }

            _runtimeMesh = Instantiate(sourceMesh);
            _runtimeMesh.name = "TerrainLabHeightRangeDebugCubeMesh";
            _runtimeMesh.hideFlags = HideFlags.HideAndDontSave;
            _meshFilter.sharedMesh = _runtimeMesh;
        }

        private static Mesh GetBuiltInCubeMesh()
        {
            if (_cachedBuiltInCubeMesh != null)
            {
                return _cachedBuiltInCubeMesh;
            }

#if UNITY_EDITOR
            _cachedBuiltInCubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (_cachedBuiltInCubeMesh != null)
            {
                return _cachedBuiltInCubeMesh;
            }
#endif

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var filter = primitive.GetComponent<MeshFilter>();
            _cachedBuiltInCubeMesh = filter != null ? filter.sharedMesh : null;
            Destroy(primitive);

            return _cachedBuiltInCubeMesh;
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                var shader = ResolveDebugShader();
                if (shader == null)
                {
                    return;
                }

                _runtimeMaterial = new Material(shader)
                {
                    name = "TerrainLabHeightRangeDebugCubeMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (_meshRenderer.sharedMaterial != _runtimeMaterial)
            {
                _meshRenderer.sharedMaterial = _runtimeMaterial;
            }
        }

        private static Shader ResolveDebugShader()
        {
            var shader = Shader.Find(DebugShaderName);
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Unlit/Color");
        }

        private static void ApplyGradientToMesh(Mesh mesh, Color32 minColor, Color32 maxColor)
        {
            if (mesh == null)
            {
                return;
            }

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                return;
            }

            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < vertices.Length; i++)
            {
                var y = vertices[i].y;
                if (y < minY)
                {
                    minY = y;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }

            var colors = new Color32[vertices.Length];
            if (Mathf.Approximately(minY, maxY))
            {
                for (var i = 0; i < colors.Length; i++)
                {
                    colors[i] = minColor;
                }

                mesh.colors32 = colors;
                return;
            }

            for (var i = 0; i < vertices.Length; i++)
            {
                var t = Mathf.InverseLerp(minY, maxY, vertices[i].y);
                colors[i] = Color32.Lerp(minColor, maxColor, t);
            }

            mesh.colors32 = colors;
        }

        private void ReleaseRuntimeMesh()
        {
            if (_runtimeMesh == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                DestroyImmediate(_runtimeMesh);
            }
            else
            {
                Destroy(_runtimeMesh);
            }
#else
            Destroy(_runtimeMesh);
#endif
            _runtimeMesh = null;
        }

        private void ReleaseRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

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
}
