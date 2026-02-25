using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class TerrainLabCharacterController : MonoBehaviour
    {
        private const string VisualName = "__PlayerVisual";

        [Header("World")]
        public TerrainLabSingleChunkWorld World;

        [Header("Input")]
#if ENABLE_INPUT_SYSTEM
        public InputActionAsset InputActions;
#endif
        public string ActionMapName = "TerrainLab";
        public string MoveActionName = "Move";
        public string JumpActionName = "Jump";
        public string RegenerateActionName = "Regenerate";

        [Header("Movement")]
        public Transform MovementCamera;
        [Min(0.1f)] public float MoveSpeed = 6f;
        [Min(0f)] public float AirControl = 0.6f;
        [Min(0f)] public float JumpHeight = 1.6f;
        [Min(0.1f)] public float GravityMagnitude = 20f;
        public float GroundSnapVelocity = -2f;

        [Header("Visual")]
        public bool AutoCreateVisual = true;
        public Color BodyColor = new(0.18f, 0.57f, 0.98f, 1f);

        private CharacterController _controller;
        private float _verticalVelocity;
        private Material _runtimeVisualMaterial;

#if ENABLE_INPUT_SYSTEM
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _regenerateAction;
#endif

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (World == null)
            {
                World = FindFirstObjectByType<TerrainLabSingleChunkWorld>();
            }

            if (MovementCamera == null && Camera.main != null)
            {
                MovementCamera = Camera.main.transform;
            }

            EnsureVisibleBody();
        }

        private void OnEnable()
        {
            BindInput();
        }

        private void OnDisable()
        {
            UnbindInput();
        }

        private void OnDestroy()
        {
            if (_runtimeVisualMaterial == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                DestroyImmediate(_runtimeVisualMaterial);
            }
            else
            {
                Destroy(_runtimeVisualMaterial);
            }
#else
            Destroy(_runtimeVisualMaterial);
#endif
            _runtimeVisualMaterial = null;
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            var moveInput = ReadMoveInput();
            var move = CalculateWorldMove(moveInput);

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = GroundSnapVelocity;
                }

                if (ReadJumpPressed())
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * 2f * GravityMagnitude);
                }
            }

            _verticalVelocity -= GravityMagnitude * deltaTime;
            move.y = _verticalVelocity;

            _controller.Move(move * deltaTime);
        }

        private Vector3 CalculateWorldMove(Vector2 moveInput)
        {
            var planar = new Vector3(moveInput.x, 0f, moveInput.y);
            if (planar.sqrMagnitude > 1f)
            {
                planar.Normalize();
            }

            if (MovementCamera != null)
            {
                var forward = MovementCamera.forward;
                var right = MovementCamera.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                planar = (right * planar.x) + (forward * planar.z);
            }

            var speed = _controller.isGrounded ? MoveSpeed : MoveSpeed * AirControl;
            return planar * speed;
        }

        private Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_moveAction != null)
            {
                return _moveAction.ReadValue<Vector2>();
            }
#endif
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        private bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null)
            {
                return _jumpAction.WasPressedThisFrame();
            }
#endif
            return Input.GetKeyDown(KeyCode.Space);
        }

        private void OnRegenerateRequested()
        {
            if (World == null)
            {
                return;
            }

            World.ForceRegenerateFromInput();
        }

        private void EnsureVisibleBody()
        {
            if (!AutoCreateVisual)
            {
                return;
            }

            var visualTransform = transform.Find(VisualName);
            GameObject visualObject;
            if (visualTransform == null)
            {
                visualObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visualObject.name = VisualName;
                visualObject.transform.SetParent(transform, worldPositionStays: false);
                RemoveCollider(visualObject);
            }
            else
            {
                visualObject = visualTransform.gameObject;
                RemoveCollider(visualObject);
            }

            visualObject.layer = gameObject.layer;

            var renderer = visualObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = visualObject.AddComponent<MeshRenderer>();
            }

            ApplyOpaqueMaterial(renderer);

            var radius = Mathf.Max(0.1f, _controller != null ? _controller.radius : 0.5f);
            var height = Mathf.Max(radius * 2f, _controller != null ? _controller.height : 2f);
            var center = _controller != null ? _controller.center : new Vector3(0f, height * 0.5f, 0f);

            visualObject.transform.localPosition = center;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                DestroyImmediate(collider);
            }
            else
            {
                Destroy(collider);
            }
#else
            Destroy(collider);
#endif
        }

        private void ApplyOpaqueMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (_runtimeVisualMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    return;
                }

                _runtimeVisualMaterial = new Material(shader)
                {
                    name = "TerrainLabPlayerVisualMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (_runtimeVisualMaterial.HasProperty("_BaseColor"))
            {
                _runtimeVisualMaterial.SetColor("_BaseColor", new Color(BodyColor.r, BodyColor.g, BodyColor.b, 1f));
            }

            if (_runtimeVisualMaterial.HasProperty("_Color"))
            {
                _runtimeVisualMaterial.SetColor("_Color", new Color(BodyColor.r, BodyColor.g, BodyColor.b, 1f));
            }

            if (_runtimeVisualMaterial.HasProperty("_Surface"))
            {
                _runtimeVisualMaterial.SetFloat("_Surface", 0f);
            }

            if (_runtimeVisualMaterial.HasProperty("_Blend"))
            {
                _runtimeVisualMaterial.SetFloat("_Blend", 0f);
            }

            if (_runtimeVisualMaterial.HasProperty("_SrcBlend"))
            {
                _runtimeVisualMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
            }

            if (_runtimeVisualMaterial.HasProperty("_DstBlend"))
            {
                _runtimeVisualMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
            }

            if (_runtimeVisualMaterial.HasProperty("_ZWrite"))
            {
                _runtimeVisualMaterial.SetFloat("_ZWrite", 1f);
            }

            _runtimeVisualMaterial.DisableKeyword("_ALPHATEST_ON");
            _runtimeVisualMaterial.DisableKeyword("_ALPHABLEND_ON");
            _runtimeVisualMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _runtimeVisualMaterial.renderQueue = -1;

            renderer.sharedMaterial = _runtimeVisualMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void BindInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (InputActions == null)
            {
                return;
            }

            var map = InputActions.FindActionMap(ActionMapName, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogWarning($"Terrain Lab: input action map '{ActionMapName}' not found.");
                return;
            }

            _moveAction = map.FindAction(MoveActionName, throwIfNotFound: false);
            _jumpAction = map.FindAction(JumpActionName, throwIfNotFound: false);
            _regenerateAction = map.FindAction(RegenerateActionName, throwIfNotFound: false);

            _moveAction?.Enable();
            _jumpAction?.Enable();
            _regenerateAction?.Enable();
            if (_regenerateAction != null)
            {
                _regenerateAction.performed += OnRegeneratePerformed;
            }
#endif
        }

        private void UnbindInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_regenerateAction != null)
            {
                _regenerateAction.performed -= OnRegeneratePerformed;
            }

            _moveAction?.Disable();
            _jumpAction?.Disable();
            _regenerateAction?.Disable();

            _moveAction = null;
            _jumpAction = null;
            _regenerateAction = null;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnRegeneratePerformed(InputAction.CallbackContext _)
        {
            OnRegenerateRequested();
        }
#endif

        private void OnGUI()
        {
            var text = "Move: WASD / Arrows | Jump: Space | Regenerate: R";
            GUI.Label(new Rect(14f, 12f, 540f, 24f), text);
        }
    }
}
