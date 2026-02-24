using System;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class GemIconRenderer : IDisposable
    {
        private readonly int _textureSize;

        private GameObject _root;
        private Transform _gemTransform;
        private Camera _camera;
        private Light _light;
        private Mesh _mesh;
        private Material _material;
        private RenderTexture _renderTexture;
        private bool _initialized;
        private float _rotationDegrees;

        public GemIconRenderer(int textureSize = 96)
        {
            _textureSize = Mathf.Clamp(textureSize, 48, 256);
        }

        public Texture IconTexture => _renderTexture;

        public void UpdateAndRender(float deltaTime)
        {
            EnsureInitialized();

            if (_gemTransform != null)
            {
                _rotationDegrees = (_rotationDegrees + (deltaTime * 42f)) % 360f;
                _gemTransform.localRotation = Quaternion.Euler(22f, _rotationDegrees, 15f);
            }

            if (_camera != null)
            {
                _camera.Render();
            }
        }

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _root = new GameObject("Runtime_GemIconRenderer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var gem = new GameObject("Gem")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            gem.transform.SetParent(_root.transform, worldPositionStays: false);
            _gemTransform = gem.transform;

            _mesh = GemVisualFactory.CreateOctahedronMesh(0.6f);
            _material = GemVisualFactory.CreateGemMaterial();

            var meshFilter = gem.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _mesh;
            var meshRenderer = gem.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;

            var cameraObject = new GameObject("GemCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            cameraObject.transform.SetParent(_root.transform, worldPositionStays: false);
            cameraObject.transform.position = new Vector3(0f, 0f, -2.4f);
            cameraObject.transform.rotation = Quaternion.identity;

            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = 26f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 10f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;

            _renderTexture = new RenderTexture(_textureSize, _textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Runtime_GemIconRT",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            _camera.targetTexture = _renderTexture;

            var lightObject = new GameObject("GemLight")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lightObject.transform.SetParent(_root.transform, worldPositionStays: false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.color = new Color(1f, 0.99f, 0.95f, 1f);
            _light.intensity = 1.25f;
            _light.shadows = LightShadows.None;

            _initialized = true;
            _rotationDegrees = 0f;
            _camera.Render();
        }

        private void Release()
        {
            _initialized = false;

            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }

            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
                _mesh = null;
            }

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _gemTransform = null;
            _camera = null;
            _light = null;
        }
    }
}
