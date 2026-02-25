using UnityEngine;
using UnityEngine.Rendering;

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    public sealed class TerrainLabWaterController : MonoBehaviour
    {
        [Header("References")]
        public TerrainLabSingleChunkWorld World;
        public Transform PlayerTarget;
        public Camera TargetCamera;

        [Header("Placement")]
        public bool FollowPlayer = true;
        [Min(0)] public int CoverageExtraChunks = 1;
        public Vector2 PositionOffset = Vector2.zero;
        public float WaterLevelOffset;
        public bool UseTransformYForWaterLevel = true;

        [Header("Surface Mesh")]
        [Range(8, 256)] public int SurfaceGridResolution = 96;

        [Header("Material")]
        public Shader WaterShader;
        public Material SourceMaterial;

        [Header("Water Colors")]
        public Color ShallowColor = new(0.42f, 0.73f, 0.88f, 1f);
        public Color MidGreenColor = new(0.31f, 0.63f, 0.54f, 1f);
        public Color DeepBlueColor = new(0.08f, 0.27f, 0.58f, 1f);
        public Color FoamColor = new(0.96f, 0.98f, 1f, 0.95f);

        [Header("Depth Gradient")]
        [Min(0f)] public float DepthBlueStart = 2f;
        [Min(0.01f)] public float DepthBlueRange = 22f;
        [Min(0f)] public float DepthGreenMin = 3f;
        [Min(0f)] public float DepthGreenPeak = 8f;
        [Min(0f)] public float DepthGreenMax = 14f;
        [Range(0f, 1f)] public float DepthGreenStrength = 0.35f;
        [Range(0f, 1f)] public float ShallowAlpha = 0.52f;
        [Range(0f, 1f)] public float DeepAlpha = 0.9f;

        [Header("Depth Auto Fit")]
        public bool AutoFitDepthRanges = true;
        [Range(0f, 0.5f)] public float AutoBlueStartRatio = 0.1f;
        [Range(0.2f, 2f)] public float AutoBlueRangeRatio = 1f;
        [Range(0.02f, 0.5f)] public float AutoShoreMaxDepthRatio = 0.18f;

        [Header("Shoreline Loop")]
        [Min(0f)] public float ShoreDepthMin = 0.05f;
        [Min(0.01f)] public float ShoreDepthMax = 2f;
        [Min(0f)] public float ShoreSpeed = 1.2f;
        [Range(0f, 2f)] public float ShoreStrength = 0.65f;
        [Min(0.001f)] public float ShoreNoiseScale = 0.08f;

        [Header("World Shore Distance Map (Required)")]
        [Range(64, 512)] public int ShoreDistanceMapResolution = 192;
        [Min(0.5f)] public float ShoreDistanceMaxMeters = 10f;

        [Header("Surface Detail")]
        public Texture2D NormalMapA;
        public Texture2D NormalMapB;
        [Min(0.0001f)] public float NormalWorldScaleA = 0.08f;
        [Min(0.0001f)] public float NormalWorldScaleB = 0.14f;
        public Vector2 NormalSpeedA = new(0.04f, 0.02f);
        public Vector2 NormalSpeedB = new(-0.03f, 0.015f);
        [Range(0f, 2f)] public float NormalStrength = 0.75f;

        [Header("Reflection + Specular")]
        public Color SpecColor = new(1f, 1f, 1f, 1f);
        [Range(0f, 4f)] public float SpecStrength = 1.2f;
        [Min(1f)] public float SpecPower = 96f;
        [Range(0f, 2f)] public float FresnelStrength = 0.75f;
        [Min(0.1f)] public float FresnelPower = 4f;
        public Color ReflectionHorizonColor = new(0.42f, 0.54f, 0.63f, 1f);
        public Color ReflectionSkyColor = new(0.66f, 0.78f, 0.91f, 1f);

        private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
        private static readonly int MidGreenColorId = Shader.PropertyToID("_MidGreenColor");
        private static readonly int DeepBlueColorId = Shader.PropertyToID("_DeepBlueColor");
        private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
        private static readonly int DepthBlueStartId = Shader.PropertyToID("_DepthBlueStart");
        private static readonly int DepthBlueRangeId = Shader.PropertyToID("_DepthBlueRange");
        private static readonly int DepthGreenMinId = Shader.PropertyToID("_DepthGreenMin");
        private static readonly int DepthGreenPeakId = Shader.PropertyToID("_DepthGreenPeak");
        private static readonly int DepthGreenMaxId = Shader.PropertyToID("_DepthGreenMax");
        private static readonly int DepthGreenStrengthId = Shader.PropertyToID("_DepthGreenStrength");
        private static readonly int ShallowAlphaId = Shader.PropertyToID("_ShallowAlpha");
        private static readonly int DeepAlphaId = Shader.PropertyToID("_DeepAlpha");
        private static readonly int ShoreDepthMinId = Shader.PropertyToID("_ShoreDepthMin");
        private static readonly int ShoreDepthMaxId = Shader.PropertyToID("_ShoreDepthMax");
        private static readonly int ShoreSpeedId = Shader.PropertyToID("_ShoreSpeed");
        private static readonly int ShoreStrengthId = Shader.PropertyToID("_ShoreStrength");
        private static readonly int ShoreNoiseScaleId = Shader.PropertyToID("_ShoreNoiseScale");
        private static readonly int NormalMapAId = Shader.PropertyToID("_NormalMapA");
        private static readonly int NormalMapBId = Shader.PropertyToID("_NormalMapB");
        private static readonly int NormalWorldScaleAId = Shader.PropertyToID("_NormalWorldScaleA");
        private static readonly int NormalWorldScaleBId = Shader.PropertyToID("_NormalWorldScaleB");
        private static readonly int NormalSpeedAId = Shader.PropertyToID("_NormalSpeedA");
        private static readonly int NormalSpeedBId = Shader.PropertyToID("_NormalSpeedB");
        private static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
        private static readonly int WaterSpecColorId = Shader.PropertyToID("_WaterSpecColor");
        private static readonly int WaterSpecStrengthId = Shader.PropertyToID("_WaterSpecStrength");
        private static readonly int WaterSpecPowerId = Shader.PropertyToID("_WaterSpecPower");
        private static readonly int WaterFresnelStrengthId = Shader.PropertyToID("_WaterFresnelStrength");
        private static readonly int WaterFresnelPowerId = Shader.PropertyToID("_WaterFresnelPower");
        private static readonly int WaterReflectionHorizonColorId = Shader.PropertyToID("_WaterReflectionHorizonColor");
        private static readonly int WaterReflectionSkyColorId = Shader.PropertyToID("_WaterReflectionSkyColor");
        private static readonly int ShoreDistanceMapId = Shader.PropertyToID("_ShoreDistanceMap");
        private static readonly int ShoreMapWorldMinId = Shader.PropertyToID("_ShoreMapWorldMin");
        private static readonly int ShoreMapWorldSizeId = Shader.PropertyToID("_ShoreMapWorldSize");
        private static readonly int ShoreDistanceMaxId = Shader.PropertyToID("_ShoreDistanceMax");

        private Transform _surfaceTransform;
        private MeshFilter _surfaceFilter;
        private MeshRenderer _surfaceRenderer;
        private Mesh _surfaceMesh;
        private int _surfaceGridResolution = -1;
        private Material _runtimeMaterial;
        private Texture2D _fallbackNormalMapA;
        private Texture2D _fallbackNormalMapB;
        private Texture2D _shoreDistanceMap;
        private bool _shoreDistanceMapDirty = true;
        private int _lastShoreMapSeed = int.MinValue;
        private int _lastShoreMapResolution = -1;
        private float _lastShoreMapCoverageWorldSize = -1f;
        private Vector2 _lastShoreMapCenterXZ = new(float.NaN, float.NaN);
        private float _lastShoreMapWaterY = float.NaN;
        private Vector2 _shoreMapWorldMinXZ = Vector2.zero;
        private Vector2 _shoreMapWorldSizeXZ = Vector2.one;
        private bool _materialDirty = true;
        private float _lastCoverageWorldSize = -1f;
        private Vector3 _lastCenterPosition = new(float.NaN, float.NaN, float.NaN);
        private float _lastWaterY = float.NaN;

        public void BindWorld(TerrainLabSingleChunkWorld world)
        {
            World = world;
        }

        public void BindPlayer(Transform player)
        {
            PlayerTarget = player;
        }

        public void RequestImmediateSync()
        {
            _lastCoverageWorldSize = -1f;
            _lastCenterPosition = new Vector3(float.NaN, float.NaN, float.NaN);
            _lastWaterY = float.NaN;
            _shoreDistanceMapDirty = true;
            _lastShoreMapSeed = int.MinValue;
            _lastShoreMapResolution = -1;
            _materialDirty = true;
            SyncToWorld(force: true);
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureSurfaceObject();
            EnsureMaterial();
            SyncToWorld(force: true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureSurfaceObject();
            EnsureMaterial();
            SyncToWorld(force: true);
        }

        private void LateUpdate()
        {
            SyncToWorld(force: false);
        }

        private void OnValidate()
        {
            CoverageExtraChunks = Mathf.Max(0, CoverageExtraChunks);
            DepthBlueStart = Mathf.Max(0f, DepthBlueStart);
            DepthBlueRange = Mathf.Max(0.01f, DepthBlueRange);
            DepthGreenMin = Mathf.Max(0f, DepthGreenMin);
            DepthGreenPeak = Mathf.Max(DepthGreenMin, DepthGreenPeak);
            DepthGreenMax = Mathf.Max(DepthGreenPeak, DepthGreenMax);
            ShoreDepthMin = Mathf.Max(0f, ShoreDepthMin);
            ShoreDepthMax = Mathf.Max(ShoreDepthMin + 0.01f, ShoreDepthMax);
            ShoreSpeed = Mathf.Max(0f, ShoreSpeed);
            ShoreNoiseScale = Mathf.Max(0.001f, ShoreNoiseScale);
            ShoreDistanceMapResolution = Mathf.Clamp(ShoreDistanceMapResolution, 64, 512);
            ShoreDistanceMaxMeters = Mathf.Max(0.5f, ShoreDistanceMaxMeters);
            NormalWorldScaleA = Mathf.Max(0.0001f, NormalWorldScaleA);
            NormalWorldScaleB = Mathf.Max(0.0001f, NormalWorldScaleB);
            NormalStrength = Mathf.Clamp(NormalStrength, 0f, 2f);
            SpecStrength = Mathf.Clamp(SpecStrength, 0f, 4f);
            SpecPower = Mathf.Max(1f, SpecPower);
            FresnelStrength = Mathf.Clamp(FresnelStrength, 0f, 2f);
            FresnelPower = Mathf.Max(0.1f, FresnelPower);
            ShallowAlpha = Mathf.Clamp01(ShallowAlpha);
            DeepAlpha = Mathf.Clamp01(DeepAlpha);
            AutoBlueStartRatio = Mathf.Clamp(AutoBlueStartRatio, 0f, 0.5f);
            AutoBlueRangeRatio = Mathf.Clamp(AutoBlueRangeRatio, 0.2f, 2f);
            AutoShoreMaxDepthRatio = Mathf.Clamp(AutoShoreMaxDepthRatio, 0.02f, 0.5f);
            SurfaceGridResolution = Mathf.Clamp(SurfaceGridResolution, 8, 256);
            _surfaceGridResolution = -1;
            _shoreDistanceMapDirty = true;
            _materialDirty = true;

            if (!isActiveAndEnabled)
            {
                return;
            }

            // Avoid creating/destroying editor objects during OnValidate callback.
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            EnsureSurfaceObject();
            EnsureMaterial();
            ApplyMaterialParameters();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial == null)
            {
                DestroyFallbackNormalMaps();
                DestroyShoreDistanceMap();
                DestroySurfaceMesh();
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
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
            DestroyFallbackNormalMaps();
            DestroyShoreDistanceMap();
            DestroySurfaceMesh();
        }

        private void ResolveReferences()
        {
            if (World == null)
            {
                World = FindFirstObjectByType<TerrainLabSingleChunkWorld>();
            }

            if (PlayerTarget == null && World != null)
            {
                PlayerTarget = World.PlayerRoot;
            }

            if (PlayerTarget == null)
            {
                var playerController = FindFirstObjectByType<TerrainLabCharacterController>();
                if (playerController != null)
                {
                    PlayerTarget = playerController.transform;
                }
            }
        }

        private void EnsureSurfaceObject()
        {
            var child = transform.Find("WaterSurface");
            var created = false;
            if (child == null)
            {
                var go = new GameObject("WaterSurface");
                go.name = "WaterSurface";
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                child = go.transform;
                created = true;
            }

            _surfaceTransform = child;
            if (created)
            {
                _surfaceTransform.localPosition = Vector3.zero;
                _surfaceTransform.localRotation = Quaternion.identity;
                _surfaceTransform.localScale = Vector3.one;
            }
            else if (_surfaceTransform.localRotation != Quaternion.identity)
            {
                // Keep the runtime surface horizontal while preserving runtime scale/position updates.
                _surfaceTransform.localRotation = Quaternion.identity;
            }

            _surfaceFilter = child.GetComponent<MeshFilter>();
            if (_surfaceFilter == null)
            {
                _surfaceFilter = child.gameObject.AddComponent<MeshFilter>();
            }

            _surfaceRenderer = child.GetComponent<MeshRenderer>();
            if (_surfaceRenderer == null)
            {
                _surfaceRenderer = child.gameObject.AddComponent<MeshRenderer>();
            }

            var primitiveCollider = child.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = false;
            }

            EnsureSurfaceMesh();

            _surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _surfaceRenderer.receiveShadows = false;
            _surfaceRenderer.lightProbeUsage = LightProbeUsage.Off;
            _surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void EnsureMaterial()
        {
            if (_surfaceRenderer == null)
            {
                return;
            }

            if (WaterShader == null)
            {
                WaterShader = Shader.Find("TerrainLab/WaterDepthShore");
            }

            var createdMaterial = false;
            if (_runtimeMaterial == null)
            {
                if (SourceMaterial != null)
                {
                    _runtimeMaterial = new Material(SourceMaterial);
                    createdMaterial = true;
                }
                else if (WaterShader != null)
                {
                    _runtimeMaterial = new Material(WaterShader);
                    createdMaterial = true;
                }
            }

            if (_runtimeMaterial == null)
            {
                return;
            }

            if (WaterShader != null && _runtimeMaterial.shader != WaterShader)
            {
                _runtimeMaterial.shader = WaterShader;
                _materialDirty = true;
            }

            _runtimeMaterial.name = "TerrainLabWaterRuntimeMaterial";
            _runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
            _surfaceRenderer.sharedMaterial = _runtimeMaterial;
            if (createdMaterial)
            {
                _materialDirty = true;
            }

            if (_materialDirty)
            {
                ApplyMaterialParameters();
            }
        }

        private void EnsureSurfaceMesh()
        {
            if (_surfaceFilter == null)
            {
                return;
            }

            var resolution = Mathf.Clamp(SurfaceGridResolution, 8, 256);
            if (_surfaceMesh != null && _surfaceGridResolution == resolution)
            {
                if (_surfaceFilter.sharedMesh != _surfaceMesh)
                {
                    _surfaceFilter.sharedMesh = _surfaceMesh;
                }

                return;
            }

            DestroySurfaceMesh();

            _surfaceMesh = BuildSurfaceGridMesh(resolution);
            _surfaceMesh.name = "TerrainLabWaterSurfaceMesh";
            _surfaceMesh.hideFlags = HideFlags.HideAndDontSave;
            _surfaceFilter.sharedMesh = _surfaceMesh;
            _surfaceGridResolution = resolution;
        }

        private void DestroySurfaceMesh()
        {
            if (_surfaceMesh == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(_surfaceMesh);
            }
            else
            {
                Destroy(_surfaceMesh);
            }
#else
            Destroy(_surfaceMesh);
#endif
            _surfaceMesh = null;
            _surfaceGridResolution = -1;
        }

        private static Mesh BuildSurfaceGridMesh(int resolution)
        {
            var clamped = Mathf.Clamp(resolution, 8, 256);
            var vertsPerAxis = clamped + 1;
            var vertexCount = vertsPerAxis * vertsPerAxis;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[clamped * clamped * 6];

            var vertexIndex = 0;
            for (var z = 0; z <= clamped; z++)
            {
                var tz = z / (float)clamped;
                var zPos = tz - 0.5f;
                for (var x = 0; x <= clamped; x++)
                {
                    var tx = x / (float)clamped;
                    var xPos = tx - 0.5f;
                    vertices[vertexIndex] = new Vector3(xPos, 0f, zPos);
                    normals[vertexIndex] = Vector3.up;
                    uvs[vertexIndex] = new Vector2(tx, tz);
                    vertexIndex++;
                }
            }

            var triIndex = 0;
            for (var z = 0; z < clamped; z++)
            {
                for (var x = 0; x < clamped; x++)
                {
                    var v0 = (z * vertsPerAxis) + x;
                    var v1 = v0 + 1;
                    var v2 = v0 + vertsPerAxis;
                    var v3 = v2 + 1;

                    triangles[triIndex++] = v0;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v1;

                    triangles[triIndex++] = v1;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v3;
                }
            }

            var mesh = new Mesh();
            if (vertexCount > ushort.MaxValue)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ApplyMaterialParameters()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetColor(ShallowColorId, ShallowColor);
            _runtimeMaterial.SetColor(MidGreenColorId, MidGreenColor);
            _runtimeMaterial.SetColor(DeepBlueColorId, DeepBlueColor);
            _runtimeMaterial.SetColor(FoamColorId, FoamColor);

            _runtimeMaterial.SetFloat(DepthBlueStartId, DepthBlueStart);
            _runtimeMaterial.SetFloat(DepthBlueRangeId, DepthBlueRange);
            _runtimeMaterial.SetFloat(DepthGreenMinId, DepthGreenMin);
            _runtimeMaterial.SetFloat(DepthGreenPeakId, DepthGreenPeak);
            _runtimeMaterial.SetFloat(DepthGreenMaxId, DepthGreenMax);
            _runtimeMaterial.SetFloat(DepthGreenStrengthId, DepthGreenStrength);
            _runtimeMaterial.SetFloat(ShallowAlphaId, ShallowAlpha);
            _runtimeMaterial.SetFloat(DeepAlphaId, DeepAlpha);

            _runtimeMaterial.SetFloat(ShoreDepthMinId, ShoreDepthMin);
            _runtimeMaterial.SetFloat(ShoreDepthMaxId, ShoreDepthMax);
            _runtimeMaterial.SetFloat(ShoreSpeedId, ShoreSpeed);
            _runtimeMaterial.SetFloat(ShoreStrengthId, ShoreStrength);
            _runtimeMaterial.SetFloat(ShoreNoiseScaleId, ShoreNoiseScale);

            var normalMapA = NormalMapA != null ? NormalMapA : GetOrCreateFallbackNormalMapA();
            var normalMapB = NormalMapB != null ? NormalMapB : GetOrCreateFallbackNormalMapB();
            _runtimeMaterial.SetTexture(NormalMapAId, normalMapA);
            _runtimeMaterial.SetTexture(NormalMapBId, normalMapB);

            _runtimeMaterial.SetFloat(NormalWorldScaleAId, NormalWorldScaleA);
            _runtimeMaterial.SetFloat(NormalWorldScaleBId, NormalWorldScaleB);
            _runtimeMaterial.SetVector(NormalSpeedAId, new Vector4(NormalSpeedA.x, NormalSpeedA.y, 0f, 0f));
            _runtimeMaterial.SetVector(NormalSpeedBId, new Vector4(NormalSpeedB.x, NormalSpeedB.y, 0f, 0f));
            _runtimeMaterial.SetFloat(NormalStrengthId, NormalStrength);

            _runtimeMaterial.SetColor(WaterSpecColorId, SpecColor);
            _runtimeMaterial.SetFloat(WaterSpecStrengthId, SpecStrength);
            _runtimeMaterial.SetFloat(WaterSpecPowerId, SpecPower);
            _runtimeMaterial.SetFloat(WaterFresnelStrengthId, FresnelStrength);
            _runtimeMaterial.SetFloat(WaterFresnelPowerId, FresnelPower);
            _runtimeMaterial.SetColor(WaterReflectionHorizonColorId, ReflectionHorizonColor);
            _runtimeMaterial.SetColor(WaterReflectionSkyColorId, ReflectionSkyColor);

            _runtimeMaterial.SetTexture(ShoreDistanceMapId, _shoreDistanceMap != null ? _shoreDistanceMap : Texture2D.whiteTexture);
            _runtimeMaterial.SetVector(ShoreMapWorldMinId, new Vector4(_shoreMapWorldMinXZ.x, _shoreMapWorldMinXZ.y, 0f, 0f));
            _runtimeMaterial.SetVector(ShoreMapWorldSizeId, new Vector4(_shoreMapWorldSizeXZ.x, _shoreMapWorldSizeXZ.y, 0f, 0f));
            _runtimeMaterial.SetFloat(ShoreDistanceMaxId, ShoreDistanceMaxMeters);

            _materialDirty = false;
        }

        private Texture2D GetOrCreateFallbackNormalMapA()
        {
            if (_fallbackNormalMapA == null)
            {
                _fallbackNormalMapA = CreateProceduralNormalMap(
                    "TerrainLabWaterFallbackNormalA",
                    128,
                    freqX: 3.8f,
                    freqY: 5.2f,
                    phase: 0.31f);
            }

            return _fallbackNormalMapA;
        }

        private Texture2D GetOrCreateFallbackNormalMapB()
        {
            if (_fallbackNormalMapB == null)
            {
                _fallbackNormalMapB = CreateProceduralNormalMap(
                    "TerrainLabWaterFallbackNormalB",
                    128,
                    freqX: 7.5f,
                    freqY: 2.9f,
                    phase: 1.17f);
            }

            return _fallbackNormalMapB;
        }

        private static Texture2D CreateProceduralNormalMap(
            string textureName,
            int size,
            float freqX,
            float freqY,
            float phase)
        {
            var resolution = Mathf.Clamp(size, 32, 512);
            var heights = new float[resolution * resolution];
            for (var y = 0; y < resolution; y++)
            {
                var v = y / (float)resolution;
                for (var x = 0; x < resolution; x++)
                {
                    var u = x / (float)resolution;
                    heights[(y * resolution) + x] = EvaluateWaveHeight(u, v, freqX, freqY, phase);
                }
            }

            var colors = new Color32[resolution * resolution];
            for (var y = 0; y < resolution; y++)
            {
                var yDown = (y - 1 + resolution) % resolution;
                var yUp = (y + 1) % resolution;
                for (var x = 0; x < resolution; x++)
                {
                    var xLeft = (x - 1 + resolution) % resolution;
                    var xRight = (x + 1) % resolution;

                    var left = heights[(y * resolution) + xLeft];
                    var right = heights[(y * resolution) + xRight];
                    var down = heights[(yDown * resolution) + x];
                    var up = heights[(yUp * resolution) + x];

                    var dx = right - left;
                    var dy = up - down;
                    var n = new Vector3(-dx, -dy, 2f).normalized;
                    colors[(y * resolution) + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: true, linear: true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2,
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixels32(colors);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return texture;
        }

        private static float EvaluateWaveHeight(float u, float v, float freqX, float freqY, float phase)
        {
            const float twoPi = Mathf.PI * 2f;
            var a = Mathf.Sin((u * freqX * twoPi) + phase) * 0.55f;
            var b = Mathf.Cos((v * freqY * twoPi) + (phase * 1.67f)) * 0.35f;
            var c = Mathf.Sin(((u + v) * ((freqX + freqY) * 0.35f) * twoPi) + (phase * 0.73f)) * 0.25f;
            return a + b + c;
        }

        private void DestroyFallbackNormalMaps()
        {
            DestroyTexture(ref _fallbackNormalMapA);
            DestroyTexture(ref _fallbackNormalMapB);
        }

        private void DestroyShoreDistanceMap()
        {
            DestroyTexture(ref _shoreDistanceMap);
            _shoreDistanceMapDirty = true;
            _lastShoreMapResolution = -1;
            _lastShoreMapSeed = int.MinValue;
            _lastShoreMapCoverageWorldSize = -1f;
            _lastShoreMapCenterXZ = new Vector2(float.NaN, float.NaN);
            _lastShoreMapWaterY = float.NaN;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(texture);
            }
            else
            {
                Destroy(texture);
            }
#else
            Destroy(texture);
#endif

            texture = null;
        }

        private void EnsureWorldSpaceShoreDistanceMap(
            TerrainLabWorldConfig config,
            int seed,
            Vector3 centerPosition,
            float coverageWorldSize,
            float waterY,
            bool force)
        {
            if (config == null)
            {
                return;
            }

            var resolution = Mathf.Clamp(ShoreDistanceMapResolution, 64, 512);
            var needsRebuild = force
                || _shoreDistanceMapDirty
                || _shoreDistanceMap == null
                || _lastShoreMapResolution != resolution
                || _lastShoreMapSeed != seed
                || !Mathf.Approximately(_lastShoreMapCoverageWorldSize, coverageWorldSize)
                || !Mathf.Approximately(_lastShoreMapCenterXZ.x, centerPosition.x)
                || !Mathf.Approximately(_lastShoreMapCenterXZ.y, centerPosition.z)
                || !Mathf.Approximately(_lastShoreMapWaterY, waterY);

            if (!needsRebuild)
            {
                return;
            }

            BuildWorldSpaceShoreDistanceMap(config, seed, centerPosition, coverageWorldSize, waterY, resolution);

            _lastShoreMapResolution = resolution;
            _lastShoreMapSeed = seed;
            _lastShoreMapCoverageWorldSize = coverageWorldSize;
            _lastShoreMapCenterXZ = new Vector2(centerPosition.x, centerPosition.z);
            _lastShoreMapWaterY = waterY;
            _shoreDistanceMapDirty = false;
            _materialDirty = true;
        }

        private void BuildWorldSpaceShoreDistanceMap(
            TerrainLabWorldConfig config,
            int seed,
            Vector3 centerPosition,
            float coverageWorldSize,
            float waterY,
            int resolution)
        {
            var clampedResolution = Mathf.Clamp(resolution, 64, 512);
            var worldSize = Mathf.Max(0.001f, coverageWorldSize);
            var minX = centerPosition.x - (worldSize * 0.5f);
            var minZ = centerPosition.z - (worldSize * 0.5f);
            var step = worldSize / Mathf.Max(1, clampedResolution - 1);

            _shoreMapWorldMinXZ = new Vector2(minX, minZ);
            _shoreMapWorldSizeXZ = new Vector2(worldSize, worldSize);

            var pixelCount = clampedResolution * clampedResolution;
            var isWater = new bool[pixelCount];
            var distancePx = new float[pixelCount];
            const float inf = 1e20f;

            for (var z = 0; z < clampedResolution; z++)
            {
                var worldZ = minZ + (z * step);
                var rowOffset = z * clampedResolution;
                for (var x = 0; x < clampedResolution; x++)
                {
                    var worldX = minX + (x * step);
                    var idx = rowOffset + x;
                    if (!TerrainLabHeightSampler.TrySampleHeight(
                            config,
                            seed,
                            new Vector2(worldX, worldZ),
                            autoGenerateMissing: true,
                            out var height))
                    {
                        height = config.HeightMin;
                    }

                    var water = height < waterY;
                    isWater[idx] = water;
                    distancePx[idx] = water ? inf : 0f;
                }
            }

            const float diagonalCost = 1.41421356f;
            for (var z = 0; z < clampedResolution; z++)
            {
                var rowOffset = z * clampedResolution;
                for (var x = 0; x < clampedResolution; x++)
                {
                    var idx = rowOffset + x;
                    if (!isWater[idx])
                    {
                        continue;
                    }

                    var best = distancePx[idx];
                    if (x > 0)
                    {
                        best = Mathf.Min(best, distancePx[idx - 1] + 1f);
                    }

                    if (z > 0)
                    {
                        best = Mathf.Min(best, distancePx[idx - clampedResolution] + 1f);
                        if (x > 0)
                        {
                            best = Mathf.Min(best, distancePx[idx - clampedResolution - 1] + diagonalCost);
                        }

                        if (x < clampedResolution - 1)
                        {
                            best = Mathf.Min(best, distancePx[idx - clampedResolution + 1] + diagonalCost);
                        }
                    }

                    distancePx[idx] = best;
                }
            }

            for (var z = clampedResolution - 1; z >= 0; z--)
            {
                var rowOffset = z * clampedResolution;
                for (var x = clampedResolution - 1; x >= 0; x--)
                {
                    var idx = rowOffset + x;
                    if (!isWater[idx])
                    {
                        continue;
                    }

                    var best = distancePx[idx];
                    if (x < clampedResolution - 1)
                    {
                        best = Mathf.Min(best, distancePx[idx + 1] + 1f);
                    }

                    if (z < clampedResolution - 1)
                    {
                        best = Mathf.Min(best, distancePx[idx + clampedResolution] + 1f);
                        if (x < clampedResolution - 1)
                        {
                            best = Mathf.Min(best, distancePx[idx + clampedResolution + 1] + diagonalCost);
                        }

                        if (x > 0)
                        {
                            best = Mathf.Min(best, distancePx[idx + clampedResolution - 1] + diagonalCost);
                        }
                    }

                    distancePx[idx] = best;
                }
            }

            var maxMeters = Mathf.Max(0.5f, ShoreDistanceMaxMeters);
            var maxDistancePx = maxMeters / Mathf.Max(0.0001f, step);
            var colors = new Color32[pixelCount];
            for (var i = 0; i < pixelCount; i++)
            {
                var d = distancePx[i];
                if (!isWater[i])
                {
                    d = 0f;
                }
                else if (d >= inf * 0.5f)
                {
                    // No shoreline found inside coverage area: treat as far from shore (no foam).
                    d = maxDistancePx;
                }

                var normalized = Mathf.Clamp01(d / Mathf.Max(0.0001f, maxDistancePx));
                var encoded = (byte)Mathf.RoundToInt(normalized * 255f);
                colors[i] = new Color32(encoded, encoded, encoded, 255);
            }

            if (_shoreDistanceMap == null
                || _shoreDistanceMap.width != clampedResolution
                || _shoreDistanceMap.height != clampedResolution)
            {
                DestroyTexture(ref _shoreDistanceMap);
                _shoreDistanceMap = new Texture2D(clampedResolution, clampedResolution, TextureFormat.RGBA32, mipChain: false, linear: true)
                {
                    name = "TerrainLabShoreDistanceMap",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            _shoreDistanceMap.SetPixels32(colors);
            _shoreDistanceMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private void SyncToWorld(bool force)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveReferences();
            EnsureSurfaceObject();
            EnsureMaterial();

            if (_surfaceTransform == null || _surfaceRenderer == null || _runtimeMaterial == null)
            {
                return;
            }

            if (World == null || World.WorldConfig == null)
            {
                return;
            }

            var config = World.WorldConfig;
            config.ValidateInPlace();
            AutoFitDepthParamsFromConfig(config);

            var chunkWorldSize = config.ChunkCells * config.CellSize;
            if (chunkWorldSize <= 0.0001f)
            {
                return;
            }

            var clampedGrid = Mathf.Max(1, World.GeneratedGridSize);
            var coverageWorldSize = (clampedGrid + (Mathf.Max(0, CoverageExtraChunks) * 2)) * chunkWorldSize;

            var centerPosition = ResolveCenterPosition(chunkWorldSize);
            var waterY = ResolveWaterLevelY(config);
            var currentSeed = World.CurrentSeed;

            if (force || !Mathf.Approximately(_lastCoverageWorldSize, coverageWorldSize))
            {
                _surfaceTransform.localScale = new Vector3(coverageWorldSize, 1f, coverageWorldSize);
                _lastCoverageWorldSize = coverageWorldSize;
            }

            if (force
                || !Mathf.Approximately(_lastCenterPosition.x, centerPosition.x)
                || !Mathf.Approximately(_lastCenterPosition.z, centerPosition.z)
                || !Mathf.Approximately(_lastWaterY, waterY))
            {
                _surfaceTransform.position = new Vector3(centerPosition.x, waterY, centerPosition.z);
                _lastCenterPosition = centerPosition;
                _lastWaterY = waterY;
            }

            EnsureWorldSpaceShoreDistanceMap(
                config,
                currentSeed,
                centerPosition,
                coverageWorldSize,
                waterY,
                force);

            if (_materialDirty)
            {
                ApplyMaterialParameters();
            }
        }

        private float ResolveWaterLevelY(TerrainLabWorldConfig config)
        {
            if (config == null)
            {
                return 0f;
            }

            var baseWaterY = config.WaterLevel + WaterLevelOffset;
            if (!UseTransformYForWaterLevel)
            {
                return baseWaterY;
            }

            return baseWaterY + transform.position.y;
        }

        private void AutoFitDepthParamsFromConfig(TerrainLabWorldConfig config)
        {
            if (!AutoFitDepthRanges || config == null)
            {
                return;
            }

            var maxDepth = Mathf.Max(0.02f, config.WaterLevel - config.HeightMin);
            var changed = false;

            var targetBlueStart = maxDepth * AutoBlueStartRatio;
            var targetBlueRange = maxDepth * AutoBlueRangeRatio;
            var targetGreenMin = maxDepth * 0.20f;
            var targetGreenPeak = maxDepth * 0.45f;
            var targetGreenMax = maxDepth * 0.78f;
            var targetShoreMin = Mathf.Max(0.02f, maxDepth * 0.01f);
            var targetShoreMax = Mathf.Max(targetShoreMin + 0.05f, maxDepth * AutoShoreMaxDepthRatio);

            changed |= SetIfDifferent(ref DepthBlueStart, targetBlueStart);
            changed |= SetIfDifferent(ref DepthBlueRange, targetBlueRange);
            changed |= SetIfDifferent(ref DepthGreenMin, targetGreenMin);
            changed |= SetIfDifferent(ref DepthGreenPeak, targetGreenPeak);
            changed |= SetIfDifferent(ref DepthGreenMax, targetGreenMax);
            changed |= SetIfDifferent(ref ShoreDepthMin, targetShoreMin);
            changed |= SetIfDifferent(ref ShoreDepthMax, targetShoreMax);

            if (changed)
            {
                _materialDirty = true;
            }
        }

        private static bool SetIfDifferent(ref float source, float value)
        {
            if (Mathf.Abs(source - value) <= 0.0001f)
            {
                return false;
            }

            source = value;
            return true;
        }

        private Vector3 ResolveCenterPosition(float chunkWorldSize)
        {
            var target = FollowPlayer ? PlayerTarget : null;
            if (target == null && World != null)
            {
                target = World.PlayerRoot;
            }

            var sourcePosition = target != null ? target.position : transform.position;
            var alignedX = Mathf.Round(sourcePosition.x / chunkWorldSize) * chunkWorldSize;
            var alignedZ = Mathf.Round(sourcePosition.z / chunkWorldSize) * chunkWorldSize;
            return new Vector3(alignedX + PositionOffset.x, 0f, alignedZ + PositionOffset.y);
        }
    }
}
